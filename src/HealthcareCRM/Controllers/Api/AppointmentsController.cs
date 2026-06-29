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

        // Mandatory gap (in minutes) that must exist between the END of one appointment
        // and the START of the next appointment for the SAME doctor.
        // A doctor CAN have multiple appointments in the same day — they just can't be
        // back-to-back without this buffer. Example with GapMinutes = 45:
        //   Existing: 10:00 - 10:30 (30 min slot)
        //   Blocked window for next booking: 10:30 - 11:15 (the 45 min gap after existing ends)
        //   So a new appointment can only start at 11:15 or later (or end by 09:15 or earlier).
        private const int GapMinutes = 45;

        public AppointmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/appointments?doctorId={id}&status={status}&date={yyyy-MM-dd}&patientId={id}
        /// Retrieves appointments, optionally filtered by doctor, status, a specific date,
        /// or patient. All filters are optional and combine with AND when provided together.
        /// Examples:
        ///   /api/appointments                         -> all appointments
        ///   /api/appointments?doctorId=...             -> only this doctor's appointments
        ///   /api/appointments?status=Scheduled          -> only scheduled appointments
        ///   /api/appointments?date=2026-06-24            -> only appointments on this date
        ///   /api/appointments?doctorId=...&date=2026-06-24&status=Scheduled -> combined
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAppointments(
            [FromQuery] Guid? doctorId,
            [FromQuery] Guid? patientId,
            [FromQuery] string? status,
            [FromQuery] DateTime? date)
        {
            var query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .AsQueryable();

            if (doctorId.HasValue)
            {
                query = query.Where(a => a.DoctorId == doctorId.Value);
            }

            if (patientId.HasValue)
            {
                query = query.Where(a => a.PatientId == patientId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            if (date.HasValue)
            {
                var dayStart = date.Value.Date;
                var dayEnd = dayStart.AddDays(1);
                query = query.Where(a => a.AppointmentDate >= dayStart && a.AppointmentDate < dayEnd);
            }

            var appointments = await query
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
        /// GET /api/appointments/availability?doctorId={id}&date={yyyy-MM-dd}
        /// Returns the doctor's already-booked appointments (with start/end times) for a
        /// given date, so the booking form can show the doctor's free/busy schedule
        /// before submission.
        /// </summary>
        [HttpGet("availability")]
        public async Task<IActionResult> GetAvailability([FromQuery] Guid doctorId, [FromQuery] DateTime date)
        {
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            var bookedSlots = await _context.Appointments
                .Where(a => a.DoctorId == doctorId
                            && a.Status != "Cancelled"
                            && a.AppointmentDate >= dayStart
                            && a.AppointmentDate < dayEnd)
                .OrderBy(a => a.AppointmentDate)
                .Select(a => new
                {
                    a.AppointmentDate,
                    a.DurationMinutes,
                    a.Status,
                    EndTime = a.AppointmentDate.AddMinutes(a.DurationMinutes)
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = bookedSlots,
                message = "Availability retrieved successfully."
            });
        }

        /// <summary>
        /// POST /api/appointments
        /// Schedules a new appointment.
        ///
        /// A doctor CAN have multiple appointments on the same day. The only rule is that
        /// consecutive appointments for the SAME doctor must have at least GapMinutes
        /// between the end of one and the start of the next. Two appointments conflict
        /// when their [Start - Gap, End + Gap) windows overlap.
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

            if (appointment.DurationMinutes <= 0)
            {
                appointment.DurationMinutes = 30;
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

            var newStart = appointment.AppointmentDate;
            var newEnd = newStart.AddMinutes(appointment.DurationMinutes);

            // If a specific doctor was selected, verify they exist and have the Doctor role
            if (appointment.DoctorId.HasValue)
            {
                var doctorExists = await _context.Users.AnyAsync(u => u.Id == appointment.DoctorId.Value && u.Role == "Doctor");
                if (!doctorExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        data = (object?)null,
                        message = "Selected doctor does not exist."
                    });
                }

                // --- Conflict validation: same doctor, requires >= GapMinutes buffer ---
                // Pull same-day non-cancelled appointments for this doctor and check in memory,
                // since the gap rule isn't a simple overlap and is easier to reason about here.
                var dayStart = newStart.Date;
                var dayEnd = dayStart.AddDays(1);

                var doctorsAppointmentsThatDay = await _context.Appointments
                    .Where(a => a.DoctorId == appointment.DoctorId.Value
                                && a.Status != "Cancelled"
                                && a.AppointmentDate >= dayStart
                                && a.AppointmentDate < dayEnd)
                    .Select(a => new { a.AppointmentDate, a.DurationMinutes })
                    .ToListAsync();

                foreach (var existing in doctorsAppointmentsThatDay)
                {
                    var existingStart = existing.AppointmentDate;
                    var existingEnd = existingStart.AddMinutes(existing.DurationMinutes);

                    // Conflict if the new appointment's [start, end) window, expanded by the
                    // required gap on both sides, overlaps the existing appointment's window.
                    // i.e. blocked if: newStart < existingEnd + Gap  AND  newEnd > existingStart - Gap
                    bool overlapsWithGap = newStart < existingEnd.AddMinutes(GapMinutes)
                                            && newEnd > existingStart.AddMinutes(-GapMinutes);

                    if (overlapsWithGap)
                    {
                        return Conflict(new
                        {
                            success = false,
                            data = (object?)null,
                            message = $"This doctor already has an appointment from {existingStart:h:mm tt} to {existingEnd:h:mm tt} on this day. " +
                                      $"Please leave at least {GapMinutes} minutes before or after this slot."
                        });
                    }
                }
            }

            // --- Conflict validation: same patient, overlapping time slot ---
            // A single patient cannot have two appointments that overlap in time,
            // even across different doctors. No extra gap is required here — just no overlap.
            var patientAppointmentsOverlap = await _context.Appointments
                .Where(a => a.PatientId == appointment.PatientId
                            && a.Status != "Cancelled")
                .Select(a => new { a.AppointmentDate, a.DurationMinutes, a.DoctorName })
                .ToListAsync();

            var patientConflict = patientAppointmentsOverlap.FirstOrDefault(a =>
            {
                var existingStart = a.AppointmentDate;
                var existingEnd = existingStart.AddMinutes(a.DurationMinutes);
                return newStart < existingEnd && newEnd > existingStart;
            });

            if (patientConflict != null)
            {
                var pStart = patientConflict.AppointmentDate;
                var pEnd = pStart.AddMinutes(patientConflict.DurationMinutes);
                return Conflict(new
                {
                    success = false,
                    data = (object?)null,
                    message = $"This patient already has an appointment with Dr. {patientConflict.DoctorName} from {pStart:MMM dd, h:mm tt} to {pEnd:h:mm tt}. A patient cannot have two overlapping appointments."
                });
            }

            appointment.Id = Guid.NewGuid();
            appointment.CreatedAt = DateTime.UtcNow;
            appointment.Status = "Scheduled";

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            // Load navigation properties for the response
            appointment.Patient = await _context.Patients.FindAsync(appointment.PatientId);
            if (appointment.DoctorId.HasValue)
            {
                appointment.Doctor = await _context.Users.FindAsync(appointment.DoctorId.Value);
            }

            return Created(string.Empty, new
            {
                success = true,
                data = appointment,
                message = "Appointment scheduled successfully."
            });
        }

        /// <summary>
        /// GET /api/appointments/{id}
        /// Retrieves details of a single appointment by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetAppointment(Guid id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound(new
                {
                    success = false,
                    data = (object?)null,
                    message = "Appointment not found."
                });
            }

            return Ok(new
            {
                success = true,
                data = appointment,
                message = "Appointment retrieved successfully."
            });
        }

        /// <summary>
        /// DELETE /api/appointments/{id}
        /// Cancels and deletes an appointment record.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteAppointment(Guid id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound(new
                {
                    success = false,
                    data = (object?)null,
                    message = "Appointment not found."
                });
            }

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                data = (object?)null,
                message = "Appointment cancelled and deleted successfully."
            });
        }

        /// <summary>
        /// PUT /api/appointments/{id}
        /// Updates an existing appointment. Enforces the same conflict rules (excluding self).
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateAppointment(Guid id, [FromBody] Appointment updated)
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

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound(new
                {
                    success = false,
                    data = (object?)null,
                    message = "Appointment not found."
                });
            }

            if (updated.DurationMinutes <= 0)
            {
                updated.DurationMinutes = 30;
            }

            // Verify if the patient exists
            var patientExists = await _context.Patients.AnyAsync(p => p.Id == updated.PatientId);
            if (!patientExists)
            {
                return BadRequest(new
                {
                    success = false,
                    data = (object?)null,
                    message = "Selected patient does not exist."
                });
            }

            var newStart = updated.AppointmentDate;
            var newEnd = newStart.AddMinutes(updated.DurationMinutes);

            // If a specific doctor was selected, verify they exist and have the Doctor role
            if (updated.DoctorId.HasValue)
            {
                var doctorExists = await _context.Users.AnyAsync(u => u.Id == updated.DoctorId.Value && u.Role == "Doctor");
                if (!doctorExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        data = (object?)null,
                        message = "Selected doctor does not exist."
                    });
                }

                // --- Conflict validation: same doctor, requires >= GapMinutes buffer (excluding self) ---
                var dayStart = newStart.Date;
                var dayEnd = dayStart.AddDays(1);

                var doctorsAppointmentsThatDay = await _context.Appointments
                    .Where(a => a.DoctorId == updated.DoctorId.Value
                                && a.Id != id // EXCLUDE SELF
                                && a.Status != "Cancelled"
                                && a.AppointmentDate >= dayStart
                                && a.AppointmentDate < dayEnd)
                    .Select(a => new { a.AppointmentDate, a.DurationMinutes })
                    .ToListAsync();

                foreach (var existing in doctorsAppointmentsThatDay)
                {
                    var existingStart = existing.AppointmentDate;
                    var existingEnd = existingStart.AddMinutes(existing.DurationMinutes);

                    // Conflict if the updated appointment's [start, end) window, expanded by the
                    // required gap on both sides, overlaps the existing appointment's window.
                    bool overlapsWithGap = newStart < existingEnd.AddMinutes(GapMinutes)
                                            && newEnd > existingStart.AddMinutes(-GapMinutes);

                    if (overlapsWithGap)
                    {
                        return Conflict(new
                        {
                            success = false,
                            data = (object?)null,
                            message = $"This doctor already has an appointment from {existingStart:h:mm tt} to {existingEnd:h:mm tt} on this day. " +
                                      $"Please leave at least {GapMinutes} minutes before or after this slot."
                        });
                    }
                }
            }

            // --- Conflict validation: same patient, overlapping time slot (excluding self) ---
            var patientAppointmentsOverlap = await _context.Appointments
                .Where(a => a.PatientId == updated.PatientId
                            && a.Id != id // EXCLUDE SELF
                            && a.Status != "Cancelled")
                .Select(a => new { a.AppointmentDate, a.DurationMinutes, a.DoctorName })
                .ToListAsync();

            var patientConflict = patientAppointmentsOverlap.FirstOrDefault(a =>
            {
                var existingStart = a.AppointmentDate;
                var existingEnd = existingStart.AddMinutes(a.DurationMinutes);
                return newStart < existingEnd && newEnd > existingStart;
            });

            if (patientConflict != null)
            {
                var pStart = patientConflict.AppointmentDate;
                var pEnd = pStart.AddMinutes(patientConflict.DurationMinutes);
                return Conflict(new
                {
                    success = false,
                    data = (object?)null,
                    message = $"This patient already has an appointment with Dr. {patientConflict.DoctorName} from {pStart:MMM dd, h:mm tt} to {pEnd:h:mm tt}. A patient cannot have two overlapping appointments."
                });
            }

            // Update fields
            appointment.PatientId = updated.PatientId;
            appointment.DoctorId = updated.DoctorId;
            appointment.DoctorName = updated.DoctorName?.Trim() ?? string.Empty;
            appointment.AppointmentDate = updated.AppointmentDate;
            appointment.DurationMinutes = updated.DurationMinutes;
            appointment.Reason = updated.Reason?.Trim();
            appointment.Status = updated.Status?.Trim() ?? "Scheduled";

            await _context.SaveChangesAsync();

            // Load navigation properties for response
            appointment.Patient = await _context.Patients.FindAsync(appointment.PatientId);
            if (appointment.DoctorId.HasValue)
            {
                appointment.Doctor = await _context.Users.FindAsync(appointment.DoctorId.Value);
            }

            return Ok(new
            {
                success = true,
                data = appointment,
                message = "Appointment updated successfully."
            });
        }
    }
}
