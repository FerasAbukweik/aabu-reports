using System.Net;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Accounts;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Enums;
using AabuReports.Core.Results;
using AabuReports.Infrastructure.ExtensionMethods;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AabuReports.Infrastructure.Services;

public class AccountService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ITokenService tokenService,
    ICookieService cookieService,
    ILogger<AccountService> logger) : IAccountService
{
    /// <remarks>
    /// the endpoint calling this must use [Transactional], so the user is rolled back if adding the role fails
    /// </remarks>
    public async Task<Result> Register(RegisterDto registerDto, CancellationToken cancellationToken = default)
    {
        var studentId = registerDto.StudentId.Trim();
        var email = registerDto.Email.Trim();

        var usedInputs = await GetAlreadyUsedInputsAsync(studentId, email);
        if (usedInputs is not null)
            return Result.Failure($"Already in use: {usedInputs}", HttpStatusCode.Conflict);

        // the access token needs at least one role, so make sure the student role exists
        var roleResult = await EnsureRoleExistsAsync(AppRole.Student);
        if (!roleResult.IsSuccess)
            return roleResult;

        var user = new ApplicationUser
        {
            StudentId = studentId,
            FullName = registerDto.FullName.Trim(),
            UserName = studentId,
            Email = email
        };

        var createResult = await userManager.CreateAsync(user, registerDto.Password);
        if (!createResult.Succeeded)
            return Result.Failure(createResult.DescribeErrors());

        var addRoleResult = await userManager.AddToRoleAsync(user, AppRole.Student.ToString());
        if (!addRoleResult.Succeeded)
        {
            logger.LogError("{ServiceName}.{MethodName} failed adding role to user with Id: {UserId}. Errors: {Errors}",
                nameof(AccountService), nameof(Register), user.Id.ToString(), addRoleResult.DescribeErrors());

            return Result.Failure("Failed to complete registration", HttpStatusCode.InternalServerError);
        }
        
        var tokensResult = await tokenService.IssueTokensAsync(user, cancellationToken);
        if (!tokensResult.IsSuccess) return tokensResult;

        cookieService.SetAuthCookies(tokensResult.Value!);

        return Result.Success(HttpStatusCode.Created);
    }

    // private

    /// <returns> the inputs that are already used by another user (e.g. "student id, email"), or null if none </returns>
    private async Task<string?> GetAlreadyUsedInputsAsync(string studentId, string email)
    {
        var usedInputs = new List<string>();

        if (await userManager.FindByNameAsync(studentId) is not null)
            usedInputs.Add("student id");

        if (await userManager.FindByEmailAsync(email) is not null)
            usedInputs.Add("email");

        return usedInputs.Count == 0 ? null : string.Join(", ", usedInputs);
    }

    private async Task<Result> EnsureRoleExistsAsync(AppRole role)
    {
        var roleName = role.ToString();

        if (await roleManager.RoleExistsAsync(roleName))
            return Result.Success();

        var createResult = await roleManager.CreateAsync(new ApplicationRole { Name = roleName });

        // another request may have created the role at the same time
        if (createResult.Succeeded || await roleManager.RoleExistsAsync(roleName))
            return Result.Success();

        logger.LogError("{ServiceName}.{MethodName} failed creating role {RoleName}. Errors: {Errors}",
            nameof(AccountService), nameof(EnsureRoleExistsAsync), roleName, createResult.DescribeErrors());

        return Result.Failure("Failed to complete registration", HttpStatusCode.InternalServerError);
    }
}
