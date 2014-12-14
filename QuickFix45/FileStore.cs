using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using QuickFix;
using QuickFix.Util;

namespace QuickFix45
{
    /// <summary>
    /// File store implementation
    /// </summary>
    public class FileStore : IMessageStore
    {
        private class MsgDef
        {
            public long Index { get; private set; }
            public int Size { get; private set; }

            public MsgDef(long index, int size)
            {
                this.Index = index;
                this.Size = size;
            }
        }

        private readonly string _seqNumsFileName;
        private readonly string _msgFileName;
        private readonly string _headerFileName;
        private readonly string _sessionFileName;

        private System.IO.FileStream _seqNumsFile;
        private System.IO.FileStream _msgFile;
        private System.IO.StreamWriter _headerFile;

        private readonly object _seqNumsFileLocker = new object();
        private readonly object _msgFileLocker = new object();
        private readonly object _headerFileLocker = new object();
        private readonly object _sessionFileLocker = new object();


        private readonly MemoryStore _cache = new MemoryStore();

        readonly ConcurrentDictionary<int, MsgDef> _offsets = new ConcurrentDictionary<int, MsgDef>();

        public static string Prefix(SessionID sessionID)
        {
            StringBuilder prefix = new System.Text.StringBuilder(sessionID.BeginString)
                .Append('-').Append(sessionID.SenderCompID);
            if (SessionID.IsSet(sessionID.SenderSubID))
                prefix.Append('_').Append(sessionID.SenderSubID);
            if (SessionID.IsSet(sessionID.SenderLocationID))
                prefix.Append('_').Append(sessionID.SenderLocationID);
            prefix.Append('-').Append(sessionID.TargetCompID);
            if (SessionID.IsSet(sessionID.TargetSubID))
                prefix.Append('_').Append(sessionID.TargetSubID);
            if (SessionID.IsSet(sessionID.TargetLocationID))
                prefix.Append('_').Append(sessionID.TargetLocationID);

            if (SessionID.IsSet(sessionID.SessionQualifier))
                prefix.Append('-').Append(sessionID.SessionQualifier);

            return prefix.ToString();
        }

        public FileStore(string path, SessionID sessionID)
        {
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);

            string prefix = Prefix(sessionID);

            _seqNumsFileName = System.IO.Path.Combine(path, prefix + ".seqnums");
            _msgFileName = System.IO.Path.Combine(path, prefix + ".body");
            _headerFileName = System.IO.Path.Combine(path, prefix + ".header");
            _sessionFileName = System.IO.Path.Combine(path, prefix + ".session");

            Open();
        }

        private void Open()
        {
            ConstructFromFileCache();
            lock (_sessionFileLocker)
                InitializeSessionCreateTime();

            _seqNumsFile = new System.IO.FileStream(_seqNumsFileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite);
            _msgFile = new System.IO.FileStream(_msgFileName, System.IO.FileMode.OpenOrCreate, System.IO.FileAccess.ReadWrite);
            _headerFile = new System.IO.StreamWriter(_headerFileName, true);
        }

        private void PurgeSingleFile(System.IO.Stream stream, string filename)
        {
            if (stream != null)
                stream.Close();
            if (System.IO.File.Exists(filename))
                System.IO.File.Delete(filename);
        }

        private void PurgeSingleFile(System.IO.StreamWriter stream, string filename)
        {
            if (stream != null)
                stream.Close();
            if (System.IO.File.Exists(filename))
                System.IO.File.Delete(filename);
        }

        private void PurgeSingleFile(string filename)
        {
            if (System.IO.File.Exists(filename))
                System.IO.File.Delete(filename);
        }

        private void PurgeFileCache()
        {
            lock (_seqNumsFileLocker)
                PurgeSingleFile(_seqNumsFile, _seqNumsFileName);

            lock (_msgFileLocker)
                PurgeSingleFile(_msgFile, _msgFileName);

            lock (_headerFileLocker)
                PurgeSingleFile(_headerFile, _headerFileName);

            lock (_sessionFileLocker)
                PurgeSingleFile(_sessionFileName);
        }


