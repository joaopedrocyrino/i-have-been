namespace IHaveBeen.Web.Observability;

// Abuse/failure metrics count every request; cloud warning logs have a fixed budget.
internal sealed class OperationalLogBudget(TimeProvider clock)
{
    private readonly object sync = new();
    private long window;
    private int count;
    public bool TryAcquire()
    {
        lock (sync)
        {
            var minute = clock.GetUtcNow().ToUnixTimeSeconds() / 60;
            if (minute != window) { window = minute; count = 0; }
            return ++count <= 30;
        }
    }
}
