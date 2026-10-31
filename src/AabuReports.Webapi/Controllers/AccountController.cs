using System.Security.Claims;
using AabuReports.Core.Abstractions.Services;
using AabuReports.Core.Dtos.Accounts;
using AabuReports.Core.Enums;
using AabuReports.Webapi.ExtensionMethods;
using AabuReports.Webapi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AabuReports.Webapi.Controllers;

public class AccountController(IAccountService accountService) : ApiControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting("verification")]
    [AllowAnonymous]
    [Transactional]
    public async Task<IActionResult> Register(RegisterDto registerDto, CancellationToken cancellationToken)
    {
        var result = await accountService.Register(registerDto, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            StudentId = User.FindFirstValue("StudentId"),
            Email = User.FindFirstValue(ClaimTypes.Email),
            Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value)
        });
    }

    [HttpPost("is-admin")]
    [Authorize]
    public ActionResult IsAdmin()
    {
        if (User.IsAdmin()) return Ok();

        return Forbid();
    }
}
