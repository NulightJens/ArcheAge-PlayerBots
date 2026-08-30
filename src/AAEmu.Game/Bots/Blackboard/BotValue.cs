namespace AAEmu.Game.Bots.Blackboard;

public abstract class BotValue<T>
{
    private T _cached;
    private DateTime? _computedAt;

    protected BotValue(TimeSpan ttl)
    {
        Ttl = ttl == Timeout.InfiniteTimeSpan
            ? Timeout.InfiniteTimeSpan
            : ttl < TimeSpan.Zero ? TimeSpan.Zero : ttl;
    }

    public TimeSpan Ttl { get; }
    public DateTime? ComputedAt => _computedAt;

    public T Get(DateTime now)
    {
        if (!_computedAt.HasValue ||
            (Ttl != Timeout.InfiniteTimeSpan && (now < _computedAt.Value || now - _computedAt.Value >= Ttl)))
        {
            _cached = Compute(now);
            _computedAt = now;
        }

        return _cached;
    }

    public bool TryGetCached(out T value)
    {
        if (!_computedAt.HasValue)
        {
            value = default;
            return false;
        }

        value = _cached;
        return true;
    }

    public virtual void Invalidate()
    {
        _computedAt = null;
    }

    protected abstract T Compute(DateTime now);
}

public sealed class CalculatedValue<T> : BotValue<T>
{
    private readonly Func<T> _compute;

    public CalculatedValue(Func<T> compute, TimeSpan ttl)
        : base(ttl)
    {
        _compute = compute ?? throw new ArgumentNullException(nameof(compute));
    }

    protected override T Compute(DateTime now) => _compute();
}

public sealed class ManualValue<T> : BotValue<T>
{
    private T _value;

    public ManualValue(T value, TimeSpan ttl = default)
        : base(ttl == default ? Timeout.InfiniteTimeSpan : ttl)
    {
        _value = value;
    }

    public T Value
    {
        get => _value;
        set
        {
            _value = value;
            Invalidate();
        }
    }

    protected override T Compute(DateTime now) => _value;
}
