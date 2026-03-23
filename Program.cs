using System.IdentityModel.Tokens.Jwt;
using System.Text;
using dnotes_backend.Data;
using dnotes_backend.Helpers;
using dnotes_backend.Middleware;
using dnotes_backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════
//  DATABASE
// ══════════════════════════════════════════════════
var conn = builder.Configuration.GetConnectionString("DefaultConnection");

if (conn.Contains("localhost"))
    Console.WriteLine("❌ Using LOCAL DB");
else
    Console.WriteLine("✅ Using AZURE DB");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),

        sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)
    )
);

// ══════════════════════════════════════════════════
//  JWT AUTH
// ══════════════════════════════════════════════════
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSection["SecretKey"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                if (ctx.Exception is SecurityTokenExpiredException)
                    ctx.Response.Headers.Append("Token-Expired", "true");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ══════════════════════════════════════════════════
//  CORS
// ══════════════════════════════════════════════════
var origins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
    p.WithOrigins(origins)
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// ══════════════════════════════════════════════════
//  SERVICES — Dependency Injection
// ══════════════════════════════════════════════════
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IVerifierService, VerifierService>();
//builder.Services.AddScoped<IStripeService, StripeService>();
builder.Services.AddScoped<IEmailService, EmailService>();
//builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<IJwtHelper, JwtHelper>();

// Background service — runs daily death trigger check
builder.Services.AddHostedService<DeathTriggerService>();

// ══════════════════════════════════════════════════
//  CONTROLLERS + SWAGGER
// ══════════════════════════════════════════════════

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DeathNote API",
        Version = "v1",
        Description = "Final messages, delivered with care."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT. Format: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {{
        new OpenApiSecurityScheme {
            Reference = new OpenApiReference {
                Type = ReferenceType.SecurityScheme, Id = "Bearer"
            }
        }, Array.Empty<string>()
    }});
});

// ══════════════════════════════════════════════════
//  HEALTH CHECKS
// ══════════════════════════════════════════════════
//builder.Services.AddHealthChecks()
//    .AddDbContextCheck<AppDbContext>("database");

// ══════════════════════════════════════════════════
//  RATE LIMITING (basic)
// ══════════════════════════════════════════════════
builder.Services.AddMemoryCache();

// ══════════════════════════════════════════════════
//  BUILD + PIPELINE
// ══════════════════════════════════════════════════
var app = builder.Build();

app.UseGlobalExceptionHandler();      // 1. catch all errors
// Serve swagger JSON and UI at the application root ("/")
app.UseSwagger(); // registers /swagger/v1/swagger.json
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeathNote API v1");
    c.RoutePrefix = string.Empty; // make Swagger open at "/"
});
// If you need Development-only behavior, keep it minimal
if (app.Environment.IsDevelopment())
{
    // optionally keep a Development-only swagger endpoint or developer pages
    // but do NOT re-register a different RoutePrefix that overrides root mapping
}
app.UseHttpsRedirection();            // 2. force HTTPS
app.UseCors("Frontend");              // 3. CORS (before auth)
app.UseAuthentication();              // 4. who are you?
app.UseAuthorization();               // 5. what can you do?
app.MapControllers();                 // 6. route requests
//app.MapHealthChecks("/health");       // 7. health check

// ══════════════════════════════════════════════════
//  AUTO MIGRATE ON STARTUP
// ══════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();