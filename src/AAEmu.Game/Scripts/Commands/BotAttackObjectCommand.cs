using System.Globalization;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Starts a contained combat trial against one explicit NPC object. Bots return
/// to Idle when that object dies or reaches an optional non-lethal health floor
/// instead of selecting another nearby hostile.
/// </summary>
public sealed class BotAttackObjectCommand : ICommand
{
    internal static Func<IEnumerable<Character>, uint, Npc> NpcResolver { get; set; } = (bots, objId) =>
        bots.Select(bot => bot.ParentWorld?.GetNpc(objId)).FirstOrDefault(npc => npc != null);

    public string[] CommandNames { get; set; } = ["botattackobject", "botattacknpc"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId|all> <npcObjId> [stopAtHpPercent] | status <npcObjId>";

    public string GetCommandHelpText() =>
        "Directs active bots to one exact NPC object for a contained combat trial; an optional 1-99% floor stops attacks non-lethally.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args is { Length: 2 } && args[0].Equals("status", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var statusObjId))
        {
            var statusTarget = NpcResolver(BotManager.Instance.GetAllBots(), statusObjId);
            if (statusTarget == null)
            {
                CommandManager.SendErrorText(this, messageOutput, $"NPC object {statusObjId} was not found.");
                return;
            }

            var position = statusTarget.Transform.World.Position;
            CommandManager.SendNormalText(this, messageOutput,
                $"NPC status: objId={statusTarget.ObjId}, template={statusTarget.TemplateId}, " +
                $"hp={statusTarget.Hp}/{statusTarget.MaxHp}, dead={statusTarget.IsDead}, " +
                $"position=({position.X:F1}, {position.Y:F1}, {position.Z:F1}).");
            return;
        }

        if (args.Length is < 2 or > 3 ||
            !uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var npcObjId) ||
            npcObjId == 0 ||
            (args.Length == 3 && !TryParseStopAtHpPercent(args[2], out _)) ||
            !TryResolveBots(args[0], out var bots))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        byte? stopAtHpPercent = args.Length == 3
            ? byte.Parse(args[2], NumberStyles.None, CultureInfo.InvariantCulture)
            : null;

        var target = NpcResolver(bots, npcObjId);
        if (target == null || target.IsDead)
        {
            CommandManager.SendErrorText(this, messageOutput, $"Living NPC object {npcObjId} was not found in the selected bot worlds.");
            return;
        }

        var engaged = 0;
        var skipped = 0;
        foreach (var bot in bots.OrderBy(candidate => candidate.Id))
        {
            var runtime = BotHost.Instance.GetRuntime(bot.Id);
            if (runtime == null || bot.ParentWorld == null ||
                !ReferenceEquals(bot.ParentWorld, target.ParentWorld) || !bot.CanAttack(target))
            {
                skipped++;
                continue;
            }

            // The combat manager owns the state consumed by BotCombatTask. A
            // retained movement runtime can temporarily carry a different combat
            // state after a bot is despawned and respawned (for example by
            // /setclass). Reattach the brain first, then arm the manager's
            // authoritative state so the health floor cannot be written only to
            // a stale runtime object.
            var combatState = EnsureAuthoritativeCombatState(bot);
            if (combatState == null)
            {
                skipped++;
                continue;
            }

            BotManager.Instance.StopFollow(bot);
            runtime.MovementState.FormationSlot = -1;
            runtime.MovementState.FormationColumns = 0;
            runtime.MovementState.FormationMemberCount = 0;
            runtime.MovementState.Destination = null;
            combatState.TargetTypeFilter = null;
            combatState.LastKnownTargetPosition = null;
            combatState.KillGoal = null;
            combatState.KillCount = 0;
            combatState.StopAtTargetHpPercent = stopAtHpPercent;
            combatState.Target = target;
            bot.CurrentTarget = target;
            combatState.IsActive = true;
            combatState.SetForcedState(BotCombatStateType.Idle);
            combatState.TransitionTo(BotCombatStateType.Combat);
            engaged++;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Contained attack started: bots={engaged}, targetObjId={target.ObjId}, template={target.TemplateId}, " +
            $"targetHp={target.Hp}/{target.MaxHp}, stopAtHp={(stopAtHpPercent.HasValue ? $"{stopAtHpPercent}%" : "death")}, " +
            $"returnState=Idle, skipped={skipped}.");
    }

    internal static bool TryParseStopAtHpPercent(string value, out byte percent)
    {
        return byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out percent) &&
               percent is >= 1 and <= 99;
    }

    internal static BotCombatState EnsureAuthoritativeCombatState(Character bot)
    {
        var manager = BotCombatManager.Instance;
        manager.StartListening(bot);
        return manager.GetState(bot);
    }

    private static bool TryResolveBots(string selector, out List<Character> bots)
    {
        if (selector.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            bots = BotManager.Instance.GetAllBots();
            return bots.Count > 0;
        }

        bots = [];
        if (!uint.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out var botId) || botId == 0)
            return false;

        var bot = BotManager.Instance.GetBot(botId);
        if (bot != null)
            bots.Add(bot);
        return bots.Count > 0;
    }
}
