using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthcareCRM.Models
{
    public class Prescription
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient? Patient { get; set; }

        public Guid? AppointmentId { get; set; }

        [ForeignKey(nameof(AppointmentId))]
        public Appointment? Appointment { get; set; }

        [Required]
        [MaxLength(100)]
        public string DoctorName { get; set; } = string.Empty;

        [Required]
        public DateTime PrescribedDate { get; set; } = DateTime.UtcNow;

        public DateTime? ValidUntil { get; set; }

        [MaxLength(1000)]
        public string? Diagnosis { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        // Status: Active | Expired | Cancelled
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Active";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<PrescriptionItem> Items { get; set; } = new();
    }

    public class PrescriptionItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PrescriptionId { get; set; }

        [ForeignKey(nameof(PrescriptionId))]
        public Prescription? Prescription { get; set; }

        [Required]
        [MaxLength(200)]
        public string MedicineName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Dosage { get; set; } // e.g. "500mg"

        [MaxLength(100)]
        public string? Frequency { get; set; } // e.g. "Twice daily"

        [MaxLength(100)]
        public string? Duration { get; set; } // e.g. "7 days"

        [MaxLength(500)]
        public string? Instructions { get; set; }
    }

    // 2. Notification / Reminder Model
    public class Notification
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid? PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient? Patient { get; set; }

        public Guid? AppointmentId { get; set; }

        [ForeignKey(nameof(AppointmentId))]
        public Appointment? Appointment { get; set; }

        // Type: AppointmentReminder | MedicationReminder | FollowUp | General
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "General";

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        // When should this reminder fire
        public DateTime? ScheduledAt { get; set; }

        // Status: Pending | Sent | Dismissed
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
