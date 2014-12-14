using QuickFix;

namespace QuickFix45
{
    public class NullLogFactory : ILogFactory
    {
        #region LogFactory Members

        public NullLogFactory()
        { }

        public ILog Create(SessionID sessionId)
        {
            return new QuickFix.NullLog();
        }

        #endregion
    }
}
