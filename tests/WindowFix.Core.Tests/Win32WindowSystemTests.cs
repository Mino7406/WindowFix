using System.Windows.Forms;
using WindowFix.Core.Matching;
using WindowFix.Core.Model;
using WindowFix.Core.Platform;

namespace WindowFix.Core.Tests;

[Trait("Category", "Integration")]
public sealed class Win32WindowSystemTests
{
    private readonly Win32WindowSystem _ws = new();

    [Fact]
    public void GetMonitors_ReturnsExactlyOnePrimary()
    {
        var monitors = _ws.GetMonitors();

        Assert.NotEmpty(monitors);
        Assert.Single(monitors, m => m.IsPrimary);
    }

    [Fact]
    public void GetMonitors_DevicePathsNonEmpty()
    {
        Assert.All(_ws.GetMonitors(), m =>
        {
            Assert.False(string.IsNullOrEmpty(m.DevicePath));
            Assert.StartsWith(@"\\.\DISPLAY", m.GdiName);
            Assert.True(m.Bounds.Width > 0 && m.Bounds.Height > 0);
        });
    }

    [Fact]
    public void GetWindows_IncludesOwnTestHostOrAtLeastOneCandidate()
    {
        RunWithForm(form =>
        {
            var windows = _ws.GetWindows(includeHidden: false);
            var own = Assert.Single(windows, w => w.Handle == form.Handle);
            Assert.True(WindowMatcher.IsCandidate(own));
            Assert.Equal("WindowFixTestForm", own.Title);
            Assert.Equal(Environment.ProcessId, own.ProcessId);
            Assert.EndsWith(".exe", own.ExeName, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void SetPlacement_OnOwnWindow_RoundTripsRect()
    {
        RunWithForm(form =>
        {
            var target = new PixelRect(100, 100, 640, 480);

            Assert.True(_ws.SetPlacement(form.Handle, target, maximize: false));
            Application.DoEvents();

            Assert.Equal(target, _ws.GetWindow(form.Handle)?.Rect);
        });
    }

    [Fact]
    public void SetPlacement_RestoresMinimizedWindowFirst()
    {
        RunWithForm(form =>
        {
            form.WindowState = FormWindowState.Minimized;
            Application.DoEvents();
            var target = new PixelRect(120, 130, 500, 400);

            Assert.True(_ws.SetPlacement(form.Handle, target, maximize: false));
            Application.DoEvents();

            var info = _ws.GetWindow(form.Handle)!;
            Assert.False(info.IsMinimized);
            Assert.Equal(target, info.Rect);
        });
    }

    [Fact]
    public void SetPlacement_CrossThreadMinimized_RestoresAndMoves()
    {
        // Real targets live on another thread/process; requests are posted, not run inline.
        using var ready = new ManualResetEventSlim();
        Form? form = null;
        var ui = new Thread(() =>
        {
            form = new Form
            {
                Text = "WindowFixCrossThread",
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(60, 60),
                Size = new System.Drawing.Size(300, 200),
            };
            form.Shown += (_, _) => { form.WindowState = FormWindowState.Minimized; ready.Set(); };
            Application.Run(form);
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        try
        {
            Assert.True(ready.Wait(5000));
            nint hwnd = (nint)form!.Invoke(() => form.Handle);
            var target = new PixelRect(140, 150, 520, 410);

            Assert.True(_ws.SetPlacement(hwnd, target, maximize: false));

            WindowInfo? info = null;
            for (int i = 0; i < 50; i++)
            {
                info = _ws.GetWindow(hwnd);
                if (info is { IsMinimized: false } && info.Rect == target) break;
                Thread.Sleep(50);
            }
            Assert.False(info!.IsMinimized);
            Assert.Equal(target, info.Rect);
        }
        finally
        {
            form?.Invoke(() => form.Close());
            ui.Join(5000);
        }
    }

    [Fact]
    public void SetPlacement_Resize_PlaysBorderDragSequenceAndKeepsAppAdjustedSize()
    {
        // Games (MapleStory) switch resolution only inside ENTERSIZEMOVE → SIZING → EXITSIZEMOVE and may snap the size.
        using var ready = new ManualResetEventSlim();
        SnapForm? form = null;
        var ui = new Thread(() =>
        {
            form = new SnapForm();
            form.Shown += (_, _) => ready.Set();
            Application.Run(form);
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        try
        {
            Assert.True(ready.Wait(5000));
            nint hwnd = (nint)form!.Invoke(() => form.Handle);

            Assert.True(_ws.SetPlacement(hwnd, new PixelRect(150, 160, 734, 577), maximize: false));

            Assert.Equal(new PixelRect(150, 160, 700, 500), _ws.GetWindow(hwnd)?.Rect);
            Assert.Equal(["enter", "sizing 734x577", "exit"], form.Seen);

            // a move without a size change stays a plain move
            form.Seen.Clear();
            Assert.True(_ws.SetPlacement(hwnd, new PixelRect(180, 190, 700, 500), maximize: false));
            Thread.Sleep(200);
            Assert.Empty(form.Seen);
        }
        finally
        {
            form?.Invoke(() => form.Close());
            ui.Join(5000);
        }
    }

    /// <summary>Snaps any size to multiples of 100 in WM_SIZING, like a game picking a supported resolution.</summary>
    private sealed class SnapForm : Form
    {
        public List<string> Seen { get; } = [];

        public SnapForm()
        {
            StartPosition = FormStartPosition.Manual;
            Location = new System.Drawing.Point(60, 60);
            Size = new System.Drawing.Size(400, 300);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x0231: Seen.Add("enter"); break;
                case 0x0232: Seen.Add("exit"); break;
                case 0x0214:
                    var r = System.Runtime.InteropServices.Marshal.PtrToStructure<System.Drawing.Rectangle>(m.LParam);
                    // RECT is left/top/right/bottom; read through Rectangle's fields as raw ints
                    int l = r.X, t = r.Y, w = r.Width - l, h = r.Height - t;
                    Seen.Add($"sizing {w}x{h}");
                    var snapped = new System.Drawing.Rectangle(l, t, l + w / 100 * 100, t + h / 100 * 100);
                    System.Runtime.InteropServices.Marshal.StructureToPtr(snapped, m.LParam, false);
                    m.Result = 1;
                    return;
            }
            base.WndProc(ref m);
        }
    }

    [Fact]
    public void GetWindow_InvalidHandle_ReturnsNull()
    {
        Assert.Null(_ws.GetWindow(unchecked((nint)0x7FFF_FFF0)));
    }

    private static void RunWithForm(Action<Form> body)
    {
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                using var form = new Form
                {
                    Text = "WindowFixTestForm",
                    StartPosition = FormStartPosition.Manual,
                    Location = new System.Drawing.Point(50, 50),
                    Size = new System.Drawing.Size(300, 200),
                    ShowInTaskbar = true,
                };
                form.Show();
                Application.DoEvents();
                body(form);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error is not null) throw new Xunit.Sdk.XunitException(error.ToString());
    }
}
