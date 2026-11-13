using System.Windows;
using WindowFix.Core.Lock;
using Forms = System.Windows.Forms;

namespace WindowFix.Tray;

/// <summary>Optional notification-area icon. Menu: open, apply a layout, lock on/off (or re-lock when paused), quit.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly AppHost _host;
    private readonly Forms.NotifyIcon _icon;

    public TrayIcon(AppHost host)
    {
        _host = host;
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/WindowFix.ico")).Stream;
        _icon = new Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize),
            Text = "WindowFix",
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };
        _icon.ContextMenuStrip.Opening += (_, _) => BuildMenu();
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) _host.ShowMainWindow(); };
        BuildMenu();
    }

    public bool Visible
    {
        get => _icon.Visible;
        set => _icon.Visible = value;
    }

    public void Update(LockState state) =>
        _icon.Text = state switch
        {
            LockState.Active => "WindowFix · 창 고정 중",
            LockState.Paused => "WindowFix · 잠깐 풀어 둠",
            _ => "WindowFix",
        };

    private void BuildMenu()
    {
        var menu = _icon.ContextMenuStrip!;
        menu.Items.Clear();
        bool dark = ThemeManager.IsDark;
        menu.Renderer = new ThemedMenuRenderer(dark);
        menu.Font = new System.Drawing.Font("Segoe UI", 9.75f);
        menu.Padding = new Forms.Padding(2, 4, 2, 4);
        menu.ShowImageMargin = true;
        if (menu.IsHandleCreated) ThemedMenuRenderer.RoundCorners(menu); else menu.HandleCreated += (_, _) => ThemedMenuRenderer.RoundCorners(menu);
        var open = new Forms.ToolStripMenuItem("WindowFix 열기", null, (_, _) => _host.ShowMainWindow()) { Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold) };
        menu.Items.Add(open);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var apply = new Forms.ToolStripMenuItem("이 배치로 맞추기");
        foreach (var p in _host.Config.Presets)
        {
            var id = p.Id;
            apply.DropDownItems.Add(new Forms.ToolStripMenuItem(p.Name, null, (_, _) => _host.ApplyPreset(id))
            {
                Checked = p.Id == _host.CurrentPreset?.Id,
            });
        }
        apply.Enabled = apply.DropDownItems.Count > 0;
        apply.DropDown.Renderer = menu.Renderer;
        apply.DropDown.Font = menu.Font;
        menu.Items.Add(apply);

        switch (_host.Lock.State)
        {
            case LockState.Paused:
                menu.Items.Add(new Forms.ToolStripMenuItem("다시 고정하기 (원래 자리로)", null, (_, _) => _host.Lock.TogglePause()));
                break;
            case LockState.Active:
                menu.Items.Add(new Forms.ToolStripMenuItem("잠깐 풀기", null, (_, _) => _host.Lock.TogglePause()));
                break;
        }
        menu.Items.Add(new Forms.ToolStripMenuItem("창 고정", null, (_, _) => _host.SetLockEnabled(!_host.Config.Settings.LockEnabled))
        {
            Checked = _host.Config.Settings.LockEnabled,
            Enabled = _host.Config.Presets.Count > 0,
        });

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("WindowFix 끝내기", null, (_, _) => _host.Shutdown()) { Tag = "danger" });
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
