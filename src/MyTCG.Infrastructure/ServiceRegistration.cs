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
        services.AddSingleton(provider => { var jwt = provider.GetRequiredService<IConfiguration>().GetSection("Jwt").Get<JwtSettings>() ?? new(); jwt.Validate(); return jwt; });
        return services;
    }
}
