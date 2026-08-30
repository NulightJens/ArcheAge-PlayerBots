using System.Runtime.CompilerServices;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Kernel;

public sealed class BotEngine
{
    private const float PrerequisiteEpsilon = 0.002f;
    private const float SelfEpsilon = 0.001f;
    private const float AlternativeEpsilon = 0.003f;
    private const int LastActionCapacity = 32;

    private readonly Dictionary<string, IBotStrategy> _strategies = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBotAction> _registeredActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IBotAction> _actions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BotActionNode> _actionNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BotTriggerNode> _triggerNodes = [];
    private readonly List<IBotMultiplier> _multipliers = [];
    private readonly List<BotActionLog> _lastActionLog = [];
    private readonly List<string> _siblingNames = [];
    private readonly Dictionary<IBotTrigger, bool> _triggerResults = new(TriggerReferenceComparer.Instance);
    private readonly BotConfig _config;
    private readonly BotActionQueue _queue = new();
    private readonly BotNextAction[] _selfPush = new BotNextAction[1];

    public BotEngine(
        BotEngineKind kind,
        BotConfig config = null,
        IEnumerable<IBotStrategy> strategies = null,
        IEnumerable<IBotAction> actions = null)
    {
        Kind = kind;
        _config = config ?? BotConfig.Instance;
        if (actions != null)
        {
            foreach (var action in actions)
                RegisterAction(action);
        }

        if (strategies != null)
        {
            foreach (var strategy in strategies)
                AddStrategyWithoutInit(strategy);
        }

        Init();
    }

    public BotEngineKind Kind { get; }
    public object SyncRoot { get; } = new();
    public BotActionQueue Queue => _queue;
    public IReadOnlyList<BotTriggerNode> TriggerNodes => _triggerNodes;
    public IReadOnlyDictionary<string, IBotStrategy> Strategies => _strategies;
    public long PushCount { get; private set; }
    public long ActionLogCount { get; private set; }
    public BotActionLog[] SnapshotLog()
    {
        lock (SyncRoot)
            return _lastActionLog.ToArray();
    }

    public IReadOnlyList<BotActionLog> LastActionLog => SnapshotLog();

