using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using QuickFix;

namespace QuickFix45
{
    // v2 TODO - consider making this internal

    /// <summary>
    /// Used by the session communications code. Not intended to be used by applications.
    /// </summary>
    public class SessionState : ISessionState
    {
        #region Private Members

        private int _isEnabled = 0;
        private int _receivedLogon = 0;
        private int _receivedReset = 0;
        private int _sentLogon = 0;
        private int _sentLogout = 0;
        private int _sentReset = 0;
        private string _logoutReason = "";
        private int _testRequestCounter = 0;
        private int _heartBtInt = 0;
        private long _lastReceivedTimeDt = DateTime.MinValue.ToBinary();
        private long _lastSentTimeDt = DateTime.MinValue.ToBinary();
        private int _logonTimeout = 10;
        private int _logoutTimeout = 2;
        private readonly ResendRange _resendRange = new ResendRange();
        private readonly ConcurrentDictionary<int, Message> _msgQueue = new ConcurrentDictionary<int, Message>();

        private readonly ILog _log;

        #endregion

        #region Unsynchronized Properties

        public IMessageStore MessageStore
        { get; set; }

        public bool IsInitiator
        { get; set; }

        public bool ShouldSendLogon
        { get { return IsInitiator && !SentLogon; } }

        public ILog Log
        { get { return _log; } }

        #endregion

        #region Synchronized Properties

        public bool IsEnabled
        {
            get { return _isEnabled != 0; }
            set { Interlocked.Exchange(ref _isEnabled, value ? 1 : 0); }
        }

        public bool ReceivedLogon
        {
            get { return _receivedLogon != 0; }
            set { Interlocked.Exchange(ref _receivedLogon, value ? 1 : 0); }
        }

        public bool ReceivedReset
        {
            get { return _receivedReset != 0; }
            set { Interlocked.Exchange(ref _receivedReset, value ? 1 : 0); }
        }
        public bool SentLogon
        {
            get { return _sentLogon != 0; }
            set { Interlocked.Exchange(ref _sentLogon, value ? 1 : 0); }
        }
        public bool SentLogout
        {
            get { return _sentLogout != 0; }
            set { Interlocked.Exchange(ref _sentLogout, value ? 1 : 0); }
        }
        public bool SentReset
        {
            get { return _sentReset != 0; }
            set { Interlocked.Exchange(ref _sentReset, value ? 1 : 0); }
        }

        public string LogoutReason
        {
            get { return _logoutReason; }
            set { Interlocked.Exchange(ref  _logoutReason, value); }
        }

        public int TestRequestCounter
        {
            get { return _testRequestCounter; }
            set { Interlocked.Exchange(ref  _testRequestCounter, value); }
        }

        public int HeartBtInt
        {
            get { return _heartBtInt; }
            set { Interlocked.Exchange(ref _heartBtInt, value); }
        }

        public int HeartBtIntAsMilliSecs
        {
            get { return HeartBtInt * 1000; }
        }

        public DateTime LastReceivedTimeDT
        {
            get { return DateTime.FromBinary(_lastReceivedTimeDt); }
            set { Interlocked.Exchange(ref _lastReceivedTimeDt, value.ToBinary()); }
        }
        public DateTime LastSentTimeDT
        {
            get { return DateTime.FromBinary(_lastSentTimeDt); }
            set { Interlocked.Exchange(ref _lastSentTimeDt, value.ToBinary()); }
        }

        public int LogonTimeout
        {
            get { return _logonTimeout; }
            set { Interlocked.Exchange(ref _logonTimeout, value); }
        }

        public long LogonTimeoutAsMilliSecs
        {
            get { return LogonTimeout * 1000; }
        }

        public int LogoutTimeout
        {
            get { return _logoutTimeout; }
            set { Interlocked.Exchange(ref _logoutTimeout, value); }
        }

        public long LogoutTimeoutAsMilliSecs
        {
            get { return LogoutTimeout * 1000; }
        }

        #endregion

        public SessionState(ILog log, int heartBtInt)
        {
            _log = log;
            this.HeartBtInt = heartBtInt;
            this.IsInitiator = (0 != heartBtInt);
            _lastReceivedTimeDt = DateTime.UtcNow.ToBinary();
            _lastSentTimeDt = DateTime.UtcNow.ToBinary();
        }

