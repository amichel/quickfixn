
namespace QuickFix
{
    /// <summary>
    /// Creates a message store that stores messages in a file
    /// </summary>
    public class AsyncFileLogFactory : ILogFactory
    {
        SessionSettings settings_;

        #region LogFactory Members

        public AsyncFileLogFactory(SessionSettings settings)
        {
            settings_ = settings;
        }

        /// <summary>
        /// Creates a file-based message store
        /// </summary>
        /// <param name="sessionId">session ID for the message store</param>
        /// <returns></returns>
        public ILog Create(SessionID sessionId)
        {
            return new AsyncFileLog(settings_.Get(sessionId).GetString(SessionSettings.FILE_LOG_PATH), sessionId);
        }

        #endregion
    }
}
