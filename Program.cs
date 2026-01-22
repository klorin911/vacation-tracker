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
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieEmailAuthHandler.SchemeName;
    options.DefaultChallengeScheme = CookieEmailAuthHandler.SchemeName;
}).AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, CookieEmailAuthHandler>(
    CookieEmailAuthHandler.SchemeName,
    options => { });
builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage.ProtectedSessionStorage>();

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IVacationService, VacationService>();
builder.Services.AddSingleton<IDraftService, DraftService>();
builder.Services.AddHostedService<DraftBackgroundService>();

var app = builder.Build();

// Seed data
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    using var context = contextFactory.CreateDbContext();
    DbInitializer.Initialize(context);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}


app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
