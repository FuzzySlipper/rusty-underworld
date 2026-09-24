namespace AbyssRpg.Host;

/// <summary>
/// Product lifecycle modes. The session plugs into Running later (UW-T02);
/// this machine owns the transitions so pause/resume/stop are honest before
/// any session exists.
/// </summary>
public enum AbyssLifecycleMode
{
    Stopped,
    Running,
    Paused,
}

public sealed class AbyssProductLifecycle
{
    public AbyssLifecycleMode Mode { get; private set; } = AbyssLifecycleMode.Stopped;

    public void Start()
    {
        if (Mode != AbyssLifecycleMode.Stopped)
            throw new InvalidOperationException($"Cannot start from {Mode}.");
        Mode = AbyssLifecycleMode.Running;
    }

    public void Pause()
    {
        if (Mode != AbyssLifecycleMode.Running)
            throw new InvalidOperationException($"Cannot pause from {Mode}.");
        Mode = AbyssLifecycleMode.Paused;
    }

    public void Resume()
    {
        if (Mode != AbyssLifecycleMode.Paused)
            throw new InvalidOperationException($"Cannot resume from {Mode}.");
        Mode = AbyssLifecycleMode.Running;
    }

    public void Stop() => Mode = AbyssLifecycleMode.Stopped;
}
