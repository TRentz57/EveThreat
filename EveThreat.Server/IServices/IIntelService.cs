using EveThreat.Server.Models;

namespace EveThreat.Server.IServices
{
    public interface IIntelService
    {
        Task<IntelResult> GetIntelAsync();
        Task<LocationResponse> GetLocationAsync();
    }
}
