using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using HealthcareCRM.Data;
using HealthcareCRM.Helpers;
using HealthcareCRM.Services;

// 1. Load local environment variables from .env file before starting the builder
EnvLoader.Load(".env");

var builder = WebApplication.CreateBuilder(args);

// 2. Add services to the container
builder.Services.AddControllersWithViews();

// 3. Configure Database connection using SQLite
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION") 
                       ?? builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Data Source=HealthcareCRM.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// 4. Register Authentication Services
builder.Services.AddScoped<IAuthService, AuthService>();

// 5. Configure API Token Bearer Authentication (for controllers that check Authorize headers)
var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET") 
                ?? "FriendswareHealthcareCRMSuperSecretKey2026ForTask2";
if (secretKey.Length < 32)
{
    secretKey = secretKey.PadRight(32, '0');
}
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "HealthcareCRM",
        ValidateAudience = true,
        ValidAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "HealthcareCRMUsers",
        ClockSkew = TimeSpan.Zero
    };
});

// 6. Configure Swagger documentation with JWT Bearer locks
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Healthcare CRM API", 
        Version = "v1",
        Description = "API documentation for the Healthcare CRM system."
    });
    
    // Add Security Definition for Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 7. Initialize Database (ensure created & run migrations/seeding automatically)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    // EnsureCreated creates the DB if it doesn't exist.
    // For existing DBs it does nothing — so we also run ALTER TABLE / CREATE TABLE
    // statements manually to add any new tables/columns without losing existing data.
    context.Database.EnsureCreated();

    // Add DoctorId column to Appointments if it doesn't exist yet (added in a later iteration)
    try {
        context.Database.ExecuteSqlRaw(@"ALTER TABLE Appointments ADD COLUMN DoctorId TEXT NULL;");
    } catch { /* column already exists — safe to ignore */ }

    // Add DurationMinutes column to Appointments if missing
    try {
        context.Database.ExecuteSqlRaw(@"ALTER TABLE Appointments ADD COLUMN DurationMinutes INTEGER NOT NULL DEFAULT 30;");
    } catch { /* already exists */ }

    // Add Role column to Users if missing
    try {
        context.Database.ExecuteSqlRaw(@"ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'Receptionist';");
    } catch { /* already exists */ }

    // Create Invoices table if missing
    context.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS Invoices (
            Id TEXT NOT NULL PRIMARY KEY,
            PatientId TEXT NOT NULL,
            AppointmentId TEXT NULL,
            InvoiceNumber TEXT NOT NULL,
            InvoiceDate TEXT NOT NULL,
            DueDate TEXT NULL,
            TotalAmount REAL NOT NULL DEFAULT 0,
            AmountPaid REAL NOT NULL DEFAULT 0,
            Status TEXT NOT NULL DEFAULT 'Unpaid',
            Notes TEXT NULL,
            CreatedAt TEXT NOT NULL,
            FOREIGN KEY (PatientId) REFERENCES Patients(Id),
            FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id)
        );");

    context.Database.ExecuteSqlRaw(@"
        CREATE UNIQUE INDEX IF NOT EXISTS IX_Invoices_InvoiceNumber ON Invoices (InvoiceNumber);");

    // Create InvoiceItems table if missing
    context.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS InvoiceItems (
            Id TEXT NOT NULL PRIMARY KEY,
            InvoiceId TEXT NOT NULL,
            Description TEXT NOT NULL,
            UnitPrice REAL NOT NULL DEFAULT 0,
            Quantity INTEGER NOT NULL DEFAULT 1,
            FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE
        );");

    // Create Payments table if missing
    context.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS Payments (
            Id TEXT NOT NULL PRIMARY KEY,
            InvoiceId TEXT NOT NULL,
            Amount REAL NOT NULL DEFAULT 0,
            PaymentDate TEXT NOT NULL,
            Method TEXT NOT NULL DEFAULT 'Cash',
            Reference TEXT NULL,
            Notes TEXT NULL,
            FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE
        );");

    // Create MedicalHistories table if missing
    context.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS MedicalHistories (
            Id TEXT NOT NULL PRIMARY KEY,
            PatientId TEXT NOT NULL,
            Diagnosis TEXT NOT NULL,
            Treatment TEXT NULL,
            DoctorName TEXT NULL,
            Notes TEXT NULL,
            VisitDate TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            FOREIGN KEY (PatientId) REFERENCES Patients(Id) ON DELETE CASCADE
        );");
}

// 8. Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Healthcare CRM API v1");
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Authentication & Authorization middlewares
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
