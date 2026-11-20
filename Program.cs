    using System.Diagnostics;

using AuthDemo.Data;
using AuthDemo.Helpers;
using AuthDemo.Middleware;
using AuthDemo.Services;
using AuthDemo.Services.Interfaces;
using AuthDemo.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// DATABASE
// =====================================================

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString(
            "DefaultConnection")
        ?? throw new InvalidOperationException(
            "DefaultConnection is missing.");

    options.UseSqlServer(
     connectionString,
     sqlOptions =>
     {
         // Increase SQL command timeout to 180 seconds
         sqlOptions.CommandTimeout(builder.Configuration.GetValue<int?>("Database:CommandTimeoutSeconds") ?? 180);

         sqlOptions.MigrationsAssembly(
             typeof(AppDbContext)
                 .Assembly
                 .GetName()
                 .Name);
     });

    // Detailed EF Core diagnostics must only be enabled locally.
    // Sensitive data logging can expose SQL parameter values.
    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        // Opt in explicitly for local troubleshooting only; may log private data.
        if (builder.Configuration.GetValue<bool>("Database:EnableSensitiveDataLogging"))
        {
            options.EnableSensitiveDataLogging();
        }
    }
});

// =====================================================
// APPLICATION SERVICES
// =====================================================

builder.Services.AddScoped<JwtHelper>();

builder.Services.AddScoped<
    PharmacyPermissionChecker>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<
    IPharmacyAuthService,
    PharmacyAuthService>();

builder.Services.AddScoped<
    IEmailService,
    EmailService>();

builder.Services.AddScoped<
    IMedicineService,
    MedicineService>();

builder.Services.AddScoped<
    IInventoryService,
    InventoryService>();

builder.Services.AddScoped<
    IPharmacyService,
    PharmacyService>();

builder.Services.AddScoped<
    IMedicineImportService,
    MedicineImportService>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<IPharmacyRealtimeNotifier, PharmacyRealtimeNotifier>();
builder.Services.AddHostedService<PharmacyAlertBackgroundService>();
builder.Services.AddHttpClient<PincodeService>();
builder.Services.AddScoped<UserPermissionService>();
builder.Services.AddScoped<PharmacyWorkflowAccess>();
builder.Services.AddScoped<PmsRazorpaySupport>();
builder.Services.AddHttpClient("PmsRazorpay", client => client.Timeout = TimeSpan.FromSeconds(20));

// =====================================================
// CONTROLLERS
// =====================================================

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter =
            false;
    });

builder.Services.AddEndpointsApiExplorer();

// =====================================================
// CORS
// =====================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowLocalhost",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// =====================================================
// SWAGGER
// =====================================================

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title =
                "Pharmacy Management API — Clear Endpoint Guide",

            Version =
                "v1",

            Description =
                "Endpoints are grouped in business-flow order. " +
                "Open a section, read the plain-language operation title, " +
                "then use Try it out. Existing routes are unchanged."
        });

    options.OperationFilter<
        ClearSwaggerOperationFilter>();

    options.OrderActionsBy(api =>
        $"{api.ActionDescriptor.RouteValues["controller"]}_" +
        $"{api.RelativePath}_" +
        $"{api.HttpMethod}");

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name =
                "Authorization",

            Type =
                SecuritySchemeType.Http,

            Scheme =
                "bearer",

            BearerFormat =
                "JWT",

            In =
                ParameterLocation.Header,

            Description =
                "Enter the JWT access token. " +
                "Do not include the word Bearer."
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Type =
                                ReferenceType.SecurityScheme,

                            Id =
                                "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});

// =====================================================
// PHARMACY JWT AUTHENTICATION
// =====================================================

var pharmacyJwtKey =
    builder.Configuration[
        "PharmacyJwt:Key"]
    ?? throw new InvalidOperationException(
        "PharmacyJwt:Key is missing.");

var pharmacyJwtIssuer =
    builder.Configuration[
        "PharmacyJwt:Issuer"]
    ?? "PharmacyOnlyApi";

var pharmacyJwtAudience =
    builder.Configuration[
        "PharmacyJwt:Audience"]
    ?? "PharmacyOnlyClients";

if (Encoding.UTF8
        .GetByteCount(pharmacyJwtKey) < 32)
{
    throw new InvalidOperationException(
        "PharmacyJwt:Key must contain at least 32 bytes.");
}

