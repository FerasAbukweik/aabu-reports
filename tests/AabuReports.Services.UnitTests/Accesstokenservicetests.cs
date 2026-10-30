using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;
using AabuReports.Core.Options;
using AabuReports.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class AccessTokenServiceTests : ServiceTestBase
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly JwtOptions _jwtOptions;

    public AccessTokenServiceTests(ITestOutputHelper output) : base(output)
    {
        _userManagerMock = CreateUserManagerMock();
        _jwtOptions = CreateJwtOptions().Value;
    }

    private AccessTokenService CreateService(string? key = null)
    {
        var options = key is null
            ? Microsoft.Extensions.Options.Options.Create(_jwtOptions)
            : CreateJwtOptions(key);

        return new AccessTokenService(NullLogger<AccessTokenService>.Instance, _userManagerMock.Object, options);
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldReturnFailure_WhenUserNameIsMissing()
    {
        // Arrange
        var user = CreateUser();
        user.UserName = null;
        LogArrange(nameof(user), user);

        // Act
        var result = await CreateService().GenerateAccessTokenAsync(user);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("user missing username or email");
        _userManagerMock.Verify(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldReturnFailure_WhenEmailIsMissing()
    {
        // Arrange
        var user = CreateUser();
        user.Email = null;
        LogArrange(nameof(user), user);

        // Act
        var result = await CreateService().GenerateAccessTokenAsync(user);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("user missing username or email");
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldReturnFailure_WhenUserHasNoRoles()
    {
        // Arrange
        var user = CreateUser();
        LogArrange(nameof(user), user);

        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());

        // Act
        var result = await CreateService().GenerateAccessTokenAsync(user);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("user has no roles");
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldReturnInternalServerError_WhenSigningKeyIsNotConfigured()
    {
        // Arrange
        var user = CreateUser();
        LogArrange(nameof(user), user);

        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { nameof(AppRole.Student) });

        // Act
        var result = await CreateService(key: "").GenerateAccessTokenAsync(user);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("Token configuration error.");
    }

    [Fact]
    public async Task GenerateAccessTokenAsync_ShouldReturnValidSignedToken_WhenUserIsValid()
    {
        // Arrange
        var user = CreateUser();
        IList<string> roles = new List<string> { nameof(AppRole.Student), nameof(AppRole.Admin) };
        LogArrange(nameof(user), user);
        LogArrange(nameof(roles), roles);

        _userManagerMock.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles);

        // Act
        var result = await CreateService().GenerateAccessTokenAsync(user);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddSeconds(_jwtOptions.AccessTokenLifeTimeInSeconds), TimeSpan.FromSeconds(5));

        // the signature, issuer, audience and lifetime must all validate with the configured key
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(result.Value.AccessToken, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key)),
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        }, out _);

        var jwt = handler.ReadJwtToken(result.Value.AccessToken);
        jwt.Subject.Should().Be(user.Id.ToString());

        var claimValues = principal.Claims.Select(c => c.Value).ToList();
        claimValues.Should().Contain(user.Id.ToString());
        claimValues.Should().Contain(user.UserName!);
        claimValues.Should().Contain(user.Email!);
        claimValues.Should().Contain(user.StudentId);
        claimValues.Should().Contain(roles);
    }
}