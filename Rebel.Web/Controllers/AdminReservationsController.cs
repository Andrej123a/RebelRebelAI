using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Models;
using Rebel.Web.Services;
using Rebel.Web.Authorization;

namespace Rebel.Web.Controllers
{
    [Authorize(Policy = AdminPolicies.Backstage)]
    public class AdminReservationsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AdminReservationsController> _logger;

        private static readonly TimeZoneInfo SkopjeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Skopje");

        public AdminReservationsController(
            AppDbContext context,
            IEmailService emailService,
            ILogger<AdminReservationsController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // ALL RESERVATIONS
        [HttpGet]
        public async Task<IActionResult> Index(
            ReservationStatus? status,
            Guid? eventId,
            DateTime? date,
            string? range,
            TimeSpan? time,
            CancellationToken cancellationToken,
            bool needsTable = false)
        {
            var nowInSkopje = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                SkopjeTimeZone);

            var selectedRange = string.IsNullOrWhiteSpace(range)
                ? null
                : range.Trim().ToLowerInvariant();

            DateTime? dateFrom = null;
            DateTime? dateTo = null;

            if (!date.HasValue)
            {
                if (selectedRange == "today")
                {
                    dateFrom = nowInSkopje.Date;
                    dateTo = dateFrom.Value.AddDays(1);
                }
                else if (selectedRange == "tomorrow")
                {
                    dateFrom = nowInSkopje.Date.AddDays(1);
                    dateTo = dateFrom.Value.AddDays(1);
                }
                else if (selectedRange == "week")
                {
                    dateFrom = nowInSkopje.Date;
                    dateTo = dateFrom.Value.AddDays(7);
                }
            }

            var query = _context.Reservations
                .AsNoTracking()
                .Include(r => r.Event)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(r =>
                    r.Status == status.Value);
            }

            if (eventId.HasValue)
            {
                query = query.Where(r =>
                    r.EventId == eventId.Value);
            }

            if (date.HasValue)
            {
                query = query.Where(r =>
                    r.ReservationDate == date.Value.Date);
            }
            else if (dateFrom.HasValue && dateTo.HasValue)
            {
                query = query.Where(r =>
                    r.ReservationDate >= dateFrom.Value &&
                    r.ReservationDate < dateTo.Value);
            }

            if (time.HasValue)
            {
                query = query.Where(r =>
                    r.ReservationTime == time.Value);
            }

            if (needsTable)
            {
                query = query.Where(r =>
                    (r.Status == ReservationStatus.Pending ||
                     r.Status == ReservationStatus.Approved) &&
                    (r.TableLabel == null || r.TableLabel == string.Empty));
            }

            var reservations = await query
                .OrderByDescending(r => r.ReservationDate)
                .ThenBy(r =>
                    r.Status == ReservationStatus.Pending ? 0 :
                    r.Status == ReservationStatus.Approved ? 1 :
                    r.Status == ReservationStatus.Arrived ? 2 :
                    r.Status == ReservationStatus.NoShow ? 3 : 4)
                .ThenBy(r => r.ReservationTime)
                .ThenBy(r => r.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            ViewBag.SelectedStatus = status;
            ViewBag.SelectedEventId = eventId;
            ViewBag.SelectedDate = date;
            ViewBag.SelectedRange = selectedRange;
            ViewBag.SelectedTime = time;
            ViewBag.NeedsTable = needsTable;

            if (eventId.HasValue)
            {
                ViewBag.SelectedEvent = await _context.Events
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        ev => ev.Id == eventId.Value,
                        cancellationToken);
            }

