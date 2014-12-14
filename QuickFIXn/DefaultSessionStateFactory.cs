namespace QuickFix
{
    public class DefaultSessionStateFactory : ISessionStateFactory
    {
        private readonly IMessageStoreFactory _storeFactory;
        private readonly ILogFactory _logfactory;
        private readonly int _heartBeatInterval;
        public DefaultSessionStateFactory(ILogFactory logfactory, int heartBeatInterval, IMessageStoreFactory storeFactory)
        {
            _logfactory = logfactory;
            _heartBeatInterval = heartBeatInterval;
            _storeFactory = storeFactory;
        }

        public ISessionState CreateState(SessionID sessionId)
        {
            ILog log;
            if (null != _logfactory)
                log = _logfactory.Create(sessionId);
            else
                log = new NullLog();

            return new SessionState(log, _heartBeatInterval)
            {
                MessageStore = _storeFactory.Create(sessionId)
            };
        }
    }
}