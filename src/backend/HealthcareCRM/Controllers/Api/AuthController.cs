using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HealthcareCRM.Models;
using HealthcareCRM.Services;

namespace HealthcareCRM.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new { success = false, data = errors, message = "Validation failed." });
            }

            var user = await _authService.RegisterAsync(model);
            if (user == null)
            {
                return BadRequest(new { success = false, data = (object?)null, message = "Email is already registered." });
            }

            var responseData = new { id = user.Id, fullName = user.FullName, email = user.Email, role = user.Role };

            return Created(string.Empty, new { success = true, data = responseData, message = "Registration successful." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new { success = false, data = errors, message = "Validation failed." });
            }

            var user = await _authService.LoginAsync(model);
            if (user == null)
            {
                return Unauthorized(new { success = false, data = (object?)null, message = "Invalid email or password." });
            }

            var token = _authService.GenerateJwtToken(user);
            var responseData = new
            {
                token,
                user = new { id = user.Id, fullName = user.FullName, email = user.Email, role = user.Role }
            };

            return Ok(new { success = true, data = responseData, message = "Login successful." });
        }
    }
}
