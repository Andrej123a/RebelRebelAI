using Rebel.Domain.Entities;

namespace Rebel.Web.Models
{
    public class AdminFloorPlanViewModel
    {
        public DateTime CurrentLocalDate { get; set; }

        public List<AdminFloorTableViewModel> Tables { get; set; } =
            new();

        public List<Reservation> UnassignedReservations { get; set; } =
            new();
    }

    public class AdminFloorTableViewModel
    {
        public PubTable Table { get; set; } = new();

        public Reservation? OperationalReservation { get; set; }

        public bool IsOccupied { get; set; }
    }
}
