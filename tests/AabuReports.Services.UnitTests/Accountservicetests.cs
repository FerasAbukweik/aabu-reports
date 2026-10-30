using System.Net;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Accounts;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.Services;
using AutoFixture;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace AabuReports.Services.UnitTests;

public class AccountServiceTests : ServiceTestBase
{
    private readonly IAccountService _accountService;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<RoleManager<ApplicationRole>> _roleManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ICookieService> _cookieServiceMock;

    public AccountServiceTests(ITestOutputHelper output) : base(output)
    {
        _userManagerMock = CreateUserManagerMock();
        _roleManagerMock = CreateRoleManagerMock();
        _tokenServiceMock = new Mock<ITokenService>();
        _cookieServiceMock = new Mock<ICookieService>();

        _accountService = new AccountService(
            _userManagerMock.Object,
            _roleManagerMock.Object,
            _tokenServiceMock.Object,
            _cookieServiceMock.Object,
            NullLogger<AccountService>.Instance);
    }

    private RegisterDto CreateRegisterDto() => new()
    {
        // padded with spaces on purpose, the service must trim them
        StudentId = $"  {NewStudentId()}  ",
        Password = "P@ssw0rd!123",
        Email = $"  {Fixture.Create<string>()[..8]}@aabu.edu.jo  ",
        FullName = $"  {Fixture.Create<string>()}  "
    };

    private void SetupNoExistingUser()
    {
        _userManagerMock.Setup(m => m.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
        _userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
    }

    private void SetupRoleExists()
    {
        _roleManagerMock.Setup(r => r.RoleExistsAsync(It.IsAny<string>())).ReturnsAsync(true);
    }

    private void SetupUserCreationSucceeds()
    {
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
    }

    #region Register

    [Theory]
    [InlineData(true, false, "Already in use: student id")]
    [InlineData(false, true, "Already in use: email")]
    [InlineData(true, true, "Already in use: student id, email")]
    public async Task Register_ShouldReturnConflict_WhenInputsAreAlreadyUsed(bool studentIdUsed, bool emailUsed, string expectedMessage)
    {
        // Arrange
        var dto = CreateRegisterDto();
        LogArrange(nameof(dto), dto);

        _userManagerMock.Setup(m => m.FindByNameAsync(dto.StudentId.Trim()))
            .ReturnsAsync(studentIdUsed ? CreateUser() : null);
        _userManagerMock.Setup(m => m.FindByEmailAsync(dto.Email.Trim()))
            .ReturnsAsync(emailUsed ? CreateUser() : null);

        // Act
        var result = await _accountService.Register(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorMessage.Should().Be(expectedMessage);

        _userManagerMock.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_ShouldReturnInternalServerError_WhenRoleCannotBeCreated()
    {
        // Arrange
        var dto = CreateRegisterDto();
        LogArrange(nameof(dto), dto);
        SetupNoExistingUser();

        // role does not exist, creating it fails, and it still does not exist when checked again
        _roleManagerMock.SetupSequence(r => r.RoleExistsAsync(nameof(AppRole.Student)))
            .ReturnsAsync(false)
            .ReturnsAsync(false);
        _roleManagerMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationRole>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "db down" }));

        // Act
        var result = await _accountService.Register(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("Failed to complete registration");

        _userManagerMock.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_ShouldContinue_WhenRoleWasCreatedByAnotherRequestAtTheSameTime()
    {
        // Arrange
        var dto = CreateRegisterDto();
        LogArrange(nameof(dto), dto);
        SetupNoExistingUser();
        SetupUserCreationSucceeds();

        // first check: missing, creation fails (someone else won the race), second check: exists
        _roleManagerMock.SetupSequence(r => r.RoleExistsAsync(nameof(AppRole.Student)))
            .ReturnsAsync(false)
            .ReturnsAsync(true);
        _roleManagerMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationRole>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "duplicate role" }));
        _tokenServiceMock.Setup(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponseDto>.Success(CreateAuthResponse()));

