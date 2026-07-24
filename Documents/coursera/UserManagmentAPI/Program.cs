using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

builder.Services.Configure<UserManagementOptions>(
    builder.Configuration.GetSection(UserManagementOptions.SectionName));
builder.Services.Configure<AuthOptions>(
    builder.Configuration.GetSection(AuthOptions.SectionName));

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IUserService, InMemoryUserService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.MapGet("/", (IOptions<UserManagementOptions> options) =>
{
    return Results.Ok(new
    {
        Message = "User Management API is running.",
        Service = options.Value.ServiceName,
        Version = options.Value.Version
    });
});

app.MapPost("/auth/token", (TokenRequest request, IOptions<AuthOptions> options) =>
{
    if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
    {
        return Results.BadRequest(new { Message = "ClientId and ClientSecret are required." });
    }

    var auth = options.Value;
    if (!string.Equals(request.ClientId, auth.ClientId, StringComparison.Ordinal) ||
        !string.Equals(request.ClientSecret, auth.ClientSecret, StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    var token = JwtTokenFactory.Create(auth, request.ClientId);
    return Results.Ok(token);
})
.AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.MapGet("/test/error", () =>
    {
        throw new InvalidOperationException("Test exception for middleware validation.");
    })
    .AllowAnonymous();
}

var usersApi = app.MapGroup("/users").RequireAuthorization();

usersApi.MapGet("/", (int page, int pageSize, string? search, IUserService userService) =>
{
    var normalizedPage = page == 0 ? 1 : page;
    var normalizedPageSize = pageSize == 0 ? 20 : pageSize;

    if (normalizedPage <= 0 || normalizedPageSize <= 0 || normalizedPageSize > 200)
    {
        return Results.BadRequest(new { Message = "page must be > 0 and pageSize must be between 1 and 200." });
    }

    return Results.Ok(userService.GetAll(normalizedPage, normalizedPageSize, search));
});

usersApi.MapGet("/{id:guid}", (Guid id, IUserService userService) =>
{
    var user = userService.GetById(id);
    return user is null
        ? Results.NotFound(new { Message = "User not found." })
        : Results.Ok(user);
});

usersApi.MapPost("/", (CreateUserRequest request, IUserService userService) =>
{
    var validationError = UserInputValidator.Validate(request.Name, request.Email);
    if (validationError is not null)
    {
        return Results.BadRequest(new { Message = validationError });
    }

    var createdUser = userService.Add(request.Name, request.Email);
    return createdUser is null
        ? Results.Conflict(new { Message = "A user with this email already exists." })
        : Results.Created($"/users/{createdUser.Id}", createdUser);
});

usersApi.MapPut("/{id:guid}", (Guid id, UpdateUserRequest request, IUserService userService) =>
{
    var validationError = UserInputValidator.Validate(request.Name, request.Email);
    if (validationError is not null)
    {
        return Results.BadRequest(new { Message = validationError });
    }

    var updateResult = userService.Update(id, request.Name, request.Email);
    if (updateResult is null)
    {
        return Results.NotFound(new { Message = "User not found." });
    }

    if (!updateResult.Success)
    {
        return Results.Conflict(new { Message = "A user with this email already exists." });
    }

    return Results.Ok(updateResult.User);
});

usersApi.MapDelete("/{id:guid}", (Guid id, IUserService userService) =>
{
    var deleted = userService.Delete(id);
    return deleted
        ? Results.NoContent()
        : Results.NotFound(new { Message = "User not found." });
});

app.Run();

record User(Guid Id, string Name, string Email);
record CreateUserRequest(string Name, string Email);
record UpdateUserRequest(string Name, string Email);
record UpdateUserResult(bool Success, User? User);
record TokenRequest(string ClientId, string ClientSecret);
record TokenResponse(string AccessToken, string TokenType, int ExpiresInSeconds);

interface IUserService
{
    IReadOnlyList<User> GetAll(int page, int pageSize, string? search);
    User? GetById(Guid id);
    User? Add(string name, string email);
    UpdateUserResult? Update(Guid id, string name, string email);
    bool Delete(Guid id);
}

sealed class InMemoryUserService : IUserService
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();
    private readonly ConcurrentDictionary<string, Guid> _emailIndex = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryUserService(IOptions<UserManagementOptions> options)
    {
        var seedUsers = options.Value.SeedUsers ?? new List<SeedUser>();

        if (seedUsers.Count == 0)
        {
            seedUsers =
            [
                new SeedUser { Name = "Ana", Email = "ana@example.com" },
                new SeedUser { Name = "Marko", Email = "marko@example.com" }
            ];
        }

        foreach (var seedUser in seedUsers)
        {
            var name = seedUser.Name.Trim();
            var email = seedUser.Email.Trim();

            if (!string.IsNullOrWhiteSpace(name) && UserInputValidator.Validate(name, email) is null)
            {
                Add(name, email);
            }
        }
    }

    public IReadOnlyList<User> GetAll(int page, int pageSize, string? search)
    {
        IEnumerable<User> query = _users.Values;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchTerm = search.Trim();
            query = query.Where(user =>
                user.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                user.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(user => user.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    public User? GetById(Guid id) => _users.TryGetValue(id, out var user) ? user : null;

    public User? Add(string name, string email)
    {
        var trimmedName = name.Trim();
        var trimmedEmail = email.Trim();
        var userId = Guid.NewGuid();
        var user = new User(userId, trimmedName, trimmedEmail);

        if (!_emailIndex.TryAdd(trimmedEmail, userId))
        {
            return null;
        }

        if (!_users.TryAdd(userId, user))
        {
            _emailIndex.TryRemove(trimmedEmail, out _);
            return null;
        }

        return user;
    }

    public UpdateUserResult? Update(Guid id, string name, string email)
    {
        var trimmedName = name.Trim();
        var trimmedEmail = email.Trim();

        if (!_users.TryGetValue(id, out var existingUser))
        {
            return null;
        }

        if (!existingUser.Email.Equals(trimmedEmail, StringComparison.OrdinalIgnoreCase))
        {
            if (!_emailIndex.TryAdd(trimmedEmail, id))
            {
                return new UpdateUserResult(false, null);
            }

            _emailIndex.TryRemove(existingUser.Email, out _);
        }

        var updatedUser = new User(id, trimmedName, trimmedEmail);
        _users[id] = updatedUser;
        return new UpdateUserResult(true, updatedUser);
    }

    public bool Delete(Guid id)
    {
        if (!_users.TryRemove(id, out var removedUser))
        {
            return false;
        }

        _emailIndex.TryRemove(removedUser.Email, out _);
        return true;
    }
}

static class UserInputValidator
{
    public static string? Validate(string? name, string? email)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            return "Name and Email are required.";
        }

        var trimmedName = name.Trim();
        var trimmedEmail = email.Trim();

        if (trimmedName.Length < 2)
        {
            return "Name must contain at least 2 characters.";
        }

        if (trimmedName.Length > 100)
        {
            return "Name must contain at most 100 characters.";
        }

        if (trimmedEmail.Length > 256)
        {
            return "Email must contain at most 256 characters.";
        }

        try
        {
            _ = new MailAddress(trimmedEmail);
        }
        catch
        {
            return "Email format is invalid.";
        }

        return null;
    }
}

static class JwtTokenFactory
{
    public static TokenResponse Create(AuthOptions options, string subject)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(options.TokenLifetimeMinutes);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new TokenResponse(accessToken, "Bearer", (int)TimeSpan.FromMinutes(options.TokenLifetimeMinutes).TotalSeconds);
    }
}

sealed class RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        logger.LogInformation(
            "Incoming {Method} {Path} TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier);

        await next(context);

        sw.Stop();
        logger.LogInformation(
            "Outgoing {Method} {Path} Status={StatusCode} DurationMs={DurationMs} TraceId={TraceId}",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds,
            context.TraceIdentifier);
    }
}

sealed class GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "Internal server error."
            }));
        }
    }
}

sealed class UserManagementOptions
{
    public const string SectionName = "UserManagement";
    public string ServiceName { get; set; } = "UserManagementAPI";
    public string Version { get; set; } = "v1";
    public List<SeedUser> SeedUsers { get; set; } = new();
}

sealed class SeedUser
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public string Issuer { get; set; } = "UserManagementAPI";
    public string Audience { get; set; } = "UserManagementAPI.Client";
    public string SigningKey { get; set; } = "ChangeThisSigningKeyToAStrongSecretAtLeast32Chars";
    public int TokenLifetimeMinutes { get; set; } = 60;
    public string ClientId { get; set; } = "techhive-client";
    public string ClientSecret { get; set; } = "techhive-secret";
}
