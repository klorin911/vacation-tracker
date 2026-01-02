using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using VacationTracker.Data.Entities;
using VacationTracker.Services;

namespace VacationTracker.Auth;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ProtectedSessionStorage _sessionStorage;
    private readonly IUserService _userService;
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());
    private const string UserEmailKey = "UserEmail";

    public CustomAuthStateProvider(ProtectedSessionStorage sessionStorage, IUserService userService)
    {
        _sessionStorage = sessionStorage;
        _userService = userService;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var userSessionResult = await _sessionStorage.GetAsync<string>(UserEmailKey);
            var email = userSessionResult.Success ? userSessionResult.Value : null;

            if (string.IsNullOrEmpty(email))
                return new AuthenticationState(Anonymous);

            var user = await _userService.GetUserByEmailAsync(email);
            if (user == null)
                return new AuthenticationState(Anonymous);

            return new AuthenticationState(BuildClaimsPrincipal(user));
        }
        catch
        {
            return new AuthenticationState(Anonymous);
        }
    }

    public async Task UpdateAuthenticationState(string? email)
    {
        var claimsPrincipal = Anonymous;

        if (!string.IsNullOrEmpty(email))
        {
            var user = await _userService.GetUserByEmailAsync(email);
            if (user != null)
            {
                await _sessionStorage.SetAsync(UserEmailKey, email);
                claimsPrincipal = BuildClaimsPrincipal(user);
            }
            else
            {
                await _sessionStorage.DeleteAsync(UserEmailKey);
            }
        }
        else
        {
            await _sessionStorage.DeleteAsync(UserEmailKey);
        }

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(claimsPrincipal)));
    }

    private static ClaimsPrincipal BuildClaimsPrincipal(User user)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("UserId", user.Id.ToString())
        }, "CustomAuth"));
    }
}
