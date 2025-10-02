using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EvCharge.Api.Infrastructure.Errors;

namespace EvCharge.Api.Controllers.Auth
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth)
        {
            _auth = auth;
        }

        /// <summary>
        /// Owner login (returns JWT).
        /// </summary>
        [AllowAnonymous]
        [HttpPost("owner/login")]
        [ProducesResponseType(typeof(AuthLoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> OwnerLogin([FromBody] LoginOwnerRequest req, CancellationToken ct)
        {
            try
            {
                var res = await _auth.LoginOwnerAsync(req, ct);
                return Ok(res);
            }
            catch (AuthException ex) when (ex.Code == "InvalidNic")
            {
                return BadRequest(new { error = ex.Code, message = ex.Message });
            }
            catch (AuthException ex) when (ex.Code == "InvalidCredentials")
            {
                return Unauthorized(new { error = ex.Code, message = ex.Message });
            }
            catch (AuthException ex) when (ex.Code == "AccountDisabled")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Code, message = ex.Message });
            }
        }
    }
}
