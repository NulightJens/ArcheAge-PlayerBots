namespace AAEmu.Game.Bots.Kernel;

public sealed class BotActionQueue
{
    private readonly Dictionary<string, BotActionBasket> _baskets = new(StringComparer.Ordinal);
    private long _nextSequence;

    public int Count => _baskets.Count;

    public void Push(BotActionBasket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        var name = basket.Node.Name;
        if (_baskets.TryGetValue(name, out var existing))
        {
            if (basket.Relevance > existing.Relevance)
                existing.Relevance = basket.Relevance;
            return;
        }

        basket.Sequence = _nextSequence++;
        _baskets.Add(name, basket);
    }

    public void Replace(BotActionBasket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        basket.Sequence = _nextSequence++;
        _baskets[basket.Node.Name] = basket;
    }

    public BotActionBasket Pop()
    {
        BotActionBasket selected = null;
        string selectedName = null;
        foreach (var pair in _baskets)
        {
            if (selected == null || pair.Value.Relevance > selected.Relevance ||
                pair.Value.Relevance == selected.Relevance && pair.Value.Sequence < selected.Sequence)
            {
                selected = pair.Value;
                selectedName = pair.Key;
            }
        }

        if (selectedName != null)
            _baskets.Remove(selectedName);
        return selected;
    }

    public void RemoveExpired(DateTime now, int expireActionTimeMs = 5000)
    {
        var expiry = TimeSpan.FromMilliseconds(Math.Max(0, expireActionTimeMs));
        List<string> expired = null;
        foreach (var pair in _baskets)
        {
            if (now >= pair.Value.CreatedAt && now - pair.Value.CreatedAt >= expiry)
                (expired ??= []).Add(pair.Key);
        }

        if (expired == null)
            return;
        foreach (var name in expired)
            _baskets.Remove(name);
    }

    public void Clear() => _baskets.Clear();
}
