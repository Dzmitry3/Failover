public sealed class GameSessionState
{
    public enum Status
    {
        WaitingToStart,
        Playing,
        Won,
        Lost
    }

    private float runStartedAt = -1f;

    public Status Current { get; private set; } = Status.WaitingToStart;
    public float RunDuration { get; private set; }
    public bool IsPlaying => Current == Status.Playing;

    public void Reset()
    {
        Current = Status.WaitingToStart;
        runStartedAt = -1f;
        RunDuration = 0f;
    }

    public void StartRun(float time)
    {
        Current = Status.Playing;
        runStartedAt = time;
        RunDuration = 0f;
    }

    public void Complete(Status status, float time)
    {
        Current = status;
        RunDuration = runStartedAt >= 0f ? time - runStartedAt : 0f;
    }
}
