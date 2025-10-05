using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Operator,BackOffice,Admin")]
    public class SessionsController : ControllerBase
    {
        private readonly ISessionService _svc;
        public SessionsController(ISessionService svc) { _svc = svc; }

        [HttpPost("checkin")]
        public async Task<ActionResult<BookingResponse>> CheckIn([FromBody] SessionCheckInRequest req, CancellationToken ct)
        {
            var nic = User.FindFirstValue("nic") ?? "system";
            var res = await _svc.CheckInAsync(req, nic, User, ct);
            return Ok(res);
        }

        [HttpPost("finalize")]
        public async Task<ActionResult<SessionReceiptResponse>> Finalize([FromBody] SessionFinalizeRequest req, CancellationToken ct)
        {
            var nic = User.FindFirstValue("nic") ?? "system";
            var res = await _svc.FinalizeAsync(req, nic, User, ct);
            return Ok(res);
        }
    }
}


