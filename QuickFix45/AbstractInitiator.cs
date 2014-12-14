using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using QuickFix;

namespace QuickFix45
{
    public abstract class AbstractInitiator : IInitiator
    {
        // from constructor
        private IApplication _app = null;
        private IMessageStoreFactory _storeFactory = null;
        private SessionSettings _settings = null;
        private ILogFactory _logFactory = null;
        private IMessageFactory _msgFactory = null;

        private bool _disposed = false;
        private readonly ConcurrentDictionary<SessionID, SessionWrapper> _sessions = new ConcurrentDictionary<SessionID, SessionWrapper>();
        private bool isStopped_ = true;
        private ITaskWorker _worker;

        #region Properties

        public bool IsStopped
        {
            get { return isStopped_; }
        }

        #endregion

        public AbstractInitiator(IApplication app, IMessageStoreFactory storeFactory, SessionSettings settings)
            : this(app, storeFactory, settings, null, null)
        { }

        public AbstractInitiator(IApplication app, IMessageStoreFactory storeFactory, SessionSettings settings, ILogFactory logFactory)
            : this(app, storeFactory, settings, logFactory, null)
        { }

        public AbstractInitiator(
            IApplication app, IMessageStoreFactory storeFactory, SessionSettings settings, ILogFactory logFactory, IMessageFactory messageFactory)
        {
            _app = app;
            _storeFactory = storeFactory;
            _settings = settings;
            _logFactory = logFactory;
            _msgFactory = messageFactory;

            HashSet<SessionID> definedSessions = _settings.GetSessions();
            if (0 == definedSessions.Count)
                throw new ConfigError("No sessions defined");
        }

        public void Start()
        {
            if (_disposed)
                throw new System.ObjectDisposedException(this.GetType().Name);

            // create all sessions
            var factory = new SessionFactory(_app, _storeFactory, _logFactory, _msgFactory);
            foreach (SessionID sessionID in _settings.GetSessions())
            {
                Dictionary dict = _settings.Get(sessionID);
                string connectionType = dict.GetString(SessionSettings.CONNECTION_TYPE);

                if ("initiator".Equals(connectionType))
                {
                    _sessions[sessionID] = new SessionWrapper(factory.Create(sessionID, dict)) { ConnectionStatus = ConnectionStatus.Disconnected };
                }
            }

            if (0 == _sessions.Count)
                throw new ConfigError("No sessions defined for initiator");

            // start it up
            isStopped_ = false;
            OnConfigure(_settings);
            _worker = new TaskWorker(OnStart);
            _worker.Start();
        }

        /// <summary>
        /// Logout existing session and close connection.  Attempt graceful disconnect first.
        /// </summary>
        public void Stop()
        {
            Stop(false);
        }

        /// <summary>
        /// Logout existing session and close connection
        /// </summary>
        /// <param name="force">If true, terminate immediately.  </param>
        public void Stop(bool force)
        {
            if (_disposed)
                throw new System.ObjectDisposedException(this.GetType().Name);

            if (IsStopped)
                return;

            var enabledSessions = new List<ISession>();
            var connected = _sessions.Where(kv => kv.Value.ConnectionStatus == ConnectionStatus.Connected);

            foreach (var kv in connected)
            {
                ISession session = Session.LookupSession(kv.Key);
                if (session.IsEnabled)
                {
                    enabledSessions.Add(session);
                    session.Logout();
                }
            }


            if (!force)
            {
                // TODO change this duration to always exceed LogoutTimeout setting
                for (int second = 0; (second < 10) && IsLoggedOn; ++second)
                    Thread.Sleep(1000);
            }

            foreach (var kv in connected)
                kv.Value.ConnectionStatus = ConnectionStatus.Disconnected;

            isStopped_ = true;
            OnStop();

            // Give OnStop() time to finish its business
            _worker.Stop(5000);

            // dispose all sessions and clear all session sets
            foreach (var s in _sessions.Values)
                s.Session.Dispose();
            _sessions.Clear();
        }

