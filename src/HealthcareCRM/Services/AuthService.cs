using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using HealthcareCRM.Data;
using HealthcareCRM.Models;

namespace HealthcareCRM.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<User>();
        }

        /// <summary>
        /// Registers a new user if the email is not already taken.
        /// </summary>
        public async Task<User?> RegisterAsync(RegisterViewModel model)
        {
            var emailLower = model.Email.Trim().ToLower();

            // Check if user already exists
            if (await _context.Users.AnyAsync(u => u.Email == emailLower))
            {
                return null;
            }

            var user = new User
            {
                FullName = model.FullName.Trim(),
                Email = emailLower,
                CreatedAt = DateTime.UtcNow
            };

            // Securely hash the password
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return user;
        }

        /// <summary>
        /// Validates login credentials and returns the User if validation succeeds.
        /// </summary>
        public async Task<User?> LoginAsync(LoginViewModel model)
        {
            var emailLower = model.Email.Trim().ToLower();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == emailLower);
            if (user == null)
            {
                return null;
            }

            // Verify password hash
            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return null;
            }

            return user;
        }

        /// <summary>
        /// Generates a signed JWT for the authenticated User.
        /// </summary>
        public string GenerateJwtToken(User user)
        {
            var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET") 
                            ?? _configuration["JWT_SECRET"] 
                            ?? "FriendswareHealthcareCRMSuperSecretKey2026ForTask2";
            
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
                         ?? _configuration["JWT_ISSUER"] 
                         ?? "HealthcareCRM";
            
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
                           ?? _configuration["JWT_AUDIENCE"] 
                           ?? "HealthcareCRMUsers";

            // Ensure key is long enough for HmacSha256 (at least 256 bits)
            if (secretKey.Length < 32)
            {
                secretKey = secretKey.PadRight(32, '0');
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7), // Token valid for 7 days
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
