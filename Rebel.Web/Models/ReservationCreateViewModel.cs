using System.ComponentModel.DataAnnotations;

namespace Rebel.Web.Models
{
    public class ReservationCreateViewModel
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your phone number.")]
        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [StringLength(30)]
        [Display(Name = "Phone number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a reservation date.")]
        [DataType(DataType.Date)]
        [Display(Name = "Reservation date")]
        public DateTime ReservationDate { get; set; }

        [Required(ErrorMessage = "Please select a reservation time.")]
        [DataType(DataType.Time)]
        [Display(Name = "Reservation time")]
        public TimeSpan ReservationTime { get; set; }

        [Range(
            1,
            20,
            ErrorMessage = "Number of guests must be between 1 and 20."
        )]
        [Display(Name = "Number of guests")]
        public int NumberOfGuests { get; set; }

        [StringLength(
            500,
            ErrorMessage = "The note cannot be longer than 500 characters."
        )]
        [Display(Name = "Special requests")]
        public string? Note { get; set; }
        public Guid? EventId { get; set; }

        public string? EventTitle { get; set; }
    }
}