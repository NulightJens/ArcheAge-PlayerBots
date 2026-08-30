using System.Numerics;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Bots.Blackboard;

public static class HazardValues
{
    public static BotValue<List<AreaTrigger>> Create(
        Character bot,
        Func<IReadOnlyCollection<AreaTrigger>> activeTriggers,
        TimeSpan ttl)
    {
        ArgumentNullException.ThrowIfNull(bot);
        activeTriggers ??= () => AreaTriggerManager.Instance.Active;
        return new HostileAreaTriggersValue(bot, activeTriggers, ttl);
    }

    private sealed class HostileAreaTriggersValue : BotValue<List<AreaTrigger>>
    {
        private readonly Character _bot;
        private readonly Func<IReadOnlyCollection<AreaTrigger>> _activeTriggers;

        public HostileAreaTriggersValue(
            Character bot,
            Func<IReadOnlyCollection<AreaTrigger>> activeTriggers,
            TimeSpan ttl)
            : base(ttl)
        {
            _bot = bot;
            _activeTriggers = activeTriggers;
        }

        protected override List<AreaTrigger> Compute(DateTime now)
        {
            var hazards = new List<AreaTrigger>();
            if (_bot.Transform == null)
                return hazards;

            var botPosition = _bot.Transform.World.Position;
            foreach (var trigger in _activeTriggers() ?? [])
            {
                if (trigger?.TargetRelation != SkillTargetRelation.Hostile || trigger.Shape == null)
                    continue;
                if (Contains(trigger, botPosition))
                    hazards.Add(trigger);
            }

            return hazards;
        }

        private static bool Contains(AreaTrigger trigger, Vector3 position)
        {
            return AreaTriggerContainment.Contains(trigger, position);
        }
    }
}
