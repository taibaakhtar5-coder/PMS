using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthcareCRM.Data;

namespace HealthcareCRM.Controllers.Api
{
    [ApiController]
    [Route("api/users")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/users/doctors
        /// Returns all registered users with Role = "Doctor", so booking forms
        /// can show a real, selectable list of available doctors instead of free text.
        /// </summary>
        [HttpGet("doctors")]
        public async Task<IActionResult> GetDoctors()
        {
            var doctors = await _context.Users
                .Where(u => u.Role == "Doctor")
                .OrderBy(u => u.FullName)
                .Select(u => new { id = u.Id, fullName = u.FullName, email = u.Email })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = doctors,
                message = "Doctors retrieved successfully."
            });
        }
    }
}
