using AAEmu.Game.Bots.Social;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Team;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public sealed class BotControlCommand : ICommand
{
    internal static Func<BotControlDispatcher> DispatcherResolver { get; set; } =
        static () => BotControlDispatcher.Instance;

    public string[] CommandNames { get; set; } = ["botcontrol"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId> <follow|stay|attack|passive|role> [tank|healer|attacker]";

    public string GetCommandHelpText() =>
        "Controls a bot you currently own through the real party lifecycle.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (!TryParse(args, out var botId, out var verb, out var role))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var result = DispatcherResolver().Dispatch(character, botId, verb, role);
        if (result.Accepted)
            CommandManager.SendNormalText(this, messageOutput, result.Message);
        else
            CommandManager.SendErrorText(this, messageOutput, result.Message);
    }

    internal static bool TryParse(
        string[] args,
        out uint botId,
        out BotControlVerb verb,
        out MemberRole role)
    {
        botId = 0;
        verb = default;
        role = MemberRole.Undecided;
        if (args == null || args.Length < 2 || args.Length > 3 ||
            !uint.TryParse(args[0], out botId) || botId == 0)
            return false;

        verb = args[1].ToLowerInvariant() switch
        {
            "follow" => BotControlVerb.Follow,
            "stay" => BotControlVerb.Stay,
            "attack" => BotControlVerb.Attack,
            "passive" => BotControlVerb.Passive,
            "role" => BotControlVerb.Role,
            _ => (BotControlVerb)(-1)
        };
        if (!Enum.IsDefined(verb))
            return false;

        if (verb != BotControlVerb.Role)
            return args.Length == 2;
        if (args.Length != 3)
            return false;

        role = args[2].ToLowerInvariant() switch
        {
            "tank" => MemberRole.Tank,
            "healer" => MemberRole.Healer,
            "attacker" => MemberRole.Attacker,
            _ => MemberRole.Undecided
        };
        return role != MemberRole.Undecided;
    }
}
