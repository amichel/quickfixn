using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using QuickFix;

namespace QuickFix45
{
    /// <summary>
    /// In-memory message store implementation
    /// </summary>
    public class MemoryStore : IMessageStore
    {
        #region Private Members

        readonly ConcurrentDictionary<int, string> _messages = new ConcurrentDictionary<int, string>();
        int _nextSenderMsgSeqNum;
        int _nextTargetMsgSeqNum;
        long _creationTime;

        #endregion

        public MemoryStore()
        {
            Reset();
        }

        public void Get(int begSeqNo, int endSeqNo, List<string> messages)
        {
            for (int current = begSeqNo; current <= endSeqNo; current++)
            {
                string message;
                if (_messages.TryGetValue(current, out message))
                    messages.Add(message);
            }
        }

        #region MessageStore Members

        public bool Set(int msgSeqNum, string msg)
        {
            _messages[msgSeqNum] = msg;
            return true;
        }

        public int GetNextSenderMsgSeqNum()
        { return _nextSenderMsgSeqNum; }

        public int GetNextTargetMsgSeqNum()
        { return _nextTargetMsgSeqNum; }

        public void SetNextSenderMsgSeqNum(int value)
        { Interlocked.Exchange(ref _nextSenderMsgSeqNum, value); }

        public void SetNextTargetMsgSeqNum(int value)
        { Interlocked.Exchange(ref _nextTargetMsgSeqNum, value); }

        public void IncrNextSenderMsgSeqNum()
        { ++_nextSenderMsgSeqNum; }

        public void IncrNextTargetMsgSeqNum()
        { Interlocked.Increment(ref _nextTargetMsgSeqNum); }

        public System.DateTime? CreationTime
        {
            get
            {
                var time = _creationTime;
                return time == 0 ? default(System.DateTime?) : DateTime.FromBinary(time);
            }
            internal set { Interlocked.Exchange(ref _creationTime, value.HasValue ? value.Value.ToBinary() : 0); }
        }

        [System.Obsolete("Use CreationTime instead")]
        public DateTime GetCreationTime()
        {
            throw new NotImplementedException();
        }


        public void Reset()
        {
            Interlocked.Exchange(ref  _nextSenderMsgSeqNum, 1);
            Interlocked.Exchange(ref  _nextTargetMsgSeqNum, 1);
            _messages.Clear();
            Interlocked.Exchange(ref _creationTime, DateTime.UtcNow.ToBinary());
        }

        public void Refresh()
        { }

        public void Dispose()
        { }

        #endregion
    }
}
