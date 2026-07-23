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
        public DbSet<MedicalHistory> MedicalHistories { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<InvoiceItem> InvoiceItems { get; set; } = null!;
        public DbSet<Payment> Payments { get; set; } = null!;
        public DbSet<Prescription> Prescriptions { get; set; } = null!;
        public DbSet<PrescriptionItem> PrescriptionItems { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure unique index on User Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configure Appointment -> Doctor (User) relationship explicitly.
            // Restrict on delete so removing a doctor account doesn't cascade-delete appointments.
            modelBuilder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany()
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Invoice -> Appointment: optional FK, restrict delete
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Appointment)
                .WithMany()
                .HasForeignKey(i => i.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // InvoiceItem -> Invoice: cascade delete (items removed when invoice removed)
            modelBuilder.Entity<InvoiceItem>()
                .HasOne(ii => ii.Invoice)
                .WithMany(i => i.Items)
                .HasForeignKey(ii => ii.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Payment -> Invoice: cascade delete
            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Invoice)
                .WithMany(i => i.Payments)
                .HasForeignKey(p => p.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique index on InvoiceNumber
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();

            // Prescription -> Patient
            modelBuilder.Entity<Prescription>()
                .HasOne(p => p.Patient)
                .WithMany()
                .HasForeignKey(p => p.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Prescription -> Appointment (optional)
            modelBuilder.Entity<Prescription>()
                .HasOne(p => p.Appointment)
                .WithMany()
                .HasForeignKey(p => p.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // PrescriptionItem -> Prescription (cascade)
            modelBuilder.Entity<PrescriptionItem>()
                .HasOne(pi => pi.Prescription)
                .WithMany(p => p.Items)
                .HasForeignKey(pi => pi.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Notification -> Patient (optional)
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Patient)
                .WithMany()
                .HasForeignKey(n => n.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Notification -> Appointment (optional)
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Appointment)
                .WithMany()
                .HasForeignKey(n => n.AppointmentId)
                .OnDelete(DeleteBehavior.Restrict);

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
