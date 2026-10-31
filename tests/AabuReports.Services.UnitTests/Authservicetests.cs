using System.Net;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Entities;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class AuthServiceTests : ServiceTestBase
{
    private readonly IAuthService _authService;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock;
    private readonly Mock<ICookieService> _cookieServiceMock;
    private readonly Mock<IEmailVerificationService> _verificationMock = new();

    public AuthServiceTests(ITestOutputHelper output) : base(output)
    {
        _userManagerMock = CreateUserManagerMock();
        _tokenServiceMock = new Mock<ITokenService>();
        _refreshTokenServiceMock = new Mock<IRefreshTokenService>();
        _cookieServiceMock = new Mock<ICookieService>();

        _verificationMock.Setup(v => v.BeginAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _authService = new AuthService(
            _userManagerMock.Object,
            _tokenServiceMock.Object,
            _refreshTokenServiceMock.Object,
            _cookieServiceMock.Object,
            NullLogger<AuthService>.Instance,
            _verificationMock.Object);
    }

    private LoginDto CreateLoginDto() => new()
    {
        StudentId = $" {NewStudentId()} ",
        Password = Fixture.Create<string>()
    };

    private RefreshToken CreateRefreshToken(ApplicationUser? user)
    {
        return new RefreshToken
        {
            RefreshTokenString = Fixture.Create<string>(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            UserId = user?.Id ?? Guid.NewGuid(),
            User = user
        };
    }

    #region Login

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenUserNotFound()
    {
        // Arrange
        var dto = CreateLoginDto();
        LogArrange(nameof(dto), dto);

        _userManagerMock.Setup(m => m.FindByNameAsync(dto.StudentId.Trim())).ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _authService.Login(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorMessage.Should().Be("Invalid student id or password");
    }

    [Fact]
    public async Task Login_ShouldReturnTooManyRequests_WhenUserIsLockedOut()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();
        LogArrange(nameof(dto), dto);
        LogArrange(nameof(user), user);

        _userManagerMock.Setup(m => m.FindByNameAsync(dto.StudentId.Trim())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(true);

        // Act
        var result = await _authService.Login(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        result.ErrorMessage.Should().Be("Account is temporarily locked, try again later");

        _userManagerMock.Verify(m => m.CheckPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_ShouldCountFailedAttemptAndReturnUnauthorized_WhenPasswordIsWrong()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();
        LogArrange(nameof(dto), dto);

        _userManagerMock.Setup(m => m.FindByNameAsync(dto.StudentId.Trim())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.AccessFailedAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _authService.Login(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorMessage.Should().Be("Invalid student id or password");

        _userManagerMock.Verify(m => m.AccessFailedAsync(user), Times.Once);
        _tokenServiceMock.Verify(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_ShouldReturnFailureAndNotSetCookies_WhenSendingCodeFails()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();
        LogArrange(nameof(dto), dto);

        _userManagerMock.Setup(m => m.FindByNameAsync(dto.StudentId.Trim())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);
        _verificationMock.Setup(v => v.BeginAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("email delivery failed", HttpStatusCode.ServiceUnavailable));

        // Act
        var result = await _authService.Login(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorMessage.Should().Be("email delivery failed");

        _cookieServiceMock.Verify(c => c.SetAuthCookies(It.IsAny<AuthResponseDto>()), Times.Never);
    }

    [Fact]
    public async Task Login_ShouldSendCodeWithoutAuthenticating_WhenCredentialsAreValid()
    {
        // Arrange
        var dto = CreateLoginDto();
        var user = CreateUser();
        LogArrange(nameof(dto), dto);

        _userManagerMock.Setup(m => m.FindByNameAsync(dto.StudentId.Trim())).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.CheckPasswordAsync(user, dto.Password)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.ResetAccessFailedCountAsync(user)).ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _authService.Login(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);

        _verificationMock.Verify(v => v.BeginAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _tokenServiceMock.Verify(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _cookieServiceMock.Verify(c => c.SetAuthCookies(It.IsAny<AuthResponseDto>()), Times.Never);
    }

    #endregion

    [Fact]
    public async Task VerifyEmail_ShouldIssueTokensAndSetCookies_WhenCodeMatches()
    {
        var user = CreateUser();
        var tokens = CreateAuthResponse();
        _verificationMock.Setup(v => v.VerifyAsync("1234567890", "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationUser>.Success(user));
        _tokenServiceMock.Setup(t => t.IssueTokensAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponseDto>.Success(tokens));

        var result = await _authService.VerifyEmail("1234567890", "123456");

        result.IsSuccess.Should().BeTrue();
        _cookieServiceMock.Verify(c => c.SetAuthCookies(tokens), Times.Once);
        _userManagerMock.Verify(u => u.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task VerifyEmail_ShouldNotIssueTokens_WhenCodeDoesNotMatch()
    {
        _verificationMock.Setup(v => v.VerifyAsync("1234567890", "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ApplicationUser>.Failure("Invalid code"));

        var result = await _authService.VerifyEmail("1234567890", "123456");

        result.IsSuccess.Should().BeFalse();
        _tokenServiceMock.Verify(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _cookieServiceMock.Verify(c => c.SetAuthCookies(It.IsAny<AuthResponseDto>()), Times.Never);
    }

    #region Refresh

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Refresh_ShouldReturnUnauthorized_WhenRefreshTokenCookieIsMissing(string? refreshToken)
    {
        // Arrange
        LogArrange(nameof(refreshToken), refreshToken);
        _cookieServiceMock.Setup(c => c.GetRefreshToken()).Returns(refreshToken);

        // Act
        var result = await _authService.Refresh();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorMessage.Should().Be("Refresh token is missing");

        _refreshTokenServiceMock.Verify(
            r => r.ConsumeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_ShouldClearCookiesAndReturnFailure_WhenConsumingTheTokenFails()
    {
        // Arrange
        var refreshToken = Fixture.Create<string>();
        LogArrange(nameof(refreshToken), refreshToken);

        _cookieServiceMock.Setup(c => c.GetRefreshToken()).Returns(refreshToken);
        _refreshTokenServiceMock.Setup(r => r.ConsumeRefreshTokenAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RefreshToken>.Failure("Refresh token expired", HttpStatusCode.Unauthorized));

        // Act
        var result = await _authService.Refresh();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorMessage.Should().Be("Refresh token expired");

        _cookieServiceMock.Verify(c => c.ClearAuthCookies(), Times.Once);
        _cookieServiceMock.Verify(c => c.SetAuthCookies(It.IsAny<AuthResponseDto>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_ShouldClearCookiesAndReturnUnauthorized_WhenConsumedTokenHasNoUser()
    {
        // Arrange
        var refreshToken = Fixture.Create<string>();
        LogArrange(nameof(refreshToken), refreshToken);

        _cookieServiceMock.Setup(c => c.GetRefreshToken()).Returns(refreshToken);
        _refreshTokenServiceMock.Setup(r => r.ConsumeRefreshTokenAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RefreshToken>.Success(CreateRefreshToken(null)));

        // Act
        var result = await _authService.Refresh();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorMessage.Should().Be("Invalid refresh token");

        _cookieServiceMock.Verify(c => c.ClearAuthCookies(), Times.Once);
    }

    [Fact]
    public async Task Refresh_ShouldClearCookiesAndReturnTooManyRequests_WhenUserIsLockedOut()
    {
        // Arrange
        var refreshToken = Fixture.Create<string>();
        var user = CreateUser();
        LogArrange(nameof(refreshToken), refreshToken);

        _cookieServiceMock.Setup(c => c.GetRefreshToken()).Returns(refreshToken);
        _refreshTokenServiceMock.Setup(r => r.ConsumeRefreshTokenAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RefreshToken>.Success(CreateRefreshToken(user)));
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(true);

        // Act
        var result = await _authService.Refresh();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        _tokenServiceMock.Verify(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()), Times.Never);
        _cookieServiceMock.Verify(c => c.ClearAuthCookies(), Times.Once);
    }

    [Fact]
    public async Task Refresh_ShouldClearCookiesAndReturnFailure_WhenIssuingNewTokensFails()
    {
        // Arrange
        var refreshToken = Fixture.Create<string>();
        var user = CreateUser();
        LogArrange(nameof(refreshToken), refreshToken);

        _cookieServiceMock.Setup(c => c.GetRefreshToken()).Returns(refreshToken);
        _refreshTokenServiceMock.Setup(r => r.ConsumeRefreshTokenAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RefreshToken>.Success(CreateRefreshToken(user)));
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _tokenServiceMock.Setup(t => t.IssueTokensAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponseDto>.Failure("token generation failed", HttpStatusCode.InternalServerError));

        // Act
        var result = await _authService.Refresh();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("token generation failed");

        _cookieServiceMock.Verify(c => c.ClearAuthCookies(), Times.Once);
        _cookieServiceMock.Verify(c => c.SetAuthCookies(It.IsAny<AuthResponseDto>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_ShouldSetNewCookies_WhenTokenIsValid()
    {
        // Arrange
        var refreshToken = Fixture.Create<string>();
        var user = CreateUser();
        var tokens = CreateAuthResponse();
        LogArrange(nameof(refreshToken), refreshToken);
        LogArrange(nameof(tokens), tokens);

        _cookieServiceMock.Setup(c => c.GetRefreshToken()).Returns(refreshToken);
        _refreshTokenServiceMock.Setup(r => r.ConsumeRefreshTokenAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RefreshToken>.Success(CreateRefreshToken(user)));
        _userManagerMock.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        _tokenServiceMock.Setup(t => t.IssueTokensAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponseDto>.Success(tokens));

        // Act
        var result = await _authService.Refresh();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _cookieServiceMock.Verify(c => c.SetAuthCookies(tokens), Times.Once);
        _cookieServiceMock.Verify(c => c.ClearAuthCookies(), Times.Never);
    }

    #endregion

    #region Logout

    [Fact]
    public async Task Logout_ShouldRevokeTokenAndClearCookies_WhenCookieHasRefreshToken()
    {
        // Arrange
        var refreshToken = Fixture.Create<string>();
        LogArrange(nameof(refreshToken), refreshToken);

        _cookieServiceMock.Setup(c => c.GetRefreshToken()).Returns(refreshToken);
        _refreshTokenServiceMock.Setup(r => r.RevokeRefreshTokenAsync(refreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(HttpStatusCode.NoContent));

        // Act
        var result = await _authService.Logout();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _refreshTokenServiceMock.Verify(r => r.RevokeRefreshTokenAsync(refreshToken, It.IsAny<CancellationToken>()), Times.Once);
        _cookieServiceMock.Verify(c => c.ClearAuthCookies(), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Logout_ShouldOnlyClearCookies_WhenThereIsNoRefreshToken(string? refreshToken)
    {
        // Arrange
        LogArrange(nameof(refreshToken), refreshToken);
        _cookieServiceMock.Setup(c => c.GetRefreshToken()).Returns(refreshToken);

        // Act
        var result = await _authService.Logout();
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _refreshTokenServiceMock.Verify(
            r => r.RevokeRefreshTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cookieServiceMock.Verify(c => c.ClearAuthCookies(), Times.Once);
    }

    #endregion
}