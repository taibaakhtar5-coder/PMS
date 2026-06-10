using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using HealthcareCRM.Controllers.Api;
using HealthcareCRM.Data;
using HealthcareCRM.Models;

namespace HealthcareCRM.Tests
{
    public class PatientTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;

        public PatientTests()
        {
            // Set up an open Sqlite in-memory connection
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            // Seed DB schema and run OnModelCreating seeds (John Doe, Jane Smith)
            using (var context = new ApplicationDbContext(_dbContextOptions))
            {
                context.Database.EnsureCreated();
            }
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task GetPatients_ReturnsAllSeededPatients()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new PatientsController(context);

            // Act
            var result = await controller.GetPatients(null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            
            var dataJson = doc.RootElement.GetProperty("data").GetRawText();
            var patients = JsonSerializer.Deserialize<List<Patient>>(dataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(patients);
            Assert.Equal(2, patients.Count); // 2 seeded patients
        }

        [Fact]
        public async Task GetPatients_WithSearchQuery_ReturnsFilteredPatients()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new PatientsController(context);

            // Act
            var result = await controller.GetPatients("Jane");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
            
            var dataJson = doc.RootElement.GetProperty("data").GetRawText();
            var patients = JsonSerializer.Deserialize<List<Patient>>(dataJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(patients);
            Assert.Single(patients);
            Assert.Equal("Jane Smith", patients[0].FullName);
        }

        [Fact]
        public async Task CreatePatient_WithValidDetails_InsertsIntoDatabase()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new PatientsController(context);
            var newPatient = new Patient
            {
                FullName = "Alice Johnson",
                Email = "alice.j@example.com",
                PhoneNumber = "+92-333-5556667",
                DateOfBirth = new DateTime(1995, 8, 12),
                Gender = "Female",
                Address = "Peshawar, Pakistan"
            };

            // Act
            var result = await controller.CreatePatient(newPatient);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var json = JsonSerializer.Serialize(createdResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

            var dbPatient = await context.Patients.FirstOrDefaultAsync(p => p.FullName == "Alice Johnson");
            Assert.NotNull(dbPatient);
            Assert.Equal("+92-333-5556667", dbPatient.PhoneNumber);
        }

        [Fact]
        public async Task UpdatePatient_WithValidDetails_PersistsChanges()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new PatientsController(context);
            var patientId = Guid.Parse("11111111-1111-1111-1111-111111111111"); // Seeded John Doe

            var updatedPatient = new Patient
            {
                FullName = "John Doe Updated",
                Email = "john.updated@example.com",
                PhoneNumber = "+92-300-1234567",
                DateOfBirth = new DateTime(1990, 5, 15),
                Gender = "Male",
                Address = "Lahore, Pakistan" // Modified
            };

            // Act
            var result = await controller.UpdatePatient(patientId, updatedPatient);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var json = JsonSerializer.Serialize(okResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean());

            var dbPatient = await context.Patients.FindAsync(patientId);
            Assert.NotNull(dbPatient);
            Assert.Equal("John Doe Updated", dbPatient.FullName);
            Assert.Equal("Lahore, Pakistan", dbPatient.Address);
        }

        [Fact]
        public async Task CreatePatient_WithMissingRequiredFields_FailsValidation()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var controller = new PatientsController(context);
            
            // Simulating missing phone number model validation failure
            var invalidPatient = new Patient
            {
                FullName = "Invalid Patient",
                PhoneNumber = "", // Required, cannot be empty
                DateOfBirth = new DateTime(2000, 1, 1),
                Gender = "Other"
            };
            controller.ModelState.AddModelError("PhoneNumber", "The Phone Number field is required.");

            // Act
            var result = await controller.CreatePatient(invalidPatient);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonSerializer.Serialize(badRequestResult.Value);
            using var doc = JsonDocument.Parse(json);
            Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("Validation failed.", doc.RootElement.GetProperty("message").GetString());
        }
    }
}
