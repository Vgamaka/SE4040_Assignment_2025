using System.IdentityModel.Tokens.Jwt;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;  

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _service;
        public BookingController(IBookingService service) { _service = service; }

        // -------- Owner endpoints --------
        [Authorize]
        [HttpPost]
        [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] BookingCreateRequest req, CancellationToken ct)
        {
            try
            {
                var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.Identity?.Name ?? "";
                var res = await _service.CreateAsync(nic, req, ct);
                return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
            }
            catch (ValidationException ex) { return BadRequest(new { error = ex.Code, message = ex.Message }); }
            catch (UpdateException ex) when (ex.Code is "CapacityFull" or "StationNotActive" or "StationClosed")
            { return Conflict(new { error = ex.Code, message = ex.Message }); }
        }

        [Authorize]
        [HttpGet("mine")]
        [ProducesResponseType(typeof(List<BookingListItem>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Mine([FromQuery] string? status, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken ct)
        {
            var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.Identity?.Name ?? "";
            var list = await _service.GetMineAsync(nic, status, fromUtc, toUtc, ct);
            return Ok(list);
        }

        [Authorize]
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetById([FromRoute] string id, CancellationToken ct)
        {
            try
            {
                var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.Identity?.Name ?? "";
                var isStaff = User.IsInRole("BackOffice") || User.IsInRole("Admin");
                var res = await _service.GetByIdAsync(id, nic, isStaff, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "BookingNotFound")
            { return NotFound(new { error = ex.Code, message = ex.Message }); }
            catch (UpdateException ex) when (ex.Code == "Forbidden")
            { return Forbid(); }
        }

        [Authorize]
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Update([FromRoute] string id, [FromBody] BookingUpdateRequest req, CancellationToken ct)
        {
            try
            {
                var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.Identity?.Name ?? "";
                var res = await _service.UpdateAsync(id, nic, req, ct);
                return Ok(res);
            }
            catch (ValidationException ex) { return BadRequest(new { error = ex.Code, message = ex.Message }); }
            catch (UpdateException ex) when (ex.Code is "Forbidden") { return Forbid(); }
            catch (UpdateException ex) when (ex.Code is "CapacityFull" or "CancelCutoff")
            { return Conflict(new { error = ex.Code, message = ex.Message }); }
            catch (NotFoundException ex) when (ex.Code == "BookingNotFound")
            { return NotFound(new { error = ex.Code, message = ex.Message }); }
        }

[Authorize(Roles = "Owner,Operator")]
[HttpPost("{id}/cancel")]
[ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> Cancel([FromRoute] string id, CancellationToken ct)
{
    try
    {
        var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.Identity?.Name
                  ?? "";
        var res = await _service.CancelAsync(id, nic, ct);
        return Ok(res);
    }
    catch (UpdateException ex) when (ex.Code is "Forbidden") { return Forbid(); }
    catch (UpdateException ex) when (ex.Code is "CancelCutoff" or "InvalidState")
    { return Conflict(new { error = ex.Code, message = ex.Message }); }
    catch (NotFoundException ex) when (ex.Code == "BookingNotFound")
    { return NotFound(new { error = ex.Code, message = ex.Message }); }
}


        // -------- BackOffice/Admin --------
        [Authorize(Roles = "BackOffice,Admin")]
        [HttpGet]
        [ProducesResponseType(typeof(List<BookingListItem>), StatusCodes.Status200OK)]
        public async Task<IActionResult> AdminList([FromQuery] string? stationId, [FromQuery] string? status, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken ct)
        {
            var list = await _service.AdminListAsync(stationId, status, fromUtc, toUtc, ct);
            return Ok(list);
        }

        [Authorize(Roles = "BackOffice,Admin,Operator")]
        [HttpPut("{id}/approve")]
        [ProducesResponseType(typeof(BookingApprovalResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Approve([FromRoute] string id, CancellationToken ct)
        {
            try
            {
                var nic = User.Identity?.Name ?? "staff";
                var res = await _service.ApproveAsync(id, nic!, ct);
                return Ok(res);
            }
            catch (UpdateException ex) when (ex.Code is "AlreadyFinalized" or "BookingStartedOrPast")
            { return Conflict(new { error = ex.Code, message = ex.Message }); }
            catch (NotFoundException ex) when (ex.Code == "BookingNotFound")
            { return NotFound(new { error = ex.Code, message = ex.Message }); }
        }

        [Authorize(Roles = "BackOffice,Admin,Operator")]
        [HttpPut("{id}/reject")]
        [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reject([FromRoute] string id, CancellationToken ct)
        {
            try
            {
                var nic = User.Identity?.Name ?? "staff";
                var res = await _service.RejectAsync(id, nic!, ct);
                return Ok(res);
            }
            catch (UpdateException ex) when (ex.Code is "AlreadyFinalized" or "BookingStartedOrPast")
            { return Conflict(new { error = ex.Code, message = ex.Message }); }
            catch (NotFoundException ex) when (ex.Code == "BookingNotFound")
            { return NotFound(new { error = ex.Code, message = ex.Message }); }
        }
    }
}
