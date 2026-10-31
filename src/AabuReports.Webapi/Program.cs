using AabuReports.Core;
using AabuReports.Infrastructure;
using AabuReports.Webapi.ExtensionMethods;
using AabuReports.Webapi.Hubs;
using AabuReports.Webapi.Middlewares;
using AabuReports.Webapi;
using AabuReports.Webapi.Hubs.MessagesHub;
using AabuReports.Webapi.Hubs.NotificationHub;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);


// add serilog
builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));

// Dependency Injection
builder.Services
    .AddCore()
    .AddInfrastructure(builder.Configuration)
    .AddWebApi(builder.Configuration);


var app = builder.Build();

app.UseGlobalExceptionMiddleWare();

if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notification");
app.MapHub<MessageHub>("/hubs/message");

await app.Services.SeedAdminAsync(app.Configuration);

app.Run();
