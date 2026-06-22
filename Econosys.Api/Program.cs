using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Econosys.Api.Data;
using Econosys.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var jwtSettings = builder.Configuration.GetSection("Jwt");

// Add services to the container
builder.Services.AddControllers();

// Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Server=localhost;Database=Econosys;User Id=sa;Password=Losenord101;TrustServerCertificate=true;"));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password requirements
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var secretKey = builder.Configuration["Jwt:SecretKey"];
if (string.IsNullOrEmpty(secretKey))
    throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (string.IsNullOrEmpty(context.Token) &&
                context.Request.Cookies.TryGetValue("access_token", out var cookieToken))
            {
                context.Token = cookieToken;
            }

            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            if (context.Exception is SecurityTokenExpiredException)
                context.Response.Headers.Append("X-Token-Expired", "true");
            return Task.CompletedTask;
        }
    };
});

// CORS
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("ActivePolicy", corsoptions =>
        {
            corsoptions.SetIsOriginAllowed(_ => true)
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
        });
    }
    else
    {
        options.AddPolicy("ActivePolicy", corsBuilder =>
        {
            // var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(";") ?? Array.Empty<string>();
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                    ?? new[] {
                        "http://localhost:4200",
                        "http://localhost:5173",
                        "https://localhost:5173",
                        "http://localhost:5174",
                        "https://localhost:5174",
                        "http://localhost:5175",
                        "https://localhost:5175",
                        "https://econosys.abloyd.se" };
            corsBuilder.SetIsOriginAllowed(origin => allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
        });
    }
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Services
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<ILegacyUserResolutionService, LegacyUserResolutionService>();
builder.Services.AddScoped<IPalletFormatOptionsService, PalletFormatOptionsService>();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Initialize database and roles
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    try
    {
        // Apply migrations
        await dbContext.Database.MigrateAsync();

        // Create roles
        var roles = new[] { "Admin", "CEO", "Finance", "Sales", "Logistics", "User", "Moderator" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed users with their roles
        var seedUsers = new[]
        {
            new { Email = "admin@econosys.com", FirstName = "System", LastName = "Administrator", Role = "Admin", Password = "Admin@12345Secure!" },
            new { Email = "michael.jensen@econosys.com", FirstName = "Michael", LastName = "Jensen", Role = "CEO", Password = "CEO@Secure2024!" },
            new { Email = "sarah.anderson@econosys.com", FirstName = "Sarah", LastName = "Anderson", Role = "Finance", Password = "Finance@2024Eco!" },
            new { Email = "james.mitchell@econosys.com", FirstName = "James", LastName = "Mitchell", Role = "Sales", Password = "Sales@Econosys24!" },
            new { Email = "emma.williams@econosys.com", FirstName = "Emma", LastName = "Williams", Role = "Logistics", Password = "Logistics@2024!" }
        };

        foreach (var seedUser in seedUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(seedUser.Email);
            if (existingUser == null)
            {
                var newUser = new ApplicationUser
                {
                    UserName = seedUser.Email,
                    Email = seedUser.Email,
                    FirstName = seedUser.FirstName,
                    LastName = seedUser.LastName,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedAt = SwedishTime.Now
                };

                var createResult = await userManager.CreateAsync(newUser, seedUser.Password);
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(newUser, seedUser.Role);
                    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                    logger.LogInformation("Created user: {Email} with role: {Role}", seedUser.Email, seedUser.Role);
                }
            }
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning($"Database initialization error: {ex.Message}. Please run 'dotnet ef database update' manually.");
    }
}

// Configure the HTTP request pipeline
// if (app.Environment.IsDevelopment())
// {
//     app.UseSwagger();
//     app.UseSwaggerUI(options =>
//     {
//         options.SwaggerEndpoint("/swagger/v1/swagger.json", "Econosys API v1");
//         options.RoutePrefix = string.Empty;
//     });
// }
// else
// {
//     app.UseHsts();
// }
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Econosys API v1");
    options.RoutePrefix = string.Empty;
});
app.UseHttpsRedirection();


// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-Powered-By");

    try
    {
        await next();
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
        app.Logger.LogDebug("Request was canceled by the client: {Method} {Path}", context.Request.Method, context.Request.Path);
    }
    catch (IOException) when (context.RequestAborted.IsCancellationRequested)
    {
        app.Logger.LogDebug("Request stream was reset by the client: {Method} {Path}", context.Request.Method, context.Request.Path);
    }
});

app.UseHttpsRedirection();

// Add CORS middleware
app.UseCors("ActivePolicy");

app.UseRateLimiter();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = SwedishTime.Now }))
    .WithName("Health")
    .WithOpenApi();

app.Run();
