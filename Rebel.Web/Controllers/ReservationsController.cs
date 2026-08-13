using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Hubs;
using Rebel.Web.Models;
using Rebel.Web.Services;

namespace Rebel.Web.Controllers
{
    public class ReservationsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<NotificationHub> _notificationHub;
        private readonly IEmailService _emailService;
        private readonly ILogger<ReservationsController> _logger;

        private static readonly TimeZoneInfo SkopjeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Skopje");

        public ReservationsController(
            AppDbContext context,
            IHubContext<NotificationHub> notificationHub,
            IEmailService emailService,
            ILogger<ReservationsController> logger)
        {
            _context = context;
            _notificationHub = notificationHub;
            _emailService = emailService;
            _logger = logger;
        }

        // CREATE GET
        [HttpGet]
        public async Task<IActionResult> Create(Guid? eventId)
        {
            var nowInSkopje = GetCurrentSkopjeTime();

            var model = new ReservationCreateViewModel
            {
                ReservationDate = nowInSkopje.Date,
                ReservationTime = await GetFirstAvailableSlot(
                    nowInSkopje.Date,
                    eventId,
                    nowInSkopje) ?? ReservationPolicy.FirstOnlineSlot,
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
                    ReservationPolicy.IsOnlineSlot(
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
                        await GetFirstAvailableSlot(
                            model.ReservationDate,
                            eventId,
                            nowInSkopje) ??
                        ReservationPolicy.FirstOnlineSlot;
                }
            }

            await PrepareForm(
                nowInSkopje,
                model.ReservationDate,
                eventId);

            return View(model);
        }

        // CREATE POST
        [HttpPost]
        [ValidateAntiForgeryToken]
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
                await PrepareForm(
                    nowInSkopje,
                    model.ReservationDate,
                    model.EventId);

