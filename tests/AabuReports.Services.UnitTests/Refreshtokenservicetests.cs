using System.Net;
using AabuReports.Core.Abstractions.Repositories;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Entities;
using AabuReports.Core.Options;
using AabuReports.Infrastructure.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class RefreshTokenServiceTests : ServiceTestBase
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly Mock<IRefreshTokenRepository> _repositoryMock;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenServiceTests(ITestOutputHelper output) : base(output)
    {
        _repositoryMock = new Mock<IRefreshTokenRepository>();

        var options = CreateJwtOptions();
        _jwtOptions = options.Value;

        _refreshTokenService = new RefreshTokenService(
            _repositoryMock.Object,
            NullLogger<RefreshTokenService>.Instance,
            options);
    }

    private RefreshToken CreateToken(DateTime expiresAt) => new()
    {
        RefreshTokenString = Fixture.Create<string>(),
        ExpiresAt = expiresAt,
        UserId = Guid.NewGuid()
    };

    #region GenerateRefreshTokenAsync

    [Fact]
    public async Task GenerateRefreshTokenAsync_ShouldReturnInternalServerError_WhenNothingWasSaved()
    {
        // Arrange
        var userId = Guid.NewGuid();
        LogArrange(nameof(userId), userId);

        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        var result = await _refreshTokenService.GenerateRefreshTokenAsync(userId);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("no changes where applied to the DB");
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_ShouldSaveRandomTokenForUser_WhenSaveSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        LogArrange(nameof(userId), userId);

        RefreshToken? added = null;
        _repositoryMock.Setup(r => r.AddRefreshToken(It.IsAny<RefreshToken>()))
            .Callback<RefreshToken>(t => added = t);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _refreshTokenService.GenerateRefreshTokenAsync(userId);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(added);
        result.Value!.UserId.Should().Be(userId);
        result.Value.ExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddSeconds(_jwtOptions.RefreshTokenLifeTimeInSeconds), TimeSpan.FromSeconds(5));

        // 32 random bytes -> 44 base64 characters
        result.Value.RefreshTokenString.Should().HaveLength(44);
        Convert.FromBase64String(result.Value.RefreshTokenString).Should().HaveCount(32);

        _repositoryMock.Verify(r => r.AddRefreshToken(It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateRefreshTokenAsync_ShouldGenerateDifferentTokensEachTime()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var first = await _refreshTokenService.GenerateRefreshTokenAsync(userId);
        var second = await _refreshTokenService.GenerateRefreshTokenAsync(userId);
        LogAct(new { first = first.Value?.RefreshTokenString, second = second.Value?.RefreshTokenString });

        // Assert
        first.Value!.RefreshTokenString.Should().NotBe(second.Value!.RefreshTokenString);
    }

    #endregion

    #region ConsumeRefreshTokenAsync

    [Fact]
    public async Task ConsumeRefreshTokenAsync_ShouldReturnUnauthorized_WhenTokenDoesNotExist()
    {
        // Arrange
        var tokenString = Fixture.Create<string>();
        LogArrange(nameof(tokenString), tokenString);

        _repositoryMock.Setup(r => r.GetByTokenAsync(tokenString, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        // Act
        var result = await _refreshTokenService.ConsumeRefreshTokenAsync(tokenString);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorMessage.Should().Be("Invalid refresh token");

        _repositoryMock.Verify(r => r.RemoveRefreshToken(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeRefreshTokenAsync_ShouldReturnUnauthorized_WhenTokenWasAlreadyDeletedByAnotherRequest()
    {
        // Arrange
        var token = CreateToken(DateTime.UtcNow.AddDays(1));
        LogArrange(nameof(token), token);

        _repositoryMock.Setup(r => r.GetByTokenAsync(token.RefreshTokenString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("row already deleted"));

        // Act
        var result = await _refreshTokenService.ConsumeRefreshTokenAsync(token.RefreshTokenString);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorMessage.Should().Be("Invalid refresh token");
    }

    [Fact]
    public async Task ConsumeRefreshTokenAsync_ShouldDeleteTokenAndReturnUnauthorized_WhenTokenIsExpired()
    {
        // Arrange
        var token = CreateToken(DateTime.UtcNow.AddMinutes(-5));
        LogArrange(nameof(token), token);

        _repositoryMock.Setup(r => r.GetByTokenAsync(token.RefreshTokenString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _refreshTokenService.ConsumeRefreshTokenAsync(token.RefreshTokenString);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorMessage.Should().Be("Refresh token expired");

        // an expired token is removed too
        _repositoryMock.Verify(r => r.RemoveRefreshToken(token), Times.Once);
    }

    [Fact]
    public async Task ConsumeRefreshTokenAsync_ShouldDeleteTokenAndReturnIt_WhenTokenIsValid()
    {
        // Arrange
        var token = CreateToken(DateTime.UtcNow.AddDays(1));
        LogArrange(nameof(token), token);

        _repositoryMock.Setup(r => r.GetByTokenAsync(token.RefreshTokenString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _refreshTokenService.ConsumeRefreshTokenAsync(token.RefreshTokenString);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(token);

        _repositoryMock.Verify(r => r.RemoveRefreshToken(token), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region RevokeRefreshTokenAsync

    [Fact]
    public async Task RevokeRefreshTokenAsync_ShouldReturnNoContentWithoutDeleting_WhenTokenDoesNotExist()
    {
        // Arrange
        var tokenString = Fixture.Create<string>();
        LogArrange(nameof(tokenString), tokenString);

        _repositoryMock.Setup(r => r.GetByTokenAsync(tokenString, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        // Act
        var result = await _refreshTokenService.RevokeRefreshTokenAsync(tokenString);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _repositoryMock.Verify(r => r.RemoveRefreshToken(It.IsAny<RefreshToken>()), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_ShouldDeleteToken_WhenTokenExists()
    {
        // Arrange
        var token = CreateToken(DateTime.UtcNow.AddDays(1));
        LogArrange(nameof(token), token);

        _repositoryMock.Setup(r => r.GetByTokenAsync(token.RefreshTokenString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var result = await _refreshTokenService.RevokeRefreshTokenAsync(token.RefreshTokenString);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _repositoryMock.Verify(r => r.RemoveRefreshToken(token), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_ShouldStillSucceed_WhenTokenWasAlreadyDeletedByAnotherRequest()
    {
        // Arrange
        var token = CreateToken(DateTime.UtcNow.AddDays(1));
        LogArrange(nameof(token), token);

        _repositoryMock.Setup(r => r.GetByTokenAsync(token.RefreshTokenString, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("row already deleted"));

        // Act
        var result = await _refreshTokenService.RevokeRefreshTokenAsync(token.RefreshTokenString);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    #endregion
}