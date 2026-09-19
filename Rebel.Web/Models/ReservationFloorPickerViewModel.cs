using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class ReservationFloorPickerViewModel
    {
        public string InputId { get; set; } = "reservation-table";

        public int PartySize { get; set; }

        public DateTime ReservationDate { get; set; }

        public TimeSpan ReservationTime { get; set; }

        public string? SelectedTableLabel { get; set; }

        public string? RecommendedTableLabel { get; set; }

        public List<FloorRoom> Rooms { get; set; } = new();

        public List<PubTable> Tables { get; set; } = new();

        public List<FloorFixture> Fixtures { get; set; } = new();

        public List<string> BusyTableLabels { get; set; } = new();
    }
}
