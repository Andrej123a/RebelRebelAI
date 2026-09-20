using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Hubs;
using Rebel.Web.Models;
using Rebel.Web.Services;

using Microsoft.AspNetCore.RateLimiting;
using Rebel.Web.Authorization;

namespace Rebel.Web.Controllers
{
    public class ReservationsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;

        private static readonly TimeZoneInfo SkopjeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Skopje");

        public ReservationsController(
            AppDbContext context,
            IHubContext<NotificationHub> notificationHub)
        {
            _context = context;
            _notificationHub = notificationHub;
        }

        // CREATE GET
        [HttpGet]
        public async Task<IActionResult> Create(Guid? eventId)
        {
            var nowInSkopje = GetCurrentSkopjeTime();
            var suggestedArrival =
                GetSuggestedArrival(nowInSkopje);

            var model = new ReservationCreateViewModel
            {
                ReservationDate = suggestedArrival.Date,
                ReservationTime = suggestedArrival.TimeOfDay,
                NumberOfGuests = 2
            };

            // Ако резервацијата доаѓа од конкретен event
            if (eventId.HasValue)
            {
                var selectedEvent = await _context.Events
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e =>
                        e.Id == eventId.Value &&
                        e.IsActive
                    );

                if (selectedEvent == null)
                {
                    return NotFound();
                }

                model.EventId = selectedEvent.Id;
                model.EventTitle = selectedEvent.Title;
                model.ReservationDate = selectedEvent.Date.Date;

                if (selectedEvent.StartTime.HasValue &&
                    ReservationPolicy.IsWithinOnlineHours(
                        selectedEvent.StartTime.Value) &&
                    selectedEvent.Date.Date
                        .Add(selectedEvent.StartTime.Value) >
                    nowInSkopje.Add(
                        ReservationPolicy.MinimumLeadTime))
                {
                    model.ReservationTime =
                        selectedEvent.StartTime.Value;
                }
                else
                {
                    model.ReservationTime =
                        GetSuggestedTimeForDate(
                            model.ReservationDate,
                            nowInSkopje);
                }
            }

            PrepareForm(nowInSkopje);

