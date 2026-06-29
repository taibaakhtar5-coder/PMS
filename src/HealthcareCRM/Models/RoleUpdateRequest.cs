using System.ComponentModel.DataAnnotations;

namespace HealthcareCRM.Models
{
    public class RoleUpdateRequest
    {
        [Required(ErrorMessage = "Role is required.")]
        [RegularExpression("^(Admin|Doctor|Receptionist)$", ErrorMessage = "Invalid Role. Allowed roles: Admin, Doctor, Receptionist.")]
        public string Role { get; set; } = string.Empty;
    }
}
