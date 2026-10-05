namespace Applets.WindowsTools;

internal sealed class DisplayCountdown
{
    public long? Due { get; private set; }
    public void Schedule(long now, int seconds) => Due = now + seconds * 1000L;
    public void Cancel() => Due = null;
    public bool TakeDue(long now)
    {
        if (Due is not { } due || now < due) return false;
        Due = null;
        return true;
    }
}

internal sealed class IdleLockPolicy
{
    private long started;
    private uint? lastInput;
    private bool requested;
    public void Reset(long now) { started = now; lastInput = null; requested = false; }
    public bool TakeDue(long now, uint inputTimestamp, int minutes)
    {
        if (lastInput != inputTimestamp) { lastInput = inputTimestamp; requested = false; }
        // LASTINPUTINFO uses the low 32 bits of the uptime counter (wraps after ~49 days).
        var idleMilliseconds = unchecked((uint)now - inputTimestamp);
        if (requested || Math.Min(now - started, idleMilliseconds) < minutes * 60_000L) return false;
        requested = true;
        return true;
    }
}
