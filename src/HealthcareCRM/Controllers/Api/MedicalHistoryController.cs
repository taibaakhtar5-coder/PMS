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
    [Route("api/medical-history")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MedicalHistoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MedicalHistoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/medical-history/{patientId}
        /// Retrieves all medical history records for a patient.
        /// </summary>
        [HttpGet("{patientId:guid}")]
        public async Task<IActionResult> GetByPatient(Guid patientId)
        {
            var patient = await _context.Patients.FindAsync(patientId);
            if (patient == null)
            {
                return NotFound(new { success = false, data = (object?)null, message = "Patient not found." });
            }

            var records = await _context.MedicalHistories
                .Where(m => m.PatientId == patientId)
                .OrderByDescending(m => m.VisitDate)
                .ToListAsync();

            return Ok(new { success = true, data = records, message = "Medical history retrieved successfully." });
        }

        /// <summary>
        /// POST /api/medical-history
        /// Creates a new medical history record.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MedicalHistory record)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { success = false, data = errors, message = "Validation failed." });
            }

            var patient = await _context.Patients.FindAsync(record.PatientId);
            if (patient == null)
            {
                return NotFound(new { success = false, data = (object?)null, message = "Patient not found." });
            }

            record.Id = Guid.NewGuid();
            record.CreatedAt = DateTime.UtcNow;

            _context.MedicalHistories.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = record, message = "Medical history record added successfully." });
        }

        /// <summary>
        /// PUT /api/medical-history/{id}
        /// Updates a medical history record.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] MedicalHistory updated)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { success = false, data = errors, message = "Validation failed." });
            }

            var record = await _context.MedicalHistories.FindAsync(id);
            if (record == null)
            {
                return NotFound(new { success = false, data = (object?)null, message = "Record not found." });
            }

            record.Diagnosis = updated.Diagnosis.Trim();
            record.Treatment = updated.Treatment?.Trim();
            record.DoctorName = updated.DoctorName?.Trim();
            record.Notes = updated.Notes?.Trim();
            record.VisitDate = updated.VisitDate;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = record, message = "Medical history record updated successfully." });
        }

        /// <summary>
        /// DELETE /api/medical-history/{id}
        /// Deletes a medical history record.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var record = await _context.MedicalHistories.FindAsync(id);
            if (record == null)
            {
                return NotFound(new { success = false, data = (object?)null, message = "Record not found." });
            }

            _context.MedicalHistories.Remove(record);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = (object?)null, message = "Medical history record deleted successfully." });
        }
    }
}
