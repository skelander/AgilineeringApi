using System.Data.Common;
using ForwardAgilityApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ForwardAgilityApi.Tests;

public class ForwardAgilityFactory : WebApplicationFactory<Program>
{
    private readonly DbConnection _connection;

    public ForwardAgilityFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));
        });

        builder.UseSetting("Jwt:Key", "test-secret-key-minimum-32-characters-long!");
        builder.UseSetting("Jwt:Issuer", "ForwardAgilityApi");
        builder.UseSetting("Jwt:Audience", "ForwardAgilityApi");
        builder.UseSetting("Security:LoginRateLimit", "1000");
        builder.UseSetting("Security:MaxFailedLoginAttempts", "3");
        builder.UseSetting("Security:LockoutDurationMinutes", "15");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
