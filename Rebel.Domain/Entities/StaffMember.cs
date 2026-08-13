using System.ComponentModel.DataAnnotations;
using Rebel.Domain.Enums;

namespace Rebel.Domain.Entities
{
    public class StaffMember
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(80)]
        public string FullName { get; set; } = string.Empty;

        public StaffRole Role { get; set; }

        [StringLength(40)]
        public string? PhoneNumber { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<StaffShift> Shifts { get; set; } =
            new List<StaffShift>();
    }
}