            return View(model);
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.ReservationCreate)]
        public async Task<IActionResult> Create(
            ReservationCreateViewModel model)
        {
            var nowInSkopje = GetCurrentSkopjeTime();

            // Повторна проверка на event-от.
            // Не се потпираме само на EventId испратено од формата.
            if (model.EventId.HasValue)
            {
                var selectedEvent = await _context.Events
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e =>
                        e.Id == model.EventId.Value &&
                        e.IsActive
                    );

                if (selectedEvent == null)
                {
                    ModelState.AddModelError(
                        nameof(model.EventId),
                        "The selected event is no longer available."
                    );
                }
                else
                {
                    model.EventTitle = selectedEvent.Title;

                    // Резервацијата мора да остане на датумот
                    // на избраниот event.
                    model.ReservationDate =
                        selectedEvent.Date.Date;

                    await ValidateEventCapacity(
                        model,
                        selectedEvent);
                }
            }

            await ValidateReservationDateTime(
                model,
                nowInSkopje);

            ValidateLargePartyDetails(model);

            if (!ModelState.IsValid)
            {
                PrepareForm(nowInSkopje);

                return View(model);
            }

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                ReservationCode = await GenerateReservationCode(),

                FullName = model.FullName.Trim(),
                // The column stays populated for compatibility with older reservations.
                Email = string.Empty,
                EmailStatus = "CodeOnly",
                PhoneNumber = model.PhoneNumber.Trim(),

                ReservationDate = model.ReservationDate.Date,
                ReservationTime = model.ReservationTime,
                NumberOfGuests = model.NumberOfGuests,

                Note = string.IsNullOrWhiteSpace(model.Note)
                    ? null
                    : model.Note.Trim(),

                EventId = model.EventId,

                Status = ReservationStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            var reservationDate =
                reservation.ReservationDate.ToString(
                    "dd MMM yyyy"
                );

            var reservationTime =
                reservation.ReservationTime.ToString(
                    @"hh\:mm"
                );

            var eventText =
                string.IsNullOrWhiteSpace(model.EventTitle)
                    ? string.Empty
                    : $" for {model.EventTitle}";

            var notification = new Notification
            {
                Title = "New table reservation",

                Message =
                    $"{reservation.FullName} requested a table for " +
                    $"{reservation.NumberOfGuests} guests on " +
                    $"{reservationDate} at {reservationTime}{eventText}.",

                Link = $"/AdminReservations/Details/{reservation.Id}",

                IsRead = false,
                CreatedAt = DateTime.UtcNow,

                ReservationId = reservation.Id
            };

            _context.Reservations.Add(reservation);
            _context.Notifications.Add(notification);
            _context.ReservationActivities.Add(new ReservationActivity
            {
                ReservationId = reservation.Id,
                Title = "Request submitted",
                Description =
                    $"{reservation.FullName} requested a table for {reservation.NumberOfGuests} guests.",
                Actor = "Guest",
                CreatedAtUtc = reservation.CreatedAtUtc
            });

            await _context.SaveChangesAsync();

            await _notificationHub.Clients.All.SendAsync(
                "ReceiveNotification",
                new
                {
                    id = notification.Id,
                    title = notification.Title,
                    message = notification.Message,
                    link = notification.Link,
                    createdAt = notification.CreatedAt
                },
                HttpContext.RequestAborted
            );

            TempData["ReservationSubmitted"] = true;
            TempData["ReservationName"] =
                reservation.FullName;
            TempData["ReservationDate"] =
                reservation.ReservationDate.ToString("dd MMM yyyy");
            TempData["ReservationTime"] =
                reservation.ReservationTime.ToString(@"hh\:mm");
            TempData["ReservationGuests"] =
                reservation.NumberOfGuests.ToString();
            TempData["ReservationEvent"] =
                model.EventTitle ?? string.Empty;
            TempData["ReservationCode"] =
                reservation.ReservationCode;

            return RedirectToAction(
                nameof(Confirmation)
            );
        }

        // CONFIRMATION
        [HttpGet]
        public IActionResult Confirmation()
        {
            if (TempData["ReservationSubmitted"] == null)
            {
                return RedirectToAction(
                    nameof(Create)
                );
            }

            ViewBag.ReservationName =
                TempData["ReservationName"]?.ToString();
            ViewBag.ReservationDate =
                TempData["ReservationDate"]?.ToString();
            ViewBag.ReservationTime =
                TempData["ReservationTime"]?.ToString();
            ViewBag.ReservationGuests =
                TempData["ReservationGuests"]?.ToString();
            ViewBag.ReservationEvent =
                TempData["ReservationEvent"]?.ToString();
            ViewBag.ReservationCode =
                TempData["ReservationCode"]?.ToString();

            return View();
        }

        [HttpGet]
        [EnableRateLimiting(RateLimitPolicies.ReservationLookup)]
        public async Task<IActionResult> Lookup(
            string? reservationCode,
            CancellationToken cancellationToken)
        {
            var model = new ReservationLookupViewModel
            {
                ReservationCode = reservationCode ?? string.Empty,
                CurrentLocalTime = GetCurrentSkopjeTime()
            };

            if (!string.IsNullOrWhiteSpace(reservationCode))
            {
                model.HasSearched = true;
                model.Reservation =
                    await FindPublicReservationAsync(
                        reservationCode,
                        cancellationToken);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.ReservationLookup)]
        public async Task<IActionResult> Lookup(
            ReservationLookupViewModel model,
            CancellationToken cancellationToken)
        {
            model.HasSearched = true;

            if (!ModelState.IsValid)
            {
                model.CurrentLocalTime = GetCurrentSkopjeTime();
                return View(model);
            }

            var normalizedCode = NormalizeReservationCode(
                model.ReservationCode);
            model.CurrentLocalTime = GetCurrentSkopjeTime();

            model.Reservation = await FindPublicReservationAsync(
                normalizedCode,
                cancellationToken);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(RateLimitPolicies.ReservationLookup)]
        public async Task<IActionResult> Cancel(
            Guid id,
            string reservationCode,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    reservation => reservation.Id == id,
                    cancellationToken);

            if (reservation is null)
            {
                return NotFound();
            }

            var codeMatches =
                reservation.ReservationCode.Equals(
                    NormalizeReservationCode(reservationCode),
                    StringComparison.OrdinalIgnoreCase);

            if (!codeMatches)
            {
                return Forbid();
            }

            if (!CanGuestCancel(
                    reservation.Status,
                    reservation.ReservationDate,
                    reservation.ReservationTime,
                    GetCurrentSkopjeTime()))
            {
                TempData["LookupMessage"] =
                    "This reservation can no longer be cancelled online.";

                return RedirectToAction(
                    nameof(Lookup),
                    new { reservationCode });
            }

            reservation.Status = ReservationStatus.Cancelled;
            reservation.TableLabel = null;
            reservation.RespondedAtUtc = DateTime.UtcNow;
            reservation.InternalNote =
                AppendInternalNote(
                    reservation.InternalNote,
                    "Guest cancelled online.");
            _context.ReservationActivities.Add(new ReservationActivity
            {
                ReservationId = reservation.Id,
                Title = "Cancelled online",
                Description =
                    "Guest cancelled the reservation using their reservation code.",
                Actor = "Guest",
                CreatedAtUtc = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);

            var notification = new Notification
            {
                Title = "Reservation cancelled",
                Message =
                    $"{reservation.FullName} cancelled their reservation for " +
                    $"{reservation.ReservationDate:dd MMM yyyy} at " +
                    $"{reservation.ReservationTime:hh\\:mm}.",
                Link = "/AdminReservations?status=Cancelled",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                ReservationId = reservation.Id
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancellationToken);

            await _notificationHub.Clients.All.SendAsync(
                "ReceiveNotification",
                new
                {
                    id = notification.Id,
                    title = notification.Title,
                    message = notification.Message,
                    link = notification.Link,
                    createdAt = notification.CreatedAt
                },
                cancellationToken
            );

            TempData["LookupMessage"] =
                "Your reservation has been cancelled.";

            return RedirectToAction(
                nameof(Lookup),
                new { reservationCode });
        }

        private static DateTime GetCurrentSkopjeTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                SkopjeTimeZone
            );
        }

        private static bool CanGuestCancel(
            ReservationStatus status,
            DateTime reservationDate,
            TimeSpan reservationTime,
            DateTime currentLocalTime)
        {
            if (status != ReservationStatus.Pending &&
                status != ReservationStatus.Approved)
            {
                return false;
            }

            return reservationDate.Date.Add(reservationTime) >
                   currentLocalTime;
        }

        private async Task<Reservation?> FindPublicReservationAsync(
            string reservationCode,
            CancellationToken cancellationToken)
        {
            var normalizedCode = NormalizeReservationCode(
                reservationCode);
            var today = GetCurrentSkopjeTime().Date;

            return await _context.Reservations
                .AsNoTracking()
                .Include(reservation => reservation.Event)
                .FirstOrDefaultAsync(reservation =>
                    reservation.ReservationCode.ToUpper() == normalizedCode &&
                    reservation.ReservationDate >= today,
                    cancellationToken);
        }

        private static string NormalizeReservationCode(
            string? reservationCode)
        {
            if (string.IsNullOrWhiteSpace(reservationCode))
            {
                return string.Empty;
            }

            return reservationCode
                .Trim()
                .Replace(" ", string.Empty)
                .ToUpperInvariant();
        }

        private async Task<string> GenerateReservationCode()
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var code = ReservationCodeGenerator.Create();

                var exists = await _context.Reservations
                    .IgnoreQueryFilters()
                    .AnyAsync(reservation =>
                        reservation.ReservationCode == code);

                if (!exists)
                {
                    return code;
                }
            }

            throw new InvalidOperationException(
                "Could not generate a unique reservation code.");
        }

        private static string AppendInternalNote(
            string? currentNote,
            string newNote)
        {
            var timestamp =
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");

            var entry = $"[{timestamp} UTC] {newNote}";

            return string.IsNullOrWhiteSpace(currentNote)
                ? entry
                : $"{currentNote}{Environment.NewLine}{entry}";
        }

        private async Task ValidateReservationDateTime(
            ReservationCreateViewModel model,
            DateTime nowInSkopje)
        {
            if (model.ReservationDate == default)
            {
                return;
            }

            var requestedDateTime =
                model.ReservationDate.Date
                    .Add(model.ReservationTime);

            if (requestedDateTime <=
                nowInSkopje.Add(
                    ReservationPolicy.MinimumLeadTime))
            {
                ModelState.AddModelError(
                    nameof(model.ReservationDate),
                    "Please choose a time at least 2 hours from now."
                );
            }

            if (!ReservationPolicy.IsWithinOnlineHours(
                    model.ReservationTime))
            {
                ModelState.AddModelError(
                    nameof(model.ReservationTime),
                    "Please choose an arrival time between 10:00 and 22:00."
                );
            }

            if (!ModelState.IsValid)
            {
                return;
            }

            var capacityWindowStart =
                model.ReservationTime.Subtract(TimeSpan.FromMinutes(30));
            var capacityWindowEnd =
                model.ReservationTime.Add(TimeSpan.FromMinutes(30));

            var reservedCoversForSlot =
                await _context.Reservations
                    .AsNoTracking()
                    .Where(reservation =>
                        reservation.ReservationDate ==
                            model.ReservationDate.Date &&
                        reservation.ReservationTime >= capacityWindowStart &&
                        reservation.ReservationTime < capacityWindowEnd &&
                        reservation.Status !=
                            ReservationStatus.Rejected &&
                        reservation.Status !=
                            ReservationStatus.NoShow &&
                        reservation.Status !=
                            ReservationStatus.Cancelled)
                    .SumAsync(reservation =>
                        reservation.NumberOfGuests);

            if (reservedCoversForSlot +
                model.NumberOfGuests >
                ReservationPolicy.MaxOnlineCoversPerSlot)
            {
                ModelState.AddModelError(
                    nameof(model.ReservationTime),
                    "We are full around that arrival time. Please choose another time."
                );
            }
        }

        private void ValidateLargePartyDetails(
            ReservationCreateViewModel model)
        {
            if (model.NumberOfGuests < 13)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(model.Note))
            {
                ModelState.AddModelError(
                    nameof(model.Note),
                    "For groups of 13 or more, please add a short note so our team can arrange seating."
                );
            }
        }

        private async Task ValidateEventCapacity(
            ReservationCreateViewModel model,
            Event selectedEvent)
        {
            if (!selectedEvent.MaxReservations.HasValue &&
                !selectedEvent.MaxGuests.HasValue)
            {
                return;
            }

            var eventReservations = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.EventId == selectedEvent.Id &&
                    reservation.Status != ReservationStatus.Rejected &&
                    reservation.Status != ReservationStatus.NoShow &&
                    reservation.Status != ReservationStatus.Cancelled)
                .Select(reservation => new
                {
                    reservation.NumberOfGuests
                })
                .ToListAsync();

            if (selectedEvent.MaxReservations.HasValue &&
                eventReservations.Count + 1 >
                selectedEvent.MaxReservations.Value)
            {
                ModelState.AddModelError(
                    nameof(model.EventId),
                    "This event is fully booked for reservations."
                );
            }

            var reservedGuests = eventReservations.Sum(reservation =>
                reservation.NumberOfGuests);

            if (selectedEvent.MaxGuests.HasValue &&
                reservedGuests + model.NumberOfGuests >
                selectedEvent.MaxGuests.Value)
            {
                ModelState.AddModelError(
                    nameof(model.NumberOfGuests),
                    "This event does not have enough remaining guest capacity."
                );
            }
        }

        private void PrepareForm(DateTime nowInSkopje)
        {
            ViewBag.MinimumReservationDate =
                nowInSkopje.ToString("yyyy-MM-dd");
        }

        private static DateTime GetSuggestedArrival(
            DateTime nowInSkopje)
        {
            var earliestArrival =
                nowInSkopje.Add(
                    ReservationPolicy.MinimumLeadTime);

            if (earliestArrival.TimeOfDay >
                ReservationPolicy.LastOnlineSlot)
            {
                return earliestArrival.Date
                    .AddDays(1)
                    .Add(ReservationPolicy.FirstOnlineSlot);
            }

            if (earliestArrival.TimeOfDay <
                ReservationPolicy.FirstOnlineSlot)
            {
                return earliestArrival.Date
                    .Add(ReservationPolicy.FirstOnlineSlot);
            }

            var intervalTicks =
                TimeSpan.FromMinutes(15).Ticks;
            var roundedTicks =
                ((earliestArrival.TimeOfDay.Ticks + intervalTicks - 1) /
                 intervalTicks) * intervalTicks;

            return earliestArrival.Date
                .Add(TimeSpan.FromTicks(roundedTicks));
        }

        private static TimeSpan GetSuggestedTimeForDate(
            DateTime selectedDate,
            DateTime nowInSkopje)
        {
            var suggestedArrival =
                GetSuggestedArrival(nowInSkopje);

            return suggestedArrival.Date == selectedDate.Date
                ? suggestedArrival.TimeOfDay
                : new TimeSpan(19, 0, 0);
        }
    }
}
