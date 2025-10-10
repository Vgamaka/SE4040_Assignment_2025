using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvCharge.Api.Infrastructure.Errors;
using System.IdentityModel.Tokens.Jwt;
using EvCharge.Api.Infrastructure.Mapping;
using System.Security.Claims;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _admin;

        public AdminController(IAdminService admin)
        {
            _admin = admin;
        }

        /// <summary>Create an Admin account (Admin-only).</summary>
        [HttpPost("admins")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateAdmin([FromBody] AdminCreateRequest req, CancellationToken ct)
        {
            try
            {
                var actor = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "admin";
                var res = await _admin.CreateAdminAsync(req, actor, ct);
                return Created("", res);
            }
            catch (ValidationException ex) { return BadRequest(new { error = ex.Code, message = ex.Message }); }
            catch (RegistrationException ex) { return Conflict(new { error = ex.Code, message = ex.Message }); }
        }

/// <summary>List BackOffice applications (filter by status: Pending|Approved|Rejected).</summary>
[HttpGet("backoffices")]
[ProducesResponseType(typeof(object), StatusCodes.Status200OK)] // returns { total, items: AdminBackOfficeListItem[] }
public async Task<IActionResult> ListBackOffices([FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
{
    var (items, total) = await _admin.ListBackOfficeApplicationsAsync(status, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
    return Ok(new { total, items });
}

        /// <summary>Approve a BackOffice application.</summary>
        [HttpPut("backoffices/{nic}/approve")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApproveBackOffice(string nic, [FromBody] BackOfficeReviewRequest req, CancellationToken ct)
        {
            try
            {
                var reviewer = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "admin";
                var res = await _admin.ApproveBackOfficeAsync(nic, reviewer, req?.Notes, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "BackOfficeNotFound")
            { return NotFound(new { error = ex.Code, message = ex.Message }); }
            catch (ValidationException ex) { return BadRequest(new { error = ex.Code, message = ex.Message }); }
            catch (UpdateException ex) when (ex.Code == "ConcurrencyConflict")
            { return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Code, detail: ex.Message); }
        }

        /// <summary>Reject a BackOffice application (notes required).</summary>
        [HttpPut("backoffices/{nic}/reject")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RejectBackOffice(string nic, [FromBody] BackOfficeReviewRequest req, CancellationToken ct)
        {
            try
            {
                var reviewer = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "admin";
                var notes = req?.Notes ?? string.Empty;
                var res = await _admin.RejectBackOfficeAsync(nic, reviewer, notes, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "BackOfficeNotFound")
            { return NotFound(new { error = ex.Code, message = ex.Message }); }
            catch (ValidationException ex) { return BadRequest(new { error = ex.Code, message = ex.Message }); }
            catch (UpdateException ex) when (ex.Code == "ConcurrencyConflict")
            { return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Code, detail: ex.Message); }
        }

        /// <summary>List all users (ev_owners) with optional filters. SuperAdmin can set includeSensitive=true.</summary>
[HttpGet("users")]
[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
public async Task<IActionResult> ListAllUsers(
    [FromQuery] string? role = null,
    [FromQuery] string? q = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] bool includeSensitive = false,
    CancellationToken ct = default)
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);

    var isSuperAdmin = User.IsInRole("SuperAdmin");
    var allowSensitive = includeSensitive && isSuperAdmin;

    // Prefer paging at repo level; if you adopted the paged repo variant, pass page/pageSize through.
    var (allItems, total) = await _admin.ListAllOwnersAsync(role, q, allowSensitive, ct);

    // Manual page slice if repo returns full list:
    var items = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();

    return Ok(new { total, items, page, pageSize });
}

/// <summary>List all stations with filters (status/type/minConnectors/backOfficeNic/q).</summary>
[HttpGet("stations")]
[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
public async Task<IActionResult> ListAllStations(
    [FromQuery] string? status = null,
    [FromQuery] string? type = null,
    [FromQuery] int? minConnectors = null,
    [FromQuery] string? backOfficeNic = null,
    [FromQuery] string? q = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
{
    page = Math.Max(1, page);
    pageSize = Math.Clamp(pageSize, 1, 100);

    var (items, total) = await _admin.ListAllStationsAsync(type, status, minConnectors, backOfficeNic, q, page, pageSize, ct);
    return Ok(new { total, items, page, pageSize });
}

    }
}
