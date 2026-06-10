using System;
using Microsoft.AspNetCore.Mvc;

namespace HealthcareCRM.Controllers.Api
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// GET /api/health
        /// Returns the health status of the API.
        /// </summary>
        [HttpGet]
        public IActionResult GetHealth()
        {
            var response = new
            {
                success = true,
                data = new
                {
                    status = "Healthy",
                    timestamp = DateTime.UtcNow
                },
                message = "Service is running and healthy."
            };

            return Ok(response);
        }
    }
}
