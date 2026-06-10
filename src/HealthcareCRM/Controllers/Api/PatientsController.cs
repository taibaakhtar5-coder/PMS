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
    [Route("api/patients")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PatientsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PatientsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// GET /api/patients
        /// Retrieves a list of patients, optionally filtered by a search query.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPatients([FromQuery] string? search)
        {
            var query = _context.Patients.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchClean = search.Trim().ToLower();
                query = query.Where(p => 
                    p.FullName.ToLower().Contains(searchClean) || 
                    (p.Email != null && p.Email.ToLower().Contains(searchClean)) || 
                    p.PhoneNumber.Contains(searchClean)
                );
            }

            var patients = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = patients,
                message = "Patients retrieved successfully."
            });
        }

        /// <summary>
        /// GET /api/patients/{id}
        /// Retrieves details of a single patient by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetPatient(Guid id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null)
            {
                return NotFound(new
                {
                    success = false,
                    data = (object?)null,
                    message = "Patient not found."
                });
            }

            return Ok(new
            {
                success = true,
                data = patient,
                message = "Patient details retrieved successfully."
            });
        }

        /// <summary>
        /// POST /api/patients
        /// Creates a new patient record.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePatient([FromBody] Patient patient)
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

            // Ensure unique ID
            patient.Id = Guid.NewGuid();
            patient.CreatedAt = DateTime.UtcNow;

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPatient), new { id = patient.Id }, new
            {
                success = true,
                data = patient,
                message = "Patient registered successfully."
            });
        }

        /// <summary>
        /// PUT /api/patients/{id}
        /// Updates an existing patient record.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdatePatient(Guid id, [FromBody] Patient updatedPatient)
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

            var patient = await _context.Patients.FindAsync(id);
            if (patient == null)
            {
                return NotFound(new
                {
                    success = false,
                    data = (object?)null,
                    message = "Patient not found."
                });
            }

            // Update allowed fields
            patient.FullName = updatedPatient.FullName.Trim();
            patient.Email = updatedPatient.Email?.Trim();
            patient.PhoneNumber = updatedPatient.PhoneNumber.Trim();
            patient.DateOfBirth = updatedPatient.DateOfBirth;
            patient.Gender = updatedPatient.Gender;
            patient.Address = updatedPatient.Address?.Trim();

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                data = patient,
                message = "Patient details updated successfully."
            });
        }
    }
}
