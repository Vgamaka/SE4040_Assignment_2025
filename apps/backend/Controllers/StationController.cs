using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvCharge.Api.Infrastructure.Errors;
using System.IdentityModel.Tokens.Jwt;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StationController : ControllerBase
    {
        private readonly IStationService _service;

        public StationController(IStationService service)
        {
            _service = service;
        }

        /// <summary>Create a new station (BackOffice/Admin). If caller is BackOffice, station is stamped to that BackOffice.</summary>
        [Authorize(Roles = "BackOffice,Admin")]
        [HttpPost]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Create([FromBody] StationCreateRequest req, CancellationToken ct)
        {
            try
            {
                var actorNic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.Identity?.Name ?? "system";
                var isBackOffice = User.IsInRole("BackOffice");
                var res = await _service.CreateAsync(req, actorNic, isBackOffice, ct);
                return CreatedAtAction(nameof(GetById), new { id = res.Id }, res);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Code, message = ex.Message });
            }
            catch (UpdateException ex) when (ex.Code == "Forbidden" || ex.Code == "BackOfficeNotApproved")
            {
                return Problem(statusCode: StatusCodes.Status403Forbidden, title: ex.Code, detail: ex.Message);
            }
        }

        /// <summary>Get station by id.</summary>
        [AllowAnonymous]
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] string id, CancellationToken ct)
        {
            try
            {
                var res = await _service.GetByIdAsync(id, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "StationNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
        }

        /// <summary>Update station (BackOffice/Admin).</summary>
        [Authorize(Roles = "BackOffice,Admin")]
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update([FromRoute] string id, [FromBody] StationUpdateRequest req, CancellationToken ct)
        {
            try
            {
                var actor = User.Identity?.Name ?? "system";
                var res = await _service.UpdateAsync(id, req, actor, ct);
                return Ok(res);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Code, message = ex.Message });
            }
            catch (NotFoundException ex) when (ex.Code == "StationNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
            catch (UpdateException ex) when (ex.Code == "ConcurrencyConflict")
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Code, detail: ex.Message);
            }
        }

        /// <summary>Deactivate station (BackOffice/Admin).</summary>
        [Authorize(Roles = "BackOffice,Admin")]
        [HttpPut("{id}/deactivate")]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Deactivate([FromRoute] string id, CancellationToken ct)
        {
            try
            {
                var actor = User.Identity?.Name ?? "system";
                var res = await _service.DeactivateAsync(id, actor, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "StationNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
        }

        /// <summary>Activate station (BackOffice/Admin).</summary>
        [Authorize(Roles = "BackOffice,Admin")]
        [HttpPut("{id}/activate")]
        [ProducesResponseType(typeof(StationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Activate([FromRoute] string id, CancellationToken ct)
        {
            try
            {
                var actor = User.Identity?.Name ?? "system";
                var res = await _service.ActivateAsync(id, actor, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "StationNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
        }

        /// <summary>List stations (public) with optional filters & paging.</summary>
        [AllowAnonymous]
        [HttpGet]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] string? type, [FromQuery] string? status = "Active", [FromQuery] int? minConnectors = null,
                                              [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var (items, total) = await _service.ListAsync(type, status, minConnectors, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
            return Ok(new { total, items });
        }

        /// <summary>Nearby search (public) with 7-day availability summary.</summary>
        [AllowAnonymous]
        [HttpGet("nearby")]
        [ProducesResponseType(typeof(List<StationListItem>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Nearby([FromQuery] double lat, [FromQuery] double lng, [FromQuery] double radiusKm = 5, [FromQuery] string? type = null, CancellationToken ct = default)
        {
            var items = await _service.NearbyAsync(lat, lng, radiusKm, type, ct);
            return Ok(items);
        }

        /// <summary>Get station schedule (public).</summary>
        [AllowAnonymous]
        [HttpGet("{id}/schedule")]
        [ProducesResponseType(typeof(StationScheduleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSchedule([FromRoute] string id, CancellationToken ct)
        {
            try
            {
                var res = await _service.GetScheduleAsync(id, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "StationNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
        }

        /// <summary>Upsert station schedule (BackOffice/Admin).</summary>
        [Authorize(Roles = "BackOffice,Admin")]
        [HttpPut("{id}/schedule")]
        [ProducesResponseType(typeof(StationScheduleResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpsertSchedule([FromRoute] string id, [FromBody] StationScheduleUpsertRequest req, CancellationToken ct)
        {
            try
            {
                var actor = User.Identity?.Name ?? "system";
                var res = await _service.UpsertScheduleAsync(id, req, actor, ct);
                return Ok(res);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Code, message = ex.Message });
            }
            catch (NotFoundException ex) when (ex.Code == "StationNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
        }
    }
}