        /// <summary>
        /// All time args are in milliseconds
        /// </summary>
        /// <param name="now">current system time</param>
        /// <param name="lastReceivedTime">last received time</param>
        /// <param name="logonTimeout">number of milliseconds to wait for a Logon from the counterparty</param>
        /// <returns></returns>
        public static bool LogonTimedOut(DateTime now, long logonTimeout, DateTime lastReceivedTime)
        {
            return (now.Subtract(lastReceivedTime).TotalMilliseconds) >= logonTimeout;
        }
        public bool LogonTimedOut()
        {
            return LogonTimedOut(DateTime.UtcNow, this.LogonTimeoutAsMilliSecs, this.LastReceivedTimeDT);
        }

        /// <summary>
        /// All time args are in milliseconds
        /// </summary>
        /// <param name="now">current system datetime</param>
        /// <param name="heartBtIntMillis">heartbeat interval in milliseconds</param>
        /// <param name="lastReceivedTime">last received datetime</param>
        /// <returns>true if timed out</returns>
        public static bool TimedOut(DateTime now, int heartBtIntMillis, DateTime lastReceivedTime)
        {
            double elapsed = now.Subtract(lastReceivedTime).TotalMilliseconds;
            return elapsed >= (2.4 * heartBtIntMillis);
        }
        public bool TimedOut()
        {
            return TimedOut(DateTime.UtcNow, this.HeartBtIntAsMilliSecs, this.LastReceivedTimeDT);
        }

        /// <summary>
        /// All time args are in milliseconds
        /// </summary>
        /// <param name="now">current system time</param>
        /// <param name="sentLogout">true if a Logout has been sent to the counterparty, otherwise false</param>
        /// <param name="logoutTimeout">number of milliseconds to wait for a Logout from the counterparty</param>
        /// <param name="lastSentTime">last sent time</param>
        /// <returns></returns>
        public static bool LogoutTimedOut(DateTime now, bool sentLogout, long logoutTimeout, DateTime lastSentTime)
        {
            return sentLogout && ((now.Subtract(lastSentTime).TotalMilliseconds) >= logoutTimeout);
        }
        public bool LogoutTimedOut()
        {
            return LogoutTimedOut(DateTime.UtcNow, this.SentLogout, this.LogoutTimeoutAsMilliSecs, this.LastSentTimeDT);
        }

        /// <summary>
        /// All time args are in milliseconds
        /// </summary>
        /// <param name="now">current system time</param>
        /// <param name="heartBtIntMillis">heartbeat interval in milliseconds</param>
        /// <param name="lastReceivedTime">last received time</param>
        /// <param name="testRequestCounter">test request counter</param>
        /// <returns>true if test request is needed</returns>
        public static bool NeedTestRequest(DateTime now, int heartBtIntMillis, DateTime lastReceivedTime, int testRequestCounter)
        {
            double elapsedMilliseconds = now.Subtract(lastReceivedTime).TotalMilliseconds;
            return elapsedMilliseconds >= (1.2 * ((testRequestCounter + 1) * heartBtIntMillis));
        }
        public bool NeedTestRequest()
        {
            return NeedTestRequest(DateTime.UtcNow, this.HeartBtIntAsMilliSecs, this.LastReceivedTimeDT, this.TestRequestCounter);
        }

        /// <summary>
        /// All time args are in milliseconds
        /// </summary>
        /// <param name="now">current system time</param>
        /// <param name="heartBtIntMillis">heartbeat interval in milliseconds</param>
        /// <param name="lastSentTime">last sent time</param>
        /// <param name="testRequestCounter">test request counter</param>
        /// <returns>true if heartbeat is needed</returns>
        public static bool NeedHeartbeat(DateTime now, int heartBtIntMillis, DateTime lastSentTime, int testRequestCounter)
        {
            double elapsed = now.Subtract(lastSentTime).TotalMilliseconds;
            return (elapsed >= Convert.ToDouble(heartBtIntMillis)) && (0 == testRequestCounter);
        }
        public bool NeedHeartbeat()
        {
            return NeedHeartbeat(DateTime.UtcNow, this.HeartBtIntAsMilliSecs, this.LastSentTimeDT, this.TestRequestCounter);
        }

