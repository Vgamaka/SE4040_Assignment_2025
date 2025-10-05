using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Operator,BackOffice,Admin")]
    public class OperatorController : ControllerBase
    {
        private readonly IOperatorService _svc;

        public OperatorController(IOperatorService svc) { _svc = svc; }

        // GET /api/Operator/inbox?date=YYYY-MM-DD
        [HttpGet("inbox")]
        [ProducesResponseType(typeof(List<OperatorInboxItem>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Inbox([FromQuery] string date, CancellationToken ct)
        {
            var nic = (
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                ?? ""
            ).Trim().ToUpperInvariant();

            var items = await _svc.InboxAsync(nic, date, ct);
            return Ok(items);
        }

        // POST /api/Operator/scan   { qrToken, bookingId? }
        [HttpPost("scan")]
        [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Scan([FromBody] OperatorScanRequest req, CancellationToken ct)
        {
            var nic = (
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                ?? ""
            ).Trim().ToUpperInvariant();

            var res = await _svc.ScanAsync(req, nic, User, ct);
            return Ok(res);
        }

        // POST /api/Operator/exception  { bookingId, reason: NoShow|Aborted|CustomerCancelOnSite, notes? }
        [HttpPost("exception")]
        [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Exception([FromBody] OperatorExceptionRequest req, CancellationToken ct)
        {
            var nic = (
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                ?? ""
            ).Trim().ToUpperInvariant();

            var res = await _svc.ExceptionAsync(req, nic, ct);
            return Ok(res);
        }
    }
}
