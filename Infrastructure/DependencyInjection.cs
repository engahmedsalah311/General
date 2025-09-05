using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Context;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using Polly;
using Polly.Extensions.Http;
using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.Tasks;
using Application.DTOs;
using Application.Services;
using Infrastructure.Repositories;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Configure DbContext (runtime)
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

            // Configure Identity
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

            // Configure Identity cookies
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.SlidingExpiration = true;
            });

            // Add CORS policy
            var corsOrigins = configuration.GetSection("CorsOrigins").Get<string[]>();
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder =>
                    {
                        builder.WithOrigins(corsOrigins ?? Array.Empty<string>())
                               .AllowAnyMethod()
                               .AllowAnyHeader()
                               .AllowCredentials();
                    });
            });

            // Register custom services
            services.AddScoped<IJwtService,JwtService>();
            services.AddScoped<IAuthService,AuthService>();

            services.AddScoped<IPaymentService, MobilemobPaymentService>();

            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IGenericService<BaseEntity, GeneralDto>, GenericService<BaseEntity, GeneralDto>>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICartService, CartService>();
            // Health checks
            var mobilemobBaseUrl = configuration["Mobilemob:BaseUrl"] ?? "https://api.mobilemob.com/v1";
            services.AddHealthChecks()
                .AddUrlGroup(
                    uri: new Uri(new Uri(mobilemobBaseUrl), "health"),
                    name: "mobilemob-api",
                    failureStatus: HealthStatus.Degraded,
                    tags: new[] { "payment", "external" });

            // HTTP client with Polly
            services.AddHttpClient<IPaymentService, MobilemobPaymentService>(
                client => ConfigureHttpClient(client, configuration))
                .AddPolicyHandler((sp, request) =>
                {
                    var logger = sp.GetRequiredService<ILogger<MobilemobPaymentService>>();
                    return GetRetryPolicy(logger);
                })
                .AddPolicyHandler((sp, request) =>
                {
                    var logger = sp.GetRequiredService<ILogger<MobilemobPaymentService>>();
                    return GetCircuitBreakerPolicy(logger);
                });

            // JWT Authentication
            var jwtSettings = configuration.GetSection("JwtSettings");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Secret"]!);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            return services;
        }

        private static void ConfigureHttpClient(HttpClient client, IConfiguration configuration)
        {
            var baseUrl = configuration["Mobilemob:BaseUrl"] ?? "https://api.mobilemob.com/v1";
            var apiKey = configuration["Mobilemob:ApiKey"];

            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => (int)msg.StatusCode == 429)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
                        TimeSpan.FromMilliseconds(new Random().Next(0, 100)),
                    onRetry: (outcome, delay, retryAttempt, context) =>
                    {
                        logger.LogWarning(
                            "Delaying for {delay}ms, then making retry {retryAttempt} for {requestUri}.",
                            delay.TotalMilliseconds,
                            retryAttempt,
                            outcome.Result?.RequestMessage?.RequestUri);
                    });
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger)
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(
                    handledEventsAllowedBeforeBreaking: 5,
                    durationOfBreak: TimeSpan.FromSeconds(30),
                    onBreak: (outcome, breakDelay) =>
                    {
                        logger.LogError(
                            "Circuit breaker opened for {breakDelay}ms due to: {ExceptionMessage}",
                            breakDelay.TotalMilliseconds,
                            outcome.Exception?.Message ?? outcome.Result?.ToString());
                    },
                    onReset: () =>
                    {
                        logger.LogInformation("Circuit breaker reset");
                    },
                    onHalfOpen: () =>
                    {
                        logger.LogInformation("Circuit breaker is half-open");
                    });
        }
    }
}
