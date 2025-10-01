using System.Text;
using System.Security.Claims;
using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

using backend.Repositories;
using backend.Services;

var builder = WebApplication.CreateBuilder(args);

// ==========================
// 🔹 MongoDB Configuration
// ==========================
var mongoSettings = builder.Configuration.GetSection("Mongo");
var mongoClient = new MongoClient(mongoSettings["ConnectionString"]);
var mongoDatabase = mongoClient.GetDatabase(mongoSettings["Database"]);

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton<IMongoDatabase>(mongoDatabase);

// ==========================
// 🔹 Register Repositories & Services
// ==========================
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<EvOwnerRepository>();
builder.Services.AddScoped<StationRepository>();
builder.Services.AddScoped<BookingRepository>();
builder.Services.AddSingleton<SessionRepository>(); // for SessionsController

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<StationService>();

// ==========================
// 🔹 CORS (for Vite dev server)
// ==========================
builder.Services.AddCors(o => o.AddPolicy("AppCors", p =>
    p.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
     .AllowAnyHeader()
     .AllowAnyMethod()
));

// ==========================
/* 🔹 Authentication (JWT) */
var jwtSection = builder.Configuration.GetSection("Jwt");
var secret   = jwtSection["Secret"]   ?? "dev-secret-change-me-please-32chars-min";
var issuer   = jwtSection["Issuer"]   ?? "default-issuer";
var audience = jwtSection["Audience"] ?? "default-audience";

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // dev
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = issuer,
            ValidAudience            = audience,
            IssuerSigningKey         = signingKey,
            ClockSkew                = TimeSpan.FromSeconds(5),

            // ✅ Treat ClaimTypes.Role as the role claim (your token already maps "role" → this)
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "unique_name"
        };

        // (optional) diagnostics during dev
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"[JWT] Auth failed: {ctx.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                var roles = string.Join(",", ctx.Principal?.Claims
                    .Where(c => c.Type == "role" || c.Type == ClaimTypes.Role)
                    .Select(c => $"{c.Type}:{c.Value}") ?? Array.Empty<string>());
                Console.WriteLine($"[JWT] Token validated. Roles seen: {roles}");
                return Task.CompletedTask;
            }
        };
    });

// ==========================
// 🔹 Authorization Policies
// ==========================
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BackofficeOnly", p => p.RequireRole("Backoffice"));
    options.AddPolicy("OperatorOnly",   p => p.RequireRole("StationOperator"));
    options.AddPolicy("OwnerOnly",      p => p.RequireRole("EvOwner"));
});

// ==========================
// 🔹 Controllers + Swagger
// ==========================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EV Charging Station Booking API",
        Version = "v1",
        Description = "Backend Web API for SE4040 EV Charging System (MongoDB Atlas + ASP.NET Core)"
    });

    // ✅ HTTP Bearer: paste raw JWT (no 'Bearer ' prefix)
    opt.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste your JWT access token here. Do NOT include the 'Bearer ' prefix."
    });

    opt.AddSecurityRequirement(new OpenApiSecurityRequirement
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

// ==========================
// 🔹 Build App
// ==========================
var app = builder.Build();

// ==========================
// 🔹 Middleware Pipeline (order matters)
// ==========================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EV Charging API v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AppCors");
// app.UseHttpsRedirection(); // optional in dev

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
