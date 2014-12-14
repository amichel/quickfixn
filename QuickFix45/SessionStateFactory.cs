using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickFix;

namespace QuickFix45
{
    public class SessionStateFactory : ISessionStateFactory
    {
        private readonly IMessageStoreFactory _storeFactory;
        private readonly ILogFactory _logfactory;
        private readonly int _heartBeatInterval;
        public SessionStateFactory(ILogFactory logfactory, int heartBeatInterval, IMessageStoreFactory storeFactory)
        {
            _logfactory = logfactory ?? new NullLogFactory();
            _heartBeatInterval = heartBeatInterval;
            _storeFactory = storeFactory;
        }

        public ISessionState CreateState(SessionID sessionId)
        {
            var log = _logfactory.Create(sessionId);

            return new QuickFix45.SessionState(log, _heartBeatInterval)
            {
                MessageStore = _storeFactory.Create(sessionId)
            };
        }
    }
}
