using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthcareCRM.Data;
using HealthcareCRM.Models;

namespace HealthcareCRM.Controllers.Api
{
    [ApiController]
    [Route("api/appointments")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AppointmentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AppointmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/appointments
        /// Retrieves all scheduled appointments.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAppointments()
        {
            var appointments = await _context.Appointments
                .Include(a => a.Patient)
                .OrderBy(a => a.AppointmentDate)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = appointments,
                message = "Appointments retrieved successfully."
            });
        }

        /// <summary>
        /// POST /api/appointments
        /// Schedules a new appointment.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateAppointment([FromBody] Appointment appointment)
        {
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

            // Verify if the patient exists
            var patientExists = await _context.Patients.AnyAsync(p => p.Id == appointment.PatientId);
            if (!patientExists)
            {
                return BadRequest(new
                {
                    success = false,
                    data = (object?)null,
                    message = "Selected patient does not exist."
                });
            }

            appointment.Id = Guid.NewGuid();
            appointment.CreatedAt = DateTime.UtcNow;
            appointment.Status = "Scheduled";

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // Load patient navigation property for the response
            appointment.Patient = await _context.Patients.FindAsync(appointment.PatientId);

            return Created(string.Empty, new
            {
                success = true,
                data = appointment,
                message = "Appointment scheduled successfully."
            });
        }
    }
}
