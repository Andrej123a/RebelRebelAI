using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Models;
using Rebel.Web.Authorization;

namespace Rebel.Web.Controllers
{
    [Authorize(Policy = AdminPolicies.Backstage)]
    public class AdminStaffScheduleController : Controller
    {
        private readonly AppDbContext _context;

        public AdminStaffScheduleController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            DateTime? startDate,
            Guid? staffMemberId,
            CancellationToken cancellationToken)
        {
            if (!IsManager())
            {
                return RedirectToAction(
                    nameof(MyShifts),
                    new { startDate });
            }

            var weekStart = GetWeekStart(startDate ?? DateTime.Today);
            var weekEnd = weekStart.AddDays(6);
            var eventWeekStart =
                DateTime.SpecifyKind(weekStart, DateTimeKind.Utc);
            var eventWeekEndExclusive =
                DateTime.SpecifyKind(weekEnd.AddDays(1), DateTimeKind.Utc);
            var eventStats = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.EventId.HasValue &&
                    reservation.ReservationDate >= weekStart &&
                    reservation.ReservationDate <= weekEnd &&
                    reservation.Status != ReservationStatus.Cancelled &&
                    reservation.Status != ReservationStatus.NoShow &&
                    reservation.Status != ReservationStatus.Rejected)
                .GroupBy(reservation => reservation.EventId!.Value)
                .Select(group => new
                {
                    EventId = group.Key,
                    ReservationCount = group.Count(),
                    GuestCount = group.Sum(reservation =>
                        reservation.NumberOfGuests)
                })
                .ToDictionaryAsync(
                    row => row.EventId,
                    row => new
                    {
                        row.ReservationCount,
                        row.GuestCount
                    },
                    cancellationToken);

            var model = new AdminStaffScheduleViewModel
            {
                StartDate = weekStart,
                EndDate = weekEnd,
                PreviousWeek = weekStart.AddDays(-7),
                NextWeek = weekStart.AddDays(7),
                NewShift = new StaffShiftInputModel
                {
                    StaffMemberId = staffMemberId ?? Guid.Empty,
                    ShiftDate = DateTime.Today.Date >= weekStart &&
                                DateTime.Today.Date <= weekEnd
                        ? DateTime.Today.Date
                        : weekStart,
                    StartsAt = new TimeSpan(10, 0, 0),
                    EndsAt = new TimeSpan(16, 0, 0)
                },
                StaffMembers = await _context.StaffMembers
                    .AsNoTracking()
                    .OrderByDescending(staff => staff.IsActive)
                    .ThenBy(staff => staff.Role)
                    .ThenBy(staff => staff.FullName)
                    .ToListAsync(cancellationToken),
                Shifts = await _context.StaffShifts
                    .AsNoTracking()
                    .Include(shift => shift.StaffMember)
                    .Where(shift =>
                        shift.ShiftDate >= weekStart &&
                        shift.ShiftDate <= weekEnd)
                    .OrderBy(shift => shift.ShiftDate)
                    .ThenBy(shift => shift.StartsAt)
                    .ThenBy(shift => shift.Role)
                    .ToListAsync(cancellationToken)
            };

            model.Events = await _context.Events
                .AsNoTracking()
                .Where(ev =>
                    ev.Date >= eventWeekStart &&
                    ev.Date < eventWeekEndExclusive &&
                    ev.IsActive)
                .OrderBy(ev => ev.Date)
                .ThenBy(ev => ev.StartTime)
                .Select(ev => new StaffScheduleEventViewModel
                {
                    Id = ev.Id,
                    Title = ev.Title,
                    Date = ev.Date,
                    StartTime = ev.StartTime,
                    ImageUrl = ev.ImageUrl
                })
                .ToListAsync(cancellationToken);

            foreach (var weekEvent in model.Events)
            {
                if (!eventStats.TryGetValue(weekEvent.Id, out var stats))
                {
                    continue;
                }

                weekEvent.ReservationCount = stats.ReservationCount;
                weekEvent.GuestCount = stats.GuestCount;
            }

            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> StaffMember(
            Guid id,
            DateTime? startDate,
            CancellationToken cancellationToken)
        {
            var weekStart = GetWeekStart(startDate ?? DateTime.Today);
            var weekEnd = weekStart.AddDays(6);

            var staffMember = await _context.StaffMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    staff => staff.Id == id,
                    cancellationToken);

            if (staffMember == null)
            {
                return NotFound();
            }

