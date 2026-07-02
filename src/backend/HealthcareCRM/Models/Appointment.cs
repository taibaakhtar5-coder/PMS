using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthcareCRM.Models
{
    public class Appointment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient? Patient { get; set; }

        // Reference to the registered Doctor (User with Role = "Doctor").
        // Nullable to remain compatible with any appointments created before this field existed.
        public Guid? DoctorId { get; set; }

        [ForeignKey(nameof(DoctorId))]
        public User? Doctor { get; set; }

        [Required]
        [MaxLength(100)]
        public string DoctorName { get; set; } = string.Empty;

        [Required]
        public DateTime AppointmentDate { get; set; }

        // Length of the appointment slot in minutes (the actual consultation time).
        // Used together with AppointmentDate to compute the End time:
        //   Start = AppointmentDate, End = AppointmentDate.AddMinutes(DurationMinutes)
        [Required]
        public int DurationMinutes { get; set; } = 30;

        [MaxLength(500)]
        public string? Reason { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Scheduled"; // Scheduled, Completed, Cancelled

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