var pharmacySigningKey =
    new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes(
            pharmacyJwtKey));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults
                .AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults
                .AuthenticationScheme;

        options.DefaultScheme =
            JwtBearerDefaults
                .AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata =
            true;

        options.SaveToken =
            true;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer =
                    true,

                ValidIssuer =
                    pharmacyJwtIssuer,

                ValidateAudience =
                    true,

                ValidAudience =
                    pharmacyJwtAudience,

                ValidateIssuerSigningKey =
                    true,

                IssuerSigningKey =
                    pharmacySigningKey,

                ValidateLifetime =
                    true,

                RequireExpirationTime =
                    true,

                ClockSkew =
                    TimeSpan.Zero,

                NameClaimType =
                    ClaimTypes.Name,

                RoleClaimType =
                    ClaimTypes.Role
            };

        options.Events =
            new JwtBearerEvents
            {
                OnChallenge =
                    async context =>
                    {
                        if (context.Response
                            .HasStarted)
                        {
                            return;
                        }

                        context.HandleResponse();

                        context.Response.StatusCode =
                            StatusCodes
                                .Status401Unauthorized;

                        context.Response.ContentType =
                            "application/json";

                        await context.Response
                            .WriteAsJsonAsync(
                                new
                                {
                                    Success =
                                        false,

                                    Message =
                                        "Authentication token is missing, invalid or expired."
                                });
                    },

                OnForbidden =
                    async context =>
                    {
                        if (context.Response
                            .HasStarted)
                        {
                            return;
                        }

                        context.Response.StatusCode =
                            StatusCodes
                                .Status403Forbidden;

                        context.Response.ContentType =
                            "application/json";

                        await context.Response
                            .WriteAsJsonAsync(
                                new
                                {
                                    Success =
                                        false,

                                    Message =
                                        "You do not have permission to access this resource."
                                });
                    }
            };
    });

builder.Services.AddAuthorization();

// =====================================================
// BUILD APPLICATION
// =====================================================

var app =
    builder.Build();

// =====================================================
// AUTOMATIC DATABASE MIGRATIONS
// =====================================================

// Apply pending migrations automatically in Development.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider
        .GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
}

// =====================================================
// SEEDERS
// =====================================================

// Create initial Pharmacy Super Admin.
await PharmacySuperAdminSeeder
    .SeedAsync(
        app.Services,
        app.Configuration);

// Permissions are user-specific. No permission-role seeding is required.

// =====================================================
// EXCEPTION HANDLING
// =====================================================

app.UseExceptionHandler(errorApplication =>
{
    errorApplication.Run(async context =>
    {
        var exceptionFeature =
            context.Features.Get<IExceptionHandlerFeature>();

        var exception = exceptionFeature?.Error;

        var logger =
            context.RequestServices
                .GetRequiredService<ILogger<Program>>();

        logger.LogError(
            exception,
            "Unhandled pharmacy API exception.");

        var statusCode = exception switch
        {
            UnauthorizedAccessException =>
                StatusCodes.Status401Unauthorized,

            KeyNotFoundException =>
                StatusCodes.Status404NotFound,

            InvalidDataException =>
                StatusCodes.Status400BadRequest,

            InvalidOperationException =>
                StatusCodes.Status400BadRequest,

            _ =>
                StatusCodes.Status500InternalServerError
        };

        var actualError =
            exception?.GetBaseException().Message
            ?? exception?.Message
            ?? "Unknown backend error.";

        context.Response.StatusCode = statusCode;
        context.Response.ContentType =
            "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            Title = "Pharmacy request failed",
            Status = statusCode,

            Detail = app.Environment.IsDevelopment()
                ? actualError
                : "An unexpected error occurred."
        });
    });
});

// =====================================================
// HTTP PIPELINE
// =====================================================

app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "Pharmacy Management API v1");

    options.DocumentTitle =
        "Pharmacy API — Clear Swagger";

    options.DisplayOperationId();

    options.DefaultModelsExpandDepth(
        -1);

    options.DocExpansion(
        Swashbuckle.AspNetCore
            .SwaggerUI
            .DocExpansion.None);
});

// Phase 12: measure slow API requests without recording credentials or payloads.
// This measures request duration, not browser rendering or ngrok round trips.
app.Use(async (context, next) =>
{
    var watch = Stopwatch.StartNew();
    try
    {
        await next(context);
    }
    finally
    {
        watch.Stop();
        if (context.Request.Path.StartsWithSegments("/api") && watch.ElapsedMilliseconds >= 1000)
        {
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("PmsSlowApi");
            logger.LogWarning("Slow API {Method} {Path} returned {Status} in {ElapsedMs}ms; trace {TraceId}",
                context.Request.Method, context.Request.Path.Value, context.Response.StatusCode,
                watch.ElapsedMilliseconds, context.TraceIdentifier);
        }
    }
});

app.UseHttpsRedirection();

// Keep this only if the project uses files from wwwroot.
app.UseStaticFiles();

app.UseCors(
    "AllowLocalhost");

// Authentication must be before
// assignment middleware and authorization.
app.UseAuthentication();

app.UseMiddleware<
    PharmacyAssignmentMiddleware>();

app.UseAuthorization();

// =====================================================
// ENDPOINTS
// =====================================================

// Redirect root URL to Swagger.
app.MapGet(
        "/",
        () =>
            Results.Redirect(
                "/swagger"))
    .ExcludeFromDescription();

app.MapControllers();
app.MapHub<AuthDemo.Hubs.PharmacyHub>("/hubs/pharmacy");

app.Run();