            var model = new AdminStaffMemberScheduleViewModel
            {
                StaffMember = staffMember,
                StartDate = weekStart,
                EndDate = weekEnd,
                PreviousWeek = weekStart.AddDays(-7),
                NextWeek = weekStart.AddDays(7),
                Shifts = await _context.StaffShifts
                    .AsNoTracking()
                    .Where(shift =>
                        shift.StaffMemberId == id &&
                        shift.ShiftDate >= weekStart &&
                        shift.ShiftDate <= weekEnd)
                    .OrderBy(shift => shift.ShiftDate)
                    .ThenBy(shift => shift.StartsAt)
                    .ToListAsync(cancellationToken)
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MyShifts(
            DateTime? startDate,
            CancellationToken cancellationToken)
        {
            var weekStart = GetWeekStart(startDate ?? DateTime.Today);
            var weekEnd = weekStart.AddDays(6);
            var model = new AdminMyShiftsViewModel
            {
                StartDate = weekStart,
                EndDate = weekEnd,
                PreviousWeek = weekStart.AddDays(-7),
                NextWeek = weekStart.AddDays(7)
            };
            var claimValue = User.FindFirst(AdminRoles.StaffMemberIdClaim)?.Value;

            if (!Guid.TryParse(claimValue, out var staffMemberId))
            {
                return View(model);
            }

            model.StaffMember = await _context.StaffMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    staff => staff.Id == staffMemberId,
                    cancellationToken);

            if (model.StaffMember == null)
            {
                return View(model);
            }

            model.Shifts = await _context.StaffShifts
                .AsNoTracking()
                .Where(shift =>
                    shift.StaffMemberId == staffMemberId &&
                    shift.ShiftDate >= weekStart &&
                    shift.ShiftDate <= weekEnd)
                .OrderBy(shift => shift.ShiftDate)
                .ThenBy(shift => shift.StartsAt)
                .ToListAsync(cancellationToken);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> CreateStaff(
            StaffMemberInputModel input,
            DateTime startDate,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Staff member could not be added. Check the name and section.";

                return RedirectToAction(
                    nameof(Index),
                    new { startDate = startDate.ToString("yyyy-MM-dd") });
            }

            var fullName = input.FullName.Trim();

            var exists = await _context.StaffMembers
                .AnyAsync(
                    staff => staff.FullName.ToUpper() ==
                             fullName.ToUpper(),
                    cancellationToken);

            if (exists)
            {
                TempData["ErrorMessage"] =
                    $"{fullName} already exists in the staff list.";

                return RedirectToAction(
                    nameof(Index),
                    new { startDate = startDate.ToString("yyyy-MM-dd") });
            }

            _context.StaffMembers.Add(new StaffMember
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Role = input.Role,
                PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber)
                    ? null
                    : input.PhoneNumber.Trim(),
                IsActive = true
            });

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{fullName} was added to the staff rota.";

            return RedirectToAction(
                nameof(Index),
                new { startDate = startDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> ToggleStaff(
            Guid id,
            DateTime startDate,
            CancellationToken cancellationToken)
        {
            var staffMember = await _context.StaffMembers
                .FirstOrDefaultAsync(
                    staff => staff.Id == id,
                    cancellationToken);

            if (staffMember == null)
            {
                return NotFound();
            }

            staffMember.IsActive = !staffMember.IsActive;
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = staffMember.IsActive
                ? $"{staffMember.FullName} is active again."
                : $"{staffMember.FullName} is hidden from new shifts.";

            return RedirectToAction(
                nameof(Index),
                new { startDate = startDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> CreateShift(
            StaffShiftInputModel input,
            DateTime startDate,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid ||
                input.EndsAt == input.StartsAt)
            {
                TempData["ErrorMessage"] =
                    "Shift could not be added. Start and end time cannot be the same.";

                return RedirectToAction(
                    nameof(Index),
                    new { startDate = startDate.ToString("yyyy-MM-dd") });
            }

            var staffMember = await _context.StaffMembers
                .FirstOrDefaultAsync(
                    staff =>
                        staff.Id == input.StaffMemberId &&
                        staff.IsActive,
                    cancellationToken);

            if (staffMember == null)
            {
                TempData["ErrorMessage"] =
                    "Choose an active staff member before adding a shift.";

                return RedirectToAction(
                    nameof(Index),
                    new { startDate = startDate.ToString("yyyy-MM-dd") });
            }

            var shiftDate = input.ShiftDate.Date;
            var newShiftStart =
                ToShiftMinute(input.StartsAt);
            var newShiftEnd =
                ToShiftMinute(input.EndsAt);

            if (newShiftEnd <= newShiftStart)
            {
                newShiftEnd += 24 * 60;
            }

            var nearbyShifts = await _context.StaffShifts
                .Where(
                    shift =>
                        shift.StaffMemberId == input.StaffMemberId &&
                        shift.ShiftDate >= shiftDate.AddDays(-1) &&
                        shift.ShiftDate <= shiftDate.AddDays(1))
                .ToListAsync(
                    cancellationToken);

            var overlaps = nearbyShifts.Any(shift =>
            {
                var dayOffset =
                    (int)(shift.ShiftDate.Date - shiftDate).TotalDays *
                    24 *
                    60;

                var existingStart =
                    dayOffset + ToShiftMinute(shift.StartsAt);
                var existingEnd =
                    dayOffset + ToShiftMinute(shift.EndsAt);

                if (existingEnd <= existingStart)
                {
                    existingEnd += 24 * 60;
                }

                return newShiftStart < existingEnd &&
                       newShiftEnd > existingStart;
            });

            if (overlaps)
            {
                TempData["ErrorMessage"] =
                    $"{staffMember.FullName} already has a shift during that time.";

                return RedirectToAction(
                    nameof(Index),
                    new { startDate = startDate.ToString("yyyy-MM-dd") });
            }

            _context.StaffShifts.Add(new StaffShift
            {
                Id = Guid.NewGuid(),
                StaffMemberId = staffMember.Id,
                Role = staffMember.Role,
                ShiftDate = shiftDate,
                StartsAt = input.StartsAt,
                EndsAt = input.EndsAt,
                Note = string.IsNullOrWhiteSpace(input.Note)
                    ? null
                    : input.Note.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{staffMember.FullName} was scheduled for {shiftDate:dd MMM}.";

            return RedirectToAction(
                nameof(Index),
                new { startDate = startDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> DeleteShift(
            Guid id,
            DateTime startDate,
            CancellationToken cancellationToken)
        {
            var shift = await _context.StaffShifts
                .Include(existingShift => existingShift.StaffMember)
                .FirstOrDefaultAsync(
                    existingShift => existingShift.Id == id,
                    cancellationToken);

            if (shift == null)
            {
                return NotFound();
            }

            var staffName =
                shift.StaffMember?.FullName ?? "Shift";

            _context.StaffShifts.Remove(shift);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{staffName}'s shift was removed.";

            return RedirectToAction(
                nameof(Index),
                new { startDate = startDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> EditShift(
            Guid id,
            StaffShiftInputModel input,
            DateTime startDate,
            CancellationToken cancellationToken)
        {
            var shift = await _context.StaffShifts
                .FirstOrDefaultAsync(
                    existingShift => existingShift.Id == id,
                    cancellationToken);

            if (shift == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid || input.StartsAt == input.EndsAt)
            {
                TempData["ErrorMessage"] =
                    "Shift could not be updated. Check the person, date and times.";

                return RedirectToAction(
                    nameof(Index),
                    new { startDate = startDate.ToString("yyyy-MM-dd") });
            }

            var staffMember = await _context.StaffMembers
                .FirstOrDefaultAsync(
                    staff => staff.Id == input.StaffMemberId && staff.IsActive,
                    cancellationToken);

            if (staffMember == null)
            {
                TempData["ErrorMessage"] = "Choose an active staff member.";

                return RedirectToAction(
                    nameof(Index),
                    new { startDate = startDate.ToString("yyyy-MM-dd") });
            }

            var shiftDate = input.ShiftDate.Date;
            var newShiftStart = ToShiftMinute(input.StartsAt);
            var newShiftEnd = ToShiftMinute(input.EndsAt);

            if (newShiftEnd <= newShiftStart)
            {
                newShiftEnd += 24 * 60;
            }

            var nearbyShifts = await _context.StaffShifts
                .AsNoTracking()
                .Where(existingShift =>
                    existingShift.Id != id &&
                    existingShift.StaffMemberId == input.StaffMemberId &&
                    existingShift.ShiftDate >= shiftDate.AddDays(-1) &&
                    existingShift.ShiftDate <= shiftDate.AddDays(1))
                .ToListAsync(cancellationToken);

            if (HasOverlap(nearbyShifts, shiftDate, newShiftStart, newShiftEnd))
            {
                TempData["ErrorMessage"] =
                    $"{staffMember.FullName} already has a shift during that time.";

                return RedirectToAction(
                    nameof(Index),
                    new { startDate = startDate.ToString("yyyy-MM-dd") });
            }

            shift.StaffMemberId = staffMember.Id;
            shift.Role = staffMember.Role;
            shift.ShiftDate = shiftDate;
            shift.StartsAt = input.StartsAt;
            shift.EndsAt = input.EndsAt;
            shift.Note = string.IsNullOrWhiteSpace(input.Note)
                ? null
                : input.Note.Trim();

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{staffMember.FullName}'s shift was updated.";

            return RedirectToAction(
                nameof(Index),
                new { startDate = startDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> CopyPreviousWeek(
            DateTime startDate,
            CancellationToken cancellationToken)
        {
            var weekStart = GetWeekStart(startDate);
            var weekEnd = weekStart.AddDays(6);
            var sourceStart = weekStart.AddDays(-7);
            var sourceEnd = sourceStart.AddDays(6);

            var sourceShifts = await _context.StaffShifts
                .AsNoTracking()
                .Where(shift =>
                    shift.ShiftDate >= sourceStart &&
                    shift.ShiftDate <= sourceEnd &&
                    shift.StaffMember != null &&
                    shift.StaffMember.IsActive)
                .ToListAsync(cancellationToken);

            if (sourceShifts.Count == 0)
            {
                TempData["ErrorMessage"] = "Last week has no shifts to copy.";

                return RedirectToAction(
                    nameof(Index),
                    new { startDate = weekStart.ToString("yyyy-MM-dd") });
            }

            var existingShifts = await _context.StaffShifts
                .AsNoTracking()
                .Where(shift =>
                    shift.ShiftDate >= weekStart &&
                    shift.ShiftDate <= weekEnd)
                .ToListAsync(cancellationToken);

            var copied = 0;

            foreach (var source in sourceShifts)
            {
                var targetDate = source.ShiftDate.AddDays(7);
                var alreadyExists = existingShifts.Any(existing =>
                    existing.StaffMemberId == source.StaffMemberId &&
                    existing.ShiftDate == targetDate &&
                    existing.StartsAt == source.StartsAt &&
                    existing.EndsAt == source.EndsAt);

                if (alreadyExists)
                {
                    continue;
                }

                _context.StaffShifts.Add(new StaffShift
                {
                    Id = Guid.NewGuid(),
                    StaffMemberId = source.StaffMemberId,
                    Role = source.Role,
                    ShiftDate = targetDate,
                    StartsAt = source.StartsAt,
                    EndsAt = source.EndsAt,
                    Note = source.Note,
                    CreatedAtUtc = DateTime.UtcNow
                });
                copied++;
            }

            await _context.SaveChangesAsync(cancellationToken);

            TempData[copied > 0 ? "SuccessMessage" : "ErrorMessage"] =
                copied > 0
                    ? $"{copied} shift{(copied == 1 ? "" : "s")} copied from last week."
                    : "This week already contains all shifts from last week.";

            return RedirectToAction(
                nameof(Index),
                new { startDate = weekStart.ToString("yyyy-MM-dd") });
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            var offset = ((int)date.DayOfWeek + 6) % 7;

            return date.Date.AddDays(-offset);
        }

        private bool IsManager()
        {
            return User.IsInRole(AdminRoles.Manager) ||
                   User.IsInRole(AdminRoles.LegacyAdmin);
        }

        private static int ToShiftMinute(TimeSpan time)
        {
            return (int)Math.Round(time.TotalMinutes);
        }

        private static bool HasOverlap(
            IEnumerable<StaffShift> shifts,
            DateTime shiftDate,
            int newShiftStart,
            int newShiftEnd)
        {
            return shifts.Any(shift =>
            {
                var dayOffset =
                    (int)(shift.ShiftDate.Date - shiftDate).TotalDays * 24 * 60;
                var existingStart = dayOffset + ToShiftMinute(shift.StartsAt);
                var existingEnd = dayOffset + ToShiftMinute(shift.EndsAt);

                if (existingEnd <= existingStart)
                {
                    existingEnd += 24 * 60;
                }

                return newShiftStart < existingEnd &&
                       newShiftEnd > existingStart;
            });
        }
    }
}
