using Ardak.Application.Interfaces;
using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Ardak.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ardak.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=ArdakDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        services.AddDbContext<ArdakDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<ArdakDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<ArdakDbContext>()
        .AddDefaultTokenProviders();

        services.Configure<SuperAdminOptions>(
            configuration.GetSection(SuperAdminOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ISuperAdminSeeder, SuperAdminSeeder>();
        services.AddScoped<ICustomerPortalService, CustomerPortalService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IPropertyService, PropertyService>();
        services.AddScoped<IProcedureService, ProcedureService>();
        services.AddSingleton<IDocumentFileValidator, DocumentFileValidator>();
        services.AddScoped<IDocumentStorageService, FileDocumentStorageService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
