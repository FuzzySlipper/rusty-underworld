namespace AbyssRpg.Host;

/// <summary>
/// The Engine lifecycle the product is in. <see cref="AbyssProduct"/> owns the
/// transitions: Start from Stopped, Pause from Running, Resume from Paused.
/// </summary>
public enum AbyssLifecycleMode
{
    Stopped,
    Running,
    Paused,
}
