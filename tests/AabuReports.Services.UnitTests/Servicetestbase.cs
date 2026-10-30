using System.Text.Json;
using System.Text.Json.Serialization;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Entities;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;
using AabuReports.Core.Options;
using AabuReports.Core.ValueObjects;
using AutoFixture;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public abstract class ServiceTestBase
{
    private static readonly JsonSerializerOptions DumpOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter() }
    };

    protected readonly ITestOutputHelper Output;
    protected readonly IFixture Fixture;

    protected ServiceTestBase(ITestOutputHelper output)
    {
        Output = output;

        Fixture = new Fixture();
        Fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => Fixture.Behaviors.Remove(b));
        Fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    // ---------- output ----------

    /// <summary> prints the generated input (call it right after the arrange part) </summary>
    protected void LogArrange(string name, object? value) => Output.WriteLine($"[ARRANGE] {name}: {Dump(value)}");

    /// <summary> prints the result of the act part </summary>
    protected void LogAct(object? result) => Output.WriteLine($"[ACT] result: {Dump(result)}");

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

    // ---------- mocks ----------

    protected static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    protected static Mock<RoleManager<ApplicationRole>> CreateRoleManagerMock()
    {
        var store = new Mock<IRoleStore<ApplicationRole>>();
        return new Mock<RoleManager<ApplicationRole>>(store.Object, null!, null!, null!, null!);
    }

    protected static Mock<IFormFile> CreateFileMock(string contentType = "image/png", long length = 1024)
    {
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.Length).Returns(length);
        file.SetupGet(f => f.ContentType).Returns(contentType);
        file.SetupGet(f => f.FileName).Returns("photo.png");
        return file;
    }

    protected static Microsoft.Extensions.Options.IOptions<JwtOptions> CreateJwtOptions(string key = "this-is-a-long-test-signing-key-0123456789-abcdef")
        => Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Key = key,
            Issuer = "aabu-test-issuer",
            Audience = "aabu-test-audience",
            AccessTokenLifeTimeInSeconds = 3600,
            RefreshTokenLifeTimeInSeconds = 86400
        });

    // ---------- factories ----------

    protected static string NewStudentId() => Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString();

    protected ApplicationUser CreateUser()
    {
        var studentId = NewStudentId();
        return new ApplicationUser
        {
            StudentId = studentId,
            FullName = Fixture.Create<string>(),
            UserName = studentId,
            Email = $"{studentId}@aabu.edu.jo"
        };
    }

    protected AuthResponseDto CreateAuthResponse() => Fixture.Create<AuthResponseDto>();

    protected Location CreateLocation(
        LocationType type = LocationType.Building,
        ReportStateEnum? state = ReportStateEnum.Normal,
        int? activeReportsCount = 0)
    {
        return new Location
        {
            Title = Fixture.Create<string>(),
            Coordinates = new MapCoordinates { Lat = 32.5, Lng = 35.9 },
            Type = type,
            State = state,
            ActiveReportsCount = activeReportsCount
        };
    }

    protected Report CreateReport(ReportStateEnum state = ReportStateEnum.Pending, Guid? userId = null, Location? location = null)
    {
        var loc = location ?? CreateLocation();
        return new Report
        {
            ReportDate = DateTime.UtcNow.AddHours(-1),
            Category = ReportCategory.Electrical,
            State = state,
            Severity = ReportSeverity.Medium,
            Description = Fixture.Create<string>(),
            LocationId = loc.Id,
            Location = loc,
            UserId = userId ?? Guid.NewGuid()
        };
    }

    protected Notification CreateNotification(Guid? userId = null)
    {
        return new Notification
        {
            Type = NotificationType.System,
            Message = Fixture.Create<string>(),
            CreatedAt = DateTime.UtcNow,
            UserId = userId ?? Guid.NewGuid()
        };
    }
}