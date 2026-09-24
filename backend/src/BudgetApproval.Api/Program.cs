using System.Reflection;
using System.Text.Json.Serialization;
using BudgetApproval.Api.Auth;
using BudgetApproval.Api.Infrastructure;
using BudgetApproval.Application.Abstractions;
using BudgetApproval.Application.Services;
using BudgetApproval.Infrastructure;
using BudgetApproval.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------ configuration
var provider = builder.Configuration.GetValue("Database:Provider", DatabaseProvider.Sqlite);
var connectionString = builder.Configuration.GetConnectionString(provider.ToString())
    ?? throw new InvalidOperationException($"Missing connection string 'ConnectionStrings:{provider}'.");
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

// ------------------------------------------------------------------ services
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddInfrastructure(provider, connectionString);
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<BudgetRequestService>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReferenceDataService>();

builder.Services
    .AddAuthentication(DemoAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(DemoAuthenticationHandler.SchemeName, null);

builder.Services.AddAuthorization(options =>
{
    // Secure by default: every endpoint requires a signed-in user unless it opts out with [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.AddPolicy(Policies.Approver, p => p.RequireRole(Policies.Approver));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyMethod()
    .WithHeaders("Content-Type", DemoAuthenticationHandler.HeaderName)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Budget Planning & Approval API",
        Version = "v1",
        Description = "Vertical slice for the fictional Northbridge Regional Services Agency. " +
                      "Click Authorize and enter a demo user id (1–6) in the X-Demo-User header."
    });
    c.AddSecurityDefinition(DemoAuthenticationHandler.SchemeName, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = DemoAuthenticationHandler.HeaderName,
        Description = "Demo persona: 1 Maya (ITS), 2 Luis (FAC), 3 Priya (OUT), 4 Samuel (HCM), 5 Dana (RSA, approver), 6 Marcus (approver)"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = DemoAuthenticationHandler.SchemeName } }] = new List<string>()
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

var app = builder.Build();

// Header-based demo authentication must never reach a shared environment by accident.
if (!app.Environment.IsDevelopment() && !app.Configuration.GetValue<bool>("Authentication:AllowDemoAuthOutsideDevelopment"))
    throw new InvalidOperationException(
        "Demo header authentication is enabled outside Development. Configure a real identity provider (see docs/security.md) " +
        "or set Authentication:AllowDemoAuthOutsideDevelopment=true for a throwaway review environment.");

// ------------------------------------------------------------------ database
// EnsureCreated keeps reviewer setup to zero steps. Swap for migrations before any shared environment (docs/production-roadmap.md).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BudgetDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db, TimeProvider.System);
}

// ------------------------------------------------------------------ pipeline
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(c => c.DocumentTitle = "Budget Approval API");
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).AllowAnonymous().ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().ExcludeFromDescription();

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program { }
