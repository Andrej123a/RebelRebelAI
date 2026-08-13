namespace Rebel.Web.Services
{
    public static class ReservationPolicy
    {
        public static readonly TimeSpan FirstOnlineSlot =
            new(10, 0, 0);

        public static readonly TimeSpan LastOnlineSlot =
            new(22, 0, 0);

        public static readonly TimeSpan SlotInterval =
            TimeSpan.FromMinutes(30);

        public static readonly TimeSpan MinimumLeadTime =
            TimeSpan.FromHours(2);

        public const int MaxOnlineCoversPerSlot = 40;

        public static IReadOnlyList<TimeSpan> GetOnlineSlots()
        {
            var slots = new List<TimeSpan>();

            for (var slot = FirstOnlineSlot;
                 slot <= LastOnlineSlot;
                 slot = slot.Add(SlotInterval))
            {
                slots.Add(slot);
            }

            return slots;
        }

        public static bool IsOnlineSlot(TimeSpan reservationTime)
        {
            return GetOnlineSlots().Contains(reservationTime);
        }
    }
}