        // Act
        var result = await _accountService.Register(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Register_ShouldCreateRole_WhenRoleDoesNotExist()
    {
        // Arrange
        var dto = CreateRegisterDto();
        LogArrange(nameof(dto), dto);
        SetupNoExistingUser();
        SetupUserCreationSucceeds();

        _roleManagerMock.Setup(r => r.RoleExistsAsync(nameof(AppRole.Student))).ReturnsAsync(false);
        _roleManagerMock.Setup(r => r.CreateAsync(It.IsAny<ApplicationRole>())).ReturnsAsync(IdentityResult.Success);
        _tokenServiceMock.Setup(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponseDto>.Success(CreateAuthResponse()));

        // Act
        var result = await _accountService.Register(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _roleManagerMock.Verify(
            r => r.CreateAsync(It.Is<ApplicationRole>(role => role.Name == nameof(AppRole.Student))),
            Times.Once);
    }

    [Fact]
    public async Task Register_ShouldReturnBadRequest_WhenUserCreationFails()
    {
        // Arrange
        var dto = CreateRegisterDto();
        LogArrange(nameof(dto), dto);
        SetupNoExistingUser();
        SetupRoleExists();

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Description = "weak password" },
                new IdentityError { Description = "no digit" }));

        // Act
        var result = await _accountService.Register(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorMessage.Should().Be("weak password; no digit");

        _userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_ShouldReturnInternalServerError_WhenAddingRoleFails()
    {
        // Arrange
        var dto = CreateRegisterDto();
        LogArrange(nameof(dto), dto);
        SetupNoExistingUser();
        SetupRoleExists();

        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(AppRole.Student)))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "role assignment failed" }));

        // Act
        var result = await _accountService.Register(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        // the real reason is only logged, the client gets a generic message
        result.ErrorMessage.Should().Be("Failed to complete registration");

        _tokenServiceMock.Verify(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Register_ShouldReturnFailureAndNotSetCookies_WhenIssuingTokensFails()
    {
        // Arrange
        var dto = CreateRegisterDto();
        LogArrange(nameof(dto), dto);
        SetupNoExistingUser();
        SetupRoleExists();
        SetupUserCreationSucceeds();

        _tokenServiceMock.Setup(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponseDto>.Failure("token generation failed", HttpStatusCode.InternalServerError));

        // Act
        var result = await _accountService.Register(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorMessage.Should().Be("token generation failed");

        _cookieServiceMock.Verify(c => c.SetAuthCookies(It.IsAny<AuthResponseDto>()), Times.Never);
    }

    [Fact]
    public async Task Register_ShouldCreateTrimmedStudentUserAndSetCookies_WhenAllStepsSucceed()
    {
        // Arrange
        var dto = CreateRegisterDto();
        var tokens = CreateAuthResponse();
        LogArrange(nameof(dto), dto);
        LogArrange(nameof(tokens), tokens);

        SetupNoExistingUser();
        SetupRoleExists();

        ApplicationUser? created = null;
        _userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), dto.Password))
            .Callback<ApplicationUser, string>((u, _) => created = u)
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), nameof(AppRole.Student)))
            .ReturnsAsync(IdentityResult.Success);
        _tokenServiceMock.Setup(t => t.IssueTokensAsync(It.IsAny<ApplicationUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthResponseDto>.Success(tokens));

        // Act
        var result = await _accountService.Register(dto);
        LogAct(result);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Created);

        created.Should().NotBeNull();
        created!.StudentId.Should().Be(dto.StudentId.Trim());
        created.UserName.Should().Be(dto.StudentId.Trim());
        created.Email.Should().Be(dto.Email.Trim());
        created.FullName.Should().Be(dto.FullName.Trim());

        _userManagerMock.Verify(m => m.AddToRoleAsync(created, nameof(AppRole.Student)), Times.Once);
        _cookieServiceMock.Verify(c => c.SetAuthCookies(tokens), Times.Once);
    }

    #endregion
}