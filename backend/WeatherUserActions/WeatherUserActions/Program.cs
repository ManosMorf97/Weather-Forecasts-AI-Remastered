using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WeatherUserActions.Data;
using WeatherUserActions.FirebaseServices;
using WeatherUserActions.Repositories;
using WeatherUserActions.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
//explain. what is singleton
builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ISelectionsRepository, SelectionsRepository>();
builder.Services.AddScoped<ISelectionsService, SelectionsService>();

var databaseSection = builder.Configuration.GetSection("Database");
var connectionString = new SqlConnectionStringBuilder
{
    DataSource = Environment.GetEnvironmentVariable("YOUR_SERVER") ?? databaseSection["Server"],
    InitialCatalog = databaseSection["Name"],
    UserID = Environment.GetEnvironmentVariable("YOUR_USER") ?? databaseSection["User"],
    Password = Environment.GetEnvironmentVariable("YOUR_PASSWORD") ?? databaseSection["Password"],
    TrustServerCertificate = true,
    MultipleActiveResultSets = true,
}.ConnectionString;

builder.Services.AddDbContext<WeatherUserActionsDbContext>(options =>
    options.UseSqlServer(connectionString));
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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
