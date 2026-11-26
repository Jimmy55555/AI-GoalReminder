using Microsoft.JSInterop;

namespace 長期目標提醒器.Services;

/// <summary>
/// 瀏覽器版主題切換：實際的 class 與 localStorage 讀寫在 App.razor 的 inline script，
/// 這裡只提供給 Blazor 按鈕呼叫的入口。
/// </summary>
public sealed class BrowserThemeService : IThemeService
{
    private readonly IJSRuntime jsRuntime;

    public BrowserThemeService(IJSRuntime jsRuntime)
    {
        this.jsRuntime = jsRuntime;
    }

    public async ValueTask<bool> IsDarkAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<bool>("appTheme.isDark");
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
        catch (JSException)
        {
            return false;
        }
    }

    public async ValueTask ToggleAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("appTheme.toggle");
        }
        catch (JSDisconnectedException)
        {
            // 連線中斷時忽略，下一次連上仍會是已儲存的偏好。
        }
        catch (JSException)
        {
            // JS 尚未準備好時忽略，不影響其他功能。
        }
    }
}
