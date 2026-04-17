using System.Security.Cryptography;
using AgilineeringApi.Data;
using AgilineeringApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Infrastructure;

public class DataSeeder(AppDbContext db, IConfiguration configuration, ILogger<DataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // One-time forced password reset: set Seed:ForceAdminPassword=true + Seed:AdminPassword=<new>
        // Remove the secret after use to prevent repeated resets.
        if (configuration.GetValue<bool>("Seed:ForceAdminPassword"))
        {
            var forcedPassword = configuration["Seed:AdminPassword"];
            if (!string.IsNullOrWhiteSpace(forcedPassword))
            {
                var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin", ct);
                if (adminUser is not null)
                {
                    adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(forcedPassword, workFactor: SecurityConstants.PasswordHashWorkFactor);
                    adminUser.FailedLoginAttempts = 0;
                    adminUser.LockoutEnd = null;
                    await db.SaveChangesAsync(ct);
                    logger.LogWarning("Admin password forcibly reset via Seed:ForceAdminPassword. Remove this secret now.");
                }
            }
        }

        if (!await db.Users.AnyAsync(ct))
        {
            var configuredPassword = configuration["Seed:AdminPassword"];
            string password;

            if (!string.IsNullOrWhiteSpace(configuredPassword))
            {
                password = configuredPassword;
                logger.LogInformation("Seeding admin account using configured Seed:AdminPassword.");
            }
            else
            {
                password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
                // Write directly to stdout so the password is not captured as a structured log field
                Console.WriteLine(
                    $"[WARN] No Seed:AdminPassword configured. Generated admin password: {password} — " +
                    "save this now, it will not be shown again. " +
                    "Set Seed:AdminPassword in your secrets to control this on the next fresh database.");
            }

            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: SecurityConstants.PasswordHashWorkFactor),
                Role = "admin"
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
