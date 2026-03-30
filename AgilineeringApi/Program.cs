using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using AgilineeringApi.Data;
using AgilineeringApi.Models;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=agilineering.db";
var connectionString = new SqliteConnectionStringBuilder(rawConnectionString)
{
    ForeignKeys = true
}.ToString();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPostsService, PostsService>();
builder.Services.AddScoped<ITagsService, TagsService>();
builder.Services.AddScoped<IPostPreviewService, PostPreviewService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
              .AllowAnyHeader()
              .WithMethods("GET", "POST", "PUT", "DELETE")));

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = builder.Configuration.GetValue("Security:LoginRateLimit", 10),
                QueueLimit = 0,
            }));
    options.AddPolicy("write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = builder.Configuration.GetValue("Security:WriteRateLimit", 30),
                QueueLimit = 0,
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    db.Database.EnsureCreated();
    ApplySchemaChanges(db, startupLogger);
    await SeedDataAsync(db);
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self'; frame-ancestors 'none'";
    context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});

app.UseCors();

app.Use(async (context, next) =>
{
    if (IsWriteMethod(context.Request.Method) && !IsPublicWriteEndpoint(context.Request))
    {
        var configuredKey = app.Configuration["AdminKey"];
        if (string.IsNullOrEmpty(configuredKey))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Write access is not available." });
            return;
        }
        var providedKey = context.Request.Headers["X-Admin-Key"].FirstOrDefault() ?? "";
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        if (!CryptographicOperations.FixedTimeEquals(configuredHash, providedHash))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Write access is not available." });
            return;
        }
    }
    await next();
});

var imagesPath = app.Configuration["Storage:ImagesPath"] ?? "images";
var imagesDir = Path.IsPathRooted(imagesPath)
    ? imagesPath
    : Path.Combine(app.Environment.ContentRootPath, imagesPath);
Directory.CreateDirectory(imagesDir);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(imagesDir),
    RequestPath = "/images"
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.Run();

static void ApplySchemaChanges(AppDbContext db, ILogger logger)
{
    // Add lockout columns to existing DBs that predate these fields.
    // SQLite throws "duplicate column name" if the column already exists — that's expected and ignored.
    // Any other exception is logged as a warning (app continues but schema may be inconsistent).
    TryAlterTable(db, logger, "ALTER TABLE Users ADD COLUMN FailedLoginAttempts INTEGER NOT NULL DEFAULT 0");
    TryAlterTable(db, logger, "ALTER TABLE Users ADD COLUMN LockoutEnd TEXT NULL");

    // PostPreviews table — added after initial release
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS PostPreviews (
            Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            PostId INTEGER NOT NULL REFERENCES Posts(Id) ON DELETE CASCADE,
            Token TEXT NOT NULL,
            Name TEXT NOT NULL,
            PasswordHash TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        )
        """);
    db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_PostPreviews_Token ON PostPreviews(Token)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_PostPreviews_PostId ON PostPreviews(PostId)");

    // WAL mode — allows concurrent reads during writes (persistent, no-op on :memory:)
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL");

    // Indexes for common query patterns (idempotent — IF NOT EXISTS)
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Posts_AuthorId ON Posts(AuthorId)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Posts_Published_CreatedAt ON Posts(Published, CreatedAt DESC)");
    db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_PostTag_TagsId ON PostTag(TagsId)");
}

static async Task SeedDataAsync(AppDbContext db)
{
    if (!await db.Users.AnyAsync())
    {
        db.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin", workFactor: 12),
            Role = "admin"
        });
        await db.SaveChangesAsync();
    }
}

static void TryAlterTable(AppDbContext db, ILogger logger, string sql)
{
    try
    {
        db.Database.ExecuteSqlRaw(sql);
    }
    catch (SqliteException ex) when (ex.Message.Contains("duplicate column name"))
    {
        // Column already exists — expected when upgrading an existing database
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Unexpected error running schema change: {Sql}", sql);
    }
}

static bool IsWriteMethod(string method) =>
    HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method);

static bool IsPublicWriteEndpoint(HttpRequest request)
{
    var path = request.Path.Value ?? "";
    return path.Contains("/preview/", StringComparison.OrdinalIgnoreCase)
        && path.EndsWith("/access", StringComparison.OrdinalIgnoreCase);
}

public partial class Program { }