    public bool EnqueueCommand(string actionName, BotEvent ev, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionName);
        lock (SyncRoot)
        {
            var node = GetActionNode(actionName);
            if (node == null)
                return false;
            _queue.Replace(new BotActionBasket(node, BotRelevance.Command, false, ev, now));
            PushCount++;
            return true;
        }
    }

    public bool DoNextAction(BotContext context, bool minimal)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Content runs under SyncRoot (IsUseful, multipliers, IsPossible, Execute). An action must never acquire
        // runtime.SyncRoot or call back into BotHost/BotCombatManager paths that take it: the host releases
        // runtime.SyncRoot before calling DoNextAction, and the command thread takes only this lock.
        lock (SyncRoot)
            return DoNextActionCore(context, minimal);
    }

    private bool DoNextActionCore(BotContext context, bool minimal)
    {
        var now = context.Now;
        _queue.RemoveExpired(now, _config.ExpireActionTimeMs);
        ProcessTriggers(context, minimal);
        PushDefaultActions(context);

        var queueSize = _queue.Count;
        var iterationLimit = queueSize * (minimal ? 2 : Math.Max(1, _config.IterationsPerTick));
        for (var iteration = 0; iteration < iterationLimit && _queue.Count > 0; iteration++)
        {
            var basket = _queue.Pop();
            if (minimal && basket.Relevance < BotRelevance.Command)
            {
                continue;
            }

            var action = basket.Node.Action;
            var relevance = basket.Relevance;
            if (!action.IsUseful(context))
            {
                AddActionLog(now, basket.Node.Name, basket.Relevance, BotActionResult.NotUseful);
                continue;
            }

            var vetoed = false;
            for (var multiplierIndex = 0; multiplierIndex < _multipliers.Count; multiplierIndex++)
            {
                relevance *= _multipliers[multiplierIndex].GetValue(action, context);
                if (relevance <= 0f)
                {
                    vetoed = true;
                    break;
                }
            }

            if (vetoed)
            {
                AddActionLog(now, basket.Node.Name, relevance, BotActionResult.Vetoed);
                continue;
            }

            if (!action.IsPossible(context))
            {
                AddActionLog(now, basket.Node.Name, relevance, BotActionResult.Impossible);
                MultiplyAndPush(
                    context,
                    basket.Node.Alternatives,
                    relevance + AlternativeEpsilon,
                    skipPrerequisites: false,
                    basket.Event,
                    now);
                continue;
            }

            if (!basket.SkipPrerequisites)
            {
                var prerequisitesPushed = MultiplyAndPush(
                    context,
                    basket.Node.Prerequisites,
                    relevance + PrerequisiteEpsilon,
                    skipPrerequisites: false,
                    basket.Event,
                    now);
                if (prerequisitesPushed)
                {
                    MultiplyAndPush(
                        context,
                        GetSelfPush(basket.Node.Name, relevance + SelfEpsilon),
                        relevance + SelfEpsilon,
                        skipPrerequisites: true,
                        basket.Event,
                        now);
                    continue;
                }
            }

            var result = action.Execute(context, basket.Event);
            AddActionLog(now, basket.Node.Name, relevance, result);
            if (result != BotActionResult.Success)
            {
                MultiplyAndPush(
                    context,
                    basket.Node.Alternatives,
                    relevance + AlternativeEpsilon,
                    skipPrerequisites: false,
                    basket.Event,
                    now);
                continue;
            }

            MultiplyAndPush(context, basket.Node.Continuers, relevance, skipPrerequisites: false, basket.Event, now);
            context.Runtime.HostMetrics?.RecordDecision(true);
            return true;
        }

        context.Runtime.HostMetrics?.RecordDecision(false);
        return false;
    }

    public void Init()
    {
        lock (SyncRoot)
        {
            _queue.Clear();
            _triggerNodes.Clear();
            _multipliers.Clear();
            _actions.Clear();
            _actionNodes.Clear();
            foreach (var strategy in _strategies.Values)
            {
                strategy.InitTriggers(_triggerNodes);
                strategy.InitMultipliers(_multipliers);
            }
        }
    }

    public void RegisterAction(IBotAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(action.Name);
        lock (SyncRoot)
        {
            _registeredActions[action.Name] = action;
            _actions[action.Name] = action;
            _actionNodes.Remove(action.Name);
        }
    }

    public bool AddStrategy(string name)
    {
        lock (SyncRoot)
        {
            if (!BotContentRegistry.TryCreateStrategy(name, out var strategy))
                return false;
            return AddStrategy(strategy);
        }
    }

    public bool AddStrategy(IBotStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        lock (SyncRoot)
        {
            AddStrategyWithoutInit(strategy);
            Init();
            return true;
        }
    }

    public bool RemoveStrategy(string name)
    {
        lock (SyncRoot)
        {
            if (!_strategies.Remove(name))
                return false;
            Init();
            return true;
        }
    }

    public bool ToggleStrategy(string name)
    {
        lock (SyncRoot)
            return _strategies.ContainsKey(name) ? RemoveStrategy(name) : AddStrategy(name);
    }

    public bool HasStrategy(string name)
    {
        lock (SyncRoot)
            return name != null && _strategies.ContainsKey(name);
    }

    public string ListStrategies()
    {
        lock (SyncRoot)
        {
            var names = new List<string>(_strategies.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(",", names);
        }
    }

    private void ProcessTriggers(BotContext context, bool minimal)
    {
        _triggerResults.Clear();
        for (var i = 0; i < _triggerNodes.Count; i++)
        {
            var node = _triggerNodes[i];
            if (minimal && (node.Actions.Length == 0 || node.Actions[0].Relevance < BotRelevance.Command))
                continue;

            // Cache one result per trigger instance; later nodes intentionally reuse it after their own due check.
            if (!_triggerResults.TryGetValue(node.Trigger, out var active))
            {
                if (!IsDue(node, context.Now))
                    continue;

                active = node.Trigger.IsActive(context);
                _triggerResults.Add(node.Trigger, active);
            }

            node.LastCheck = context.Now;
            if (active)
            {
                MultiplyAndPush(context, node.Actions, 0f, skipPrerequisites: false, node.Trigger.Event, context.Now);
            }
        }
    }

    private void PushDefaultActions(BotContext context)
    {
        foreach (var strategy in _strategies.Values)
            MultiplyAndPush(context, strategy.DefaultActions, 0f, skipPrerequisites: false, default, context.Now);
    }

    private bool MultiplyAndPush(
        BotContext context,
        IReadOnlyList<BotNextAction> actions,
        float forceRelevance,
        bool skipPrerequisites,
        BotEvent ev,
        DateTime now)
    {
        if (actions == null)
            return false;

        var pushed = false;

        for (var i = 0; i < actions.Count; i++)
        {
            var next = actions[i];
            var node = GetActionNode(next.Name);
            if (node == null)
                continue;

            var relevance = forceRelevance > 0f ? forceRelevance : next.Relevance;
            if (relevance <= 0f)
                continue;

            context.Runtime.HostMetrics?.RecordActionBasketCreated();
            _queue.Push(new BotActionBasket(node, relevance, skipPrerequisites, ev, now));
            PushCount++;
            pushed = true;
        }

        return pushed;
    }

    private BotActionNode GetActionNode(string name)
    {
        if (_actionNodes.TryGetValue(name, out var node))
            return node;

        if (!_actions.TryGetValue(name, out var action))
        {
            if (!_registeredActions.TryGetValue(name, out action) && !BotContentRegistry.TryCreateAction(name, out action))
                return null;
            _actions[name] = action;
        }

        node = new BotActionNode(action);
        _actionNodes[name] = node;
        return node;
    }

    private IReadOnlyList<BotNextAction> GetSelfPush(string name, float relevance)
    {
        _selfPush[0] = new BotNextAction(name, relevance);
        return _selfPush;
    }

    private void AddStrategyWithoutInit(IBotStrategy strategy)
    {
        _strategies.Remove(strategy.Name);
        if (!string.IsNullOrWhiteSpace(strategy.SiblingGroup))
        {
            _siblingNames.Clear();
            foreach (var existing in _strategies.Values)
            {
                if (string.Equals(existing.SiblingGroup, strategy.SiblingGroup, StringComparison.OrdinalIgnoreCase))
                    _siblingNames.Add(existing.Name);
            }

            foreach (var siblingName in _siblingNames)
                _strategies.Remove(siblingName);
        }

        _strategies[strategy.Name] = strategy;
    }

    private static bool IsDue(BotTriggerNode node, DateTime now)
    {
        if (node.LastCheck == DateTime.MinValue || now < node.LastCheck)
            return true;
        return now - node.LastCheck >= TimeSpan.FromMilliseconds(Math.Max(0, node.Trigger.CheckIntervalMs));
    }

    private void AddActionLog(DateTime now, string action, float relevance, BotActionResult result)
    {
        ActionLogCount++;
        if (_lastActionLog.Count == LastActionCapacity)
            _lastActionLog.RemoveAt(0);
        _lastActionLog.Add(new BotActionLog(now, action, relevance, result));
    }

    private sealed class TriggerReferenceComparer : IEqualityComparer<IBotTrigger>
    {
        public static TriggerReferenceComparer Instance { get; } = new();

        public bool Equals(IBotTrigger x, IBotTrigger y) => ReferenceEquals(x, y);

        public int GetHashCode(IBotTrigger obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
