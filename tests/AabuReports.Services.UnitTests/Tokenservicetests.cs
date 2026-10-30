using System.Net;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Entities;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Services;
using AutoFixture;
using FluentAssertions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class TokenServiceTests : ServiceTestBase
{
    private readonly ITokenService _tokenService;
    private readonly Mock<IAccessTokenService> _accessTokenServiceMock;
    private readonly Mock<IRefreshTokenService> _refreshTokenServiceMock;

    public TokenServiceTests(ITestOutputHelper output) : base(output)
    {
        _accessTokenServiceMock = new Mock<IAccessTokenService>();
        _refreshTokenServiceMock = new Mock<IRefreshTokenService>();

        _tokenService = new TokenService(
            _accessTokenServiceMock.Object,
            _refreshTokenServiceMock.Object,
            CreateJwtOptions());
    }

    [Fact]
    public async Task IssueTokensAsync_ShouldReturnFailureAndNotCreateRefreshToken_WhenAccessTokenFails()
    {
        // Arrange
        var user = CreateUser();
        LogArrange(nameof(user), user);

        _accessTokenServiceMock.Setup(a => a.GenerateAccessTokenAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AccessTokenDto>.Failure("user has no roles", HttpStatusCode.InternalServerError));

        // Act
        var result = await _tokenService.IssueTokensAsync(user);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("user has no roles");

        _refreshTokenServiceMock.Verify(
            r => r.GenerateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IssueTokensAsync_ShouldReturnFailure_WhenRefreshTokenFails()
    {
        // Arrange
        var user = CreateUser();
        var accessToken = Fixture.Create<AccessTokenDto>();
        LogArrange(nameof(user), user);
        LogArrange(nameof(accessToken), accessToken);

        _accessTokenServiceMock.Setup(a => a.GenerateAccessTokenAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AccessTokenDto>.Success(accessToken));
        _refreshTokenServiceMock.Setup(r => r.GenerateRefreshTokenAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RefreshToken>.Failure("no changes where applied to the DB", HttpStatusCode.InternalServerError));

        // Act
        var result = await _tokenService.IssueTokensAsync(user);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("no changes where applied to the DB");
    }

    [Fact]
    public async Task IssueTokensAsync_ShouldReturnBothTokens_WhenAllStepsSucceed()
    {
        // Arrange
        var user = CreateUser();
        var accessToken = Fixture.Create<AccessTokenDto>();
        var refreshToken = new RefreshToken
        {
            RefreshTokenString = Fixture.Create<string>(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            UserId = user.Id
        };
        LogArrange(nameof(accessToken), accessToken);
        LogArrange(nameof(refreshToken), refreshToken);

        _accessTokenServiceMock.Setup(a => a.GenerateAccessTokenAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AccessTokenDto>.Success(accessToken));
        _refreshTokenServiceMock.Setup(r => r.GenerateRefreshTokenAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<RefreshToken>.Success(refreshToken));

        // Act
        var result = await _tokenService.IssueTokensAsync(user);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.AccessToken.Should().Be(accessToken.AccessToken);
        result.Value.AccessTokenExpiresAt.Should().Be(accessToken.ExpiresAt);
        result.Value.RefreshToken.Should().Be(refreshToken.RefreshTokenString);
        result.Value.RefreshTokenExpiresAt.Should().Be(refreshToken.ExpiresAt);
    }
}