using System.Globalization;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Assigns active bots to follow an online character directly, without requiring
/// party or raid membership. The existing bot mover tracks the target's live
/// transform while deterministic grid slots prevent large populations from stacking.
/// </summary>
public sealed class BotFollowCommand : ICommand
{
    private const float DefaultBaseDistance = 3f;
    private const int AutomaticColumns = 0;
    private const float DefaultSpacing = 2.5f;

    public string[] CommandNames { get; set; } = ["botfollow", "followbot"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() =>
        "<botId|all> <characterName|stop|status> [baseDistance] [columns|auto] [spacing]";

    public string GetCommandHelpText() =>
        "Tracks and follows an online character without party membership. " +
        "Bots occupy deterministic grid slots behind the target. " +
        "Defaults: 3m rear gap, automatic square columns, 2.5m spacing.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args == null || args.Length < 2 || args.Length > 5 || !TryResolveBots(args[0], out var bots))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        if (bots.Count == 0)
        {
            SendNoBots(args[0], messageOutput);
            return;
        }

        if (args[1].Equals("stop", StringComparison.OrdinalIgnoreCase))
        {
            StopFollowing(bots);
            CommandManager.SendNormalText(this, messageOutput,
                $"Stopped direct follow for {bots.Count} active bot(s); state is now Idle.");
            return;
        }

        if (args[1].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            SendStatus(bots, messageOutput);
            return;
        }

        if (!TryParseOptions(args, out var baseDistance, out var requestedColumns, out var spacing))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var target = WorldManager.Instance.GetCharacter(args[1]);
        if (target?.Transform == null || target.ParentWorld == null)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Online follow target '{args[1]}' was not found in the world.");
            return;
        }

        var orderedBots = bots.OrderBy(bot => bot.Id).ToArray();
        var eligible = orderedBots
            .Where(bot => bot.Transform != null && bot.ParentWorld != null &&
                          ReferenceEquals(bot.ParentWorld, target.ParentWorld) &&
                          bot.Transform.InstanceId == target.Transform.InstanceId &&
                          BotHost.Instance.GetRuntime(bot.Id) != null)
            .ToArray();
        var skippedDifferentInstance = orderedBots.Length - eligible.Length;
        var columns = ResolveColumns(requestedColumns, eligible.Length);

        for (var assigned = 0; assigned < eligible.Length; assigned++)
        {
            var bot = eligible[assigned];
            var runtime = BotHost.Instance.GetRuntime(bot.Id)!;
            BotManager.Instance.SetFollowTarget(bot, target, baseDistance);
            runtime.MovementState.FormationSlot = assigned;
            runtime.MovementState.FormationColumns = columns;
            runtime.MovementState.FormationMemberCount = eligible.Length;
            runtime.MovementState.FormationSpacing = spacing;
            runtime.CombatState.Target = null;
            bot.CurrentTarget = null;
            runtime.CombatState.SetForcedState(BotCombatStateType.Following);
            runtime.CombatState.TransitionTo(BotCombatStateType.Following);
        }

        var rows = eligible.Length == 0 ? 0 : (eligible.Length + columns - 1) / columns;
        var width = eligible.Length == 0 ? 0f : (Math.Min(columns, eligible.Length) - 1) * spacing;
        var depth = eligible.Length == 0 ? 0f : baseDistance + (rows - 1) * spacing;
        CommandManager.SendNormalText(this, messageOutput,
            $"Direct follow assigned: bots={eligible.Length}, target='{target.Name}', targetId={target.Id}, " +
            $"targetObjId={target.ObjId}, formation=grid, columns={columns}, rows={rows}, " +
            $"rearGap={baseDistance:F1}m, spacing={spacing:F1}m, footprint={width:F1}x{depth:F1}m, " +
            $"skippedDifferentInstanceOrRuntime={skippedDifferentInstance}.");
    }

    private static bool TryResolveBots(string selector, out List<Character> bots)
    {
        bots = [];
        if (selector.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            bots = BotManager.Instance.GetAllBots();
            return true;
        }

        if (!uint.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out var botId) || botId == 0)
            return false;

        var bot = BotManager.Instance.GetBot(botId);
        if (bot != null)
            bots.Add(bot);
        return true;
    }

    private static bool TryParseOptions(
        string[] args,
        out float baseDistance,
        out int columns,
        out float spacing)
    {
        baseDistance = DefaultBaseDistance;
        columns = AutomaticColumns;
        spacing = DefaultSpacing;

        if (args.Length > 2 &&
            (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out baseDistance) ||
             !float.IsFinite(baseDistance) || baseDistance < 0.5f || baseDistance > 100f))
            return false;
        if (args.Length > 3 && !args[3].Equals("auto", StringComparison.OrdinalIgnoreCase) &&
            (!int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out columns) ||
             columns < 1 || columns > 1000))
            return false;
        if (args.Length > 4 &&
            (!float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out spacing) ||
             !float.IsFinite(spacing) || spacing < 0.5f || spacing > 20f))
            return false;

        return true;
    }

    internal static int ResolveColumns(int requestedColumns, int botCount)
    {
        if (botCount <= 0)
            return 0;

        return requestedColumns > 0
            ? Math.Min(requestedColumns, botCount)
            : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(botCount)));
    }

    private static void StopFollowing(IEnumerable<Character> bots)
    {
        foreach (var bot in bots)
        {
            var runtime = BotHost.Instance.GetRuntime(bot.Id);
            if (runtime == null)
                continue;

            BotManager.Instance.StopFollow(bot);
            runtime.MovementState.FormationSlot = -1;
            runtime.MovementState.FormationColumns = 0;
            runtime.MovementState.FormationMemberCount = 0;
            runtime.MovementState.FormationSpacing = DefaultSpacing;
            runtime.CombatState.Target = null;
            bot.CurrentTarget = null;
            runtime.CombatState.SetForcedState(BotCombatStateType.Idle);
            runtime.CombatState.TransitionTo(BotCombatStateType.Idle);
        }
    }

    private void SendStatus(IEnumerable<Character> bots, IMessageOutput messageOutput)
    {
        var groups = bots
            .Select(bot => BotHost.Instance.GetRuntime(bot.Id)?.MovementState.FollowTarget)
            .Where(target => target != null)
            .GroupBy(target => $"{target.Name}#{target.Id}")
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}={group.Count()}")
            .ToArray();
        CommandManager.SendNormalText(this, messageOutput,
            groups.Length == 0 ? "No selected bots have a direct follow target." : string.Join(", ", groups));
    }

    private void SendNoBots(string selector, IMessageOutput messageOutput)
    {
        if (selector.Equals("all", StringComparison.OrdinalIgnoreCase))
            CommandManager.SendErrorText(this, messageOutput, "No active bots found.");
        else
            CommandManager.SendErrorText(this, messageOutput, $"No active bot found with id {selector}.");
    }
}
