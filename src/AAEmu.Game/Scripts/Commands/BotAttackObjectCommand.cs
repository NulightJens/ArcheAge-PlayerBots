using System.Globalization;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Starts a contained combat trial against one explicit NPC object. Bots return
/// to Idle when that object dies instead of selecting another nearby hostile.
/// </summary>
public sealed class BotAttackObjectCommand : ICommand
{
    public string[] CommandNames { get; set; } = ["botattackobject", "botattacknpc"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId|all> <npcObjId> | status <npcObjId>";

    public string GetCommandHelpText() =>
        "Directs active bots to one exact NPC object for a contained combat trial; survivors return to Idle when it dies.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args is { Length: 2 } && args[0].Equals("status", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var statusObjId))
        {
            var statusTarget = character?.ParentWorld?.GetNpc(statusObjId);
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

        if (args is not { Length: 2 } ||
            !uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var npcObjId) ||
            npcObjId == 0 ||
            !TryResolveBots(args[0], out var bots))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var target = character?.ParentWorld?.GetNpc(npcObjId);
        if (target == null || target.IsDead)
        {
            CommandManager.SendErrorText(this, messageOutput, $"Living NPC object {npcObjId} was not found in the command character's world.");
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

            BotManager.Instance.StopFollow(bot);
            runtime.MovementState.FormationSlot = -1;
            runtime.MovementState.FormationColumns = 0;
            runtime.MovementState.FormationMemberCount = 0;
            runtime.MovementState.Destination = null;
            runtime.CombatState.TargetTypeFilter = null;
            runtime.CombatState.LastKnownTargetPosition = null;
            runtime.CombatState.KillGoal = null;
            runtime.CombatState.KillCount = 0;
            runtime.CombatState.Target = target;
            bot.CurrentTarget = target;
            runtime.CombatState.IsActive = true;
            runtime.CombatState.SetForcedState(BotCombatStateType.Idle);
            runtime.CombatState.TransitionTo(BotCombatStateType.Combat);
            engaged++;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Contained attack started: bots={engaged}, targetObjId={target.ObjId}, template={target.TemplateId}, " +
            $"targetHp={target.Hp}/{target.MaxHp}, returnState=Idle, skipped={skipped}.");
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