        private void ConstructFromFileCache()
        {
            _offsets.Clear();
            if (System.IO.File.Exists(_headerFileName))
            {
                using (var reader = new System.IO.StreamReader(_headerFileName))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] headerParts = line.Split(',');
                        if (headerParts.Length == 3)
                        {
                            _offsets[Convert.ToInt32(headerParts[0])] = new MsgDef(
                                Convert.ToInt64(headerParts[1]), Convert.ToInt32(headerParts[2]));
                        }
                    }
                }
            }

            if (System.IO.File.Exists(_seqNumsFileName))
            {
                using (var seqNumReader = new System.IO.StreamReader(_seqNumsFileName))
                {
                    string[] parts = seqNumReader.ReadToEnd().Split(':');
                    if (parts.Length == 2)
                    {
                        _cache.SetNextSenderMsgSeqNum(Convert.ToInt32(parts[0]));
                        _cache.SetNextTargetMsgSeqNum(Convert.ToInt32(parts[1]));
                    }
                }
            }
        }

        private void InitializeSessionCreateTime()
        {
            if (System.IO.File.Exists(_sessionFileName) && new System.IO.FileInfo(_sessionFileName).Length > 0)
            {
                using (var reader = new System.IO.StreamReader(_sessionFileName))
                {
                    string s = reader.ReadToEnd();
                    _cache.CreationTime = UtcDateTimeSerializer.FromString(s);
                }
            }
            else
            {
                using (var writer = new System.IO.StreamWriter(_sessionFileName, false))
                {
                    if (_cache.CreationTime != null)
                        writer.Write(UtcDateTimeSerializer.ToString(_cache.CreationTime.Value));
                }
            }
        }


        #region MessageStore Members

        /// <summary>
        /// Get messages within the range of sequence numbers
        /// </summary>
        /// <param name="startSeqNum"></param>
        /// <param name="endSeqNum"></param>
        /// <param name="messages"></param>
        public void Get(int startSeqNum, int endSeqNum, List<string> messages)
        {
            for (int i = startSeqNum; i <= endSeqNum; i++)
            {
                MsgDef value;
                if (_offsets.TryGetValue(i, out value))
                {
                    var msgBytes = new byte[value.Size];
                    lock (_msgFileLocker)
                    {
                        _msgFile.Seek(value.Index, System.IO.SeekOrigin.Begin);
                        _msgFile.Read(msgBytes, 0, msgBytes.Length);
                    }
                    messages.Add(Encoding.UTF8.GetString(msgBytes));
                }
            }
        }

        /// <summary>
        /// Store a message
        /// </summary>
        /// <param name="msgSeqNum"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public bool Set(int msgSeqNum, string msg)
        {
            lock (_msgFileLocker)
            {
                _msgFile.Seek(0, System.IO.SeekOrigin.End);

                long offset = _msgFile.Position;
                byte[] msgBytes = Encoding.UTF8.GetBytes(msg);
                int size = msgBytes.Length;

                var b = new StringBuilder();
                b.Append(msgSeqNum).Append(",").Append(offset).Append(",").Append(size);
                lock (_headerFileLocker)
                {
                    _headerFile.WriteLine(b.ToString());
                    _headerFile.Flush();
                }

                _offsets[msgSeqNum] = new MsgDef(offset, size);
                _msgFile.Write(msgBytes, 0, size);
                _msgFile.Flush();
            }

            return true;
        }

        public int GetNextSenderMsgSeqNum()
        {
            return _cache.GetNextSenderMsgSeqNum();
        }

        public int GetNextTargetMsgSeqNum()
        {
            return _cache.GetNextTargetMsgSeqNum();
        }

        public void SetNextSenderMsgSeqNum(int value)
        {
            _cache.SetNextSenderMsgSeqNum(value);
            SetSeqNum();
        }

        public void SetNextTargetMsgSeqNum(int value)
        {
            _cache.SetNextTargetMsgSeqNum(value);
            SetSeqNum();
        }

        public void IncrNextSenderMsgSeqNum()
        {
            _cache.IncrNextSenderMsgSeqNum();
            SetSeqNum();
        }

        public void IncrNextTargetMsgSeqNum()
        {
            _cache.IncrNextTargetMsgSeqNum();
            SetSeqNum();
        }

        private void SetSeqNum()
        {
            lock (_seqNumsFileLocker)
            {
                _seqNumsFile.Seek(0, System.IO.SeekOrigin.Begin);
                var writer = new System.IO.StreamWriter(_seqNumsFile);
                writer.Write(GetNextSenderMsgSeqNum().ToString("D10") + " : " + GetNextTargetMsgSeqNum().ToString("D10") + "  ");
                writer.Flush();
            }
        }

        public DateTime? CreationTime
        {
            get
            {
                return _cache.CreationTime;
            }
        }

        [System.Obsolete("Use CreationTime instead")]
        public DateTime GetCreationTime()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            _cache.Reset();
            PurgeFileCache();
            Open();
        }

        public void Refresh()
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            lock (_seqNumsFileLocker)
                _seqNumsFile.Dispose();

            lock (_msgFileLocker)
                _msgFile.Dispose();

            lock (_headerFileLocker)
                _headerFile.Dispose();
        }

        #endregion
    }
}
