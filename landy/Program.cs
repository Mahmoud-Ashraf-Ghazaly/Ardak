using Ardak.Application.Interfaces;
using Ardak.Infrastructure;
using Ardak.Infrastructure.Data;
using Ardak.Model.Constants;
using Microsoft.AspNetCore.Mvc;

namespace landy;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Register Infrastructure services (DbContext, Identity, Seeder, Services)
        builder.Services.AddInfrastructureServices(builder.Configuration);

        // Add MVC with global AutoValidateAntiforgeryToken
        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        });

        // Configure Application Cookie
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
        });

        // Configure Authorization Policies
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.RequireSuperAdmin, policy =>
                policy.RequireRole(AppRoles.SuperAdmin));

            options.AddPolicy(AppPolicies.RequireAdminOrSuperAdmin, policy =>
                policy.RequireRole(AppRoles.SuperAdmin, AppRoles.Admin));

            options.AddPolicy(AppPolicies.RequireCustomer, policy =>
                policy.RequireRole(AppRoles.Customer));

            options.AddPolicy(AppPolicies.ManageAdmins, policy =>
                policy.RequireRole(AppRoles.SuperAdmin));
        });

        var app = builder.Build();

        // Seed database on startup (idempotent, never resets existing passwords)
        using (var scope = app.Services.CreateScope())
        {
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<ArdakDbContext>();
                await db.Database.EnsureCreatedAsync();

                var seeder = scope.ServiceProvider.GetRequiredService<ISuperAdminSeeder>();
                await seeder.SeedAsync();
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "An error occurred during database initialization/seeding.");
            }
        }

        // Configure HTTP request pipeline
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Account}/{action=Login}/{id?}");

        await app.RunAsync();
    }
}
