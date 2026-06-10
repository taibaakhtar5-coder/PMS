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

        /// <summary>
        /// POST /api/auth/register
        /// Registers a new user.
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
        {
            // Server-side validation is mandatory
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new
                {
                    success = false,
                    data = errors,
                    message = "Validation failed."
                });
            }

            var user = await _authService.RegisterAsync(model);
            if (user == null)
            {
                return BadRequest(new
                {
                    success = false,
                    data = (object?)null,
                    message = "Email is already registered."
                });
            }

            // Return 201 Created on success, do not return password
            var responseData = new
            {
                id = user.Id,
                fullName = user.FullName,
                email = user.Email
            };

            return Created(string.Empty, new
            {
                success = true,
                data = responseData,
                message = "Registration successful."
            });
        }

        /// <summary>
        /// POST /api/auth/login
        /// Authenticates user and returns JWT.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            // Server-side validation is mandatory
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new
                {
                    success = false,
                    data = errors,
                    message = "Validation failed."
                });
            }

            var user = await _authService.LoginAsync(model);
            if (user == null)
            {
                // Return 401 Unauthorized with a human-readable message on authentication failure
                return Unauthorized(new
                {
                    success = false,
                    data = (object?)null,
                    message = "Invalid email or password."
                });
            }

            // Generate JWT and store client info
            var token = _authService.GenerateJwtToken(user);
            var responseData = new
            {
                token,
                user = new
                {
                    id = user.Id,
                    fullName = user.FullName,
                    email = user.Email
                }
            };

            return Ok(new
            {
                success = true,
                data = responseData,
                message = "Login successful."
            });
        }
    }
}
