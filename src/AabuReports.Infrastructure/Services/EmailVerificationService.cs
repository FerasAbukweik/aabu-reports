using System.Net;
using System.Security.Cryptography;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Entities.Identity;
using AabuReports.Core.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace AabuReports.Infrastructure.Services;

public class EmailVerificationService(
    IRedisService redis,
    IEmailService emailService,
    IConfiguration configuration,
    UserManager<ApplicationUser> userManager,
    ILogger<EmailVerificationService> logger) : IEmailVerificationService
{
    private const string InvalidCode = "Invalid or expired verification code. Request a new code and try again.";

    private static string Key(string studentId) => $"auth:verification:{studentId.Trim()}";

    public Task<Result> BeginAsync(ApplicationUser user, CancellationToken cancellationToken = default) =>
        SendAsync(user, cancellationToken);

    public async Task<Result> ResendAsync(string studentId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(studentId.Trim());
        if (user is null || await userManager.IsLockedOutAsync(user)) return Result.Failure(InvalidCode);
        if (await redis.GetAsync<string>(Key(user.StudentId), cancellationToken) is null)
            return Result.Failure("Verification expired. Please log in again to request a code.");
        return await SendAsync(user, cancellationToken);
    }

    public async Task<Result<ApplicationUser>> VerifyAsync(string studentId, string code, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(studentId.Trim());
        if (user is null || await userManager.IsLockedOutAsync(user))
            return Result<ApplicationUser>.Failure(InvalidCode);

        var savedCode = await redis.GetAsync<string>(Key(user.StudentId), cancellationToken);
        if (savedCode is null) return Result<ApplicationUser>.Failure(InvalidCode);
        if (savedCode != code)
        {
            await userManager.AccessFailedAsync(user);
            return Result<ApplicationUser>.Failure(InvalidCode);
        }

        return Result<ApplicationUser>.Success(user);
    }

    private async Task<Result> SendAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return Result.Failure("This account has no email address.");
        
        // random 6 digits number
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        
        // Expiry starts when the new code is successfully sent and stored.
        var lifetime = TimeSpan.FromMinutes(configuration.GetValue("Email:VerificationCodeLifetimeMinutes", 5));
        
        try
        {
            await emailService.SendVerificationCodeAsync(user.Email, code, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "{ServiceName}.{MethodName} failed to send verification email for student {StudentId}",
                nameof(EmailVerificationService), nameof(SendAsync), user.StudentId);
            return Result.Failure("Could not send verification email. Please try again.", HttpStatusCode.ServiceUnavailable);
        }
        await redis.SetAsync(Key(user.StudentId), code, cancellationToken, lifetime, lifetime);
        return Result.Success();
    }

}
