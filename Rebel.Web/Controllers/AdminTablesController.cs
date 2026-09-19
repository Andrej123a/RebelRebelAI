using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Authorization;
using Rebel.Web.Models;

namespace Rebel.Web.Controllers
{
    [Authorize(Policy = AdminPolicies.Backstage)]
    public class AdminTablesController : Controller
    {
        private static readonly string[] AllowedTableTypes =
        [
            "Classic",
            "HighTop",
            "BarCounter",
            "Lounge",
            "Outdoor"
        ];

        private static readonly string[] AllowedTableShapes =
        [
            "Round",
            "Square",
            "Rectangle",
            "Counter",
            "Lounge"
        ];

        private static readonly string[] AllowedRoomShapes =
        [
            "Rectangle",
            "Square",
            "LShape",
            "Patio"
        ];

        private static readonly string[] AllowedFixtureKinds =
        [
            "door", "toilet", "entrance", "bar", "terrace"
        ];

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
            await EnsureFloorFixturesSchemaAsync(cancellationToken);
            await EnsureFloorRoomsAsync(cancellationToken);

            var nowInSkopje = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                SkopjeTimeZone);

            var today = nowInSkopje.Date;

            var rooms = await _context.FloorRooms
                .AsNoTracking()
                .OrderBy(room => room.PositionY)
                .ThenBy(room => room.PositionX)
                .ThenBy(room => room.Name)
                .ToListAsync(cancellationToken);

            var tables = await _context.PubTables
                .AsNoTracking()
                .Include(table => table.FloorRoom)
                .OrderBy(table => table.FloorRoom!.Name)
                .ThenBy(table => table.Label)
                .ToListAsync(cancellationToken);

            var fixtures = await _context.FloorFixtures
                .AsNoTracking()
                .OrderBy(fixture => fixture.PositionY)
                .ThenBy(fixture => fixture.PositionX)
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

            var tableViewModels = tables
                .Select(table =>
                {
                    reservationsByTable.TryGetValue(
                        table.Label,
                        out var tableReservations);

                    tableReservations ??= new List<Reservation>();

                    var arrivedReservation = tableReservations
                        .Where(reservation =>
                            reservation.Status == ReservationStatus.Arrived &&
                            reservation.ReservationDate == today)
                        .OrderByDescending(reservation =>
                            reservation.ReservationTime)
                        .FirstOrDefault();

                    var nextReservation = tableReservations
                        .Where(reservation =>
                            reservation.Status == ReservationStatus.Approved &&
                            (reservation.ReservationDate > today ||
                             (reservation.ReservationDate == today &&
                              reservation.ReservationTime >=
                                  nowInSkopje.TimeOfDay)))
                        .OrderBy(reservation => reservation.ReservationDate)
                        .ThenBy(reservation => reservation.ReservationTime)
                        .FirstOrDefault();

                    return new AdminFloorTableViewModel
                    {
                        Table = table,
                        OperationalReservation =
                            arrivedReservation ?? nextReservation,
                        IsOccupied = arrivedReservation != null
                    };
                })
                .ToList();

            var roomViewModels = rooms
                .Select(room => new AdminFloorRoomViewModel
                {
                    Room = room,
                    Tables = tableViewModels
                        .Where(item =>
                            item.Table.FloorRoomId == room.Id &&
                            item.Table.IsActive)
                        .OrderBy(item => item.Table.LayoutY)
                        .ThenBy(item => item.Table.LayoutX)
                        .ThenBy(item => item.Table.Label)
                        .ToList()
                })
                .Where(item => item.Room.IsActive || item.Tables.Count > 0)
                .ToList();

