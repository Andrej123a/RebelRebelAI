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
    public class AdminTablesController : Controller
    {
        private static readonly TimeZoneInfo SkopjeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Skopje");

        private readonly AppDbContext _context;

        public AdminTablesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            CancellationToken cancellationToken)
        {
            var nowInSkopje = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                SkopjeTimeZone);

            var today = nowInSkopje.Date;

            var tables = await _context.PubTables
                .AsNoTracking()
                .OrderBy(table => table.Area)
                .ThenBy(table => table.Label)
                .ToListAsync(cancellationToken);

            var operationalReservations = await _context.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.TableLabel != null &&
                    reservation.ReservationDate >= today &&
                    (reservation.Status == ReservationStatus.Approved ||
                     reservation.Status == ReservationStatus.Arrived))
                .OrderBy(reservation => reservation.ReservationDate)
                .ThenBy(reservation => reservation.ReservationTime)
                .ToListAsync(cancellationToken);

            var reservationsByTable = operationalReservations
                .GroupBy(
                    reservation => reservation.TableLabel!.Trim(),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList(),
                    StringComparer.OrdinalIgnoreCase);

            var model = new AdminFloorPlanViewModel
            {
                CurrentLocalDate = today,
                UnassignedReservations = await _context.Reservations
                    .AsNoTracking()
                    .Where(reservation =>
                        reservation.ReservationDate == today &&
                        reservation.Status == ReservationStatus.Approved &&
                        (reservation.TableLabel == null ||
                         reservation.TableLabel == string.Empty))
                    .OrderBy(reservation => reservation.ReservationTime)
                    .ThenBy(reservation => reservation.FullName)
                    .ToListAsync(cancellationToken),
                Tables = tables
                    .Select(table =>
                    {
                        reservationsByTable.TryGetValue(
                            table.Label,
                            out var tableReservations);

                        tableReservations ??= new List<Reservation>();

                        var arrivedReservation = tableReservations
                            .Where(reservation =>
                                reservation.Status ==
                                    ReservationStatus.Arrived &&
                                reservation.ReservationDate == today)
                            .OrderByDescending(reservation =>
                                reservation.ReservationTime)
                            .FirstOrDefault();

                        var nextReservation = tableReservations
                            .Where(reservation =>
                                reservation.Status ==
                                    ReservationStatus.Approved &&
                                (reservation.ReservationDate > today ||
                                 (reservation.ReservationDate == today &&
                                  reservation.ReservationTime >=
                                      nowInSkopje.TimeOfDay)))
                            .OrderBy(reservation =>
                                reservation.ReservationDate)
                            .ThenBy(reservation =>
                                reservation.ReservationTime)
                            .FirstOrDefault();

                        return new AdminFloorTableViewModel
                        {
                            Table = table,
                            OperationalReservation =
                                arrivedReservation ?? nextReservation,
                            IsOccupied = arrivedReservation != null
                        };
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public IActionResult Create()
        {
            return View(new PubTable
            {
                Capacity = 4,
                IsActive = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Create(
            PubTable table,
            CancellationToken cancellationToken)
        {
            Normalize(table);

            if (!ModelState.IsValid)
            {
                return View(table);
            }

            var labelExists = await _context.PubTables
                .AnyAsync(
                    existingTable =>
                        existingTable.Label.ToUpper() == table.Label,
                    cancellationToken);

            if (labelExists)
            {
                ModelState.AddModelError(
                    nameof(table.Label),
                    "A table with this label already exists.");

                return View(table);
            }

            table.Id = Guid.NewGuid();

            _context.PubTables.Add(table);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{table.Label} was added to the floor plan.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Edit(
            Guid id,
            CancellationToken cancellationToken)
        {
            var table = await _context.PubTables
                .FirstOrDefaultAsync(
                    existingTable => existingTable.Id == id,
                    cancellationToken);

            if (table == null)
            {
                return NotFound();
            }

            return View(table);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Edit(
            Guid id,
            PubTable table,
            CancellationToken cancellationToken)
        {
            if (id != table.Id)
            {
                return NotFound();
            }

            Normalize(table);

            if (!ModelState.IsValid)
            {
                return View(table);
            }

            var labelExists = await _context.PubTables
                .AnyAsync(
                    existingTable =>
                        existingTable.Id != table.Id &&
                        existingTable.Label.ToUpper() == table.Label,
                    cancellationToken);

            if (labelExists)
            {
                ModelState.AddModelError(
                    nameof(table.Label),
                    "A table with this label already exists.");

                return View(table);
            }

            var existingTable = await _context.PubTables
                .FirstOrDefaultAsync(
                    currentTable => currentTable.Id == id,
                    cancellationToken);

            if (existingTable == null)
            {
                return NotFound();
            }

            if (!string.Equals(
                    existingTable.Label,
                    table.Label,
                    StringComparison.OrdinalIgnoreCase))
            {
                var today = DateTime.UtcNow.Date;

                var hasOpenAssignments = await _context.Reservations
                    .AsNoTracking()
                    .AnyAsync(
                        reservation =>
                            reservation.TableLabel == existingTable.Label &&
                            reservation.ReservationDate.Date >= today &&
                            (reservation.Status == ReservationStatus.Approved ||
                             reservation.Status == ReservationStatus.Arrived),
                        cancellationToken);

                if (hasOpenAssignments)
                {
                    ModelState.AddModelError(
                        nameof(table.Label),
                        "This table has active reservations. Reassign them before changing the label.");

                    return View(table);
                }
            }

            existingTable.Label = table.Label;
            existingTable.Area = table.Area;
            existingTable.Capacity = table.Capacity;
            existingTable.IsActive = table.IsActive;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{existingTable.Label} was updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> ToggleActive(
            Guid id,
            CancellationToken cancellationToken)
        {
            var table = await _context.PubTables
                .FirstOrDefaultAsync(
                    existingTable => existingTable.Id == id,
                    cancellationToken);

            if (table == null)
            {
                return NotFound();
            }

            if (table.IsActive)
            {
                var today = DateTime.UtcNow.Date;

                var hasOpenAssignments = await _context.Reservations
                    .AsNoTracking()
                    .AnyAsync(
                        reservation =>
                            reservation.TableLabel == table.Label &&
                            reservation.ReservationDate.Date >= today &&
                            (reservation.Status == ReservationStatus.Approved ||
                             reservation.Status == ReservationStatus.Arrived),
                        cancellationToken);

                if (hasOpenAssignments)
                {
                    TempData["ErrorMessage"] =
                        $"{table.Label} still has active reservations. Reassign them before deactivating the table.";

                    return RedirectToAction(nameof(Index));
                }
            }

            table.IsActive = !table.IsActive;
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] = table.IsActive
                ? $"{table.Label} is active for reservation assignments."
                : $"{table.Label} is inactive and hidden from assignment choices.";

            return RedirectToAction(nameof(Index));
        }

        private static void Normalize(PubTable table)
        {
            table.Label = table.Label.Trim().ToUpperInvariant();

            table.Area = string.IsNullOrWhiteSpace(table.Area)
                ? null
                : table.Area.Trim();
        }
    }
}
