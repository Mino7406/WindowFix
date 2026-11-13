using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WindowFix.Dialogs;

/// <summary>Themed replacement for MessageBox / InputBox: rounded card, pops in, follows dark/light theme.</summary>
public partial class AppDialog : Window
{
    private AppDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var ease = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut };
            Pop.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
            Pop.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(120)));
            if (InputBox.Visibility == Visibility.Visible) { InputBox.Focus(); InputBox.SelectAll(); }
        };
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    private static AppDialog Create(Window? owner, string title, string message)
    {
        var d = new AppDialog();
        d.TitleText.Text = title;
        d.MessageText.Text = message;
        d.MessageText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        if (owner is { IsVisible: true }) d.Owner = owner;
        else d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return d;
    }

    /// <summary>Yes/no question. <paramref name="danger"/> paints the confirm button red (delete, etc.).</summary>
    public static bool Confirm(Window? owner, string title, string message, string ok = "확인", string cancel = "취소", bool danger = false)
    {
        var d = Create(owner, title, message);
        d.OkButton.Content = ok;
        d.CancelButton.Content = cancel;
        if (danger) d.OkButton.SetResourceReference(BackgroundProperty, "Danger.Fg");
        return d.ShowDialog() == true;
    }

    /// <summary>Text input; null when cancelled or left empty.</summary>
    public static string? Prompt(Window? owner, string title, string message, string initial)
    {
        var d = Create(owner, title, message);
        d.InputBox.Visibility = Visibility.Visible;
        d.InputBox.Text = initial;
        d.OkButton.Content = "저장";
        if (d.ShowDialog() != true) return null;
        var text = d.InputBox.Text.Trim();
        return text.Length == 0 ? null : text;
    }

    /// <summary>Information with a single button.</summary>
    public static void Info(Window? owner, string title, string message)
    {
        var d = Create(owner, title, message);
        d.CancelButton.Visibility = Visibility.Collapsed;
        d.ShowDialog();
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
