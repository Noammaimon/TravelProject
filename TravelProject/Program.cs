using Microsoft.EntityFrameworkCore;
using TravelProject.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDb>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("TravelAgencyDB"),
    b => b.UseOracleSQLCompatibility("11")));

builder.Services.AddHostedService<ReminderWorker>();

var app = builder.Build();
app.UseSession();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenario
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var cultureInfo = new System.Globalization.CultureInfo("he-IL");
cultureInfo.DateTimeFormat.Calendar = new System.Globalization.GregorianCalendar();

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(cultureInfo),
    SupportedCultures = new List<System.Globalization.CultureInfo> { cultureInfo },
    SupportedUICultures = new List<System.Globalization.CultureInfo> { cultureInfo }
};

app.UseRequestLocalization(localizationOptions);

app.Run();
