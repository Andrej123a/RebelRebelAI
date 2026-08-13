using System.ComponentModel.DataAnnotations;
using Rebel.Domain.Enums;

namespace Rebel.Domain.Entities
{
    public class StaffShift
    {
        public Guid Id { get; set; }

        public Guid StaffMemberId { get; set; }

        public StaffMember? StaffMember { get; set; }

        public StaffRole Role { get; set; }

        public DateTime ShiftDate { get; set; }

        public TimeSpan StartsAt { get; set; }

        public TimeSpan EndsAt { get; set; }

        [StringLength(160)]
        public string? Note { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