        public bool IsLoggedOn
        {
            get
            {
                foreach (var kv in _sessions)
                {
                    if (kv.Value.ConnectionStatus == ConnectionStatus.Connected)
                    {
                        ISession session = Session.LookupSession(kv.Key);
                        if (session.IsLoggedOn) return true;
                    }
                }
                return false;
            }
        }

        #region Virtual Methods

        /// <summary>
        /// Override this to configure additional implemenation-specific settings
        /// </summary>
        /// <param name="settings"></param>
        protected virtual void OnConfigure(SessionSettings settings)
        { }

        [System.Obsolete("This method's intended purpose is unclear.  Don't use it.")]
        protected virtual void OnInitialize(SessionSettings settings)
        { }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Implemented to start connecting to targets.
        /// </summary>
        protected abstract void OnStart();
        /// <summary>
        /// Implemented to connect and poll for events.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        protected abstract bool OnPoll(double timeout);
        /// <summary>
        /// Implemented to stop a running initiator.
        /// </summary>
        protected abstract void OnStop();
        /// <summary>
        /// Implemented to connect a session to its target.
        /// </summary>
        /// <param name="sessionID"></param>
        /// <param name="settings"></param>
        protected abstract void DoConnect(SessionID sessionID, QuickFix.Dictionary settings);

        #endregion

        #region Protected Methods

        protected void Connect()
        {
            foreach (var kv in _sessions)
            {
                if (kv.Value.ConnectionStatus == ConnectionStatus.Disconnected)
                {
                    ISession session = Session.LookupSession(kv.Key);
                    if (session.IsEnabled)
                    {
                        if (session.IsNewSession)
                            session.Reset("New session");
                        if (session.IsSessionTime)
                            DoConnect(kv.Key, _settings.Get(kv.Key));
                    }
                }
            }
        }

        private void SetStatus(SessionID sessionId, ConnectionStatus status)
        {
            SessionWrapper session;
            if (_sessions.TryGetValue(sessionId, out session))
                session.ConnectionStatus = status;
        }

        private bool CheckStatus(SessionID sessionId, ConnectionStatus status)
        {
            SessionWrapper session;
            return _sessions.TryGetValue(sessionId, out session) && session.ConnectionStatus == status;
        }

        protected void SetPending(SessionID sessionID)
        {
            SetStatus(sessionID, ConnectionStatus.Pending);
        }

        protected void SetConnected(SessionID sessionID)
        {
            SetStatus(sessionID, ConnectionStatus.Connected);
        }

        protected void SetDisconnected(SessionID sessionID)
        {
            SetStatus(sessionID, ConnectionStatus.Disconnected);
        }

        protected bool IsPending(SessionID sessionID)
        {
            return CheckStatus(sessionID, ConnectionStatus.Pending);
        }

        protected bool IsConnected(SessionID sessionID)
        {
            return CheckStatus(sessionID, ConnectionStatus.Connected);
        }

        protected bool IsDisconnected(SessionID sessionID)
        {
            return CheckStatus(sessionID, ConnectionStatus.Disconnected);
        }

        #endregion


        /// <summary>
        /// Get the SessionIDs for the sessions managed by this initiator.
        /// </summary>
        /// <returns>the SessionIDs for the sessions managed by this initiator</returns>
        public HashSet<SessionID> GetSessionIDs()
        {
            return new HashSet<SessionID>(_sessions.Keys);
        }

        /// <summary>
        /// Any subclasses of AbstractInitiator should override this if they have resources to dispose
        /// that aren't already covered in its OnStop() handler.
        /// Any override should call base.Dispose(disposing).
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            this.Stop();
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }

    internal class SessionWrapper
    {
        public SessionWrapper(ISession session)
        {
            Session = session;
        }

        public ConnectionStatus ConnectionStatus { get; set; }

        public ISession Session { get; private set; }
    }

    internal enum ConnectionStatus
    {
        Pending,
        Connected,
        Disconnected
    }
}
