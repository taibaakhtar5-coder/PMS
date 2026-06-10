using System;
using Microsoft.AspNetCore.Mvc;
using HealthcareCRM.Helpers;

namespace HealthcareCRM.Controllers
{
    [JwtAuthorize]
    public class AppointmentController : Controller
    {
        /// <summary>
        /// GET /Appointment/List
        /// Renders the clinic appointments schedule registry.
        /// </summary>
        [HttpGet]
        public IActionResult List()
        {
            return View();
        }
    }
}
