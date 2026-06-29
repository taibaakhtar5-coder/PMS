using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HealthcareCRM.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using HealthcareCRM.Controllers.Api;
using HealthcareCRM.Data;
using HealthcareCRM.Models;

namespace HealthcareCRM.Tests
{
    public class AppointmentTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
        private readonly Guid _patient1Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private readonly Guid _patient2Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private readonly Guid _doctor1Id = Guid.NewGuid();

        public AppointmentTests()
        {
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            using (var context = new ApplicationDbContext(_dbContextOptions))
            {
                context.Database.EnsureCreated();

                // Add a doctor to the database for testing doctor role check
                var doctor = new User
                {
                    Id = _doctor1Id,
                    FullName = "Doctor House",
                    Email = "house@example.com",
                    PasswordHash = "hashed",
                    Role = "Doctor",
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(doctor);
                context.SaveChanges();
            }
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task CreateAppointment_WithValidSlot_SavesToDatabase()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new AppointmentsController(context);
            var newAppt = new Appointment
            {
                PatientId = _patient1Id,
                DoctorId = _doctor1Id,
                DoctorName = "Doctor House",
                AppointmentDate = DateTime.Today.AddHours(10), // 10:00 AM
                DurationMinutes = 30,
                Reason = "General Consultation"
            };

            // Act
            var result = await controller.CreateAppointment(newAppt);

            // Assert
            var createdResult = Assert.IsType<CreatedResult>(result);
            var json = JsonSerializer.Serialize(createdResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

            var savedAppt = await context.Appointments.FirstOrDefaultAsync(a => a.Reason == "General Consultation");
            Assert.NotNull(savedAppt);
            Assert.Equal(_patient1Id, savedAppt.PatientId);
            Assert.Equal(_doctor1Id, savedAppt.DoctorId);
            Assert.Equal(30, savedAppt.DurationMinutes);
        }

        [Fact]
        public async Task CreateAppointment_DoctorOverlapWithin45MinGap_ReturnsConflict()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new AppointmentsController(context);
            
            // Seed first appointment: 10:00 AM to 10:30 AM (Duration = 30)
            var existingAppt = new Appointment
            {
                Id = Guid.NewGuid(),
                PatientId = _patient1Id,
                DoctorId = _doctor1Id,
                DoctorName = "Doctor House",
                AppointmentDate = DateTime.Today.AddHours(10),
                DurationMinutes = 30,
                Status = "Scheduled"
            };
            context.Appointments.Add(existingAppt);
            await context.SaveChangesAsync();

            // Act: Schedule second appointment at 11:00 AM (only 30 mins gap, less than 45 mins buffer)
            var conflictingAppt = new Appointment
            {
                PatientId = _patient2Id,
                DoctorId = _doctor1Id,
                DoctorName = "Doctor House",
                AppointmentDate = DateTime.Today.AddHours(11), // 11:00 AM
                DurationMinutes = 30,
                Reason = "Checkup"
            };
            var result = await controller.CreateAppointment(conflictingAppt);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            var json = JsonSerializer.Serialize(conflictResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("Please leave at least 45 minutes", doc.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task CreateAppointment_PatientOverlappingAppointment_ReturnsConflict()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new AppointmentsController(context);

            // Seed first appointment for patient1: 10:00 AM to 10:30 AM
            var existingAppt = new Appointment
            {
                Id = Guid.NewGuid(),
                PatientId = _patient1Id,
                DoctorId = _doctor1Id,
                DoctorName = "Doctor House",
                AppointmentDate = DateTime.Today.AddHours(10),
                DurationMinutes = 30,
                Status = "Scheduled"
            };
            context.Appointments.Add(existingAppt);
            await context.SaveChangesAsync();

            // Act: Schedule another appointment for same patient at 10:15 AM (overlapping)
            var overlappingAppt = new Appointment
            {
                PatientId = _patient1Id,
                DoctorId = null, // different or no doctor
                DoctorName = "Doctor Strange",
                AppointmentDate = DateTime.Today.AddHours(10).AddMinutes(15), // 10:15 AM
                DurationMinutes = 30,
                Reason = "Second opinion"
            };
            var result = await controller.CreateAppointment(overlappingAppt);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            var json = JsonSerializer.Serialize(conflictResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains("overlapping appointments", doc.RootElement.GetProperty("message").GetString());
        }

        [Fact]
        public async Task GetAppointments_WithFilters_ReturnsFilteredList()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new AppointmentsController(context);

            var appt1 = new Appointment
            {
                Id = Guid.NewGuid(),
                PatientId = _patient1Id,
                DoctorId = _doctor1Id,
                DoctorName = "Doctor House",
                AppointmentDate = DateTime.Today.AddHours(10),
                Status = "Scheduled"
            };
            var appt2 = new Appointment
            {
                Id = Guid.NewGuid(),
                PatientId = _patient2Id,
                DoctorId = null,
                DoctorName = "Doctor Strange",
                AppointmentDate = DateTime.Today.AddDays(1).AddHours(11),
                Status = "Completed"
            };
            context.Appointments.AddRange(appt1, appt2);
            await context.SaveChangesAsync();

            // Act: Filter by status "Completed"
            var result = await controller.GetAppointments(null, null, "Completed", null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            
            var dataRaw = doc.RootElement.GetProperty("data").GetRawText();
            var appointments = JsonSerializer.Deserialize<List<Appointment>>(dataRaw, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(appointments);
            Assert.Single(appointments);
            Assert.Equal("Doctor Strange", appointments[0].DoctorName);
        }

        [Fact]
        public async Task UpdateAppointment_WithValidChanges_UpdatesDatabase()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new AppointmentsController(context);

            var existingAppt = new Appointment
            {
                Id = Guid.NewGuid(),
                PatientId = _patient1Id,
                DoctorId = _doctor1Id,
                DoctorName = "Doctor House",
                AppointmentDate = DateTime.Today.AddHours(10),
                DurationMinutes = 30,
                Status = "Scheduled"
            };
            context.Appointments.Add(existingAppt);
            await context.SaveChangesAsync();

            // Act: Update reason and duration
            var updatedAppt = new Appointment
            {
                PatientId = _patient1Id,
                DoctorId = _doctor1Id,
                DoctorName = "Doctor House",
                AppointmentDate = DateTime.Today.AddHours(10),
                DurationMinutes = 45, // changed
                Reason = "Updated reason", // changed
                Status = "Scheduled"
            };

            var result = await controller.UpdateAppointment(existingAppt.Id, updatedAppt);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

            var savedAppt = await context.Appointments.FindAsync(existingAppt.Id);
            Assert.NotNull(savedAppt);
            Assert.Equal("Updated reason", savedAppt.Reason);
            Assert.Equal(45, savedAppt.DurationMinutes);
        }

        [Fact]
        public async Task DeleteAppointment_RemovesFromDatabase()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new AppointmentsController(context);

            var appt = new Appointment
            {
                Id = Guid.NewGuid(),
                PatientId = _patient1Id,
                DoctorId = _doctor1Id,
                DoctorName = "Doctor House",
                AppointmentDate = DateTime.Today.AddHours(10),
                Status = "Scheduled"
            };
            context.Appointments.Add(appt);
            await context.SaveChangesAsync();

            // Act: Delete
            var result = await controller.DeleteAppointment(appt.Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

            var deletedAppt = await context.Appointments.FindAsync(appt.Id);
            Assert.Null(deletedAppt);
        }

        [Fact]
        public void ApiController_HasAuthorizeAttribute()
        {
            var type = typeof(AppointmentsController);
            var attributes = type.GetCustomAttributes(typeof(AuthorizeAttribute), true);
            Assert.NotEmpty(attributes);
            
            var authAttribute = (AuthorizeAttribute)attributes.First();
            Assert.Equal(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme, authAttribute.AuthenticationSchemes);
        }

        [Fact]
        public void MvcController_HasJwtAuthorizeAttribute()
        {
            var type = typeof(HealthcareCRM.Controllers.AppointmentController);
            var attributes = type.GetCustomAttributes(typeof(JwtAuthorizeAttribute), true);
            Assert.NotEmpty(attributes);
        }
    }
}
