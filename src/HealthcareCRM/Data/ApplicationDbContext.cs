using Microsoft.EntityFrameworkCore;
using HealthcareCRM.Models;

namespace HealthcareCRM.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Patient> Patients { get; set; } = null!;
        public DbSet<Appointment> Appointments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure unique index on User Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Seed a sample patient to make testing Patient CRUD or list features easier for Member B
            modelBuilder.Entity<Patient>().HasData(
                new Patient
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    FullName = "John Doe",
                    Email = "johndoe@example.com",
                    PhoneNumber = "+92-300-1234567",
                    DateOfBirth = new System.DateTime(1990, 5, 15),
                    Gender = "Male",
                    Address = "Rawalpindi, Pakistan",
                    CreatedAt = System.DateTime.UtcNow
                },
                new Patient
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    FullName = "Jane Smith",
                    Email = "janesmith@example.com",
                    PhoneNumber = "+92-321-7654321",
                    DateOfBirth = new System.DateTime(1985, 10, 20),
                    Gender = "Female",
                    Address = "Islamabad, Pakistan",
                    CreatedAt = System.DateTime.UtcNow
                }
            );
        }
    }
}
