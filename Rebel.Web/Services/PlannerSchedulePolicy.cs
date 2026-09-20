namespace Rebel.Web.Services
{
    public static class PlannerSchedulePolicy
    {
        public static TimeSpan GetClosingBoundary(DateTime serviceDate)
        {
            return serviceDate.DayOfWeek == DayOfWeek.Friday ||
                   serviceDate.DayOfWeek == DayOfWeek.Saturday
                ? TimeSpan.FromHours(25)
                : TimeSpan.FromHours(24);
        }

        public static TimeSpan ResolveEventEnd(
            DateTime serviceDate,
            TimeSpan eventStart,
            TimeSpan? configuredEnd)
        {
            var closingBoundary = GetClosingBoundary(serviceDate);

            if (!configuredEnd.HasValue)
            {
                return closingBoundary;
            }

            var eventEnd = configuredEnd.Value;
            if (eventEnd <= eventStart)
            {
                eventEnd = eventEnd.Add(TimeSpan.FromHours(24));
            }

            return eventEnd > closingBoundary
                ? closingBoundary
                : eventEnd;
        }
    }
}