            return View(reservations);
        }

        [HttpGet]
        public async Task<IActionResult> Planner(
            DateTime? startDate,
            DateTime? date,
            CancellationToken cancellationToken)
        {
            var nowInSkopje = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                SkopjeTimeZone);

            var today = nowInSkopje.Date;
            var requestedStart = startDate?.Date ?? today;
            var weekStart = requestedStart.AddDays(
                -((int)requestedStart.DayOfWeek + 6) % 7);
            var endDate = weekStart.AddDays(7);
            var selectedDate = date?.Date ?? today;

            if (selectedDate < weekStart || selectedDate >= endDate)
            {
                selectedDate = weekStart;
            }

            var reservations = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Event)
                .Where(r =>
                    r.ReservationDate >= weekStart &&
                    r.ReservationDate < endDate &&
                    r.Status != ReservationStatus.Rejected &&
                    r.Status != ReservationStatus.Cancelled)
                .OrderBy(r => r.ReservationDate)
                .ThenBy(r => r.ReservationTime)
                .ThenBy(r => r.FullName)
                .ToListAsync(cancellationToken);

            ViewBag.Today = today;
            ViewBag.StartDate = weekStart;
            ViewBag.EndDate = endDate.AddDays(-1);
            ViewBag.PreviousWeek = weekStart.AddDays(-7);
            ViewBag.NextWeek = weekStart.AddDays(7);
            ViewBag.SelectedDate = selectedDate;
            ViewBag.SlotCapacity =
                ReservationPolicy.MaxOnlineCoversPerSlot;
            ViewBag.ReservationSlots =
                ReservationPolicy.GetOnlineSlots();

            return View(reservations);
        }

        // TONIGHT VIEW
        [HttpGet]
        public async Task<IActionResult> Tonight(
            CancellationToken cancellationToken)
        {
            var nowInSkopje = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                SkopjeTimeZone);

            var today = nowInSkopje.Date;
            var tomorrow = today.AddDays(1);

            var reservations = await _context.Reservations
                .AsNoTracking()
                .Where(r =>
                    r.ReservationDate >= today &&
                    r.ReservationDate < tomorrow &&
                    r.Status != ReservationStatus.Rejected &&
                    r.Status != ReservationStatus.Cancelled)
                .OrderBy(r => r.ReservationTime)
                .ThenBy(r =>
                    r.Status == ReservationStatus.Pending ? 0 :
                    r.Status == ReservationStatus.Approved ? 1 :
                    r.Status == ReservationStatus.Arrived ? 2 :
                    r.Status == ReservationStatus.NoShow ? 3 : 4)
                .ToListAsync(cancellationToken);

            var activeTables = await _context.PubTables
                .AsNoTracking()
                .Where(table => table.IsActive)
                .OrderBy(table => table.Area)
                .ThenBy(table => table.Label)
                .ToListAsync(cancellationToken);

            var shifts = await _context.StaffShifts
                .AsNoTracking()
                .Include(shift => shift.StaffMember)
                .Where(shift => shift.ShiftDate == today)
                .OrderBy(shift => shift.Role)
                .ThenBy(shift => shift.StartsAt)
                .ToListAsync(cancellationToken);

            var eventDayStart = DateTime.SpecifyKind(today, DateTimeKind.Utc);
            var eventDayEnd = eventDayStart.AddDays(1);
            var events = await _context.Events
                .AsNoTracking()
                .Where(eventItem =>
                    eventItem.IsActive &&
                    eventItem.Date >= eventDayStart &&
                    eventItem.Date < eventDayEnd)
                .OrderBy(eventItem => eventItem.StartTime)
                .ToListAsync(cancellationToken);

            var slotLoads = reservations
                    .Where(r =>
                        r.Status != ReservationStatus.Rejected &&
                        r.Status != ReservationStatus.NoShow &&
                        r.Status != ReservationStatus.Cancelled)
                    .GroupBy(r => r.ReservationTime)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Sum(r =>
                            r.NumberOfGuests));

            var warnings = BuildTonightWarnings(
                reservations,
                shifts,
                activeTables,
                slotLoads);

            return View(new AdminTonightViewModel
            {
                LocalDate = today,
                CurrentTime = nowInSkopje.TimeOfDay,
                SlotCapacity = ReservationPolicy.MaxOnlineCoversPerSlot,
                ActiveTableCapacity = activeTables.Sum(table => table.Capacity),
                Reservations = reservations,
                ActiveTables = activeTables,
                StaffShifts = shifts,
                Events = events,
                SlotLoads = slotLoads,
                Warnings = warnings
            });
        }

        private static List<AdminOperationalWarning> BuildTonightWarnings(
            List<Rebel.Domain.Entities.Reservation> reservations,
            List<Rebel.Domain.Entities.StaffShift> shifts,
            List<Rebel.Domain.Entities.PubTable> activeTables,
            Dictionary<TimeSpan, int> slotLoads)
        {
            var warnings = new List<AdminOperationalWarning>();
            var pendingCount = reservations.Count(reservation =>
                reservation.Status == ReservationStatus.Pending);
            var withoutTableCount = reservations.Count(reservation =>
                reservation.Status == ReservationStatus.Approved &&
                string.IsNullOrWhiteSpace(reservation.TableLabel));

            if (pendingCount > 0)
            {
                warnings.Add(new AdminOperationalWarning
                {
                    Title = $"{pendingCount} booking request{(pendingCount == 1 ? "" : "s")} need a decision.",
                    Detail = "Review these before service so guests are not left waiting.",
                    Controller = "AdminReservations",
                    Action = "Index",
                    RouteValues = new Dictionary<string, string?>
                    {
                        ["status"] = ReservationStatus.Pending.ToString()
                    }
                });
            }

            if (withoutTableCount > 0)
            {
                warnings.Add(new AdminOperationalWarning
                {
                    Title = $"{withoutTableCount} approved booking{(withoutTableCount == 1 ? " has" : "s have")} no table.",
                    Detail = "Assign a table before the guests arrive.",
                    Controller = "AdminTables",
                    Action = "Index"
                });
            }

            foreach (var role in new[] { StaffRole.Front, StaffRole.Kitchen })
            {
                if (shifts.All(shift => shift.Role != role))
                {
                    warnings.Add(new AdminOperationalWarning
                    {
                        Title = $"Tonight has no {role.ToString().ToLowerInvariant()} cover.",
                        Detail = "Add someone to the rota before service starts.",
                        Tone = "danger",
                        Controller = "AdminStaffSchedule",
                        Action = "Index"
                    });
                }
            }

            var busiestSlot = slotLoads
                .OrderByDescending(slot => slot.Value)
                .FirstOrDefault();
            var tableCapacity = activeTables.Sum(table => table.Capacity);

            if (busiestSlot.Value > tableCapacity && tableCapacity > 0)
            {
                warnings.Add(new AdminOperationalWarning
                {
                    Title = $"{busiestSlot.Key:hh\\:mm} has {busiestSlot.Value} guests for {tableCapacity} active seats.",
                    Detail = "Check table turnover and large parties before accepting more guests.",
                    Tone = "danger",
                    Controller = "AdminReservations",
                    Action = "Planner"
                });
            }

            return warnings;
        }

        // DETAILS
        [HttpGet]
        public async Task<IActionResult> Details(
            Guid id,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Event)
                .FirstOrDefaultAsync(
                    r => r.Id == id,
                    cancellationToken);

            if (reservation == null)
            {
                return NotFound();
            }

            await LoadActiveTables(
                reservation,
                cancellationToken);

            ViewBag.ReservationActivities =
                await _context.ReservationActivities
                    .AsNoTracking()
                    .Where(activity =>
                        activity.ReservationId == reservation.Id)
                    .OrderByDescending(activity =>
                        activity.CreatedAtUtc)
                    .ToListAsync(cancellationToken);

            return View(reservation);
        }

        // APPROVE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            Guid id,
            string? adminNote,
            string? tableLabel,
            string? internalNote,
            string? returnUrl,
            CancellationToken cancellationToken)
        {
            return await UpdateReservationStatus(
                id,
                ReservationStatus.Approved,
                adminNote,
                tableLabel,
                internalNote,
                returnUrl,
                cancellationToken);
        }

        // REJECT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(
            Guid id,
            string? adminNote,
            string? returnUrl,
            CancellationToken cancellationToken)
        {
            return await UpdateReservationStatus(
                id,
                ReservationStatus.Rejected,
                adminNote,
                null,
                null,
                returnUrl,
                cancellationToken);
        }

        // CANCEL
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(
            Guid id,
            string? adminNote,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Event)
                .FirstOrDefaultAsync(
                    r => r.Id == id,
                    cancellationToken);

            if (reservation == null)
            {
                return NotFound();
            }

            if (reservation.Status != ReservationStatus.Pending &&
                reservation.Status != ReservationStatus.Approved)
            {
                TempData["ErrorMessage"] =
                    "Only pending or approved reservations can be cancelled.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            reservation.Status = ReservationStatus.Cancelled;
            reservation.RespondedAtUtc = DateTime.UtcNow;
            reservation.AdminNote =
                NormalizeOptionalText(adminNote, 500);
            reservation.TableLabel = null;
            reservation.InternalNote =
                NormalizeOptionalText(
                    AppendStaffNote(
                        reservation.InternalNote,
                        "Reservation cancelled by admin."),
                    500);
            AddReservationActivity(
                reservation.Id,
                "Cancelled by admin",
                "Reservation was cancelled and the table was released.",
                "Admin");

            var reservationNotifications =
                await _context.Notifications
                    .Where(n =>
                        n.ReservationId == reservation.Id)
                    .ToListAsync(cancellationToken);

            if (reservationNotifications.Count > 0)
            {
                _context.Notifications.RemoveRange(
                    reservationNotifications);
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(reservation.Email))
            {
                reservation.EmailStatus = "CodeOnly";
                reservation.LastEmailError = null;
                await _context.SaveChangesAsync(cancellationToken);

                TempData["SuccessMessage"] =
                    $"{reservation.FullName}'s reservation has been cancelled.";

                return RedirectToAction(nameof(Index), new
                {
                    status = ReservationStatus.Cancelled
                });
            }

            try
            {
                var htmlBody = ReservationEmailTemplate.BuildCancelled(
                    reservation.FullName,
                    reservation.ReservationDate,
                    reservation.ReservationTime,
                    reservation.NumberOfGuests,
                    reservation.ReservationCode,
                    "cid:rebel-logo",
                    reservation.Event?.Title);

                await _emailService.SendEmailAsync(
                    reservation.Email,
                    "Reservation cancelled | Rebel Rebel by Fat Kitchen",
                    htmlBody,
                    cancellationToken);

                reservation.EmailStatus = "CancelledSent";
                reservation.LastEmailSentAtUtc = DateTime.UtcNow;
                reservation.LastEmailError = null;
                AddReservationActivity(
                    reservation.Id,
                    "Cancellation email sent",
                    $"Cancellation email was sent to {reservation.Email}.",
                    "System");

                await _context.SaveChangesAsync(cancellationToken);

                TempData["SuccessMessage"] =
                    $"{reservation.FullName}'s reservation has been cancelled and email sent.";
            }
            catch (Exception ex)
            {
                reservation.EmailStatus = "CancelledEmailFailed";
                reservation.LastEmailError =
                    NormalizeOptionalText(ex.Message, 500);
                AddReservationActivity(
                    reservation.Id,
                    "Cancellation email failed",
                    $"Cancellation email could not be sent to {reservation.Email}.",
                    "System");

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogError(
                    ex,
                    "Reservation {ReservationId} was cancelled, but the cancellation email could not be sent.",
                    reservation.Id);

                TempData["ErrorMessage"] =
                    $"{reservation.FullName}'s reservation was cancelled, but the email could not be sent.";
            }

            return RedirectToAction(nameof(Index), new
            {
                status = ReservationStatus.Cancelled
            });
        }

        // MARK AS ARRIVED
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkArrived(
            Guid id,
            CancellationToken cancellationToken)
        {
            return await UpdateAttendanceStatus(
                id,
                ReservationStatus.Arrived,
                cancellationToken);
        }

        // MARK AS NO-SHOW
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNoShow(
            Guid id,
            CancellationToken cancellationToken)
        {
            return await UpdateAttendanceStatus(
                id,
                ReservationStatus.NoShow,
                cancellationToken);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendGuestEmail(
            Guid id,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Event)
                .FirstOrDefaultAsync(
                    r => r.Id == id,
                    cancellationToken);

            if (reservation == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(reservation.Email))
            {
                TempData["ErrorMessage"] =
                    "This is a code-based reservation and has no guest email address.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            var email = BuildReservationEmail(reservation);

            if (email == null)
            {
                TempData["ErrorMessage"] =
                    "There is no guest email template for this reservation status yet.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            try
            {
                await _emailService.SendEmailAsync(
                    reservation.Email,
                    email.Value.Subject,
                    email.Value.HtmlBody,
                    cancellationToken);

                reservation.EmailStatus = email.Value.SuccessStatus;
                reservation.LastEmailSentAtUtc = DateTime.UtcNow;
                reservation.LastEmailError = null;
                AddReservationActivity(
                    reservation.Id,
                    "Guest email resent",
                    $"Guest email was resent to {reservation.Email}.",
                    "Admin");

                await _context.SaveChangesAsync(cancellationToken);

                TempData["SuccessMessage"] =
                    "Guest email resent successfully.";
            }
            catch (Exception ex)
            {
                reservation.EmailStatus = email.Value.FailedStatus;
                reservation.LastEmailError =
                    NormalizeOptionalText(ex.Message, 500);
                AddReservationActivity(
                    reservation.Id,
                    "Email resend failed",
                    $"Guest email could not be resent to {reservation.Email}.",
                    "Admin");

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogError(
                    ex,
                    "Reservation {ReservationId} email resend failed.",
                    reservation.Id);

                TempData["ErrorMessage"] =
                    "The guest email could not be resent.";
            }

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        // DELETE
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(
            Guid id,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    r => r.Id == id,
                    cancellationToken);

            if (reservation == null)
            {
                return NotFound();
            }

            var reservationNotifications =
                await _context.Notifications
                    .Where(n =>
                        n.ReservationId == reservation.Id)
                    .ToListAsync(cancellationToken);

            if (reservationNotifications.Count > 0)
            {
                _context.Notifications.RemoveRange(
                    reservationNotifications);
            }

            reservation.IsDeleted = true;
            reservation.DeletedAtUtc = DateTime.UtcNow;
            AddReservationActivity(
                reservation.Id,
                "Archived",
                "Reservation was moved out of the active desk.",
                "Admin");

            await _context.SaveChangesAsync(
                cancellationToken);

            TempData["SuccessMessage"] =
                "The reservation has been archived.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> UpdateReservationStatus(
            Guid id,
            ReservationStatus newStatus,
            string? adminNote,
            string? tableLabel,
            string? internalNote,
            string? returnUrl,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    r => r.Id == id,
                    cancellationToken);

            if (reservation == null)
            {
                return NotFound();
            }

            if (reservation.Status !=
                ReservationStatus.Pending)
            {
                TempData["ErrorMessage"] =
                    "This reservation has already been processed.";

                return RedirectAfterReservationDecision(id, returnUrl);
            }

            var normalizedAdminNote =
                string.IsNullOrWhiteSpace(adminNote)
                    ? null
                    : adminNote.Trim();

            if (normalizedAdminNote?.Length > 500)
            {
                TempData["ErrorMessage"] =
                    "The admin message cannot be longer than 500 characters.";

                return RedirectAfterReservationDecision(id, returnUrl);
            }

            reservation.Status = newStatus;
            reservation.RespondedAtUtc = DateTime.UtcNow;
            reservation.AdminNote = normalizedAdminNote;
            AddReservationActivity(
                reservation.Id,
                newStatus == ReservationStatus.Approved
                    ? "Approved"
                    : "Rejected",
                newStatus == ReservationStatus.Approved
                    ? "Reservation was approved by admin."
                    : "Reservation was rejected by admin.",
                "Admin");

            if (newStatus == ReservationStatus.Approved)
            {
                var tableValidationError =
                    await ValidateTableAssignment(
                        reservation.Id,
                        tableLabel,
                        reservation.NumberOfGuests,
                        reservation.ReservationDate,
                        reservation.ReservationTime,
                        cancellationToken);

                if (!string.IsNullOrWhiteSpace(tableValidationError))
                {
                    TempData["ErrorMessage"] =
                        tableValidationError;

                    return RedirectAfterReservationDecision(id, returnUrl);
                }

                reservation.TableLabel =
                    NormalizeOptionalText(tableLabel, 40);

                reservation.InternalNote =
                    NormalizeOptionalText(internalNote, 500);

                if (!string.IsNullOrWhiteSpace(reservation.TableLabel))
                {
                    AddReservationActivity(
                        reservation.Id,
                        "Table assigned",
                        $"Assigned to table {reservation.TableLabel}.",
                        "Admin");
                }
            }
            else
            {
                reservation.TableLabel = null;
                reservation.InternalNote = null;
            }

            var reservationNotifications =
                await _context.Notifications
                    .Where(n =>
                        n.ReservationId == reservation.Id)
                    .ToListAsync(cancellationToken);

            if (reservationNotifications.Count > 0)
            {
                _context.Notifications.RemoveRange(
                    reservationNotifications);
            }

            try
            {
                await _context.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException ex)
                when (IsUniqueConstraintViolation(ex))
            {
                TempData["ErrorMessage"] =
                    "That table was just assigned to another reservation at the same time. Choose another table.";

                return RedirectAfterReservationDecision(id, returnUrl);
            }

            if (string.IsNullOrWhiteSpace(reservation.Email))
            {
                var isApproved =
                    newStatus == ReservationStatus.Approved;

                reservation.EmailStatus = "CodeOnly";
                reservation.LastEmailError = null;
                await _context.SaveChangesAsync(cancellationToken);

                TempData["SuccessMessage"] = isApproved
                    ? "Reservation approved. The guest can see the update with their code."
                    : "Reservation rejected. The guest can see the update with their code.";

                return RedirectAfterReservationDecision(id, returnUrl);
            }

            try
            {
                var isApproved =
                    newStatus ==
                    ReservationStatus.Approved;

                var subject = isApproved
                    ? "Your table is locked in 🍻 | Rebel Rebel by Fat Kitchen"
                    : "Not this round | Rebel Rebel by Fat Kitchen";

                var htmlBody = isApproved
                    ? ReservationEmailTemplate.BuildApproved(
                        reservation.FullName,
                        reservation.ReservationDate,
                        reservation.ReservationTime,
                        reservation.NumberOfGuests,
                        reservation.ReservationCode,
                        "cid:rebel-logo",
                        reservation.Event?.Title)
                    : ReservationEmailTemplate.BuildDeclined(
                        reservation.FullName,
                        reservation.ReservationDate,
                        reservation.ReservationTime,
                        reservation.NumberOfGuests,
                        reservation.ReservationCode,
                        "cid:rebel-logo",
                        reservation.Event?.Title);

                await _emailService.SendEmailAsync(
                    reservation.Email,
                    subject,
                    htmlBody,
                    cancellationToken);

                reservation.EmailStatus = isApproved
                    ? "ApprovedSent"
                    : "RejectedSent";
                reservation.LastEmailSentAtUtc = DateTime.UtcNow;
                reservation.LastEmailError = null;
                AddReservationActivity(
                    reservation.Id,
                    isApproved
                        ? "Approval email sent"
                        : "Rejection email sent",
                    $"Guest email was sent to {reservation.Email}.",
                    "System");

                await _context.SaveChangesAsync(cancellationToken);

                TempData["SuccessMessage"] = isApproved
                    ? "Reservation approved and confirmation email sent."
                    : "Reservation rejected and notification email sent.";
            }
            catch (Exception ex)
            {
                reservation.EmailStatus =
                    newStatus == ReservationStatus.Approved
                        ? "ApprovedEmailFailed"
                        : "RejectedEmailFailed";
                reservation.LastEmailError =
                    NormalizeOptionalText(ex.Message, 500);
                AddReservationActivity(
                    reservation.Id,
                    newStatus == ReservationStatus.Approved
                        ? "Approval email failed"
                        : "Rejection email failed",
                    $"Guest email could not be sent to {reservation.Email}.",
                    "System");

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogError(
                    ex,
                    "Reservation {ReservationId} was processed, but the email could not be sent.",
                    reservation.Id);

                TempData["ErrorMessage"] =
                    newStatus ==
                    ReservationStatus.Approved
                        ? "Reservation was approved, but the email could not be sent."
                        : "Reservation was rejected, but the email could not be sent.";
            }

            return RedirectAfterReservationDecision(id, returnUrl);
        }

        private IActionResult RedirectAfterReservationDecision(
            Guid reservationId,
            string? returnUrl)
        {
            return !string.IsNullOrWhiteSpace(returnUrl) &&
                   Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(
                    nameof(Details),
                    new { id = reservationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateFloorDetails(
            Guid id,
            string? tableLabel,
            string? internalNote,
            string? returnUrl,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    r => r.Id == id,
                    cancellationToken);

            if (reservation == null)
            {
                return NotFound();
            }

            if (reservation.Status == ReservationStatus.Rejected ||
                reservation.Status == ReservationStatus.NoShow ||
                reservation.Status == ReservationStatus.Cancelled)
            {
                TempData["ErrorMessage"] =
                    "Rejected, no-show and cancelled reservations cannot be assigned to a table.";

                return RedirectAfterFloorUpdate(id, returnUrl);
            }

            var tableValidationError =
                await ValidateTableAssignment(
                    reservation.Id,
                    tableLabel,
                    reservation.NumberOfGuests,
                    reservation.ReservationDate,
                    reservation.ReservationTime,
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(tableValidationError))
            {
                TempData["ErrorMessage"] =
                    tableValidationError;

                return RedirectAfterFloorUpdate(id, returnUrl);
            }

            reservation.TableLabel =
                NormalizeOptionalText(tableLabel, 40);

            reservation.InternalNote =
                NormalizeOptionalText(internalNote, 500);
            AddReservationActivity(
                reservation.Id,
                "Floor details updated",
                string.IsNullOrWhiteSpace(reservation.TableLabel)
                    ? "Table assignment was cleared."
                    : $"Table assignment set to {reservation.TableLabel}.",
                "Admin");

            try
            {
                await _context.SaveChangesAsync(
                    cancellationToken);
            }
            catch (DbUpdateException ex)
                when (IsUniqueConstraintViolation(ex))
            {
                TempData["ErrorMessage"] =
                    "That table was just assigned to another reservation at the same time. Choose another table.";

                return RedirectAfterFloorUpdate(id, returnUrl);
            }

            TempData["SuccessMessage"] =
                string.IsNullOrWhiteSpace(reservation.TableLabel)
                    ? $"{reservation.FullName} is now unassigned from the floor."
                    : $"{reservation.FullName} is assigned to table {reservation.TableLabel}.";

            return RedirectAfterFloorUpdate(id, returnUrl);
        }

        private IActionResult RedirectAfterFloorUpdate(
            Guid reservationId,
            string? returnUrl)
        {
            return !string.IsNullOrWhiteSpace(returnUrl) &&
                   Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(
                    nameof(Details),
                    new { id = reservationId });
        }

        private async Task LoadActiveTables(
            Rebel.Domain.Entities.Reservation reservation,
            CancellationToken cancellationToken)
        {
            ViewBag.ActiveTables = await _context.PubTables
                .AsNoTracking()
                .Where(table => table.IsActive)
                .OrderBy(table => table.Area)
                .ThenBy(table => table.Label)
                .ToListAsync(cancellationToken);

            ViewBag.BusyTables = await _context.Reservations
                .AsNoTracking()
                .Where(existingReservation =>
                    existingReservation.Id != reservation.Id &&
                    existingReservation.ReservationDate.Date ==
                        reservation.ReservationDate.Date &&
                    existingReservation.ReservationTime ==
                        reservation.ReservationTime &&
                    !string.IsNullOrWhiteSpace(
                        existingReservation.TableLabel) &&
                    (existingReservation.Status ==
                        ReservationStatus.Approved ||
                     existingReservation.Status ==
                        ReservationStatus.Arrived))
                .Select(existingReservation =>
                    existingReservation.TableLabel!)
                .ToListAsync(cancellationToken);
        }

        private async Task<string?> ValidateTableAssignment(
            Guid reservationId,
            string? tableLabel,
            int guestCount,
            DateTime reservationDate,
            TimeSpan reservationTime,
            CancellationToken cancellationToken)
        {
            var normalizedTableLabel =
                NormalizeOptionalText(tableLabel, 40);

            if (string.IsNullOrWhiteSpace(normalizedTableLabel))
            {
                return null;
            }

            var table = await _context.PubTables
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    activeTable =>
                        activeTable.IsActive &&
                        activeTable.Label == normalizedTableLabel,
                    cancellationToken);

            if (table == null)
            {
                return "Please choose an active configured table.";
            }

            if (guestCount > table.Capacity)
            {
                return
                    $"{table.Label} seats {table.Capacity}, but this reservation is for {guestCount} guests.";
            }

            var existingReservation = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.Id != reservationId &&
                    reservation.ReservationDate.Date ==
                        reservationDate.Date &&
                    reservation.ReservationTime ==
                        reservationTime &&
                    reservation.TableLabel == normalizedTableLabel &&
                    (reservation.Status == ReservationStatus.Approved ||
                     reservation.Status == ReservationStatus.Arrived))
                .OrderBy(reservation => reservation.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingReservation != null)
            {
                return
                    $"{table.Label} is already assigned to {existingReservation.FullName} at {reservationTime:hh\\:mm}. Choose another table or time.";
            }

            return null;
        }

        private static string? NormalizeOptionalText(
            string? value,
            int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Trim();

            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }

        private static string AppendStaffNote(
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

        private static ReservationEmail? BuildReservationEmail(
            Rebel.Domain.Entities.Reservation reservation)
        {
            const string logoUrl = "cid:rebel-logo";

            return reservation.Status switch
            {
                ReservationStatus.Pending => new ReservationEmail(
                    "We got your reservation request | Rebel Rebel by Fat Kitchen",
                    ReservationEmailTemplate.BuildReceived(
                        reservation.FullName,
                        reservation.ReservationDate,
                        reservation.ReservationTime,
                        reservation.NumberOfGuests,
                        reservation.ReservationCode,
                        logoUrl,
                        reservation.Event?.Title),
                    "RequestSent",
                    "RequestEmailFailed"),

                ReservationStatus.Approved => new ReservationEmail(
                    "Your table is locked in | Rebel Rebel by Fat Kitchen",
                    ReservationEmailTemplate.BuildApproved(
                        reservation.FullName,
                        reservation.ReservationDate,
                        reservation.ReservationTime,
                        reservation.NumberOfGuests,
                        reservation.ReservationCode,
                        logoUrl,
                        reservation.Event?.Title),
                    "ApprovedSent",
                    "ApprovedEmailFailed"),

                ReservationStatus.Rejected => new ReservationEmail(
                    "Not this round | Rebel Rebel by Fat Kitchen",
                    ReservationEmailTemplate.BuildDeclined(
                        reservation.FullName,
                        reservation.ReservationDate,
                        reservation.ReservationTime,
                        reservation.NumberOfGuests,
                        reservation.ReservationCode,
                        logoUrl,
                        reservation.Event?.Title),
                    "RejectedSent",
                    "RejectedEmailFailed"),

                ReservationStatus.Cancelled => new ReservationEmail(
                    "Reservation cancelled | Rebel Rebel by Fat Kitchen",
                    ReservationEmailTemplate.BuildCancelled(
                        reservation.FullName,
                        reservation.ReservationDate,
                        reservation.ReservationTime,
                        reservation.NumberOfGuests,
                        reservation.ReservationCode,
                        logoUrl,
                        reservation.Event?.Title),
                    "CancelledSent",
                    "CancelledEmailFailed"),

                _ => null
            };
        }

        private static bool IsUniqueConstraintViolation(
            DbUpdateException exception)
        {
            return exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            };
        }

        private readonly record struct ReservationEmail(
            string Subject,
            string HtmlBody,
            string SuccessStatus,
            string FailedStatus);

        private async Task<IActionResult> UpdateAttendanceStatus(
            Guid id,
            ReservationStatus newStatus,
            CancellationToken cancellationToken)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    r => r.Id == id,
                    cancellationToken);

            if (reservation == null)
            {
                return NotFound();
            }

            if (reservation.Status ==
                    ReservationStatus.Pending ||
                reservation.Status ==
                    ReservationStatus.Rejected ||
                reservation.Status ==
                    ReservationStatus.Cancelled)
            {
                TempData["ErrorMessage"] =
                    "Only approved reservations can receive an attendance status.";

                return RedirectToAction(nameof(Tonight));
            }

            if (reservation.Status == ReservationStatus.Arrived)
            {
                TempData["ErrorMessage"] =
                    "This reservation has already arrived and cannot be changed.";

                return RedirectToAction(nameof(Tonight));
            }

            if (reservation.Status == ReservationStatus.NoShow ||
                reservation.Status == ReservationStatus.Cancelled)
            {
                TempData["ErrorMessage"] =
                    "This reservation is already closed and cannot be changed from tonight view.";

                return RedirectToAction(nameof(Tonight));
            }

            if (reservation.Status != ReservationStatus.Approved)
            {
                TempData["ErrorMessage"] =
                    "Only approved reservations can be marked as arrived or no-show.";

                return RedirectToAction(nameof(Tonight));
            }

            reservation.Status = newStatus;

            if (newStatus == ReservationStatus.NoShow)
            {
                reservation.TableLabel = null;
                reservation.InternalNote = null;
            }

            AddReservationActivity(
                reservation.Id,
                newStatus == ReservationStatus.Arrived
                    ? "Guest arrived"
                    : "Marked no-show",
                newStatus == ReservationStatus.Arrived
                    ? "Reservation was checked in for service."
                    : "Reservation was closed as a no-show and the table was released.",
                "Admin");

            await _context.SaveChangesAsync(
                cancellationToken);

            TempData["SuccessMessage"] =
                newStatus == ReservationStatus.Arrived
                    ? $"{reservation.FullName} marked as arrived."
                    : $"{reservation.FullName} marked as no-show.";

            return RedirectToAction(nameof(Tonight));
        }

        private void AddReservationActivity(
            Guid reservationId,
            string title,
            string description,
            string actor)
        {
            var resolvedActor = string.Equals(
                    actor,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase)
                ? User.FindFirst(AdminRoles.DisplayNameClaim)?.Value ??
                  User.Identity?.Name ??
                  "Staff"
                : actor;

            _context.ReservationActivities.Add(
                new Rebel.Domain.Entities.ReservationActivity
                {
                    ReservationId = reservationId,
                    Title = title,
                    Description = NormalizeRequiredText(
                        description,
                        500),
                    Actor = NormalizeRequiredText(
                        resolvedActor,
                        30),
                    CreatedAtUtc = DateTime.UtcNow
                });
        }

        private static string NormalizeRequiredText(
            string value,
            int maxLength)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? "System"
                : value.Trim();

            return normalized.Length <= maxLength
                ? normalized
                : normalized[..maxLength];
        }
    }
}
