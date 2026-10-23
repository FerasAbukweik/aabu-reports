using aabu_reports.core.DTOs.Account;
using aabu_reports.core.Interfaces.ServiceContracts;
using aabu_reports.webapi.ExtensionMethods;
using aabu_reports.webapi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace aabu_reports.webapi.Controllers;

public class AuthController(IAuthService authService) : ApiControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto loginDto, CancellationToken cancellationToken)
    {
        var result = await authService.Login(loginDto, cancellationToken);

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
