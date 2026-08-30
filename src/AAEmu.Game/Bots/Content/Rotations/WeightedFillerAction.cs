using AAEmu.Game.Bots.Kernel;

namespace AAEmu.Game.Bots.Content.Rotations;

public sealed record WeightedRotationAction(IBotAction Action, float Weight, Func<BotContext, bool> Gate = null);

public sealed class WeightedFillerAction : IBotAction
{
    private readonly IReadOnlyList<WeightedRotationAction> _rows;
    private readonly Func<int> _roll;
    private readonly object _selectionLock = new();
    private readonly List<string> _selectedActionNames = [];
    private readonly List<WeightedRotationAction> _eligible = [];

    public WeightedFillerAction(IEnumerable<WeightedRotationAction> rows, Func<int> roll = null)
    {
        _rows = rows?.Where(row => row.Action != null && row.Weight > 0).ToArray() ?? [];
        _roll = roll ?? (() => AAEmu.Game.Bots.Host.BotHost.Instance.Roll());
    }

    public string Name => "filler";
    public string LastSelectedActionName
    {
        get
        {
            lock (_selectionLock)
                return _selectedActionNames.Count == 0 ? null : _selectedActionNames[^1];
        }
    }

    public IReadOnlyList<string> LastSelectedActionNames
    {
        get
        {
            lock (_selectionLock)
                return _selectedActionNames.ToArray();
        }
    }

    public bool IsUseful(BotContext context)
    {
        for (var index = 0; index < _rows.Count; index++)
            if (IsEligible(_rows[index], context))
                return true;
        return false;
    }

    public bool IsPossible(BotContext context)
    {
        for (var index = 0; index < _rows.Count; index++)
            if (IsEligible(_rows[index], context) && (context == null || _rows[index].Action.IsPossible(context)))
                return true;
        return false;
    }

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var action = SelectAction(context);
        if (action == null)
            return BotActionResult.Impossible;

        lock (_selectionLock)
        {
            if (_selectedActionNames.Count == 8)
                _selectedActionNames.RemoveAt(0);
            _selectedActionNames.Add(action.Name);
        }
        return action.Execute(context, ev);
    }

    public IBotAction SelectAction(BotContext context = null)
    {
        _eligible.Clear();
        for (var index = 0; index < _rows.Count; index++)
        {
            var row = _rows[index];
            if (IsEligible(row, context) && (context == null || row.Action.IsPossible(context)))
                _eligible.Add(row);
        }

        if (_eligible.Count == 0)
            return null;

        var total = 0;
        for (var index = 0; index < _eligible.Count; index++)
            total += Math.Max(1, (int)MathF.Round(_eligible[index].Weight));

        var draw = Math.Abs(_roll()) % total;
        var cursor = 0;
        for (var index = 0; index < _eligible.Count; index++)
        {
            var row = _eligible[index];
            cursor += Math.Max(1, (int)MathF.Round(row.Weight));
            if (draw < cursor)
                return row.Action;
        }

        return _eligible[^1].Action;
    }

    private static bool IsEligible(WeightedRotationAction row, BotContext context) =>
        (context == null || row.Gate?.Invoke(context) != false) &&
        (context == null || row.Action.IsUseful(context));
}
