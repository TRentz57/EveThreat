using EveThreat.Server.IServices;
using Microsoft.AspNetCore.Mvc;

namespace EveThreat.Server.Controllers
{
    public class IntelController : Controller
    {
        private readonly IIntelService _intel;

        public IntelController(IIntelService intel)
        {
            _intel = intel;
        }

        [HttpGet("api/intel/{systemId}")]
        public async Task<IActionResult> GetIntel(int systemId)
        {
            var result = await _intel.GetIntelAsync();
            return Ok(result);
        }

        [HttpGet("intel")]
        public async Task<IActionResult> GetIntel()
        {
            var result = await _intel.GetIntelAsync();
            return Ok(result);
        }
    }
}
