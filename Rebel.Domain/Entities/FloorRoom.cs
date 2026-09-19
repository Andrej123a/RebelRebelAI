using System.ComponentModel.DataAnnotations;

namespace Rebel.Domain.Entities
{
    public class FloorRoom
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(80)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Shape { get; set; } = "Rectangle";

        [Range(0, 4000)]
        public int PositionX { get; set; } = 32;

        [Range(0, 4000)]
        public int PositionY { get; set; } = 32;

        [Range(240, 2200)]
        public int Width { get; set; } = 720;

        [Range(180, 1600)]
        public int Height { get; set; } = 460;

        [Range(-180, 180)]
        public int Rotation { get; set; }

        public bool IsActive { get; set; } = true;

        public List<PubTable> Tables { get; set; } = new();

        public List<FloorFixture> Fixtures { get; set; } = new();
    }
}
