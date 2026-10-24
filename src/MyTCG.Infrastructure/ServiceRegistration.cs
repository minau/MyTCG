using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyTCG.Application;
namespace MyTCG.Infrastructure;
public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>((provider, o) => o.UseNpgsql(provider.GetRequiredService<IConfiguration>().GetConnectionString("Database") ?? throw new InvalidOperationException("ConnectionStrings:Database requis.")));
        services.AddIdentityCore<AppUser>(o => {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 12; o.Password.RequireNonAlphanumeric = true;
            o.Password.RequireDigit = true; o.Password.RequireUppercase = true; o.Password.RequireLowercase = true;
            o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITcgService, TcgService>();
        services.AddScoped<BoosterOpeningService>();
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<BoosterOptions>().Configure(o => { o.DailyLimitEnabled = config.GetValue("Boosters:DailyLimitEnabled", true); o.DailyLimit = config.GetValue("Boosters:DailyLimit", 3); o.FifthMultipliers = config.GetSection("Boosters:FifthMultipliers").Get<double[]>() ?? [1, 2, 4, 8, 16]; }).Validate(o => o.DailyLimit == 3 && o.FifthMultipliers.Length == 5 && o.FifthMultipliers.All(x => double.IsFinite(x) && x > 0 && x <= 1000) && o.FifthMultipliers.Zip(o.FifthMultipliers.Skip(1)).All(x => x.First < x.Second), "Configuration boosters invalide").ValidateOnStart();
        var dataProtection = services.AddDataProtection().SetApplicationName("MyTCG");
        if (config["DataProtection:KeyPath"] is { Length: > 0 } keyPath)
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        services.AddSingleton<IMediaStorage, LocalMediaStorage>();
        services.AddSingleton(provider => { var jwt = provider.GetRequiredService<IConfiguration>().GetSection("Jwt").Get<JwtSettings>() ?? new(); jwt.Validate(); return jwt; });
        return services;
    }
}