        /// <summary>
        /// All time args are in milliseconds
        /// </summary>
        /// <param name="now">current system time</param>
        /// <param name="heartBtIntMillis">heartbeat interval in milliseconds</param>
        /// <param name="lastSentTime">last sent time</param>
        /// <param name="lastReceivedTime">last received time</param>
        /// <returns>true if within heartbeat interval</returns>
        public static bool WithinHeartbeat(DateTime now, int heartBtIntMillis, DateTime lastSentTime, DateTime lastReceivedTime)
        {
            return ((now.Subtract(lastSentTime).TotalMilliseconds) < Convert.ToDouble(heartBtIntMillis))
                && ((now.Subtract(lastReceivedTime).TotalMilliseconds) < Convert.ToDouble(heartBtIntMillis));
        }
        public bool WithinHeartbeat()
        {
            return WithinHeartbeat(DateTime.UtcNow, this.HeartBtIntAsMilliSecs, this.LastSentTimeDT, this.LastReceivedTimeDT);
        }

        public ResendRange GetResendRange()
        {
            return _resendRange;
        }

        public void Get(int begSeqNo, int endSeqNo, List<string> messages)
        {
            MessageStore.Get(begSeqNo, endSeqNo, messages);
        }

        public void SetResendRange(int begin, int end, int chunkEnd = -1)
        {
            _resendRange.BeginSeqNo = begin;
            _resendRange.EndSeqNo = end;
            _resendRange.ChunkEndSeqNo = chunkEnd == -1 ? end : chunkEnd;
        }

        public bool ResendRequested()
        {
            return !(_resendRange.BeginSeqNo == 0 && _resendRange.EndSeqNo == 0);
        }

        public void Queue(int msgSeqNum, Message msg)
        {
            _msgQueue[msgSeqNum] = msg;
        }

        public void ClearQueue()
        {
            _msgQueue.Clear();
        }

        public QuickFix.Message Dequeue(int num)
        {
            Message msg = null;
            _msgQueue.TryRemove(num, out msg);
            return msg;
        }

        public Message Retrieve(int msgSeqNum)
        {
            Message msg = null;
            _msgQueue.TryGetValue(msgSeqNum, out msg);
            return msg;
        }

        /// <summary>
        /// All time values are displayed in milliseconds.
        /// </summary>
        /// <returns>a string that represents the session state</returns>
        public override string ToString()
        {
            return new System.Text.StringBuilder("SessionState ")
                .Append("[ Now=").Append(DateTime.UtcNow)
                .Append(", HeartBtInt=").Append(this.HeartBtIntAsMilliSecs)
                .Append(", LastSentTime=").Append(this.LastSentTimeDT)
                .Append(", LastReceivedTime=").Append(this.LastReceivedTimeDT)
                .Append(", TestRequestCounter=").Append(this.TestRequestCounter)
                .Append(", WithinHeartbeat=").Append(WithinHeartbeat())
                .Append(", NeedHeartbeat=").Append(NeedHeartbeat())
                .Append(", NeedTestRequest=").Append(NeedTestRequest())
                .Append(", ResendRange=").Append(GetResendRange())
                .Append(" ]").ToString();

        }

        #region MessageStore-manipulating Members

        public bool Set(int msgSeqNum, string msg)
        {
            return this.MessageStore.Set(msgSeqNum, msg);
        }

        public int GetNextSenderMsgSeqNum()
        {
            return this.MessageStore.GetNextSenderMsgSeqNum();
        }

        public int GetNextTargetMsgSeqNum()
        {
            return this.MessageStore.GetNextTargetMsgSeqNum();
        }

        public void SetNextSenderMsgSeqNum(int value)
        {
            this.MessageStore.SetNextSenderMsgSeqNum(value);
        }

        public void SetNextTargetMsgSeqNum(int value)
        {
            this.MessageStore.SetNextTargetMsgSeqNum(value);
        }

        public void IncrNextSenderMsgSeqNum()
        {
            this.MessageStore.IncrNextSenderMsgSeqNum();
        }

        public void IncrNextTargetMsgSeqNum()
        {
            this.MessageStore.IncrNextTargetMsgSeqNum();
        }

        public System.DateTime? CreationTime
        {
            get
            {
                return this.MessageStore.CreationTime;
            }
        }

        [Obsolete("Use Reset(reason) instead.")]
        public void Reset()
        {
            this.Reset("(unspecified reason)");
        }

        public void Reset(string reason)
        {
            this.MessageStore.Reset();
            this.Log.OnEvent("Session reset: " + reason);
        }

        public void Refresh()
        {
            this.MessageStore.Refresh();
        }

        #endregion

        public void Dispose()
        {
            if (_log != null) { _log.Dispose(); }
            if (MessageStore != null) { MessageStore.Dispose(); }
        }
    }
}

