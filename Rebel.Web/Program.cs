using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Rebel.Infrastructure.Data;
using Rebel.Web.Services;
using Rebel.Web.Hubs;
using Rebel.Web.Authorization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var hideAiFeatures = builder.Configuration.GetValue<bool>(
    "Presentation:HideAiFeatures");

var defaultConnection =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "DefaultConnection is missing from configuration.");

if (builder.Environment.IsProduction())
{
    if (defaultConnection.Contains(
            "CHANGE_ME",
            StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "Production database connection string still contains CHANGE_ME.");
    }

    var allowedHosts = builder.Configuration["AllowedHosts"];
    if (string.IsNullOrWhiteSpace(allowedHosts) ||
        allowedHosts.Split(';', StringSplitOptions.TrimEntries)
            .Contains("*", StringComparer.Ordinal))
    {
        throw new InvalidOperationException(
            "Production AllowedHosts must contain the public host name, not '*'.");
    }
}

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(),
        tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddScoped<IBeerRecommendationService, BeerRecommendationService>();
builder.Services.AddScoped<IBeerGuideNarrator, OpenAiBeerGuideNarrator>();
builder.Services.AddSingleton<IBeerCatalogMatcher, BeerCatalogMatcher>();
builder.Services.AddSingleton<IFoodCatalogMatcher, FoodCatalogMatcher>();
builder.Services.AddSingleton<IBeerPreferenceParser, BeerPreferenceParser>();
builder.Services.AddSingleton<IBeerConversationQueryBuilder, BeerConversationQueryBuilder>();
builder.Services.AddSingleton<IBeerChatStateService, BeerChatStateService>();
builder.Services.AddSingleton<IBeerFeedbackLearningService, BeerFeedbackLearningService>();
builder.Services.AddSingleton<IBeerGuideTestLabService, BeerGuideTestLabService>();
builder.Services.AddSingleton<IBeerProfileSuggestionService, BeerProfileSuggestionService>();
builder.Services.AddSingleton<IBeerProfileReviewQueueService, BeerProfileReviewQueueService>();
builder.Services.AddSingleton<IBeerNoMatchRecoveryService, BeerNoMatchRecoveryService>();
builder.Services.AddScoped<IBeerGuideChatService, OpenAiBeerGuideChatService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        defaultConnection
    )
);

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        RateLimitPolicies.Login,
        context => CreateFixedWindowPartition(context, 10, TimeSpan.FromMinutes(10)));
    options.AddPolicy(
        RateLimitPolicies.ReservationCreate,
        context => CreateFixedWindowPartition(context, 4, TimeSpan.FromMinutes(10)));
    options.AddPolicy(
        RateLimitPolicies.ReservationLookup,
        context => CreateFixedWindowPartition(context, 10, TimeSpan.FromMinutes(1)));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain";
        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please wait and try again.",
            cancellationToken);
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AdminPolicies.Backstage,
        policy => policy.RequireRole(
            AdminRoles.LegacyAdmin,
            AdminRoles.Manager,
            AdminRoles.Staff));

    options.AddPolicy(
        AdminPolicies.ManagerOnly,
        policy => policy.RequireRole(
            AdminRoles.LegacyAdmin,
            AdminRoles.Manager));
});

// EMAIL SETTINGS
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings")
);

// EMAIL SERVICE
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEventImageStorage, EventImageStorage>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

if (hideAiFeatures)
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        var isPublicAiRoute =
            path.StartsWithSegments("/RebelAI", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/BeerGuide", StringComparison.OrdinalIgnoreCase);
        var isAdminAiRoute =
            path.StartsWithSegments("/AdminBeerCatalog", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/AdminBeerFeedback", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/AdminBeerGuideLab", StringComparison.OrdinalIgnoreCase);

        if (isPublicAiRoute || isAdminAiRoute)
        {
            context.Response.Redirect(isAdminAiRoute ? "/Admin" : "/Home/Menu");
            return;
        }

        await next();
    });
}

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapRazorPages();
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live")
    }).AllowAnonymous();
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready")
    }).AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext =
        services.GetRequiredService<AppDbContext>();

    if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        await dbContext.Database.MigrateAsync();
    }

    var userManager =
        services.GetRequiredService<UserManager<IdentityUser>>();

    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    foreach (var roleName in new[]
    {
        AdminRoles.LegacyAdmin,
        AdminRoles.Manager,
        AdminRoles.Staff
    })
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    if (app.Configuration.GetValue<bool>("AdminUser:BootstrapEnabled"))
    {
        var adminEmail = app.Configuration["AdminUser:Email"]
            ?? throw new InvalidOperationException(
                "AdminUser:Email is required while admin bootstrap is enabled.");
        var adminPassword = app.Configuration["AdminUser:Password"]
            ?? throw new InvalidOperationException(
                "AdminUser:Password is required while admin bootstrap is enabled.");

        var adminUser =
            await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(
                adminUser,
                adminPassword
            );

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(error => error.Description));
                throw new InvalidOperationException(
                    $"Admin bootstrap failed: {errors}");
            }
        }

        foreach (var roleName in new[]
        {
            AdminRoles.LegacyAdmin,
            AdminRoles.Manager
        })
        {
            if (!await userManager.IsInRoleAsync(adminUser, roleName))
            {
                await userManager.AddToRoleAsync(adminUser, roleName);
            }
        }
    }
}
app.MapHub<NotificationHub>("/notificationHub");


app.Run();

static RateLimitPartition<string> CreateFixedWindowPartition(
    HttpContext context,
    int permitLimit,
    TimeSpan window)
{
    var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
            AutoReplenishment = true,
            PermitLimit = permitLimit,
            QueueLimit = 0,
            Window = window
        });
}
