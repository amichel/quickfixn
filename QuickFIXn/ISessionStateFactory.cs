namespace QuickFix
{
    public interface ISessionStateFactory
    {
        ISessionState CreateState(SessionID sessionId);
    }
}