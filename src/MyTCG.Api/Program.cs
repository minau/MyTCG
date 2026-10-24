using Microsoft.OpenApi;
using MyTCG.Api;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyTCG.Application;
using MyTCG.Contracts;
using MyTCG.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders(); builder.Logging.AddJsonConsole();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? []).WithMethods("GET", "POST", "PUT").WithHeaders("Content-Type", "Authorization", "X-MyTCG-Client").AllowCredentials()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
    var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new() {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ClockSkew = TimeSpan.FromSeconds(15), NameClaimType = "name", RoleClaimType = "role", ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
    o.Events = new JwtBearerEvents { OnTokenValidated = async context => {
        var p = context.Principal!;
        if (!Guid.TryParse(p.FindFirstValue("sub"), out var uid) || !Guid.TryParse(p.FindFirstValue("sid"), out var sid)
            || !await context.HttpContext.RequestServices.GetRequiredService<IAccountService>().IsSessionValid(sid, uid))
            context.Fail("Session révoquée ou expirée.");
    }};
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("Auth:PermitLimit", 20), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, context, cancellationToken) => {
    document.Components ??= new();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" };
    foreach (var path in document.Paths.Where(p => p.Key is "/api/users/me" or "/api/auth/logout"))
        foreach (var operation in path.Value.Operations!.Values)
            operation.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
    return Task.CompletedTask;
}));
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgresql");
var app = builder.Build();
_ = app.Services.GetRequiredService<JwtSettings>();
var origins = app.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];

// Run a single dedicated migration job, never automatic migration in every API replica.
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync(); // EF/Npgsql migration lock protects concurrent migrators.
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    if (!await roles.RoleExistsAsync("User"))
    {
        var result = await roles.CreateAsync(new IdentityRole<Guid>("User"));
        if (!result.Succeeded && !await roles.RoleExistsAsync("User")) throw new InvalidOperationException("Échec initialisation rôle User.");
    }
    return;
}
app.Use(async (context, next) => {
    try { await next(); }
    catch (BadHttpRequestException) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new ApiError("validation", ["Requête JSON invalide."]));
    }
    catch (AccountException ex) {
        context.Response.StatusCode = ex.Code == "unauthorized" ? 401 : 400;
        await context.Response.WriteAsJsonAsync(new ApiError(ex.Code, ex.Errors));
    }
    catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" }) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new ApiError("duplicate", ["Username ou email déjà utilisé."]));
    }
    catch (Exception ex) {
        // Do not log exception objects: database exception details can contain account data.
        app.Logger.LogError("Unhandled {ErrorType}; trace {TraceId}", ex.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new ApiError("server_error", ["Une erreur interne est survenue."]));
    }
});
app.UseCors();
// Web requests using cookies must opt into a non-simple header and have an allowed Origin.
// Mobile requests never consume cookies, only the explicitly supplied secure-storage token.
app.Use(async (ctx, next) => {
    if (ctx.Request.Path.StartsWithSegments("/api/auth") && HttpMethods.IsPost(ctx.Request.Method)) {
        var client = ctx.Request.Headers["X-MyTCG-Client"].ToString();
        if (client is not ("web" or "mobile") || (client == "web" && !origins.Contains(ctx.Request.Headers.Origin.ToString(), StringComparer.Ordinal))) {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new ApiError("csrf", ["Origine ou type de client refusé."])); return;
        }
    }
    await next();
});
app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization();
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
    app.UseSwaggerUI(o => {
        o.RoutePrefix = "docs";
        o.SwaggerEndpoint("/openapi/v1.json", "MyTCG API V1");
        o.DocumentTitle = "MyTCG — API";
        o.DisplayRequestDuration();
        o.UseRequestInterceptor("request => { request.headers['X-MyTCG-Client'] = 'mobile'; return request; }");
    });
}
app.MapHealthChecks("/health");
var auth = app.MapGroup("/api/auth").RequireRateLimiting("auth");
auth.MapPost("/register", async (RegisterRequest r, IAccountService accounts) => {
    Validate(r); var user = await accounts.Register(r); return Results.Created("/api/users/me", user);
});
auth.MapPost("/login", async (LoginRequest r, IAccountService accounts, HttpContext ctx) => {
    Validate(r); return WriteSession(ctx, await accounts.Login(r));
});
auth.MapPost("/refresh", async (RefreshRequest r, IAccountService accounts, HttpContext ctx) => {
    var token = IsWeb(ctx) ? ctx.Request.Cookies["mytcg.refresh"] : r.RefreshToken;
    try { return WriteSession(ctx, await accounts.Refresh(token ?? "")); }
    catch (AccountException) { ClearCookie(ctx); throw; }
});
auth.MapPost("/logout", async (IAccountService accounts, HttpContext ctx) => {
    await accounts.Logout(Guid.Parse(ctx.User.FindFirstValue("sid")!), UserId(ctx));
    ClearCookie(ctx); return Results.NoContent();
}).RequireAuthorization();
app.MapGet("/api/users/me", async (IAccountService accounts, HttpContext ctx) => await accounts.GetUser(UserId(ctx))).RequireAuthorization();
app.MapPut("/api/users/me", async (UpdateUserRequest r, IAccountService accounts, HttpContext ctx) => {
    Validate(r); return await accounts.UpdateUser(UserId(ctx), r);
}).RequireAuthorization();
app.Run();

static Guid UserId(HttpContext ctx) => Guid.Parse(ctx.User.FindFirstValue("sub")!);
static bool IsWeb(HttpContext ctx) => ctx.Request.Headers["X-MyTCG-Client"] == "web";
static void Validate(object request) {
    var errors = new List<ValidationResult>();
    if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
        throw new AccountException("validation", errors.Select(e => e.ErrorMessage!).ToArray());
}
static CookieOptions Cookie(HttpContext ctx) => new() {
    HttpOnly = true, Secure = !ctx.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() || ctx.Request.IsHttps,
    SameSite = SameSiteMode.Strict, Path = "/api/auth", IsEssential = true
};
static void ClearCookie(HttpContext ctx) => ctx.Response.Cookies.Delete("mytcg.refresh", Cookie(ctx));
static IResult WriteSession(HttpContext ctx, IssuedSession session) {
    if (!IsWeb(ctx)) return Results.Ok(session.Response with { RefreshToken = session.RefreshToken });
    var options = Cookie(ctx); options.Expires = session.RefreshExpiresAt;
    ctx.Response.Cookies.Append("mytcg.refresh", session.RefreshToken, options);
    return Results.Ok(session.Response);
}
public partial class Program { }
