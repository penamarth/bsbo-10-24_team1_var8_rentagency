using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RentAgency.Web.Data;
using RentAgency.Web.Features;
using RentAgency.Web.Features.Accounts;
using RentAgency.Web.Features.Complaints;
using RentAgency.Web.Features.Housing;
using RentAgency.Web.Features.Rentals;
using RentAgency.Web.Infrastructure.Files;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<RentAgencyDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<RentAgencyDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();
builder.Services.AddAuthorization();

builder.Services.AddScoped<IUserDirectory, UserDirectory>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHousing();
builder.Services.AddRentals();
builder.Services.AddComplaints();
builder.Services.AddScoped<RentAgencySystem>();

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddSingleton<IFileStorage>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IOptions<StorageOptions>>().Value;
    var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
    var rootPath = string.IsNullOrWhiteSpace(options.RootPath)
        ? Path.Combine(environment.ContentRootPath, "App_Data", "uploads")
        : options.RootPath;

    return new LocalFileStorage(rootPath);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RentAgencyDbContext>();
    db.Database.Migrate();
}

await IdentitySeeder.SeedAsync(app.Services, app.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseStatusCodePagesWithReExecute("/Home/HandleStatus", "?code={0}");

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program;
