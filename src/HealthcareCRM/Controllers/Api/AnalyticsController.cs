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
    [Route("api/analytics")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AnalyticsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public AnalyticsController(ApplicationDbContext context) => _context = context;

        // GET /api/analytics/overview
        // Returns all KPIs needed for dashboard cards + charts
        [HttpGet("overview")]
        public async Task<IActionResult> Overview()
        {
            var now = DateTime.UtcNow;
            var todayStart = now.Date;
            var todayEnd = todayStart.AddDays(1);
            var last30Days = now.AddDays(-30);
            var last7Days = now.AddDays(-7);

            // ── Patients ──────────────────────────────────────────────
            var totalPatients = await _context.Patients.CountAsync();
            var newPatientsLast30Days = await _context.Patients
                .CountAsync(p => p.CreatedAt >= last30Days);

            // Patients registered per day (last 14 days) — for line chart
            var patientsByDay = await _context.Patients
                .Where(p => p.CreatedAt >= now.AddDays(-14))
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .OrderBy(x => x.date)
                .ToListAsync();

            // Gender distribution — for pie chart
            var genderDist = await _context.Patients
                .GroupBy(p => p.Gender)
                .Select(g => new { gender = g.Key, count = g.Count() })
                .ToListAsync();

            // ── Appointments ──────────────────────────────────────────
            var totalAppointments = await _context.Appointments.CountAsync();
            var todayAppointments = await _context.Appointments
                .CountAsync(a => a.AppointmentDate >= todayStart && a.AppointmentDate < todayEnd);
            var scheduledCount = await _context.Appointments.CountAsync(a => a.Status == "Scheduled");
            var completedCount = await _context.Appointments.CountAsync(a => a.Status == "Completed");
            var cancelledCount = await _context.Appointments.CountAsync(a => a.Status == "Cancelled");

            // Appointments per day (last 14 days) — for bar chart
            var appointmentsByDay = await _context.Appointments
                .Where(a => a.AppointmentDate >= now.AddDays(-14))
                .GroupBy(a => a.AppointmentDate.Date)
                .Select(g => new { date = g.Key, count = g.Count() })
                .OrderBy(x => x.date)
                .ToListAsync();

            // Appointment status breakdown — for donut chart
            var appointmentStatus = new[]
            {
                new { status = "Scheduled", count = scheduledCount },
                new { status = "Completed", count = completedCount },
                new { status = "Cancelled", count = cancelledCount }
            };

            // ── Billing ───────────────────────────────────────────────
            var totalRevenue = await _context.Invoices
                .Where(i => i.Status != "Cancelled")
                .SumAsync(i => (double?)i.TotalAmount) ?? 0;

            var collectedRevenue = await _context.Invoices
                .SumAsync(i => (double?)i.AmountPaid) ?? 0;

            var outstandingRevenue = totalRevenue - collectedRevenue;

            var unpaidCount = await _context.Invoices.CountAsync(i => i.Status == "Unpaid");
            var paidCount = await _context.Invoices.CountAsync(i => i.Status == "Paid");

            // Revenue per day last 14 days — for area chart
            var revenueByDay = await _context.Payments
                .Where(p => p.PaymentDate >= now.AddDays(-14))
                .GroupBy(p => p.PaymentDate.Date)
                .Select(g => new { date = g.Key, amount = g.Sum(x => (double?)x.Amount) ?? 0 })
                .OrderBy(x => x.date)
                .ToListAsync();

            // ── Prescriptions ─────────────────────────────────────────
            var totalPrescriptions = await _context.Prescriptions.CountAsync();
            var activePrescriptions = await _context.Prescriptions.CountAsync(p => p.Status == "Active");

            // ── Notifications ─────────────────────────────────────────
            var pendingNotifications = await _context.Notifications.CountAsync(n => n.Status == "Pending");

            return Ok(new
            {
                success = true,
                data = new
                {
                    // KPI Cards
                    totalPatients,
                    newPatientsLast30Days,
                    totalAppointments,
                    todayAppointments,
                    totalRevenue,
                    collectedRevenue,
                    outstandingRevenue,
                    unpaidCount,
                    paidCount,
                    totalPrescriptions,
                    activePrescriptions,
                    pendingNotifications,

                    // Chart data
                    patientsByDay,
                    genderDist,
                    appointmentsByDay,
                    appointmentStatus,
                    revenueByDay
                },
                message = "Analytics retrieved."
            });
        }
    }
}
