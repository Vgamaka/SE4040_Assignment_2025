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
    public class BackOfficeController : ControllerBase
    {
        private readonly IBackOfficeService _bo;
        private readonly IStationService _stations;

        public BackOfficeController(IBackOfficeService bo, IStationService stations)
        {
            _bo = bo; _stations = stations;
        }

        /// <summary>Public: apply to become a BackOffice (business registration).</summary>
        [AllowAnonymous]
        [HttpPost("apply")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Apply([FromBody] BackOfficeApplyRequest req, CancellationToken ct)
        {
            try
            {
                var res = await _bo.ApplyAsync(req, ct);
                return Ok(res);
            }
            catch (RegistrationException ex) { return Conflict(new { error = ex.Code, message = ex.Message }); }
            catch (ValidationException ex) { return BadRequest(new { error = ex.Code, message = ex.Message }); }
        }

        /// <summary>BackOffice: view my profile.</summary>
        [Authorize(Roles = "BackOffice")]
        [HttpGet("me")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "";
            var res = await _bo.GetMyProfileAsync(nic, ct);
            return Ok(res);
        }

        /// <summary>BackOffice: create an Operator (and optionally scope to stations).</summary>
        [Authorize(Roles = "BackOffice")]
        [HttpPost("operators")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateOperator([FromBody] OperatorCreateRequest req, CancellationToken ct)
        {
            try
            {
                var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "";
                var res = await _bo.CreateOperatorAsync(req, nic, ct);
                return Created("", res);
            }
            catch (RegistrationException ex) { return Conflict(new { error = ex.Code, message = ex.Message }); }
            catch (ValidationException ex) { return BadRequest(new { error = ex.Code, message = ex.Message }); }
            catch (NotFoundException ex) when (ex.Code == "StationNotFound")
            { return NotFound(new { error = ex.Code, message = ex.Message }); }
            catch (UpdateException ex) when (ex.Code == "ForbiddenStationScope")
            { return Problem(statusCode: StatusCodes.Status403Forbidden, title: ex.Code, detail: ex.Message); }
        }

        /// <summary>BackOffice: list my Operators.</summary>
        [Authorize(Roles = "BackOffice")]
        [HttpGet("operators")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> ListOperators([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "";
            var (items, total) = await _bo.ListOperatorsAsync(nic, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
            return Ok(new { total, items });
        }

        /// <summary>BackOffice: attach (replace) station scope for an Operator.</summary>
        [Authorize(Roles = "BackOffice")]
        [HttpPut("operators/{operatorNic}/stations")]
        [ProducesResponseType(typeof(OwnerResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AttachStations(string operatorNic, [FromBody] OperatorAttachStationsRequest req, CancellationToken ct)
        {
            try
            {
                var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "";
                var res = await _bo.AttachStationsToOperatorAsync(operatorNic, req.StationIds, nic, ct);
                return Ok(res);
            }
            catch (NotFoundException ex) when (ex.Code is "OperatorNotFound" or "StationNotFound")
            { return NotFound(new { error = ex.Code, message = ex.Message }); }
            catch (UpdateException ex) when (ex.Code is "Forbidden" or "ForbiddenStationScope")
            { return Problem(statusCode: StatusCodes.Status403Forbidden, title: ex.Code, detail: ex.Message); }
            catch (ValidationException ex) { return BadRequest(new { error = ex.Code, message = ex.Message }); }
        }

        /// <summary>BackOffice: list my stations.</summary>
        [Authorize(Roles = "BackOffice")]
        [HttpGet("stations")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public async Task<IActionResult> MyStations([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            var nic = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? "";
            var (items, total) = await _stations.ListByBackOfficeAsync(nic, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
            return Ok(new { total, items });
        }
    }
}
