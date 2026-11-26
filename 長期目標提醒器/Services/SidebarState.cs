namespace 長期目標提醒器.Services;

public sealed class SidebarState
{
    public bool Collapsed { get; private set; }

    public bool MobileOpen { get; private set; }

    public event Action? Changed;

    public void ToggleCollapsed()
    {
        Collapsed = !Collapsed;
        Changed?.Invoke();
    }

    public void ToggleMobile()
    {
        MobileOpen = !MobileOpen;
        Changed?.Invoke();
    }

    public void CloseMobile()
    {
        if (!MobileOpen)
        {
            return;
        }

        MobileOpen = false;
        Changed?.Invoke();
    }
}
