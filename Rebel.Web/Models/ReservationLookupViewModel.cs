using System.ComponentModel.DataAnnotations;
using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class ReservationLookupViewModel
    {
        [Required(ErrorMessage = "Reservation code is required.")]
        [StringLength(16)]
        [Display(Name = "Reservation code")]
        public string ReservationCode { get; set; } = string.Empty;

        public Reservation? Reservation { get; set; }

        public bool HasSearched { get; set; }

        public DateTime CurrentLocalTime { get; set; } = DateTime.Now;
    }
}
