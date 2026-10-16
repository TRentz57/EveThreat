namespace EveThreat.Server.IServices
{
    public interface IEveAuthService
    {
        string BuildLoginUrl();
        Task ProcessCallbackAsync(string code, string state);
        Task<string> RefreshAccessTokenAsync();
    }
}
