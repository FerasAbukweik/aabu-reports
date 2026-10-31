using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Webapi.ExtensionMethods;
using AabuReports.Webapi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AabuReports.Webapi.Controllers;

public class AuthController(IAuthService authService, IEmailVerificationService verificationService) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto loginDto, CancellationToken cancellationToken)
    {
        var result = await authService.Login(loginDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("verification")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailDto dto, CancellationToken cancellationToken)
    {
        var result = await authService.VerifyEmail(dto.StudentId, dto.Code, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("resend-code")]
    [AllowAnonymous]
    [EnableRateLimiting("verification")]
    public async Task<IActionResult> ResendCode(ResendCodeDto dto, CancellationToken cancellationToken)
    {
        var result = await verificationService.ResendAsync(dto.StudentId, cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [Transactional]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await authService.Refresh(cancellationToken);

        return result.ToActionResult();
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var result = await authService.Logout(cancellationToken);

        return result.ToActionResult();
    }
}
