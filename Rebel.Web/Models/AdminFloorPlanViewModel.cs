using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class AdminFloorPlanViewModel
    {
        public DateTime CurrentLocalDate { get; set; }

        public List<AdminFloorRoomViewModel> Rooms { get; set; } =
            new();

        public List<AdminFloorTableViewModel> Tables { get; set; } =
            new();

        public List<FloorFixture> Fixtures { get; set; } = new();

        public List<Reservation> UnassignedReservations { get; set; } =
            new();

        public AdminFloorLayoutSaveRequest Layout { get; set; } = new();
    }

    public class AdminFloorRoomViewModel
    {
        public FloorRoom Room { get; set; } = new();

        public List<AdminFloorTableViewModel> Tables { get; set; } =
            new();
    }

    public class AdminFloorTableViewModel
    {
        public PubTable Table { get; set; } = new();

        public Reservation? OperationalReservation { get; set; }

        public bool IsOccupied { get; set; }
    }

    public class AdminFloorLayoutSaveRequest
    {
        public List<AdminFloorLayoutRoomRequest> Rooms { get; set; } =
            new();

        public List<AdminFloorLayoutTableRequest> Tables { get; set; } =
            new();

        public List<AdminFloorLayoutFixtureRequest> Fixtures { get; set; } = new();

        public List<Guid> RemovedTableIds { get; set; } = new();

        public List<Guid> RemovedFixtureIds { get; set; } = new();
    }

    public class AdminFloorLayoutRoomRequest
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Shape { get; set; } = "Rectangle";

        public int PositionX { get; set; }

        public int PositionY { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Rotation { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class AdminFloorLayoutTableRequest
    {
        public Guid? Id { get; set; }

        public string ClientId { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string? Area { get; set; }

        public int Capacity { get; set; }

        public string TableType { get; set; } = "Classic";

        public string Shape { get; set; } = "Square";

        public decimal LayoutX { get; set; }

        public decimal LayoutY { get; set; }

        public decimal LayoutWidth { get; set; }

        public decimal LayoutHeight { get; set; }

        public int Rotation { get; set; }

        public Guid? FloorRoomId { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class AdminFloorLayoutFixtureRequest
    {
        public Guid? Id { get; set; }
        public string ClientId { get; set; } = string.Empty;
        public string Kind { get; set; } = "door";
        public string Label { get; set; } = "Door";
        public Guid? FloorRoomId { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Rotation { get; set; }
    }

}
