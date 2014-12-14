using System;
using System.Collections.Generic;

namespace QuickFix
{
    public interface ISessionState : IDisposable
    {
        IMessageStore MessageStore { get; set; }
        bool IsInitiator { get; set; }
        bool ShouldSendLogon { get; }
        ILog Log { get; }
        bool IsEnabled { get; set; }
        bool ReceivedLogon { get; set; }
        bool ReceivedReset { get; set; }
        bool SentLogon { get; set; }
        bool SentLogout { get; set; }
        bool SentReset { get; set; }
        string LogoutReason { get; set; }
        int TestRequestCounter { get; set; }
        int HeartBtInt { get; set; }
        int HeartBtIntAsMilliSecs { get; }
        DateTime LastReceivedTimeDT { get; set; }
        DateTime LastSentTimeDT { get; set; }
        int LogonTimeout { get; set; }
        long LogonTimeoutAsMilliSecs { get; }
        int LogoutTimeout { get; set; }
        long LogoutTimeoutAsMilliSecs { get; }
        System.DateTime? CreationTime { get; }
        bool LogonTimedOut();
        bool TimedOut();
        bool LogoutTimedOut();
        bool NeedTestRequest();
        bool NeedHeartbeat();
        bool WithinHeartbeat();
        ResendRange GetResendRange();
        void Get(int begSeqNo, int endSeqNo, List<string> messages);
        void SetResendRange(int begin, int end, int chunkEnd = -1);
        bool ResendRequested();
        void Queue(int msgSeqNum, Message msg);
        void ClearQueue();
        QuickFix.Message Dequeue(int num);
        Message Retrieve(int msgSeqNum);

        /// <summary>
        /// All time values are displayed in milliseconds.
        /// </summary>
        /// <returns>a string that represents the session state</returns>
        string ToString();

        bool Set(int msgSeqNum, string msg);
        int GetNextSenderMsgSeqNum();
        int GetNextTargetMsgSeqNum();
        void SetNextSenderMsgSeqNum(int value);
        void SetNextTargetMsgSeqNum(int value);
        void IncrNextSenderMsgSeqNum();
        void IncrNextTargetMsgSeqNum();

        [Obsolete("Use Reset(reason) instead.")]
        void Reset();

        void Reset(string reason);
        void Refresh();
    }
}