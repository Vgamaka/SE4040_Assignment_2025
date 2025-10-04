using System.Threading;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QrController : ControllerBase
    {
        private readonly ISessionService _sessions;
        private readonly IBookingService _bookings;

        public QrController(ISessionService sessions, IBookingService bookings)
        {
            _sessions = sessions;
            _bookings = bookings;
        }

        /// <summary>Verify a QR token (Operator / staff use this at the station).</summary>
        [Authorize(Roles = "Operator,BackOffice,Admin")]
        [HttpPost("verify")]
        [ProducesResponseType(typeof(QrVerifyResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<QrVerifyResponse>> Verify([FromBody] QrVerifyRequest req, CancellationToken ct)
        {
            var res = await _sessions.VerifyQrAsync(req, ct);
            if (!res.Valid) return Unauthorized(res);
            return Ok(res);
        }

        /// <summary>Issue (or re-issue) a QR token for an Approved booking (Owner or staff).</summary>
        [Authorize(Roles = "Owner,BackOffice,Admin")]
        [HttpPost("issue/{bookingId}")]
        [ProducesResponseType(typeof(BookingApprovalResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Issue(string bookingId, CancellationToken ct)
        {
            var nic = (
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                ?? ""
            ).Trim().ToUpperInvariant();

            var isStaff = User.IsInRole("Admin") || User.IsInRole("BackOffice");

            try
            {
                var res = await _bookings.IssueQrAsync(bookingId, nic, isStaff, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "BookingNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
            catch (UpdateException ex) when (ex.Code == "Forbidden")
            {
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: ex.Code, detail: ex.Message);
            }
            catch (UpdateException ex) when (ex.Code == "InvalidState")
            {
                return BadRequest(new { error = ex.Code, message = ex.Message });
            }
            catch (UpdateException ex) when (ex.Code == "ConcurrencyConflict")
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Code, detail: ex.Message);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Code, message = ex.Message });
            }
        }
    }
}
