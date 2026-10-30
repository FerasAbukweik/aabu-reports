using AabuReports.Core.Entities;
using AabuReports.Infrastructure.Repositories;
using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace AabuReports.Repository.UnitTests;

public class RefreshTokenRepositoryTests : RepositoryTestBase
{
    private readonly RefreshTokenRepository _repository;

    public RefreshTokenRepositoryTests(ITestOutputHelper output) : base(output)
    {
        _repository = new RefreshTokenRepository(_dbContext);
    }

    [Fact]
    public async Task AddRefreshToken_ShouldPersistTheToken_WhenChangesAreSaved()
    {
        // Arrange
        var user = await SeedUserAsync();
        var token = new RefreshToken
        {
            RefreshTokenString = _fixture.Create<string>(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            UserId = user.Id
        };
        LogArrange(nameof(token), token);

        // Act
        _repository.AddRefreshToken(token);
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var stored = await _dbContext.RefreshTokens.SingleAsync(t => t.Id == token.Id);
        stored.RefreshTokenString.Should().Be(token.RefreshTokenString);
        stored.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task AddRefreshToken_ShouldThrow_WhenTheTokenStringAlreadyExists()
    {
        // Arrange
        var user = await SeedUserAsync();
        var existing = await SeedRefreshTokenAsync(user.Id);
        LogArrange(nameof(existing), existing);

        _repository.AddRefreshToken(new RefreshToken
        {
            RefreshTokenString = existing.RefreshTokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            UserId = user.Id
        });

        // Act
        var act = () => _repository.SaveChangesAsync();
        LogAct("expecting the save to throw");

        // Assert: the token string has a unique index
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldReturnFalse_WhenNothingChanged()
    {
        LogArrange("pending changes", 0);

        // Act
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeFalse();
    }

    [Fact]
    public async Task GetByTokenAsync_ShouldReturnTheTokenWithItsUserAndNotTrackIt()
    {
        // Arrange
        var user = await SeedUserAsync();
        var token = await SeedRefreshTokenAsync(user.Id);
        LogArrange(nameof(token), token);

        // Act
        var result = await _repository.GetByTokenAsync(token.RefreshTokenString);
        LogAct(result);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(token.Id);
        result.User.Should().NotBeNull();
        result.User!.Id.Should().Be(user.Id);
        _dbContext.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetByTokenAsync_ShouldReturnNull_WhenTokenDoesNotExist()
    {
        var tokenString = _fixture.Create<string>();
        LogArrange(nameof(tokenString), tokenString);

        // Act
        var result = await _repository.GetByTokenAsync(tokenString);
        LogAct(result);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveRefreshToken_ShouldDeleteTheRow_WhenChangesAreSaved()
    {
        // Arrange
        var user = await SeedUserAsync();
        var token = await SeedRefreshTokenAsync(user.Id);
        LogArrange(nameof(token), token);

        var loaded = await _repository.GetByTokenAsync(token.RefreshTokenString);

        // Act
        _repository.RemoveRefreshToken(loaded!);
        var saved = await _repository.SaveChangesAsync();
        LogAct(saved);

        // Assert
        saved.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        (await _dbContext.RefreshTokens.AnyAsync(t => t.Id == token.Id)).Should().BeFalse();
        (await _dbContext.Users.AnyAsync(u => u.Id == user.Id)).Should().BeTrue(); // the user stays
    }

    [Fact]
    public async Task RemoveRefreshToken_ShouldThrowConcurrencyException_WhenAnotherRequestDeletedTheRowFirst()
    {
        // Arrange
        var user = await SeedUserAsync();
        var token = await SeedRefreshTokenAsync(user.Id);
        LogArrange(nameof(token), token);

        var loaded = await _repository.GetByTokenAsync(token.RefreshTokenString);

        // another request uses the same token first
        await _dbContext.RefreshTokens.Where(t => t.Id == token.Id).ExecuteDeleteAsync();

        _repository.RemoveRefreshToken(loaded!);

        // Act
        var act = () => _repository.SaveChangesAsync();
        LogAct("expecting the save to throw");

        // Assert: this is what RefreshTokenService relies on to reject a reused token
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task RemoveExpiredRefreshTokensAsync_ShouldDeleteOnlyExpiredTokens()
    {
        // Arrange
        var user = await SeedUserAsync();
        var expired1 = await SeedRefreshTokenAsync(user.Id, DateTime.UtcNow.AddMinutes(-5));
        var expired2 = await SeedRefreshTokenAsync(user.Id, DateTime.UtcNow.AddDays(-3));
        var valid = await SeedRefreshTokenAsync(user.Id, DateTime.UtcNow.AddDays(1));

        LogArrange("arranged data", new { user, expired1, expired2, valid });

        // Act
        var removed = await _repository.RemoveExpiredRefreshTokensAsync();
        LogAct(removed);

        // Assert
        removed.Should().BeTrue();

        _dbContext.ChangeTracker.Clear();
        var left = await _dbContext.RefreshTokens.Select(t => t.Id).ToListAsync();
        left.Should().BeEquivalentTo(new[] { valid.Id });
        left.Should().NotContain(new[] { expired1.Id, expired2.Id });
    }

    [Fact]
    public async Task RemoveExpiredRefreshTokensAsync_ShouldReturnFalse_WhenNothingIsExpired()
    {
        // Arrange
        var user = await SeedUserAsync();
        await SeedRefreshTokenAsync(user.Id, DateTime.UtcNow.AddDays(1));

        LogArrange(nameof(user), user);

        // Act
        var removed = await _repository.RemoveExpiredRefreshTokensAsync();
        LogAct(removed);

        // Assert
        removed.Should().BeFalse();
    }
}