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
    [Route("api/prescriptions")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PrescriptionsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public PrescriptionsController(ApplicationDbContext context) => _context = context;

        // GET /api/prescriptions?patientId=&status=
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] Guid? patientId, [FromQuery] string? status)
        {
            var query = _context.Prescriptions
                .Include(p => p.Patient)
                .Include(p => p.Appointment)
                .Include(p => p.Items)
                .AsQueryable();

            if (patientId.HasValue) query = query.Where(p => p.PatientId == patientId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);

            var list = await query.OrderByDescending(p => p.PrescribedDate).ToListAsync();
            return Ok(new { success = true, data = list, message = "Prescriptions retrieved." });
        }

        // GET /api/prescriptions/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var p = await _context.Prescriptions
                .Include(x => x.Patient)
                .Include(x => x.Appointment)
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound(new { success = false, data = (object?)null, message = "Not found." });
            return Ok(new { success = true, data = p, message = "OK" });
        }

        // POST /api/prescriptions
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreatePrescriptionRequest req)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { success = false, data = errors, message = "Validation failed." });
            }

            var patientExists = await _context.Patients.AnyAsync(p => p.Id == req.PatientId);
            if (!patientExists) return BadRequest(new { success = false, data = (object?)null, message = "Patient not found." });

            var prescription = new Prescription
            {
                Id = Guid.NewGuid(),
                PatientId = req.PatientId,
                AppointmentId = req.AppointmentId,
                DoctorName = req.DoctorName,
                PrescribedDate = DateTime.UtcNow,
                ValidUntil = req.ValidUntil,
                Diagnosis = req.Diagnosis,
                Notes = req.Notes,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                Items = req.Items.Select(i => new PrescriptionItem
                {
                    Id = Guid.NewGuid(),
                    MedicineName = i.MedicineName,
                    Dosage = i.Dosage,
                    Frequency = i.Frequency,
                    Duration = i.Duration,
                    Instructions = i.Instructions
                }).ToList()
            };

            _context.Prescriptions.Add(prescription);

            // Auto-create appointment reminder notification if appointment linked
            if (req.AppointmentId.HasValue)
            {
                var appt = await _context.Appointments.Include(a => a.Patient).FirstOrDefaultAsync(a => a.Id == req.AppointmentId.Value);
                if (appt != null)
                {
                    _context.Notifications.Add(new Notification
                    {
                        Id = Guid.NewGuid(),
                        PatientId = req.PatientId,
                        AppointmentId = req.AppointmentId,
                        Type = "MedicationReminder",
                        Title = $"Prescription issued for {appt.Patient?.FullName ?? "Patient"}",
                        Message = $"A new prescription with {prescription.Items.Count} medication(s) has been issued by Dr. {req.DoctorName}.",
                        ScheduledAt = DateTime.UtcNow,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            await _context.Entry(prescription).Reference(p => p.Patient).LoadAsync();

            return Created(string.Empty, new { success = true, data = prescription, message = "Prescription created." });
        }

        // PATCH /api/prescriptions/{id}/status
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdatePrescriptionStatusRequest req)
        {
            var allowed = new[] { "Active", "Expired", "Cancelled" };
            if (!allowed.Contains(req.Status))
                return BadRequest(new { success = false, data = (object?)null, message = "Invalid status." });

            var p = await _context.Prescriptions.FindAsync(id);
            if (p == null) return NotFound(new { success = false, data = (object?)null, message = "Not found." });

            p.Status = req.Status;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, data = p, message = $"Status updated to {req.Status}." });
        }

        // DELETE /api/prescriptions/{id}
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var p = await _context.Prescriptions.FindAsync(id);
            if (p == null) return NotFound(new { success = false, data = (object?)null, message = "Not found." });
            _context.Prescriptions.Remove(p);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, data = (object?)null, message = "Deleted." });
        }
    }

    public class CreatePrescriptionRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid PatientId { get; set; }
        public Guid? AppointmentId { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        public string DoctorName { get; set; } = string.Empty;
        public DateTime? ValidUntil { get; set; }
        public string? Diagnosis { get; set; }
        public string? Notes { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        public List<PrescriptionItemRequest> Items { get; set; } = new();
    }

    public class PrescriptionItemRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string MedicineName { get; set; } = string.Empty;
        public string? Dosage { get; set; }
        public string? Frequency { get; set; }
        public string? Duration { get; set; }
        public string? Instructions { get; set; }
    }

    public class UpdatePrescriptionStatusRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Status { get; set; } = string.Empty;
    }
}
