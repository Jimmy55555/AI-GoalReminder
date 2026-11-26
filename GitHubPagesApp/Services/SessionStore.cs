using System.Text.Json;
using Microsoft.JSInterop;

namespace 長期目標提醒器.Services;

/// <summary>
/// 只在使用者瀏覽器保存 Supabase access/refresh token；敏感金鑰永遠不放前端。
/// </summary>
public sealed class SessionStore(IJSRuntime jsRuntime)
{
    private const string StorageKey = "goal_reminder_session";

    public SupabaseSession? Current { get; private set; }

    public string? AccessToken => Current?.AccessToken;
    public string? UserId => Current?.UserId;

    public async Task InitializeAsync()
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            Current = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<SupabaseSession>(json, SupabaseClient.JsonOptions);
        }
        catch (JSException)
        {
            Current = null;
        }
        catch (InvalidOperationException)
        {
            Current = null;
        }
    }

    public async Task SaveAsync(SupabaseSession session)
    {
        Current = session;
        var json = JsonSerializer.Serialize(session, SupabaseClient.JsonOptions);
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    public async Task ClearAsync()
    {
        Current = null;
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
