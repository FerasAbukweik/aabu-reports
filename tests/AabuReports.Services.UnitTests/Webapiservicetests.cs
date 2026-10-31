using System.ComponentModel.DataAnnotations;
using AabuReports.Core.Dtos.Auth;
using AabuReports.Core.Dtos.LazyLoading;
using AabuReports.Webapi.Middlewares;
using AabuReports.Webapi.Services;
using AabuReports.Webapi.Settings;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AabuReports.Services.UnitTests;

public class WebApiServiceTests
{
    [Fact]
    public void AuthCookies_ShouldScopeRefreshToAuthEndpoints_AndExpireLegacyCookie()
    {
        var context = new DefaultHttpContext();
        var settings = new AuthCookieSettings();
        var service = new CookieService(new HttpContextAccessor { HttpContext = context }, Options.Create(settings));

        service.SetAuthCookies(new AuthResponseDto
        {
            AccessToken = "access", AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
            RefreshToken = "refresh", RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(1)
        });

        var headers = context.Response.Headers.SetCookie.ToArray();
        headers.Should().Contain(h => h!.StartsWith(settings.RefreshTokenCookieName + "=refresh")
            && h.Contains("path=/api/auth;") && h.Contains("httponly") && h.Contains("secure"));
        new PathString("/api/auth/login").StartsWithSegments(settings.RefreshTokenPath).Should().BeTrue();
        new PathString("/api/auth/refresh").StartsWithSegments(settings.RefreshTokenPath).Should().BeTrue();
        new PathString("/api/auth/logout").StartsWithSegments(settings.RefreshTokenPath).Should().BeTrue();
    }

    [Fact]
    public void ClearAuthCookies_ShouldDeleteCurrentAndLegacyRefreshCookies()
    {
        var context = new DefaultHttpContext();
        var settings = new AuthCookieSettings();
        var service = new CookieService(new HttpContextAccessor { HttpContext = context }, Options.Create(settings));

        service.ClearAuthCookies();

        var headers = context.Response.Headers.SetCookie.ToArray();
        headers.Should().Contain(h => h!.Contains("path=/api/auth;") && h.Contains("1970"));
        headers.Should().Contain(h => h!.Contains("path=/api/auth/refresh;") && h.Contains("1970"));
    }

    [Fact]
    public async Task ExceptionMiddleware_ShouldReturnGenericErrorWithoutInternalDetails()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleWare(_ => throw new InvalidOperationException("private database details"),
            NullLogger<GlobalExceptionMiddleWare>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("An unexpected error occurred").And.NotContain("private database details");
    }

    [Fact]
    public async Task ExceptionMiddleware_ShouldNotWriteServerErrorForDisconnectedClient()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        context.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleWare(_ => throw new OperationCanceledException(),
            NullLogger<GlobalExceptionMiddleWare>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Length.Should().Be(0);
        context.Response.StatusCode.Should().NotBe(500);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(101, 0, false)]
    [InlineData(20, -1, false)]
    [InlineData(20, 0, true)]
    [InlineData(100, 200, true)]
    public void Pagination_ShouldRejectUnboundedOrNegativeRequests(int take, int skip, bool valid)
    {
        var dto = new LazyLoadingDto { Take = take, Skip = skip };
        Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), true).Should().Be(valid);
    }
}
