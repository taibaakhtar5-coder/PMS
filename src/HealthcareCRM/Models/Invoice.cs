using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthcareCRM.Models
{
    public class Invoice
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid PatientId { get; set; }

        [ForeignKey(nameof(PatientId))]
        public Patient? Patient { get; set; }

        // Optional link to an appointment
        public Guid? AppointmentId { get; set; }

        [ForeignKey(nameof(AppointmentId))]
        public Appointment? Appointment { get; set; }

        [Required]
        [MaxLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty; // e.g. INV-20260624-001

        [Required]
        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        public DateTime? DueDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal AmountPaid { get; set; } = 0;

        // Status: Unpaid | PartiallyPaid | Paid | Cancelled
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Unpaid";

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<InvoiceItem> Items { get; set; } = new();
        public List<Payment> Payments { get; set; } = new();
    }

    public class InvoiceItem
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid InvoiceId { get; set; }

        [ForeignKey(nameof(InvoiceId))]
        public Invoice? Invoice { get; set; }

        [Required]
        [MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Total => UnitPrice * Quantity;
    }

    public class Payment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid InvoiceId { get; set; }

        [ForeignKey(nameof(InvoiceId))]
        public Invoice? Invoice { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(50)]
        public string Method { get; set; } = "Cash"; // Cash, Card, Bank Transfer, Online

        [MaxLength(200)]
        public string? Reference { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}
