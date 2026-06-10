using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;
using HealthcareCRM.Data;
using HealthcareCRM.Helpers;
using HealthcareCRM.Models;
using HealthcareCRM.Services;

namespace HealthcareCRM.Tests
{
    public class AuthTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _dbContextOptions;
        private readonly IConfiguration _configuration;

        public AuthTests()
        {
            // 1. Set up an open Sqlite in-memory connection that lasts for the lifetime of each test
            _connection = new SqliteConnection("Filename=:memory:");
            _connection.Open();

            _dbContextOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            // Seed DB schema
            using (var context = new ApplicationDbContext(_dbContextOptions))
            {
                context.Database.EnsureCreated();
            }

            // Mock configurations
            var configValues = new Dictionary<string, string?>
            {
                {"JWT_SECRET", "FriendswareHealthcareCRMSuperSecretKey2026ForTask2"},
                {"JWT_ISSUER", "HealthcareCRM"},
                {"JWT_AUDIENCE", "HealthcareCRMUsers"}
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();

            // Make sure env variables match config during test runs
            Environment.SetEnvironmentVariable("JWT_SECRET", "FriendswareHealthcareCRMSuperSecretKey2026ForTask2");
            Environment.SetEnvironmentVariable("JWT_ISSUER", "HealthcareCRM");
            Environment.SetEnvironmentVariable("JWT_AUDIENCE", "HealthcareCRMUsers");
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
        }

        [Fact]
        public async Task Register_WithValidDetails_Succeeds()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var service = new AuthService(context, _configuration);
            var model = new RegisterViewModel
            {
                FullName = "Dr. Alice Smith",
                Email = "alice@crm.com",
                Password = "Password123!",
                ConfirmPassword = "Password123!"
            };

            // Act
            var user = await service.RegisterAsync(model);

            // Assert
            Assert.NotNull(user);
            Assert.Equal("alice@crm.com", user.Email);
            Assert.Equal("Dr. Alice Smith", user.FullName);
            Assert.NotEqual("Password123!", user.PasswordHash); // Password must be hashed
        }

        [Fact]
        public async Task Register_WithDuplicateEmail_Fails()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var service = new AuthService(context, _configuration);
            var model1 = new RegisterViewModel
            {
                FullName = "Dr. Alice Smith",
                Email = "duplicate@crm.com",
                Password = "Password123!",
                ConfirmPassword = "Password123!"
            };
            var model2 = new RegisterViewModel
            {
                FullName = "Dr. Bob Miller",
                Email = "duplicate@crm.com",
                Password = "DifferentPass123!",
                ConfirmPassword = "DifferentPass123!"
            };

            // Act & Assert
            var user1 = await service.RegisterAsync(model1);
            Assert.NotNull(user1);

            var user2 = await service.RegisterAsync(model2);
            Assert.Null(user2); // Registering the same email should fail and return null
        }

        [Fact]
        public async Task Login_WithCorrectCredentials_Succeeds()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var service = new AuthService(context, _configuration);
            
            // Seed a user
            var regModel = new RegisterViewModel
            {
                FullName = "Dr. Charlie Brown",
                Email = "charlie@crm.com",
                Password = "SecurePassword1!",
                ConfirmPassword = "SecurePassword1!"
            };
            await service.RegisterAsync(regModel);

            var loginModel = new LoginViewModel
            {
                Email = "charlie@crm.com",
                Password = "SecurePassword1!"
            };

            // Act
            var loggedInUser = await service.LoginAsync(loginModel);

            // Assert
            Assert.NotNull(loggedInUser);
            Assert.Equal("charlie@crm.com", loggedInUser.Email);
        }

        [Fact]
        public async Task Login_WithIncorrectPassword_Fails()
        {
            // Arrange
            using var context = new ApplicationDbContext(_dbContextOptions);
            var service = new AuthService(context, _configuration);
            
            // Seed a user
            var regModel = new RegisterViewModel
            {
                FullName = "Dr. Charlie Brown",
                Email = "charlie@crm.com",
                Password = "SecurePassword1!",
                ConfirmPassword = "SecurePassword1!"
            };
            await service.RegisterAsync(regModel);

            var loginModel = new LoginViewModel
            {
                Email = "charlie@crm.com",
                Password = "WrongPassword!" // Incorrect
            };

            // Act
            var loggedInUser = await service.LoginAsync(loginModel);

            // Assert
            Assert.Null(loggedInUser); // Invalid login credentials should return null
        }

        [Fact]
        public void JwtAuthorizeFilter_WithoutCookie_RedirectsToLogin()
        {
            // Arrange
            var filter = new JwtAuthorizeAttribute();
            
            // Create mock http context details
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor()
            );
            
            var authorizationContext = new AuthorizationFilterContext(
                actionContext,
                new List<IFilterMetadata>()
            );

            // Act
            filter.OnAuthorization(authorizationContext);

            // Assert
            Assert.NotNull(authorizationContext.Result);
            var redirectResult = Assert.IsType<RedirectToActionResult>(authorizationContext.Result);
            Assert.Equal("Login", redirectResult.ActionName);
            Assert.Equal("Account", redirectResult.ControllerName);
        }
    }
}
