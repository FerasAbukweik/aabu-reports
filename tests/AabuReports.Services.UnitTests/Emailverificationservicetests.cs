using System.Net;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class EmailVerificationServiceTests : ServiceTestBase
{
    private readonly Mock<IRedisService> _redis = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<UserManager<ApplicationUser>> _users;
    private readonly EmailVerificationService _service;
    private readonly ApplicationUser _user;
    private string? _cachedCode;
    private string? _sentCode;

    public EmailVerificationServiceTests(ITestOutputHelper output) : base(output)
    {
        _users = CreateUserManagerMock();
        _user = CreateUser();
        _users.Setup(u => u.FindByNameAsync(_user.StudentId)).ReturnsAsync(_user);
        _users.Setup(u => u.UpdateAsync(_user)).ReturnsAsync(IdentityResult.Success);
        _users.Setup(u => u.ResetAccessFailedCountAsync(_user)).ReturnsAsync(IdentityResult.Success);
        _users.Setup(u => u.AccessFailedAsync(_user)).ReturnsAsync(IdentityResult.Success);
        _redis.Setup(r => r.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _cachedCode);
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>()))
            .Callback<string, string, CancellationToken, TimeSpan?, TimeSpan?>((_, value, _, _, _) => _cachedCode = value)
            .Returns(Task.CompletedTask);
        _redis.Setup(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => _cachedCode = null)
            .Returns(Task.CompletedTask);
        _email.Setup(e => e.SendVerificationCodeAsync(_user.Email!, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, code, _) => _sentCode = code)
            .Returns(Task.CompletedTask);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:VerificationCodeLifetimeMinutes"] = "7"
        }).Build();
        _service = new EmailVerificationService(_redis.Object, _email.Object, configuration,
            _users.Object, NullLogger<EmailVerificationService>.Instance);
    }

    [Fact]
    public async Task Begin_ShouldEmailAndCacheSixDigitCodeWithConfiguredExpiry()
    {
        (await _service.BeginAsync(_user)).IsSuccess.Should().BeTrue();

        _sentCode.Should().MatchRegex("^[0-9]{6}$");
        _cachedCode.Should().Be(_sentCode);
        _redis.Verify(r => r.SetAsync($"auth:verification:{_user.StudentId}", _sentCode!,
            It.IsAny<CancellationToken>(), TimeSpan.FromMinutes(7), TimeSpan.FromMinutes(7)), Times.Once);
    }

    [Fact]
    public async Task Resend_ShouldOverwriteCachedCode()
    {
        await _service.BeginAsync(_user);
        _cachedCode = "old-code";

        (await _service.ResendAsync(_user.StudentId)).IsSuccess.Should().BeTrue();

        _cachedCode.Should().Be(_sentCode).And.NotBe("old-code");
        (await _service.VerifyAsync(_user.StudentId, "old-code")).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ShouldReturnUserWhenCodeMatchesAndLeaveCodeToExpire()
    {
        await _service.BeginAsync(_user);

        var result = await _service.VerifyAsync(_user.StudentId, _sentCode!);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(_user);
        _user.EmailConfirmed.Should().BeFalse();
        _cachedCode.Should().Be(_sentCode);
        _users.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        _redis.Verify(r => r.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verify_ShouldRejectCodeMissingFromCache()
    {
        await _service.BeginAsync(_user);
        _cachedCode = null;

        (await _service.VerifyAsync(_user.StudentId, _sentCode!)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ShouldRejectIncorrectCode()
    {
        await _service.BeginAsync(_user);

        (await _service.VerifyAsync(_user.StudentId, "incorrect")).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Verify_ShouldRejectLockedAccount()
    {
        await _service.BeginAsync(_user);
        _users.Setup(u => u.IsLockedOutAsync(_user)).ReturnsAsync(true);

        (await _service.VerifyAsync(_user.StudentId, _sentCode!)).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Begin_ShouldReturnServiceUnavailable_WhenEmailFails()
    {
        _email.Setup(e => e.SendVerificationCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Mail.SmtpException("SMTP unavailable"));

        (await _service.BeginAsync(_user)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        _cachedCode.Should().BeNull();
    }
}
