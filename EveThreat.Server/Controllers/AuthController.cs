using EveThreat.Server.IServices;
using EveThreat.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace EveThreat.Server.Controllers
{
    public class AuthController : Controller
    {
        private readonly IEveAuthService _auth;

        public AuthController(IEveAuthService auth)
        {
            _auth = auth;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            var url = _auth.BuildLoginUrl();
            return Redirect(url);
        }
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string code, string state)
        {
            await _auth.ProcessCallbackAsync(code, state);
            return Redirect("/data");
        }
    }
}
