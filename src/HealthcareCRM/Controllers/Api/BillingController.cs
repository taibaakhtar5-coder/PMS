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
    [Route("api/billing")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BillingController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BillingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET /api/billing?patientId=&status=&dateFrom=&dateTo=
        [HttpGet]
        public async Task<IActionResult> GetInvoices(
            [FromQuery] Guid? patientId,
            [FromQuery] string? status,
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo)
        {
            var query = _context.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Appointment)
                .Include(i => i.Items)
                .Include(i => i.Payments)
                .AsQueryable();

            if (patientId.HasValue)
                query = query.Where(i => i.PatientId == patientId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(i => i.Status == status);

            if (dateFrom.HasValue)
                query = query.Where(i => i.InvoiceDate >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                query = query.Where(i => i.InvoiceDate < dateTo.Value.Date.AddDays(1));

            var invoices = await query
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            return Ok(new { success = true, data = invoices, message = "Invoices retrieved successfully." });
        }

        // GET /api/billing/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetInvoice(Guid id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Patient)
                .Include(i => i.Appointment)
                .Include(i => i.Items)
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return NotFound(new { success = false, data = (object?)null, message = "Invoice not found." });

            return Ok(new { success = true, data = invoice, message = "Invoice retrieved successfully." });
        }

        // POST /api/billing — Create invoice with items
        [HttpPost]
        public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequest request)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { success = false, data = errors, message = "Validation failed." });
            }

            var patientExists = await _context.Patients.AnyAsync(p => p.Id == request.PatientId);
            if (!patientExists)
                return BadRequest(new { success = false, data = (object?)null, message = "Patient not found." });

            // Auto-generate invoice number: INV-YYYYMMDD-XXXX
            var today = DateTime.UtcNow;
            var countToday = await _context.Invoices
                .CountAsync(i => i.InvoiceDate.Date == today.Date);
            var invoiceNumber = $"INV-{today:yyyyMMdd}-{(countToday + 1):D4}";

            var totalAmount = request.Items.Sum(item => item.UnitPrice * item.Quantity);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                PatientId = request.PatientId,
                AppointmentId = request.AppointmentId,
                InvoiceNumber = invoiceNumber,
                InvoiceDate = today,
                DueDate = request.DueDate,
                TotalAmount = totalAmount,
                AmountPaid = 0,
                Status = "Unpaid",
                Notes = request.Notes,
                CreatedAt = today,
                Items = request.Items.Select(i => new InvoiceItem
                {
                    Id = Guid.NewGuid(),
                    Description = i.Description,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity
                }).ToList()
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            await _context.Entry(invoice).Reference(i => i.Patient).LoadAsync();

            return Created(string.Empty, new { success = true, data = invoice, message = "Invoice created successfully." });
        }

        // POST /api/billing/{id}/payment — Record a payment
        [HttpPost("{id:guid}/payment")]
        public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordPaymentRequest request)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Payments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
                return NotFound(new { success = false, data = (object?)null, message = "Invoice not found." });

            if (invoice.Status == "Cancelled")
                return BadRequest(new { success = false, data = (object?)null, message = "Cannot record payment for a cancelled invoice." });

            if (invoice.Status == "Paid")
                return BadRequest(new { success = false, data = (object?)null, message = "This invoice is already fully paid." });

            var remaining = invoice.TotalAmount - invoice.AmountPaid;
            if (request.Amount <= 0)
                return BadRequest(new { success = false, data = (object?)null, message = "Payment amount must be greater than zero." });

            if (request.Amount > remaining)
                return BadRequest(new { success = false, data = (object?)null, message = $"Payment amount (Rs. {request.Amount:F2}) exceeds remaining balance (Rs. {remaining:F2})." });

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = id,
                Amount = request.Amount,
                PaymentDate = DateTime.UtcNow,
                Method = request.Method ?? "Cash",
                Reference = request.Reference,
                Notes = request.Notes
            };

            _context.Payments.Add(payment);

            invoice.AmountPaid += request.Amount;

            // Auto-update status
            if (invoice.AmountPaid >= invoice.TotalAmount)
                invoice.Status = "Paid";
            else
                invoice.Status = "PartiallyPaid";

            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = invoice, message = "Payment recorded successfully." });
        }

        // PATCH /api/billing/{id}/status — Update invoice status
        [HttpPatch("{id:guid}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
        {
            var allowed = new[] { "Unpaid", "PartiallyPaid", "Paid", "Cancelled" };
            if (!allowed.Contains(request.Status))
                return BadRequest(new { success = false, data = (object?)null, message = "Invalid status. Allowed: Unpaid, PartiallyPaid, Paid, Cancelled." });

            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
                return NotFound(new { success = false, data = (object?)null, message = "Invoice not found." });

            invoice.Status = request.Status;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = invoice, message = $"Invoice status updated to {request.Status}." });
        }

        // DELETE /api/billing/{id} — Cancel invoice (soft delete via status)
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> CancelInvoice(Guid id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
                return NotFound(new { success = false, data = (object?)null, message = "Invoice not found." });

            if (invoice.Status == "Paid")
                return BadRequest(new { success = false, data = (object?)null, message = "Cannot cancel a fully paid invoice." });

            invoice.Status = "Cancelled";
            await _context.SaveChangesAsync();

            return Ok(new { success = true, data = (object?)null, message = "Invoice cancelled successfully." });
        }
    }

    // --- Request DTOs ---
    public class CreateInvoiceRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid PatientId { get; set; }
        public Guid? AppointmentId { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Notes { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        public List<InvoiceItemRequest> Items { get; set; } = new();
    }

    public class InvoiceItemRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Description { get; set; } = string.Empty;
        [System.ComponentModel.DataAnnotations.Required]
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class RecordPaymentRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public decimal Amount { get; set; }
        public string? Method { get; set; } = "Cash";
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateStatusRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        public string Status { get; set; } = string.Empty;
    }
}