            var model = new AdminFloorPlanViewModel
            {
                CurrentLocalDate = today,
                Rooms = roomViewModels,
                Tables = tableViewModels,
                Fixtures = fixtures,
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
                Layout = new AdminFloorLayoutSaveRequest
                {
                    Rooms = roomViewModels
                        .Select(item => new AdminFloorLayoutRoomRequest
                        {
                            Id = item.Room.Id,
                            Name = item.Room.Name,
                            Shape = item.Room.Shape,
                            PositionX = item.Room.PositionX,
                            PositionY = item.Room.PositionY,
                            Width = item.Room.Width,
                            Height = item.Room.Height,
                            Rotation = item.Room.Rotation,
                            IsActive = item.Room.IsActive
                        })
                        .ToList(),
                    Tables = tableViewModels
                        .Select(item => new AdminFloorLayoutTableRequest
                        {
                            Id = item.Table.Id,
                            Label = item.Table.Label,
                            Area = item.Table.Area,
                            Capacity = item.Table.Capacity,
                            TableType = item.Table.TableType,
                            Shape = item.Table.Shape,
                            LayoutX = item.Table.LayoutX,
                            LayoutY = item.Table.LayoutY,
                            LayoutWidth = item.Table.LayoutWidth,
                            LayoutHeight = item.Table.LayoutHeight,
                            Rotation = item.Table.Rotation,
                            FloorRoomId = item.Table.FloorRoomId,
                            IsActive = item.Table.IsActive
                        })
                        .ToList(),
                    Fixtures = fixtures
                        .Select(fixture => new AdminFloorLayoutFixtureRequest
                        {
                            Id = fixture.Id,
                            Kind = fixture.Kind,
                            Label = fixture.Label,
                            FloorRoomId = fixture.FloorRoomId,
                            PositionX = fixture.PositionX,
                            PositionY = fixture.PositionY,
                            Width = fixture.Width,
                            Height = fixture.Height,
                            Rotation = fixture.Rotation
                        })
                        .ToList()
                }
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> SaveLayout(
            [FromBody] AdminFloorLayoutSaveRequest request,
            CancellationToken cancellationToken)
        {
            await EnsureFloorFixturesSchemaAsync(cancellationToken);
            await EnsureFloorRoomsAsync(cancellationToken);

            if (request.Tables.Count == 0)
            {
                return BadRequest(new { message = "No tables were sent." });
            }

            var incomingLabels = request.Tables
                .Select(table => NormalizeLabel(table.Label))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToList();

            if (incomingLabels.Count != incomingLabels
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count())
            {
                return BadRequest(new
                {
                    message = "Two tables have the same name."
                });
            }

            var existingRooms = await _context.FloorRooms
                .ToDictionaryAsync(room => room.Id, cancellationToken);
            var existingTables = await _context.PubTables
                .ToDictionaryAsync(table => table.Id, cancellationToken);
            var existingLabels = existingTables.Values
                .ToDictionary(
                    table => table.Label,
                    table => table.Id,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var roomRequest in request.Rooms)
            {
                if (!existingRooms.TryGetValue(roomRequest.Id, out var room))
                {
                    continue;
                }

                room.Name = NormalizeRoomName(roomRequest.Name);
                room.Shape = NormalizeChoice(
                    roomRequest.Shape,
                    AllowedRoomShapes,
                    "Rectangle");
                room.PositionX = Math.Clamp(roomRequest.PositionX, 0, 5000);
                room.PositionY = Math.Clamp(roomRequest.PositionY, 0, 5000);
                room.Width = Math.Clamp(roomRequest.Width, 240, 2200);
                room.Height = Math.Clamp(roomRequest.Height, 180, 1600);
                room.Rotation = Math.Clamp(roomRequest.Rotation, -180, 180);
                room.IsActive = roomRequest.IsActive;
            }

            var created = 0;

            foreach (var tableRequest in request.Tables)
            {
                var normalizedLabel = NormalizeLabel(tableRequest.Label);

                if (string.IsNullOrWhiteSpace(normalizedLabel))
                {
                    normalizedLabel = NextTableLabel(existingLabels.Keys);
                }

                if (existingLabels.TryGetValue(normalizedLabel, out var labelOwnerId) &&
                    labelOwnerId != tableRequest.Id)
                {
                    return BadRequest(new
                    {
                        message = $"Table {normalizedLabel} already exists."
                    });
                }

                var table = tableRequest.Id.HasValue &&
                            existingTables.TryGetValue(
                                tableRequest.Id.Value,
                                out var existingTable)
                    ? existingTable
                    : new PubTable
                    {
                        Id = Guid.NewGuid()
                    };

                if (tableRequest.Id is null ||
                    !existingTables.ContainsKey(table.Id))
                {
                    created++;
                    _context.PubTables.Add(table);
                }

                table.Label = normalizedLabel;
                table.Area = NormalizeArea(tableRequest.Area);
                table.Capacity = Math.Clamp(tableRequest.Capacity, 1, 30);
                table.TableType = NormalizeChoice(
                    tableRequest.TableType,
                    AllowedTableTypes,
                    "Classic");
                table.Shape = NormalizeChoice(
                    tableRequest.Shape,
                    AllowedTableShapes,
                    DefaultShapeFor(table.TableType));
                table.LayoutX = ClampDecimal(tableRequest.LayoutX, 0, 100);
                table.LayoutY = ClampDecimal(tableRequest.LayoutY, 0, 100);
                table.LayoutWidth = ClampDecimal(
                    tableRequest.LayoutWidth,
                    6,
                    60);
                table.LayoutHeight = ClampDecimal(
                    tableRequest.LayoutHeight,
                    6,
                    60);
                table.Rotation = Math.Clamp(
                    tableRequest.Rotation,
                    -180,
                    180);
                table.FloorRoomId = existingRooms.ContainsKey(
                    tableRequest.FloorRoomId ?? Guid.Empty)
                    ? tableRequest.FloorRoomId
                    : existingRooms.Keys.FirstOrDefault();
                table.IsActive = tableRequest.IsActive;

                existingLabels[table.Label] = table.Id;
            }

            if (request.RemovedFixtureIds.Count > 0)
            {
                var removedFixtures = await _context.FloorFixtures
                    .Where(fixture => request.RemovedFixtureIds.Contains(fixture.Id))
                    .ToListAsync(cancellationToken);
                _context.FloorFixtures.RemoveRange(removedFixtures);
            }

            var existingFixtures = await _context.FloorFixtures
                .ToDictionaryAsync(fixture => fixture.Id, cancellationToken);
            var createdFixtures = new List<object>();

            foreach (var fixtureRequest in request.Fixtures)
            {
                var fixture = fixtureRequest.Id.HasValue &&
                              existingFixtures.TryGetValue(fixtureRequest.Id.Value, out var savedFixture)
                    ? savedFixture
                    : new FloorFixture { Id = Guid.NewGuid() };

                if (!fixtureRequest.Id.HasValue || !existingFixtures.ContainsKey(fixture.Id))
                {
                    _context.FloorFixtures.Add(fixture);
                    createdFixtures.Add(new { clientId = fixtureRequest.ClientId, id = fixture.Id });
                }

                fixture.Kind = NormalizeChoice(
                    fixtureRequest.Kind,
                    AllowedFixtureKinds,
                    "door").ToLowerInvariant();
                fixture.Label = NormalizeFixtureLabel(fixtureRequest.Label, fixture.Kind);
                fixture.FloorRoomId = existingRooms.ContainsKey(
                    fixtureRequest.FloorRoomId ?? Guid.Empty)
                    ? fixtureRequest.FloorRoomId
                    : existingRooms.Keys.FirstOrDefault();
                fixture.PositionX = Math.Clamp(fixtureRequest.PositionX, 0, 6000);
                fixture.PositionY = Math.Clamp(fixtureRequest.PositionY, 0, 6000);
                fixture.Width = Math.Clamp(fixtureRequest.Width, 36, 1200);
                fixture.Height = Math.Clamp(fixtureRequest.Height, 24, 1200);
                fixture.Rotation = Math.Clamp(fixtureRequest.Rotation, -180, 180);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                createdFixtures,
                message = created == 0
                    ? "Floor layout saved."
                    : $"Floor layout saved. {created} new table{(created == 1 ? string.Empty : "s")} added."
            });
        }

        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Create(
            CancellationToken cancellationToken)
        {
            await EnsureFloorRoomsAsync(cancellationToken);

            return View(await BuildTableEditorModelAsync(
                new PubTable
                {
                    Capacity = 4,
                    IsActive = true,
                    TableType = "Classic",
                    Shape = "Square"
                },
                cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Create(
            AdminTableEditorViewModel model,
            CancellationToken cancellationToken)
        {
            Normalize(model.Table);

            if (!ModelState.IsValid)
            {
                return View(await BuildTableEditorModelAsync(
                    model.Table,
                    cancellationToken));
            }

            var labelExists = await _context.PubTables
                .AnyAsync(
                    existingTable =>
                        existingTable.Label.ToUpper() == model.Table.Label,
                    cancellationToken);

            if (labelExists)
            {
                ModelState.AddModelError(
                    "Table.Label",
                    "A table with this label already exists.");

                return View(await BuildTableEditorModelAsync(
                    model.Table,
                    cancellationToken));
            }

            model.Table.Id = Guid.NewGuid();
            await ApplyRoomAssignmentAsync(model.Table, cancellationToken);

            _context.PubTables.Add(model.Table);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{model.Table.Label} was added to the floor plan.";

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

            return View(await BuildTableEditorModelAsync(
                table,
                cancellationToken));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> Edit(
            Guid id,
            AdminTableEditorViewModel model,
            CancellationToken cancellationToken)
        {
            if (id != model.Table.Id)
            {
                return NotFound();
            }

            Normalize(model.Table);

            if (!ModelState.IsValid)
            {
                return View(await BuildTableEditorModelAsync(
                    model.Table,
                    cancellationToken));
            }

            var labelExists = await _context.PubTables
                .AnyAsync(
                    existingTable =>
                        existingTable.Id != model.Table.Id &&
                        existingTable.Label.ToUpper() == model.Table.Label,
                    cancellationToken);

            if (labelExists)
            {
                ModelState.AddModelError(
                    "Table.Label",
                    "A table with this label already exists.");

                return View(await BuildTableEditorModelAsync(
                    model.Table,
                    cancellationToken));
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
                    model.Table.Label,
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
                        "Table.Label",
                        "This table has active reservations. Reassign them before changing the label.");

                    return View(await BuildTableEditorModelAsync(
                        model.Table,
                        cancellationToken));
                }
            }

            existingTable.Label = model.Table.Label;
            existingTable.Area = model.Table.Area;
            existingTable.Capacity = model.Table.Capacity;
            existingTable.TableType = model.Table.TableType;
            existingTable.Shape = model.Table.Shape;
            existingTable.LayoutX = model.Table.LayoutX;
            existingTable.LayoutY = model.Table.LayoutY;
            existingTable.LayoutWidth = model.Table.LayoutWidth;
            existingTable.LayoutHeight = model.Table.LayoutHeight;
            existingTable.Rotation = model.Table.Rotation;
            existingTable.FloorRoomId = model.Table.FloorRoomId;
            existingTable.IsActive = model.Table.IsActive;

            await ApplyRoomAssignmentAsync(existingTable, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{existingTable.Label} was updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public IActionResult CreateRoom()
        {
            return View(new FloorRoom());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> CreateRoom(
            FloorRoom room,
            CancellationToken cancellationToken)
        {
            Normalize(room);

            if (!ModelState.IsValid)
            {
                return View(room);
            }

            room.Id = Guid.NewGuid();
            _context.FloorRooms.Add(room);
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{room.Name} was added to the floor layout.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> EditRoom(
            Guid id,
            CancellationToken cancellationToken)
        {
            var room = await _context.FloorRooms
                .FirstOrDefaultAsync(
                    existingRoom => existingRoom.Id == id,
                    cancellationToken);

            return room == null ? NotFound() : View(room);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> EditRoom(
            Guid id,
            FloorRoom room,
            CancellationToken cancellationToken)
        {
            if (id != room.Id)
            {
                return NotFound();
            }

            Normalize(room);

            if (!ModelState.IsValid)
            {
                return View(room);
            }

            var existingRoom = await _context.FloorRooms
                .FirstOrDefaultAsync(
                    currentRoom => currentRoom.Id == id,
                    cancellationToken);

            if (existingRoom == null)
            {
                return NotFound();
            }

            existingRoom.Name = room.Name;
            existingRoom.Shape = room.Shape;
            existingRoom.PositionX = room.PositionX;
            existingRoom.PositionY = room.PositionY;
            existingRoom.Width = room.Width;
            existingRoom.Height = room.Height;
            existingRoom.IsActive = room.IsActive;

            await _context.SaveChangesAsync(cancellationToken);

            TempData["SuccessMessage"] =
                $"{existingRoom.Name} was updated.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = AdminPolicies.ManagerOnly)]
        public async Task<IActionResult> DeleteRoom(
            Guid id,
            CancellationToken cancellationToken)
        {
            var room = await _context.FloorRooms
                .Include(existingRoom => existingRoom.Tables)
                .Include(existingRoom => existingRoom.Fixtures)
                .FirstOrDefaultAsync(
                    existingRoom => existingRoom.Id == id,
                    cancellationToken);

            if (room == null)
            {
                return NotFound(new { message = "Floor not found." });
            }

            var tableLabels = room.Tables
                .Where(table => table.IsActive)
                .Select(table => table.Label)
                .ToList();
            var today = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                SkopjeTimeZone).Date;
            var hasOpenAssignments = tableLabels.Count > 0 &&
                await _context.Reservations
                    .AsNoTracking()
                    .AnyAsync(
                        reservation =>
                            reservation.TableLabel != null &&
                            tableLabels.Contains(reservation.TableLabel) &&
                            reservation.ReservationDate >= today &&
                            (reservation.Status == ReservationStatus.Approved ||
                             reservation.Status == ReservationStatus.Arrived),
                        cancellationToken);

            if (hasOpenAssignments)
            {
                return Conflict(new
                {
                    message = "This floor has active reservations. Reassign them before deleting it."
                });
            }

            room.IsActive = false;
            foreach (var table in room.Tables)
            {
                table.IsActive = false;
            }

            _context.FloorFixtures.RemoveRange(room.Fixtures);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new
            {
                message = $"{room.Name} was removed from the floor plan."
            });
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

        private async Task EnsureFloorFixturesSchemaAsync(
            CancellationToken cancellationToken)
        {
            await _context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "FloorFixtures" (
                    "Id" uuid NOT NULL,
                    "FloorRoomId" uuid NULL,
                    "Kind" character varying(24) NOT NULL,
                    "Label" character varying(40) NOT NULL,
                    "PositionX" integer NOT NULL,
                    "PositionY" integer NOT NULL,
                    "Width" integer NOT NULL,
                    "Height" integer NOT NULL,
                    "Rotation" integer NOT NULL,
                    CONSTRAINT "PK_FloorFixtures" PRIMARY KEY ("Id")
                );

                ALTER TABLE "FloorFixtures"
                ADD COLUMN IF NOT EXISTS "FloorRoomId" uuid NULL;

                ALTER TABLE "FloorRooms"
                ADD COLUMN IF NOT EXISTS "Rotation" integer NOT NULL DEFAULT 0;

                UPDATE "FloorFixtures"
                SET "FloorRoomId" = (
                    SELECT "Id" FROM "FloorRooms"
                    WHERE "IsActive" = TRUE
                    ORDER BY "PositionY", "PositionX"
                    LIMIT 1
                )
                WHERE "FloorRoomId" IS NULL;
                """,
                cancellationToken);
        }

        private static string NormalizeFixtureLabel(string? label, string kind)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label.Trim()[..Math.Min(label.Trim().Length, 40)];
            }

            return kind switch
            {
                "toilet" => "Toilet",
                "entrance" => "Entrance",
                "bar" => "Bar",
                "terrace" => "Terrace",
                _ => "Door"
            };
        }

        private async Task EnsureFloorRoomsAsync(
            CancellationToken cancellationToken)
        {
            await _context.Database.ExecuteSqlRawAsync(
                """
                ALTER TABLE "FloorRooms"
                ADD COLUMN IF NOT EXISTS "Rotation" integer NOT NULL DEFAULT 0;
                """,
                cancellationToken);

            if (await _context.FloorRooms.AnyAsync(cancellationToken))
            {
                return;
            }

            _context.FloorRooms.AddRange(
                new FloorRoom
                {
                    Id = Guid.NewGuid(),
                    Name = "Main floor",
                    Shape = "Rectangle",
                    PositionX = 32,
                    PositionY = 32,
                    Width = 920,
                    Height = 560
                },
                new FloorRoom
                {
                    Id = Guid.NewGuid(),
                    Name = "Bar side",
                    Shape = "LShape",
                    PositionX = 990,
                    PositionY = 32,
                    Width = 420,
                    Height = 420
                });

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<AdminTableEditorViewModel> BuildTableEditorModelAsync(
            PubTable table,
            CancellationToken cancellationToken)
        {
            await EnsureFloorRoomsAsync(cancellationToken);

            return new AdminTableEditorViewModel
            {
                Table = table,
                Rooms = await _context.FloorRooms
                    .AsNoTracking()
                    .OrderBy(room => room.Name)
                    .ToListAsync(cancellationToken)
            };
        }

        private async Task ApplyRoomAssignmentAsync(
            PubTable table,
            CancellationToken cancellationToken)
        {
            await EnsureFloorRoomsAsync(cancellationToken);

            var roomExists = table.FloorRoomId.HasValue &&
                             await _context.FloorRooms.AnyAsync(
                                 room => room.Id == table.FloorRoomId.Value,
                                 cancellationToken);

            if (roomExists)
            {
                return;
            }

            table.FloorRoomId = await _context.FloorRooms
                .OrderBy(room => room.PositionY)
                .ThenBy(room => room.PositionX)
                .Select(room => room.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static void Normalize(PubTable table)
        {
            table.Label = NormalizeLabel(table.Label);
            table.Area = NormalizeArea(table.Area);
            table.Capacity = Math.Clamp(table.Capacity, 1, 30);
            table.TableType = NormalizeChoice(
                table.TableType,
                AllowedTableTypes,
                "Classic");
            table.Shape = NormalizeChoice(
                table.Shape,
                AllowedTableShapes,
                DefaultShapeFor(table.TableType));
            table.LayoutX = ClampDecimal(table.LayoutX, 0, 100);
            table.LayoutY = ClampDecimal(table.LayoutY, 0, 100);
            table.LayoutWidth = ClampDecimal(table.LayoutWidth, 6, 60);
            table.LayoutHeight = ClampDecimal(table.LayoutHeight, 6, 60);
            table.Rotation = Math.Clamp(table.Rotation, -180, 180);
        }

        private static void Normalize(FloorRoom room)
        {
            room.Name = NormalizeRoomName(room.Name);
            room.Shape = NormalizeChoice(
                room.Shape,
                AllowedRoomShapes,
                "Rectangle");
            room.PositionX = Math.Clamp(room.PositionX, 0, 5000);
            room.PositionY = Math.Clamp(room.PositionY, 0, 5000);
            room.Width = Math.Clamp(room.Width, 240, 2200);
            room.Height = Math.Clamp(room.Height, 180, 1600);
            room.Rotation = Math.Clamp(room.Rotation, -180, 180);
        }

        private static string NormalizeLabel(string? label) =>
            (label ?? string.Empty).Trim().ToUpperInvariant();

        private static string NormalizeRoomName(string? name) =>
            string.IsNullOrWhiteSpace(name)
                ? "Room"
                : name.Trim();

        private static string? NormalizeArea(string? area) =>
            string.IsNullOrWhiteSpace(area)
                ? "Main floor"
                : area.Trim();

        private static string NormalizeChoice(
            string? value,
            IReadOnlyCollection<string> allowedValues,
            string fallback)
        {
            var match = allowedValues.FirstOrDefault(
                allowedValue => allowedValue.Equals(
                    value,
                    StringComparison.OrdinalIgnoreCase));

            return match ?? fallback;
        }

        private static string DefaultShapeFor(string tableType) =>
            tableType switch
            {
                "HighTop" => "Round",
                "BarCounter" => "Counter",
                "Lounge" => "Lounge",
                _ => "Square"
            };

        private static decimal ClampDecimal(
            decimal value,
            decimal minimum,
            decimal maximum) =>
            Math.Min(maximum, Math.Max(minimum, value));

        private static string NextTableLabel(IEnumerable<string> existingLabels)
        {
            var labels = existingLabels.ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var index = 1; index < 500; index++)
            {
                var candidate = $"T{index}";
                if (!labels.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"T{DateTime.UtcNow:HHmmss}";
        }
    }
}
