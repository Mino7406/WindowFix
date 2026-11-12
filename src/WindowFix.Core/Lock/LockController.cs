namespace WindowFix.Core.Lock;

public enum LockState { Off, Active, Paused }

/// <summary>Lock on/off plus the temporary pause toggled by the floating button. Pause is never persisted.</summary>
public sealed class LockController
{
    public LockState State { get; private set; } = LockState.Off;

    public event Action<LockState>? StateChanged;

    /// <summary>Raised when a pause ends: windows should go back to their saved places.</summary>
    public event Action? RequestReapply;

    public void SetEnabled(bool on) => Set(on ? (State == LockState.Off ? LockState.Active : State) : LockState.Off);

    public void TogglePause()
    {
        switch (State)
        {
            case LockState.Active:
                Set(LockState.Paused);
                break;
            case LockState.Paused:
                Set(LockState.Active);
                RequestReapply?.Invoke();
                break;
        }
    }

    private void Set(LockState next)
    {
        if (next == State) return;
        State = next;
        StateChanged?.Invoke(next);
    }
}
