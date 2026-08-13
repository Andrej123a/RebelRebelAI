using System.ComponentModel.DataAnnotations;

namespace Rebel.Domain.Entities
{
    public class ReservationActivity
    {
        public int Id { get; set; }

        public Guid ReservationId { get; set; }

        public Reservation Reservation { get; set; } = null!;

        [Required]
        [StringLength(80)]
        public string Title { get; set; } = null!;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = null!;

        [Required]
        [StringLength(30)]
        public string Actor { get; set; } = "System";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
