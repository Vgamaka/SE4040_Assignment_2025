using System.Text;
using EvCharge.Api.Infrastructure;
using EvCharge.Api.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EvCharge.Api.Repositories;
using EvCharge.Api.Services;
using EvCharge.Api.Infrastructure.Qr;

var builder = WebApplication.CreateBuilder(args);

// Bind options
builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection("Mongo"));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection("Cors"));

// Add core services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EV Charging Station Booking API",
        Version = "v1",
        Description = "Backend Web API for SE4040 EV Charging System (MongoDB Atlas + ASP.NET Core)"
    });

    // JWT bearer in Swagger
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Paste your JWT here (no 'Bearer ' prefix).",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

// Mongo
builder.Services.AddMongo();

// JWT Authentication
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // dev
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// Jwt service
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// after builder.Services.AddMongo();
builder.Services.AddSingleton<IEvOwnerRepository, EvOwnerRepository>();
builder.Services.AddScoped<IOwnerService, OwnerService>();
// after repository registrations:
builder.Services.AddScoped<IAuthService, AuthService>();
//Add registrations (after Mongo & existing services)
builder.Services.AddSingleton<IStationRepository, StationRepository>();
builder.Services.AddSingleton<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IStationService, StationService>();
// Options
builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection("Booking"));
// Booking module
builder.Services.AddSingleton<IBookingRepository, BookingRepository>();
builder.Services.AddSingleton<IQrTokenService, QrTokenService>();
builder.Services.AddScoped<IBookingService, BookingService>();
// BackOffice module
builder.Services.AddScoped<IBackOfficeService, BackOfficeService>();
builder.Services.AddScoped<IAdminService, AdminService>();

// CORS
var cors = builder.Configuration.GetSection("Cors").Get<CorsOptions>()!;
builder.Services.AddCors(o =>
{
    o.AddPolicy("app", p =>
    {
        p.WithOrigins(cors.AllowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials();
    });
});

var app = builder.Build();

app.MapGet("/__diag", (IConfiguration cfg, IWebHostEnvironment env) =>
{
    var mongo = cfg.GetSection("Mongo").Get<EvCharge.Api.Options.MongoOptions>();
    return Results.Ok(new { env = env.EnvironmentName, db = mongo?.Database, hasConn = !string.IsNullOrWhiteSpace(mongo?.ConnectionString) });
});


// Swagger in Dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EV Charging Station Booking API v1");
        c.RoutePrefix = "swagger";
    });
}

// Kestrel URL is driven by appsettings.Development.json (http://localhost:8085)
app.UseCors("app");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

