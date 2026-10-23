using System.Security.Claims;
using aabu_reports.core.DTOs.Account;
using aabu_reports.core.Interfaces.ServiceContracts;
using aabu_reports.webapi.ExtensionMethods;
using aabu_reports.webapi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace aabu_reports.webapi.Controllers;

public class AccountController(IAccountService accountService) : ApiControllerBase
{
    [HttpPost("register")]
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
}
