using System.Reflection;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OrderFlow.Api.Auth;
using OrderFlow.Api.Infrastructure;
using OrderFlow.Infrastructure;
using OrderFlow.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------------- logging ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

// ---------------------------------------------------------------------------- auth ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<TokenService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                 ?? throw new InvalidOperationException("The Jwt configuration section is missing.");

if (jwtOptions.SigningKey.Length < 32)
{
    // Fail at startup rather than issuing tokens that are cheap to forge. An HS256 key
    // shorter than the hash output weakens the signature.
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),

            // The default five minutes is generous for a token that lives an hour.
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// ------------------------------------------------------------------------- web api ---
builder.Services.AddControllers(options => options.Filters.Add<ValidationActionFilter>());
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

const string AdminPanelCors = "admin-panel";
builder.Services.AddCors(options => options.AddPolicy(AdminPanelCors, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    swagger.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OrderFlow API",
        Version = "v1",
        Description = "Order and inventory management. Sign in at /auth/login, then use Authorize.",
    });

    // Registers the bearer scheme so the Authorize button appears and Swagger UI sends the
    // token on subsequent calls. Without this the docs are only readable, not usable.
    swagger.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the accessToken returned by /auth/login.",
    });

    // OpenAPI.NET v2 (which Swashbuckle 10 uses) replaced the old
    // "scheme object carrying a Reference" pattern with an explicit reference type.
    swagger.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });

    var xmlDocumentation = Path.Combine(
        AppContext.BaseDirectory,
        $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");

    if (File.Exists(xmlDocumentation))
    {
        swagger.IncludeXmlComments(xmlDocumentation);
    }
});

builder.Services.AddOrderFlowInfrastructure(builder.Configuration);

// ------------------------------------------------------------------------ pipeline ---
var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.UseCors(AdminPanelCors);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness answers "is this process alive?" and runs no dependency checks. If it checked
// the database, a brief outage would make Kubernetes restart every pod — which cannot fix
// a database problem and turns a partial outage into a total one.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Readiness answers "can this process serve traffic?" and does check the database. A
// failure pulls the pod out of the load balancer without restarting it, so it rejoins on
// its own once the database recovers.
app.MapHealthChecks("/health/ready");

app.UseSwagger();
app.UseSwaggerUI(ui =>
{
    ui.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderFlow API v1");
    ui.DocumentTitle = "OrderFlow API";
});

await app.Services.MigrateAndSeedAsync();

await app.RunAsync();

/// <summary>Exposed so the integration tests can host the API in-process.</summary>
public partial class Program;
