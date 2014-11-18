using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace QuickFix
{
    /// <summary>
    /// Async File log implementation
    /// </summary>
    public class AsyncFileLog : ILog
    {
        private readonly BlockingCollection<string> _messagesBuffer = new BlockingCollection<string>();
        private readonly BlockingCollection<string> _eventsBuffer = new BlockingCollection<string>();
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

        private readonly string _fileLogPath;
        private string _prefix;

        public AsyncFileLog(string fileLogPath, string prefix = "GLOBAL")
        {
            _fileLogPath = fileLogPath;
            _prefix = prefix;
            Init();
        }

        public AsyncFileLog(string fileLogPath, SessionID sessionId)
            : this(fileLogPath, Prefix(sessionId))
        {
        }


        private void Init()
        {
            if (!Directory.Exists(_fileLogPath))
                Directory.CreateDirectory(_fileLogPath);

            Task.Factory.StartNew(FlushEventsBuffer, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(FlushMessagesBuffer, TaskCreationOptions.LongRunning);
        }

        private void FlushEventsBuffer()
        {
            FlushBuffer(_eventsBuffer, ".event.current.log");
        }

        private void FlushMessagesBuffer()
        {
            FlushBuffer(_messagesBuffer, ".messages.current.log");
        }

        private void FlushBuffer(BlockingCollection<string> buffer, string fileExtension)
        {
            var filename = Path.Combine(_fileLogPath, _prefix + fileExtension);
            using (var log = new StreamWriter(filename, true) { AutoFlush = true })
            {
                foreach (var message in buffer.GetConsumingEnumerable(_tokenSource.Token))
                    log.WriteLine(Fields.Converters.DateTimeConverter.Convert(DateTime.UtcNow) + " : " + message);

                string mess;
                while (buffer.TryTake(out mess))
                    log.WriteLine(Fields.Converters.DateTimeConverter.Convert(DateTime.UtcNow) + " : " + mess);
            }
        }

        private static string Prefix(SessionID sessionId)
        {
            var prefix = new System.Text.StringBuilder(sessionId.BeginString)
                .Append('-').Append(sessionId.SenderCompID);
            if (SessionID.IsSet(sessionId.SenderSubID))
                prefix.Append('_').Append(sessionId.SenderSubID);
            if (SessionID.IsSet(sessionId.SenderLocationID))
                prefix.Append('_').Append(sessionId.SenderLocationID);
            prefix.Append('-').Append(sessionId.TargetCompID);
            if (SessionID.IsSet(sessionId.TargetSubID))
                prefix.Append('_').Append(sessionId.TargetSubID);
            if (SessionID.IsSet(sessionId.TargetLocationID))
                prefix.Append('_').Append(sessionId.TargetLocationID);

            if (SessionID.IsSet(sessionId.SessionQualifier))
                prefix.Append('-').Append(sessionId.SessionQualifier);

            return prefix.ToString();
        }


        #region Log Members

        public void Clear()
        {
            _tokenSource.Cancel();
        }

        public void OnIncoming(string msg)
        {
            _messagesBuffer.Add(msg);
        }

        public void OnOutgoing(string msg)
        {
            _messagesBuffer.Add(msg);
        }

        public void OnEvent(string s)
        {
            _eventsBuffer.Add(s);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _tokenSource.Cancel();
        }

        #endregion
    }
}
