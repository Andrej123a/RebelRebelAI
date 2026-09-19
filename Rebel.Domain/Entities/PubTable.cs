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

        public Guid? FloorRoomId { get; set; }

        public FloorRoom? FloorRoom { get; set; }

        [Range(1, 30)]
        public int Capacity { get; set; }

        [StringLength(30)]
        public string TableType { get; set; } = "Classic";

        [StringLength(20)]
        public string Shape { get; set; } = "Square";

        [Range(0, 100)]
        public decimal LayoutX { get; set; } = 50;

        [Range(0, 100)]
        public decimal LayoutY { get; set; } = 50;

        [Range(6, 60)]
        public decimal LayoutWidth { get; set; } = 14;

        [Range(6, 60)]
        public decimal LayoutHeight { get; set; } = 14;

        [Range(-180, 180)]
        public int Rotation { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
