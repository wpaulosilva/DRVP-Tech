using System;
using System.Text;
using System.Reflection;
using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.Services;
using DanceSchoolApp.Server.Services.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
string YELLOW = Console.IsOutputRedirected ? "" : "\x1b[93m";
string NORMAL = Console.IsOutputRedirected ? "" : "\x1b[39m";

//  Auto-register all services via reflection 
var serviceTypes = Assembly.GetExecutingAssembly()
    .GetTypes()
    .Where(t => t.IsClass && t.Name.EndsWith("Service"));

foreach (var service in serviceTypes)
{
    builder.Services.AddScoped(service);
}

builder.Services.AddHostedService<ClassLifecycleWorker>();
builder.Services.AddHostedService<HolidaySyncWorker>();

//  HttpClient Factory for external API calls
builder.Services.AddHttpClient();
builder.Services.AddControllers();


//  OpenAPI 
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();



//  Database 
var conn = Environment.GetEnvironmentVariable("DanceSchoolApp_DB");
if (conn == null) Console.WriteLine($"{YELLOW}Warning{NORMAL} - DanceSchoolApp_DB environment variable is not set.!");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(conn));

//  CORS 
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("https://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

//  JWT Authentication (cookie-based) 
var jwtSecret = Environment.GetEnvironmentVariable("DanceSchoolApp_JWT_Secret");
if (string.IsNullOrWhiteSpace(jwtSecret))
    Console.WriteLine($"{YELLOW}Warning{NORMAL} — DanceSchoolApp_JWT_Secret environment variable is not set.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "DanceSchoolApp",
            ValidAudience = "DanceSchoolApp",
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret ?? string.Empty)),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };

        // Tell the JWT middleware to read the token from the HttpOnly cookie
        // instead of the Authorization: Bearer header.
        // This is the key change that makes cookie-based JWT work.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Cookies["access_token"];
                if (!string.IsNullOrWhiteSpace(token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

//  Email Service 
var email = Environment.GetEnvironmentVariable("DanceSchoolApp_Email_Password");
if (email == null) Console.WriteLine($"{YELLOW}Warning{NORMAL} - DanceSchoolApp_Email_Password environment variable is not set!");


var app = builder.Build();


app.UseDefaultFiles();
app.UseStaticFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();

public partial class Program { }