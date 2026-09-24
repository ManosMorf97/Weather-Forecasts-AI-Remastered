using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Analytics;
using WeatherUserActions.AppwriteServices;
using WeatherUserActions.BackgroundServices;
using WeatherUserActions.Data;
using WeatherUserActions.Email;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services;

var builder = WebApplication.CreateBuilder(args);

// Machine-local secrets (SMTP credentials, ...). Git-ignored; loaded last so it overrides
// appsettings*.json. Copy secrets.example.json to secrets.json to set up locally.
builder.Configuration.AddJsonFile("secrets.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
//explain. what is singleton
builder.Services.AddSingleton<IAppwriteAuthService, AppwriteAuthService>();
builder.Services.AddSingleton<IAppwriteUsersService, AppwriteUsersService>();
builder.Services.AddSingleton<IAnalyticsReportRenderer, AnalyticsReportRenderer>();

// Real SMTP delivery only when a mail host is configured; otherwise log the message.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Email:Host"]))
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
}
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ISelectionsRepository, SelectionsRepository>();
builder.Services.AddScoped<ISelectionsService, SelectionsService>();
builder.Services.AddScoped<IUserServicesRepository, UserServicesRepository>();
builder.Services.AddScoped<IUserServicesService, UserServicesService>();
builder.Services.AddScoped<IForecastsRepository, ForecastsRepository>();
builder.Services.AddScoped<IForecastsService, ForecastsService>();
builder.Services.AddScoped<IRatingsRepository, RatingsRepository>();
builder.Services.AddScoped<IRatingsService, RatingsService>();
builder.Services.AddScoped<IAggregatedForecastsRepository, AggregatedForecastsRepository>();
builder.Services.AddScoped<IAggregatedForecastsService, AggregatedForecastsService>();
builder.Services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddHostedService<AnalyticsReportWorker>();

var databaseSection = builder.Configuration.GetSection("Database");
var connectionString = new SqlConnectionStringBuilder
{
    DataSource = Environment.GetEnvironmentVariable("YOUR_DATABASE_SERVER") ?? databaseSection["Server"],
    InitialCatalog = databaseSection["Name"],
    UserID = Environment.GetEnvironmentVariable("YOUR_USER") ?? databaseSection["User"],
    Password = Environment.GetEnvironmentVariable("YOUR_PASSWORD") ?? databaseSection["Password"],
    TrustServerCertificate = true,
    MultipleActiveResultSets = true,
}.ConnectionString;

builder.Services.AddDbContext<WeatherUserActionsDbContext>(options =>
{
    options.UseSqlServer(connectionString);

    // Development-only: log SQL with parameter values to the console.
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
    }
});

// The frontend origin allowed to call this API directly (i.e. not through the Vite dev proxy,
// which never needs CORS since the browser only ever talks to Vite's own origin).
const string FrontendCorsPolicy = "Frontend";
var frontendUrl = Environment.GetEnvironmentVariable("YOUR_FRONTEND_URL")
    ?? builder.Configuration["Cors:AllowedOrigin"];
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        if (!string.IsNullOrWhiteSpace(frontendUrl))
        {
            policy.WithOrigins(frontendUrl).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors(FrontendCorsPolicy);
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
