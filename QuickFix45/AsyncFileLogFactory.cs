
using QuickFix;

namespace QuickFix45
{
    /// <summary>
    /// Creates a message store that stores messages in a file
    /// </summary>
    public class AsyncFileLogFactory : ILogFactory
    {
        readonly SessionSettings _settings;

        #region LogFactory Members

        public AsyncFileLogFactory(SessionSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Creates a file-based message store
        /// </summary>
        /// <param name="sessionId">session ID for the message store</param>
        /// <returns></returns>
        public ILog Create(SessionID sessionId)
        {
            return new AsyncFileLog(_settings.Get(sessionId).GetString(SessionSettings.FILE_LOG_PATH), sessionId);
        }

        #endregion
    }
}
