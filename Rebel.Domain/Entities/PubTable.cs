using System.ComponentModel.DataAnnotations;

namespace Rebel.Domain.Entities
{
    public class PubTable
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(40)]
        public string Label { get; set; } = string.Empty;

        [StringLength(80)]
        public string? Area { get; set; }

        [Range(1, 30)]
        public int Capacity { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
