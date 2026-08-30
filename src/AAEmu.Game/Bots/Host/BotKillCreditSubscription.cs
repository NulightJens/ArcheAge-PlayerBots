using System.Runtime.CompilerServices;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using NLog;

namespace AAEmu.Game.Bots.Host;

internal sealed class BotKillCreditSubscription
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly Character _bot;
    private readonly BotCombatState _state;
    private readonly object _syncRoot = new();
    private readonly ConditionalWeakTable<Unit, KillMarker> _deliveredVictims = new();
    private bool _subscribed;

    internal BotHostMetrics Metrics { get; set; }

    public BotKillCreditSubscription(Character bot, BotCombatState state)
    {
        _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void Subscribe()
    {
        lock (_syncRoot)
        {
            if (_subscribed)
                return;

            _bot.Events.OnKill += OnKill;
            _subscribed = true;
        }
    }

    public void Unsubscribe()
    {
        lock (_syncRoot)
        {
            if (!_subscribed)
                return;

            _subscribed = false;
            _bot.Events.OnKill -= OnKill;
        }
    }

    private void OnKill(object sender, OnKillArgs args)
    {
        lock (_syncRoot)
        {
            if (!_subscribed || !ReferenceEquals(args.Killer, _bot) || args.Victim is not Npc victim ||
                !ReferenceEquals(sender, victim))
                return;

            var marker = _deliveredVictims.GetValue(victim, static _ => new KillMarker());
            if (!marker.TryMarkDelivered())
            {
                Logger.Trace($"BOT id={_bot.Id} ev=kill_credit_duplicate killer={_bot.Id} target={victim.ObjId}");
                return;
            }

            if (_state.TargetTypeFilter.HasValue && victim.TemplateId != _state.TargetTypeFilter.Value)
                return;

            var newCount = _state.CreditKill();
            Metrics?.RecordCreditedKill();
            Logger.Trace($"BOT id={_bot.Id} ev=kill_credit killer={_bot.Id} target={victim.ObjId} target_type={victim.TemplateId} old_count={newCount - 1} new_count={newCount}");
        }
    }

    private sealed class KillMarker
    {
        private int _delivered;

        public bool TryMarkDelivered()
        {
            return Interlocked.Exchange(ref _delivered, 1) == 0;
        }
    }
}
