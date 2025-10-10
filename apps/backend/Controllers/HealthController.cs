using Microsoft.AspNetCore.Mvc;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok(new { status = "ok", utc = DateTime.UtcNow });
    }
}
