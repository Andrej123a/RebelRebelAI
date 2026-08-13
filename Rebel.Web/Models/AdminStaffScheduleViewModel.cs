using System.ComponentModel.DataAnnotations;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;

namespace Rebel.Web.Models
{
    public class AdminStaffScheduleViewModel
    {
        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public DateTime PreviousWeek { get; set; }

        public DateTime NextWeek { get; set; }

        public List<StaffMember> StaffMembers { get; set; } =
            new();

        public List<StaffShift> Shifts { get; set; } =
            new();

        public List<StaffScheduleEventViewModel> Events { get; set; } =
            new();

        public StaffMemberInputModel NewStaff { get; set; } =
            new();

        public StaffShiftInputModel NewShift { get; set; } =
            new();
    }

    public class StaffMemberInputModel
    {
        [Required(ErrorMessage = "Staff name is required.")]
        [StringLength(80)]
        [Display(Name = "Staff name")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Section")]
        public StaffRole Role { get; set; } = StaffRole.Front;

        [StringLength(40)]
        [Display(Name = "Phone")]
        public string? PhoneNumber { get; set; }
    }

    public class StaffShiftInputModel
    {
        [Required]
        [Display(Name = "Staff member")]
        public Guid StaffMemberId { get; set; }

        [Required]
        [Display(Name = "Date")]
        public DateTime ShiftDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Start")]
        public TimeSpan StartsAt { get; set; } = new(10, 0, 0);

        [Required]
        [Display(Name = "End")]
        public TimeSpan EndsAt { get; set; } = new(16, 0, 0);

        [StringLength(160)]
        [Display(Name = "Note")]
        public string? Note { get; set; }
    }

    public class StaffScheduleEventViewModel
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        public TimeSpan? StartTime { get; set; }

        public string? ImageUrl { get; set; }

        public int ReservationCount { get; set; }

        public int GuestCount { get; set; }
    }

    public class AdminStaffMemberScheduleViewModel
    {
        public StaffMember StaffMember { get; set; } = new();

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public DateTime PreviousWeek { get; set; }

        public DateTime NextWeek { get; set; }

        public List<StaffShift> Shifts { get; set; } =
            new();
    }

    public class AdminMyShiftsViewModel
    {
        public StaffMember? StaffMember { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime PreviousWeek { get; set; }
        public DateTime NextWeek { get; set; }
        public List<StaffShift> Shifts { get; set; } = new();
    }
}
