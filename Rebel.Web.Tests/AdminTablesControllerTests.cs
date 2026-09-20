using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Controllers;
using Rebel.Web.Models;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class AdminTablesControllerTests
{
    [Fact]
    public async Task SaveLayout_DeactivatesRemovedTable()
    {
        await using var context = CreateContext();
        var table = await SeedFloorAsync(context);
        var controller = new AdminTablesController(context);

        var result = await controller.SaveLayout(
            new AdminFloorLayoutSaveRequest
            {
                RemovedTableIds = [table.Id]
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.False(table.IsActive);
    }

    [Fact]
    public async Task SaveLayout_RejectsRemovalWhenTableHasActiveReservation()
    {
        await using var context = CreateContext();
        var table = await SeedFloorAsync(context);
        context.Reservations.Add(new Reservation
        {
            Id = Guid.NewGuid(),
            ReservationCode = "RR-TEST000001",
            EmailStatus = "CodeOnly",
            FullName = "Test Guest",
            Email = string.Empty,
            PhoneNumber = "+38970000000",
            ReservationDate = DateTime.UtcNow.Date.AddDays(1),
            ReservationTime = new TimeSpan(19, 0, 0),
            NumberOfGuests = 2,
            Status = ReservationStatus.Approved,
            TableLabel = table.Label
        });
        await context.SaveChangesAsync();
        var controller = new AdminTablesController(context);

        var result = await controller.SaveLayout(
            new AdminFloorLayoutSaveRequest
            {
                RemovedTableIds = [table.Id]
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.True(table.IsActive);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<PubTable> SeedFloorAsync(AppDbContext context)
    {
        var room = new FloorRoom
        {
            Id = Guid.NewGuid(),
            Name = "Main floor",
            Shape = "Rectangle",
            Width = 900,
            Height = 600,
            IsActive = true
        };
        var table = new PubTable
        {
            Id = Guid.NewGuid(),
            Label = "T1",
            Area = room.Name,
            FloorRoomId = room.Id,
            Capacity = 4,
            IsActive = true
        };
        context.FloorRooms.Add(room);
        context.PubTables.Add(table);
        await context.SaveChangesAsync();

        return table;
    }
}
