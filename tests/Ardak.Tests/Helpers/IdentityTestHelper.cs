using Ardak.Infrastructure.Data;
using Ardak.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ardak.Tests.Helpers;

public static class IdentityTestHelper
{
    public static ServiceProvider CreateTestServiceProvider(string databaseName)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddDebug());

        services.AddDbContext<ArdakDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ArdakDbContext>()
        .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }
}
