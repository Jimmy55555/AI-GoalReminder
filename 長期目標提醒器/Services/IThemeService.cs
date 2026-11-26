namespace 長期目標提醒器.Services;

/// <summary>
/// 外觀主題切換。Web 版用 localStorage；未來 APP 版可換成原生 Preferences，UI 不用改。
/// </summary>
public interface IThemeService
{
    ValueTask<bool> IsDarkAsync();

    ValueTask ToggleAsync();
}
