using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickFix;

namespace QuickFix45
{
    public class SessionFactory : QuickFix.SessionFactory
    {
        public SessionFactory(IApplication app, IMessageStoreFactory storeFactory)
            : base(app, storeFactory)
        {
        }

        public SessionFactory(IApplication app, IMessageStoreFactory storeFactory, ILogFactory logFactory)
            : base(app, storeFactory, logFactory)
        {
        }

        public SessionFactory(IApplication app, IMessageStoreFactory storeFactory, ILogFactory logFactory, IMessageFactory messageFactory)
            : base(app, storeFactory, logFactory, messageFactory)
        {
        }

        protected override ISession CreateSession(SessionID sessionID, Dictionary settings, DataDictionaryProvider dd, int heartBtInt,
            string senderDefaultApplVerId)
        {
            return new Session(
                application_,
                messageStoreFactory_,
                sessionID,
                dd,
                new SessionSchedule(settings),
                heartBtInt,
                logFactory_,
                messageFactory_,
                senderDefaultApplVerId);
        }
    }
}
