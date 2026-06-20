using Microsoft.EntityFrameworkCore;
using Ecommerce.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Multi-Tenant E-Commerce, Inventory, and POS SaaS API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT access token"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// Db Connection Selection: Fallback to SQLite locally for easy testing
bool usePostgres = false;
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (!string.IsNullOrEmpty(connectionString))
{
    try
    {
        var host = "localhost";
        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            if (part.Trim().StartsWith("Host=", StringComparison.OrdinalIgnoreCase))
            {
                var kv = part.Split('=');
                if (kv.Length > 1)
                {
                    host = kv[1].Trim();
                }
                break;
            }
        }

        using (var client = new TcpClient())
        {
            var result = client.BeginConnect(host, 5432, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(1000)); // allow up to 1 second for remote servers
            if (success)
            {
                client.EndConnect(result);
                usePostgres = true;
            }
        }
    }
    catch
    {
        // Fallback to SQLite
    }
}

if (usePostgres)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
    Console.WriteLine("--> Using PostgreSQL Database");
}
else
{
    // local SQLite fallback
    var sqliteConn = "Data Source=ecommerce.db";
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(sqliteConn));
    Console.WriteLine("--> Using SQLite Database locally (PostgreSQL port 5432 is offline)");
}

// Configure CORS for Angular Dashboard frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "super-secret-key-change-in-prod-long-enough-32-chars";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "POS SaaS API v1");
    c.RoutePrefix = "swagger"; // Swagger dashboard is at /swagger
});

app.UseCors("AllowAll");

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Apply DB Migrations/Creation and Seeding automatically
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        Console.WriteLine("--> Checking/Creating Database...");
        try
        {
            var dropSql = @"
                DROP TABLE IF EXISTS audit_logs CASCADE;
                DROP TABLE IF EXISTS suppliers CASCADE;
                DROP TABLE IF EXISTS company_settings CASCADE;
                DROP TABLE IF EXISTS settings CASCADE;
                DROP TABLE IF EXISTS support_tickets CASCADE;
                DROP TABLE IF EXISTS notifications CASCADE;
                DROP TABLE IF EXISTS reviews CASCADE;
                DROP TABLE IF EXISTS coupon_usages CASCADE;
                DROP TABLE IF EXISTS payments CASCADE;
                DROP TABLE IF EXISTS order_items CASCADE;
                DROP TABLE IF EXISTS orders CASCADE;
                DROP TABLE IF EXISTS addresses CASCADE;
                DROP TABLE IF EXISTS coupons CASCADE;
                DROP TABLE IF EXISTS wishlist CASCADE;
                DROP TABLE IF EXISTS cart_items CASCADE;
                DROP TABLE IF EXISTS carts CASCADE;
                DROP TABLE IF EXISTS inventory CASCADE;
                DROP TABLE IF EXISTS product_variants CASCADE;
                DROP TABLE IF EXISTS product_images CASCADE;
                DROP TABLE IF EXISTS products CASCADE;
                DROP TABLE IF EXISTS brands CASCADE;
                DROP TABLE IF EXISTS categories CASCADE;
                DROP TABLE IF EXISTS role_permissions CASCADE;
                DROP TABLE IF EXISTS user_roles CASCADE;
                DROP TABLE IF EXISTS permissions CASCADE;
                DROP TABLE IF EXISTS roles CASCADE;
                DROP TABLE IF EXISTS users CASCADE;
                DROP TABLE IF EXISTS companies CASCADE;
                DROP TABLE IF EXISTS subscription_plans CASCADE;
            ";
            dbContext.Database.ExecuteSqlRaw(dropSql);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"--> Note: Drop tables step skipped/failed: {ex.Message}");
        }

        dbContext.Database.EnsureCreated();
        Console.WriteLine("--> Database is ready & seeded.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"--> Error setting up database: {ex.Message}");
    }
}

app.Run();
