using System;
using QuickFix.Fields;

namespace QuickFix
{
    public interface ISession : IDisposable
    {
        IMessageStore MessageStore { get; }
        ILog Log { get; }
        bool IsInitiator { get; }
        bool IsAcceptor { get; }
        bool IsEnabled { get; }
        bool IsSessionTime { get; }
        bool IsLoggedOn { get; }
        bool SentLogon { get; }
        bool ReceivedLogon { get; }
        bool IsNewSession { get; }

        /// <summary>
        /// Session setting for heartbeat interval (in seconds)
        /// </summary>
        int HeartBtInt { get; }

        /// <summary>
        /// Session setting for enabling message latency checks
        /// </summary>
        bool CheckLatency { get; set; }

        /// <summary>
        /// Session setting for maximum message latency (in seconds)
        /// </summary>
        int MaxLatency { get; set; }

        /// <summary>
        /// Send a logout if counterparty times out and does not heartbeat
        /// in response to a TestRequeset. Defaults to false
        /// </summary>
        bool SendLogoutBeforeTimeoutDisconnect { get; set; }

        /// <summary>
        /// Gets or sets the next expected outgoing sequence number
        /// </summary>
        int NextSenderMsgSeqNum { get; set; }

        /// <summary>
        /// Gets or sets the next expected incoming sequence number
        /// </summary>
        int NextTargetMsgSeqNum { get; set; }

        /// <summary>
        /// Logon timeout in seconds
        /// </summary>
        int LogonTimeout { get; set; }

        /// <summary>
        /// Logout timeout in seconds
        /// </summary>
        int LogoutTimeout { get; set; }

        /// <summary>
        /// Whether to persist messages or not. Setting to false forces quickfix 
        /// to always send GapFills instead of resending messages.
        /// </summary>
        bool PersistMessages { get; set; }

        /// <summary>
        /// Determines if session state should be restored from persistance
        /// layer when logging on. Useful for creating hot failover sessions.
        /// </summary>
        bool RefreshOnLogon { get; set; }

        /// <summary>
        /// Reset sequence numbers on logon request
        /// </summary>
        bool ResetOnLogon { get; set; }

        /// <summary>
        /// Reset sequence numbers to 1 after a normal logout
        /// </summary>
        bool ResetOnLogout { get; set; }

        /// <summary>
        /// Reset sequence numbers to 1 after abnormal termination
        /// </summary>
        bool ResetOnDisconnect { get; set; }

        /// <summary>
        /// Whether to send redundant resend requests
        /// </summary>
        bool SendRedundantResendRequests { get; set; }

        /// <summary>
        /// Whether to validate length and checksum of messages
        /// </summary>
        bool ValidateLengthAndChecksum { get; set; }

        /// <summary>
        /// Validates Comp IDs for each message
        /// </summary>
        bool CheckCompID { get; set; }

        /// <summary>
        /// Determines if milliseconds should be added to timestamps.
        /// Only avilable on FIX4.2. or greater
        /// </summary>
        bool MillisecondsInTimeStamp { get; set; }

        /// <summary>
        /// Adds the last message sequence number processed in the header (tag 369)
        /// </summary>
        bool EnableLastMsgSeqNumProcessed { get; set; }

        /// <summary>
        /// Ignores resend requests marked poss dup
        /// </summary>
        bool IgnorePossDupResendRequests { get; set; }

        /// <summary>
        /// Sets a maximum number of messages to request in a resend request.
        /// </summary>
        int MaxMessagesInResendRequest { get; set; }

        ApplVerID targetDefaultApplVerID { get; set; }
        string SenderDefaultApplVerID { get; set; }
        SessionID SessionID { get; set; }
        IApplication Application { get; set; }
        DataDictionaryProvider DataDictionaryProvider { get; set; }
        DataDictionary.DataDictionary SessionDataDictionary { get; }
        DataDictionary.DataDictionary ApplicationDataDictionary { get; }

        /// <summary>
        /// Returns whether the Session has a Responder. This method is synchronized
        /// </summary>
        bool HasResponder { get; }

        /// <summary>
        /// Returns whether the Sessions will allow ResetSequence messages sent as
        /// part of a resend request (PossDup=Y) to omit the OrigSendingTime
        /// </summary>
        bool RequiresOrigSendingTime { get; set; }

        /// <summary>
        /// Sends a message via the session indicated by the header fields
        /// </summary>
        /// <param name="message">message to send</param>
        /// <returns>true if was sent successfully</returns>
        bool Send(Message message);

        /// <summary>
        /// Sends a message
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        bool Send(string message);

        /// <summary>
        /// Sets some internal state variables.  Despite the name, it does do anything to make a logon occur.
        /// </summary>
        void Logon();

        /// <summary>
        /// Sets some internal state variables.  Despite the name, it does not cause a logout to occur.
        /// </summary>
        void Logout();

        /// <summary>
        /// Sets some internal state variables.  Despite the name, it does not cause a logout to occur.
        /// </summary>
        void Logout(string reason);

        /// <summary>
        /// Logs out from session and closes the network connection
        /// </summary>
        /// <param name="reason"></param>
        void Disconnect(string reason);

        /// <summary>
        /// There's no message to process, but check the session state to see if there's anything to do
        /// (e.g. send heartbeat, logout at end of session, etc)
        /// </summary>
        void Next();

        /// <summary>
        /// Process a message (in string form) from the counterparty
        /// </summary>
        /// <param name="msgStr"></param>
        void Next(string msgStr);

        /// <summary>
        /// Process a message from the counterparty. (TODO: consider changing this method to private in v2.0.)
        /// </summary>
        /// <param name="message"></param>
        void Next(Message message);

        bool Verify(Message message);
        bool Verify(Message msg, bool checkTooHigh, bool checkTooLow);
        void SetResponder(IResponder responder);

        /// FIXME
        void Refresh();

        [Obsolete("Use Reset(reason) instead.")]
        void Reset();

        /// <summary>
        /// Send a logout, disconnect, and reset session state
        /// </summary>
        /// <param name="loggedReason">reason for the reset (for the log)</param>
        void Reset(string loggedReason);

        /// <summary>
        /// Send a logout, disconnect, and reset session state
        /// </summary>
        /// <param name="loggedReason">reason for the reset (for the log)</param>
        /// <param name="logoutMessage">message to put in the Logout message's Text field (ignored if null/empty string)</param>
        void Reset(string loggedReason, string logoutMessage);

        bool GenerateTestRequest(string id);

        /// <summary>
        /// Send a basic Logout message
        /// </summary>
        /// <returns></returns>
        bool GenerateLogout();

        bool GenerateHeartbeat();
        bool GenerateHeartbeat(Message testRequest);
        bool GenerateReject(Message message, FixValues.SessionRejectReason reason);
        bool GenerateReject(Message message, FixValues.SessionRejectReason reason, int field);
        void Dispose();
    }
}