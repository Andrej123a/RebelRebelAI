using System.ComponentModel.DataAnnotations;

namespace Rebel.Web.Models
{
    public class AdminStaffAccountsViewModel
    {
        public List<AdminStaffAccountRowViewModel> Accounts { get; set; } = new();

        public List<StaffAccountProfileOptionViewModel> StaffProfiles { get; set; } = new();

        public AdminStaffAccountInputModel NewAccount { get; set; } = new();
    }

    public class AdminStaffAccountRowViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool IsDisabled { get; set; }

        public bool IsCurrentUser { get; set; }

        public bool IsOwner { get; set; }

        public Guid? StaffMemberId { get; set; }

        public string? StaffMemberName { get; set; }
    }

    public class StaffAccountProfileOptionViewModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Section { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string? LinkedAccountId { get; set; }
    }

    public class AdminStaffAccountInputModel
    {
        [Required]
        [StringLength(80)]
        [Display(Name = "Name")]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Staff|Manager)$")]
        public string Role { get; set; } = "Staff";

        [Required]
        [Display(Name = "Staff profile")]
        public Guid? StaffMemberId { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8)]
        [Display(Name = "Initial password")]
        public string TemporaryPassword { get; set; } = string.Empty;
    }
}
