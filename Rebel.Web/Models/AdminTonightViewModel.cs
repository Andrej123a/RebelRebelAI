using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class AdminTonightViewModel
    {
        public DateTime LocalDate { get; set; }

        public TimeSpan CurrentTime { get; set; }

        public int SlotCapacity { get; set; }

        public int ActiveTableCapacity { get; set; }

        public List<Reservation> Reservations { get; set; } = new();

        public List<PubTable> ActiveTables { get; set; } = new();

        public List<StaffShift> StaffShifts { get; set; } = new();

        public List<Event> Events { get; set; } = new();

        public Dictionary<TimeSpan, int> SlotLoads { get; set; } = new();

        public List<AdminOperationalWarning> Warnings { get; set; } = new();
    }

    public class AdminOperationalWarning
    {
        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Tone { get; set; } = "attention";

        public string Controller { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public Dictionary<string, string?> RouteValues { get; set; } = new();
    }
}
