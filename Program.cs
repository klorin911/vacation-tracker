using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using VacationTracker.Auth;
using VacationTracker.Components;
using VacationTracker.Data;
using VacationTracker.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedSessionStorage>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVacationService, VacationService>();

var app = builder.Build();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.EnsureCreated();
    if (!context.Users.Any())
    {
        context.Users.Add(new VacationTracker.Data.Entities.User
        {
            Email = "kylerlorin@gmail.com",
            Name = "Kyler Lorin",
            Role = VacationTracker.Data.Entities.Role.Admin,
            WeekQuota = 2,
            DayQuota = 2
        });
        context.Users.Add(new VacationTracker.Data.Entities.User
        {
            Email = "admin@example.com",
            Name = "Employee User",
            Role = VacationTracker.Data.Entities.Role.Employee,
            WeekQuota = 5,
            DayQuota = 5
        });
        context.Users.Add(new VacationTracker.Data.Entities.User
        {
            Email = "user@example.com",
            Name = "Normal User",
            Role = VacationTracker.Data.Entities.Role.Employee,
            WeekQuota = 5,
            DayQuota = 5
        });
        context.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
