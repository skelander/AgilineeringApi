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
    private readonly string _imagesDir = Path.Combine(Path.GetTempPath(), $"fa-test-{Guid.NewGuid():N}");

    public ForwardAgilityFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
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
        builder.UseSetting("Security:WriteRateLimit", "1000");
        builder.UseSetting("Security:MaxFailedLoginAttempts", "3");
        builder.UseSetting("Security:LockoutDurationMinutes", "15");
        builder.UseSetting("Storage:ImagesPath", _imagesDir);
        builder.UseSetting("AdminKey", "test-admin-key");
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.Add("X-Admin-Key", "test-admin-key");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            try { Directory.Delete(_imagesDir, recursive: true); } catch { }
        }
    }
}
