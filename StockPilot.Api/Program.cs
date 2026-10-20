
using Scalar.AspNetCore;
using StockPilot.Api.Middleware;
using StockPilot.Api.Extensions;
using StockPilot.Api.Endpoints;





var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAppCors(builder.Configuration);
builder.Services.AddProductImport();

var app = builder.Build();
app.UseRenderForwardedHeaders();
app.MapScalarApiReference().AllowAnonymous();
app.MapOpenApi().AllowAnonymous();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseCors(AppBuilderServiceExtensions.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapApiEndpoints();
await app.WarmUpDatabaseAsync();
app.Run();
public partial class Program { }
