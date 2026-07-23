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
    [Route("api/notifications")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public NotificationsController(ApplicationDbContext context) => _context = context;

        // GET /api/notifications?status=Pending&type=
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? type)
        {
            var query = _context.Notifications
                .Include(n => n.Patient)
                .Include(n => n.Appointment)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(n => n.Status == status);
            if (!string.IsNullOrWhiteSpace(type)) query = query.Where(n => n.Type == type);

            var list = await query.OrderByDescending(n => n.CreatedAt).ToListAsync();
            return Ok(new { success = true, data = list, message = "Notifications retrieved." });
        }

        // GET /api/notifications/pending-count — for navbar badge
        [HttpGet("pending-count")]
        public async Task<IActionResult> PendingCount()
        {
            var count = await _context.Notifications.CountAsync(n => n.Status == "Pending");
            return Ok(new { success = true, data = count, message = "OK" });
        }

        // POST /api/notifications — create a manual reminder
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNotificationRequest req)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { success = false, data = errors, message = "Validation failed." });
            }

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                PatientId = req.PatientId,
                AppointmentId = req.AppointmentId,
                Type = req.Type ?? "General",
                Title = req.Title,
                Message = req.Message,
                ScheduledAt = req.ScheduledAt,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return Created(string.Empty, new { success = true, data = notification, message = "Notification created." });
        }

        // PATCH /api/notifications/{id}/dismiss
        [HttpPatch("{id:guid}/dismiss")]
        public async Task<IActionResult> Dismiss(Guid id)
        {
            var n = await _context.Notifications.FindAsync(id);
            if (n == null) return NotFound(new { success = false, data = (object?)null, message = "Not found." });
            n.Status = "Dismissed";
            await _context.SaveChangesAsync();
            return Ok(new { success = true, data = (object?)null, message = "Dismissed." });
        }

        // PATCH /api/notifications/dismiss-all — dismiss all pending
        [HttpPatch("dismiss-all")]
        public async Task<IActionResult> DismissAll()
        {
            var pending = await _context.Notifications.Where(n => n.Status == "Pending").ToListAsync();
            pending.ForEach(n => n.Status = "Dismissed");
            await _context.SaveChangesAsync();
            return Ok(new { success = true, data = pending.Count, message = $"{pending.Count} notifications dismissed." });
        }

        // Auto-generate appointment reminders for upcoming appointments
        // POST /api/notifications/generate-reminders
        [HttpPost("generate-reminders")]
        public async Task<IActionResult> GenerateReminders()
        {
            var now = DateTime.UtcNow;
            var next24h = now.AddHours(24);

            // Find upcoming appointments in next 24 hours that don't already have a reminder
            var upcoming = await _context.Appointments
                .Include(a => a.Patient)
                .Where(a => a.Status == "Scheduled"
                            && a.AppointmentDate >= now
                            && a.AppointmentDate <= next24h)
                .ToListAsync();

            var existingApptIds = await _context.Notifications
                .Where(n => n.Type == "AppointmentReminder" && n.AppointmentId != null)
                .Select(n => n.AppointmentId)
                .ToListAsync();

            int created = 0;
            foreach (var appt in upcoming.Where(a => !existingApptIds.Contains(a.Id)))
            {
                _context.Notifications.Add(new Notification
                {
                    Id = Guid.NewGuid(),
                    PatientId = appt.PatientId,
                    AppointmentId = appt.Id,
                    Type = "AppointmentReminder",
                    Title = $"Upcoming Appointment — {appt.Patient?.FullName}",
                    Message = $"Appointment with Dr. {appt.DoctorName} is scheduled for {appt.AppointmentDate:MMM dd, yyyy} at {appt.AppointmentDate:h:mm tt}.",
                    ScheduledAt = appt.AppointmentDate.AddHours(-1),
                    Status = "Pending",
                    CreatedAt = now
                });
                created++;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, data = created, message = $"{created} reminder(s) generated." });
        }
    }

    public class CreateNotificationRequest
    {
        public Guid? PatientId { get; set; }
        public Guid? AppointmentId { get; set; }
        public string? Type { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        public string Title { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required]
        public string Message { get; set; } = string.Empty;
        public DateTime? ScheduledAt { get; set; }
    }
}
