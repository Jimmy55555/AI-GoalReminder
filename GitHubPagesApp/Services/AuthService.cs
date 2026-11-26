namespace 長期目標提醒器.Services;

public sealed class AuthService(
    SessionStore sessionStore,
    SupabaseAuthClient authClient)
{
    public bool IsAuthenticated => sessionStore.Current is not null;
    public string? Email => sessionStore.Current?.Email;

    public async Task InitializeAsync()
    {
        await sessionStore.InitializeAsync();

        var session = sessionStore.Current;
        if (session is null)
        {
            return;
        }

        if (session.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return;
        }

        try
        {
            var refreshed = await authClient.RefreshAsync(session.RefreshToken);
            if (string.IsNullOrWhiteSpace(refreshed.AccessToken))
            {
                await sessionStore.ClearAsync();
                return;
            }

            await sessionStore.SaveAsync(refreshed);
        }
        catch (SupabaseException)
        {
            await sessionStore.ClearAsync();
        }
        catch (HttpRequestException)
        {
            await sessionStore.ClearAsync();
        }
    }

    public async Task SignInAsync(string email, string password)
    {
        var session = await authClient.SignInAsync(email, password);
        await sessionStore.SaveAsync(session);
    }

    public Task SignOutAsync() => sessionStore.ClearAsync();
}
