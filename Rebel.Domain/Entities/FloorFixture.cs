using System.ComponentModel.DataAnnotations;

namespace Rebel.Domain.Entities
{
    public class FloorFixture
    {
        public Guid Id { get; set; }

        public Guid? FloorRoomId { get; set; }

        public FloorRoom? FloorRoom { get; set; }

        [Required, StringLength(24)]
        public string Kind { get; set; } = "door";

        [Required, StringLength(40)]
        public string Label { get; set; } = "Door";

        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public int Width { get; set; } = 100;
        public int Height { get; set; } = 50;
        public int Rotation { get; set; }
    }
}