                return View(model);
            }

            var reservation = new Reservation
            {
                Id = Guid.NewGuid(),
                ReservationCode = await GenerateReservationCode(),

                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
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

            try
            {
                var receivedEmail = ReservationEmailTemplate.BuildReceived(
                    reservation.FullName,
                    reservation.ReservationDate,
                    reservation.ReservationTime,
                    reservation.NumberOfGuests,
                    reservation.ReservationCode,
                    "cid:rebel-logo",
                    model.EventTitle);

                await _emailService.SendEmailAsync(
                    reservation.Email,
                    "We got your reservation request | Rebel Rebel by Fat Kitchen",
                    receivedEmail,
                    HttpContext.RequestAborted);

                reservation.EmailStatus = "RequestSent";
                reservation.LastEmailSentAtUtc = DateTime.UtcNow;
                reservation.LastEmailError = null;

                _context.ReservationActivities.Add(new ReservationActivity
                {
                    ReservationId = reservation.Id,
                    Title = "Request email sent",
                    Description =
                        $"Request confirmation was sent to {reservation.Email}.",
                    Actor = "System",
                    CreatedAtUtc = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                reservation.EmailStatus = "RequestEmailFailed";
                reservation.LastEmailError = TruncateEmailError(ex);

                _context.ReservationActivities.Add(new ReservationActivity
                {
                    ReservationId = reservation.Id,
                    Title = "Request email failed",
                    Description =
                        $"Request confirmation could not be sent to {reservation.Email}.",
                    Actor = "System",
                    CreatedAtUtc = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                _logger.LogError(
                    ex,
                    "Reservation {ReservationId} was created, but the request received email could not be sent.",
                    reservation.Id);
            }

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

        [HttpGet]
        public async Task<IActionResult> AvailableSlots(
            DateTime date,
            Guid? eventId,
            CancellationToken cancellationToken)
        {
            if (date == default)
            {
                return BadRequest();
            }

            var nowInSkopje =
                GetCurrentSkopjeTime();

            var unavailableSlots =
                await GetUnavailableSlots(
                    date.Date,
                    eventId,
                    nowInSkopje);

            var slotLoads =
                await GetSlotLoads(
                    date.Date);

            var slots = ReservationPolicy
                .GetOnlineSlots()
                .Select(slot =>
                {
                    var bookedGuests =
                        slotLoads.TryGetValue(slot, out var load)
                            ? load
                            : 0;

                    var remainingSeats =
                        Math.Max(
                            0,
                            ReservationPolicy.MaxOnlineCoversPerSlot -
                            bookedGuests);

                    var isTooSoon =
                        date.Date == nowInSkopje.Date &&
                        date.Date.Add(slot) <=
                        nowInSkopje.Add(
                            ReservationPolicy.MinimumLeadTime);

                    var isFull =
                        remainingSeats <= 0;

                    return new
                    {
                        value = slot.ToString(@"hh\:mm"),
                        label = slot.ToString(@"hh\:mm"),
                        bookedGuests,
                        remainingSeats,
                        maxGuests = ReservationPolicy.MaxOnlineCoversPerSlot,
                        unavailableReason = isTooSoon
                            ? "tooSoon"
                            : isFull
                                ? "full"
                                : null,
                        isUnavailable =
                            unavailableSlots.Contains(slot) ||
                            isFull
                    };
                });

            return Json(slots);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

        private static string TruncateEmailError(Exception exception)
        {
            var message = string.IsNullOrWhiteSpace(exception.Message)
                ? exception.GetType().Name
                : exception.Message;

            return message.Length <= 500
                ? message
                : message[..500];
        }

        private async Task<string> GenerateReservationCode()
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var code =
                    $"RR-{Guid.NewGuid():N}"[..9].ToUpperInvariant();

                var exists = await _context.Reservations
                    .IgnoreQueryFilters()
                    .AnyAsync(reservation =>
                        reservation.ReservationCode == code);

                if (!exists)
                {
                    return code;
                }
            }

            return $"RR-{DateTime.UtcNow:HHmmss}";
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

            if (!ReservationPolicy.IsOnlineSlot(
                    model.ReservationTime))
            {
                ModelState.AddModelError(
                    nameof(model.ReservationTime),
                    "Please choose one of the available reservation times."
                );
            }

            if (!ModelState.IsValid)
            {
                return;
            }

            var reservedCoversForSlot =
                await _context.Reservations
                    .AsNoTracking()
                    .Where(reservation =>
                        reservation.ReservationDate ==
                            model.ReservationDate.Date &&
                        reservation.ReservationTime ==
                            model.ReservationTime &&
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
                    "That time is fully booked. Please choose another slot."
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

        private async Task PrepareForm(
            DateTime nowInSkopje,
            DateTime selectedDate,
            Guid? eventId)
        {
            ViewBag.MinimumReservationDate =
                nowInSkopje.ToString("yyyy-MM-dd");

            ViewBag.LatestAllowedBookingTime =
                nowInSkopje.Add(
                    ReservationPolicy.MinimumLeadTime);

            ViewBag.ReservationSlots =
                ReservationPolicy.GetOnlineSlots();

            ViewBag.UnavailableSlots =
                await GetUnavailableSlots(
                    selectedDate.Date,
                    eventId,
                    nowInSkopje);

            ViewBag.SlotLoads =
                await GetSlotLoads(
                    selectedDate.Date);
        }

        private async Task<HashSet<TimeSpan>> GetUnavailableSlots(
            DateTime selectedDate,
            Guid? eventId,
            DateTime nowInSkopje)
        {
            var unavailableSlots = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.ReservationDate == selectedDate.Date &&
                    reservation.Status != ReservationStatus.Rejected &&
                    reservation.Status != ReservationStatus.NoShow &&
                    reservation.Status != ReservationStatus.Cancelled)
                .GroupBy(reservation =>
                    reservation.ReservationTime)
                .Where(group =>
                    group.Sum(reservation =>
                        reservation.NumberOfGuests) >=
                    ReservationPolicy.MaxOnlineCoversPerSlot)
                .Select(group => group.Key)
                .ToListAsync();

            var unavailableSet =
                unavailableSlots.ToHashSet();

            var latestAllowedBookingTime =
                nowInSkopje.Add(
                    ReservationPolicy.MinimumLeadTime);

            if (selectedDate.Date == nowInSkopje.Date)
            {
                foreach (var slot in ReservationPolicy.GetOnlineSlots())
                {
                    var slotDateTime =
                        selectedDate.Date.Add(slot);

                    if (slotDateTime <= latestAllowedBookingTime)
                    {
                        unavailableSet.Add(slot);
                    }
                }
            }

            if (eventId.HasValue)
            {
                var selectedEvent = await _context.Events
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e =>
                        e.Id == eventId.Value &&
                        e.IsActive);

                if (selectedEvent != null &&
                    await IsEventFullyBooked(selectedEvent))
                {
                    foreach (var slot in ReservationPolicy.GetOnlineSlots())
                    {
                        unavailableSet.Add(slot);
                    }
                }
            }

            return unavailableSet;
        }

        private async Task<TimeSpan?> GetFirstAvailableSlot(
            DateTime selectedDate,
            Guid? eventId,
            DateTime nowInSkopje)
        {
            var unavailableSlots =
                await GetUnavailableSlots(
                    selectedDate,
                    eventId,
                    nowInSkopje);

            return ReservationPolicy
                .GetOnlineSlots()
                .Cast<TimeSpan?>()
                .FirstOrDefault(slot =>
                    slot.HasValue &&
                    !unavailableSlots.Contains(slot.Value));
        }

        private async Task<Dictionary<TimeSpan, int>> GetSlotLoads(
            DateTime selectedDate)
        {
            return await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.ReservationDate == selectedDate.Date &&
                    reservation.Status != ReservationStatus.Rejected &&
                    reservation.Status != ReservationStatus.NoShow &&
                    reservation.Status != ReservationStatus.Cancelled)
                .GroupBy(reservation =>
                    reservation.ReservationTime)
                .ToDictionaryAsync(
                    group => group.Key,
                    group => group.Sum(reservation =>
                        reservation.NumberOfGuests));
        }

        private async Task<bool> IsEventFullyBooked(
            Event selectedEvent)
        {
            if (!selectedEvent.MaxReservations.HasValue &&
                !selectedEvent.MaxGuests.HasValue)
            {
                return false;
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

            return
                selectedEvent.MaxReservations.HasValue &&
                eventReservations.Count >=
                    selectedEvent.MaxReservations.Value ||
                selectedEvent.MaxGuests.HasValue &&
                eventReservations.Sum(reservation =>
                    reservation.NumberOfGuests) >=
                    selectedEvent.MaxGuests.Value;
        }
    }
}
