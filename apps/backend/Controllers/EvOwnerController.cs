using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvCharge.Api.Infrastructure.Errors;

namespace EvCharge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EvOwnerController : ControllerBase
    {
        private readonly IOwnerService _service;

        public EvOwnerController(IOwnerService service)
        {
            _service = service;
        }

        /// <summary>
        /// Register a new EV Owner (NIC as PK).
        /// </summary>
        [AllowAnonymous]
        [HttpPost]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] OwnerRegisterRequest req, CancellationToken ct)
        {
            try
            {
                var result = await _service.RegisterAsync(req, ct);
                return CreatedAtAction(nameof(GetByNic), new { nic = result.Nic }, result);
            }
            catch (RegistrationException ex) when (ex.Code is "InvalidNic" or "InvalidFullName" or "InvalidEmail" or "WeakPassword" or "InvalidPhone")
            {
                return BadRequest(new { error = ex.Code, message = ex.Message });
            }
            catch (RegistrationException ex) when (ex.Code is "DuplicateNic" or "DuplicateEmail")
            {
                return Conflict(new { error = ex.Code, message = ex.Message });
            }
        }

        /// <summary>
        /// Get an EV Owner by NIC.
        /// </summary>
        [Authorize]
        [HttpGet("{nic}")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByNic([FromRoute] string nic, CancellationToken ct)
        {
            try
            {
                var result = await _service.GetByNicAsync(nic, ct);
                return Ok(result);
            }
            catch (NotFoundException ex) when (ex.Code == "OwnerNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
        }

        /// <summary>
        /// Update an EV Owner profile (self or BackOffice/Admin).
        /// </summary>
        [Authorize]
        [HttpPut("{nic}")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update([FromRoute] string nic, [FromBody] OwnerUpdateRequest req, CancellationToken ct)
        {
            var actorNic = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.Identity?.Name
                        ?? string.Empty;

            bool isBackOffice = User.IsInRole("BackOffice") || User.IsInRole("Admin");
            if (!isBackOffice && !string.Equals(actorNic, nic, StringComparison.Ordinal))
                return Forbid();

            try
            {
                var res = await _service.UpdateAsync(nic, req, actorNic, ct);
                return Ok(res);
            }
            catch (UpdateException ex) when (ex.Code is "InvalidFullName" or "InvalidEmail" or "InvalidPhone")
            {
                return BadRequest(new { error = ex.Code, message = ex.Message });
            }
            catch (UpdateException ex) when (ex.Code is "DuplicateEmail")
            {
                return Conflict(new { error = ex.Code, message = ex.Message });
            }
            catch (NotFoundException ex) when (ex.Code == "OwnerNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
            catch (UpdateException ex) when (ex.Code == "ConcurrencyConflict")
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message, title: ex.Code);
            }
        }

        /// <summary>
        /// Deactivate own account (self-only). Login will be blocked after deactivation.
        /// </summary>
        [Authorize]
        [HttpPut("{nic}/deactivate")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Deactivate([FromRoute] string nic, CancellationToken ct)
        {
            var actorNic = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.Identity?.Name
                        ?? string.Empty;

            if (!string.Equals(actorNic, nic, StringComparison.Ordinal))
                return Forbid(); // self-only

            try
            {
                var res = await _service.DeactivateAsync(nic, actorNic, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "OwnerNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
            catch (UpdateException ex) when (ex.Code == "ConcurrencyConflict")
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message, title: ex.Code);
            }
        }

        /// <summary>
        /// Reactivate an owner account (BackOffice/Admin only).
        /// </summary>
        [Authorize(Roles = "BackOffice,Admin")]
        [HttpPut("{nic}/reactivate")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Reactivate([FromRoute] string nic, CancellationToken ct)
        {
            var actorNic = User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                        ?? User.Identity?.Name
                        ?? string.Empty;

            try
            {
                var res = await _service.ReactivateAsync(nic, actorNic, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code == "OwnerNotFound")
            {
                return NotFound(new { error = ex.Code, message = ex.Message });
            }
            catch (UpdateException ex) when (ex.Code == "ConcurrencyConflict")
            {
                return Problem(statusCode: StatusCodes.Status409Conflict, detail: ex.Message, title: ex.Code);
            }
        }
    }
}
