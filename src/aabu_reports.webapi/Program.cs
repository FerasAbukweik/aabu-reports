using aabu_reports;
using aabu_reports.core;
using aabu_reports.infrastructure;
using aabu_reports.webapi;
using aabu_reports.webapi.ExtensionMethods;
using aabu_reports.webapi.Middlewares;
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.Services.SeedAdminAsync(app.Configuration);

app.Run();
