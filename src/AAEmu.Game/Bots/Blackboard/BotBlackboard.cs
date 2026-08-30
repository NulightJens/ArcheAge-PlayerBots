using System.Collections.Generic;

namespace AAEmu.Game.Bots.Blackboard;

public sealed class BotBlackboard
{
    private interface IRegisteredValue
    {
        (string name, string value, DateTime computedAt) Snapshot(string name);
        void Invalidate();
    }

    private sealed class RegisteredValue<T> : IRegisteredValue
    {
        public RegisteredValue(BotValue<T> value)
        {
            Value = value;
        }

        public BotValue<T> Value { get; }

        public (string name, string value, DateTime computedAt) Snapshot(string name)
        {
            var computedAt = Value.ComputedAt ?? DateTime.MinValue;
            return (name, Value.TryGetCached(out var cached) ? cached?.ToString() ?? "null" : "<not computed>", computedAt);
        }

        public void Invalidate() => Value.Invalidate();
    }

    private readonly Dictionary<string, IRegisteredValue> _values = new(StringComparer.Ordinal);

    public void Register<T>(ValueKey<T> key, BotValue<T> value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key.Name);
        ArgumentNullException.ThrowIfNull(value);
        _values[key.Name] = new RegisteredValue<T>(value);
    }

    public T Get<T>(ValueKey<T> key, DateTime now)
    {
        return ((RegisteredValue<T>)_values[key.Name]).Value.Get(now);
    }

    public bool TryGet<T>(ValueKey<T> key, DateTime now, out T value)
    {
        if (_values.TryGetValue(key.Name, out var registered) && registered is RegisteredValue<T> typed)
        {
            value = typed.Value.Get(now);
            return true;
        }

        value = default;
        return false;
    }

    public bool TryGet<T>(ValueKey<T> key, DateTime now)
    {
        return TryGet(key, now, out _);
    }

    public void Invalidate<T>(ValueKey<T> key)
    {
        ((RegisteredValue<T>)_values[key.Name]).Value.Invalidate();
    }

    public void InvalidateAll()
    {
        foreach (var value in _values.Values)
            value.Invalidate();
    }

    public IEnumerable<(string name, string value, DateTime computedAt)> Snapshot()
    {
        foreach (var (name, value) in _values)
            yield return value.Snapshot(name);
    }
}
