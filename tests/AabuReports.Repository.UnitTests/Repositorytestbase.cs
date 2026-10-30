using System.Text.Json;
using System.Text.Json.Serialization;
using AabuReports.Core.Entities;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;
using AabuReports.Core.ValueObjects;
using AabuReports.Infrastructure;
using AutoFixture;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace AabuReports.Repository.UnitTests;

/// <summary>
/// same pattern as the other repository tests: the real ApplicationDbContext is used directly (_dbContext),
/// data is seeded straight into it, and the change tracker is cleared before the act.
/// SQLite (in memory) is used instead of UseInMemoryDatabase because the in-memory provider does not support
/// ExecuteUpdate / ExecuteDelete and does not enforce foreign keys or the delete rules.
/// </summary>
public abstract class RepositoryTestBase : IDisposable
{
    private static readonly JsonSerializerOptions DumpOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter() }
    };

    protected readonly ITestOutputHelper _output;
    protected readonly IFixture _fixture;
    protected readonly ApplicationDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private int _counter;

    protected RepositoryTestBase(ITestOutputHelper output)
    {
        _output = output;

        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        // the in-memory database lives as long as the connection is open
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new ApplicationDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------- output ----------

    /// <summary> prints the generated input (call it right after the arrange part) </summary>
    protected void LogArrange(string name, object? value) => _output.WriteLine($"[ARRANGE] {name}: {Dump(value)}");

    /// <summary> prints the result of the act part </summary>
    protected void LogAct(object? result) => _output.WriteLine($"[ACT] result: {Dump(result)}");

    private static string Dump(object? value)
    {
        if (value is null) return "null";

        try
        {
            return JsonSerializer.Serialize(value, value.GetType(), DumpOptions);
        }
        catch (Exception)
        {
            return value.ToString() ?? value.GetType().Name;
        }
    }

    // ---------- seeding (saved, then the tracker is cleared so the repository reads from the DB) ----------

    protected async Task SeedAsync(params object[] entities)
    {
        _dbContext.AddRange(entities);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    protected ApplicationUser CreateUser()
    {
        var studentId = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString();
        return new ApplicationUser
        {
            StudentId = studentId,
            FullName = _fixture.Create<string>(),
            UserName = studentId,
            Email = $"{studentId}@aabu.edu.jo"
        };
    }

    protected async Task<ApplicationUser> SeedUserAsync()
    {
        var user = CreateUser();
        await SeedAsync(user);
        return user;
    }

    /// <summary> every location gets different coordinates (they are unique in the DB) </summary>
    protected async Task<Location> SeedLocationAsync(string? title = null, LocationType type = LocationType.Building)
    {
        var n = ++_counter;
        var location = new Location
        {
            Title = title ?? $"Location {n:D3}",
            Coordinates = new MapCoordinates { Lat = 32 + n * 0.001, Lng = 35 + n * 0.001 },
            Type = type
        };
        await SeedAsync(location);
        return location;
    }

    protected async Task<Report> SeedReportAsync(
        Guid userId,
        Guid locationId,
        ReportStateEnum state = ReportStateEnum.Pending,
        DateTime? reportDate = null,
        DateTime? fixDate = null,
        int? rate = null,
        string? description = null,
        ReportCategory category = ReportCategory.Electrical,
        ReportSeverity severity = ReportSeverity.Medium)
    {
        var report = new Report
        {
            ReportDate = reportDate ?? DateTime.UtcNow.AddHours(-1),
            Category = category,
            State = state,
            Severity = severity,
            Description = description ?? _fixture.Create<string>(),
            LocationId = locationId,
            UserId = userId,
            FixDate = fixDate ?? (state == ReportStateEnum.Fixed ? DateTime.UtcNow : null)
        };
        if (rate != null) report.TryRate(rate.Value); // only works for fixed reports

        await SeedAsync(report);
        return report;
    }

    protected async Task<Image> SeedImageAsync(Guid? reportId, string? publicId = null)
    {
        var id = publicId ?? _fixture.Create<string>();
        var image = new Image { Url = $"https://cdn.test/{id}.png", PublicId = id, ReportId = reportId };
        await SeedAsync(image);
        return image;
    }

    protected async Task<Message> SeedMessageAsync(Guid reportId, Guid senderId, DateTime? sentAt = null)
    {
        var message = new Message
        {
            Content = _fixture.Create<string>(),
            SentAt = sentAt ?? DateTime.UtcNow,
            SenderId = senderId,
            ReportId = reportId
        };
        await SeedAsync(message);
        return message;
    }

    protected async Task<Notification> SeedNotificationAsync(Guid userId, DateTime? createdAt = null, bool isRead = false, Guid? reportId = null)
    {
        var notification = new Notification
        {
            Type = NotificationType.System,
            Message = _fixture.Create<string>(),
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UserId = userId,
            ReportId = reportId
        };
        if (isRead) notification.MarkAsRead();

        await SeedAsync(notification);
        return notification;
    }

    protected async Task<RefreshToken> SeedRefreshTokenAsync(Guid userId, DateTime? expiresAt = null)
    {
        var token = new RefreshToken
        {
            RefreshTokenString = _fixture.Create<string>(),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(1),
            UserId = userId
        };
        await SeedAsync(token);
        return token;
    }
}