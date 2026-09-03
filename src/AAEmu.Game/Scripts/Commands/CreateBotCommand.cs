#if !PLAYERBOTS_AAEMU_3_0
using System.Globalization;
using AAEmu.Game.Bots.Population.Identity;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Creates one persistent identity under the configured server-owned account
/// and immediately admits it through BotManager's ordinary lifecycle.
/// </summary>
public sealed class BotCreateCommand : ICommand
{
    private readonly Func<BotIdentityCreationRequest, BotIdentityCreationResult> _create;

    public BotCreateCommand() : this(request => BotManager.Instance.CreateBot(request))
    {
    }

    internal BotCreateCommand(Func<BotIdentityCreationRequest, BotIdentityCreationResult> create)
    {
        _create = create ?? throw new ArgumentNullException(nameof(create));
    }

    public string[] CommandNames { get; set; } = ["createbot", "create_bot"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<name> <race> <gender> <archetype> <level> [here|race-spawn]";

    public string GetCommandHelpText() =>
        "Creates and admits a persistent character owned by the configured PlayerBots server account.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args is not { Length: 5 or 6 } ||
            string.IsNullOrWhiteSpace(args[0]) ||
            !TryParseNamedEnum(args[1], out Race race) || race == Race.None ||
            !TryParseNamedEnum(args[2], out Gender gender) ||
            string.IsNullOrWhiteSpace(args[3]) ||
            !int.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out var level))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var placementToken = args.Length == 6 ? args[5] : "race-spawn";
        BotIdentityPlacement placement;
        if (string.Equals(placementToken, "race-spawn", StringComparison.OrdinalIgnoreCase))
        {
            placement = BotIdentityPlacement.RaceSpawn;
        }
        else if (string.Equals(placementToken, "here", StringComparison.OrdinalIgnoreCase) &&
                 TryCaptureHere(character, out placement))
        {
        }
        else
        {
            CommandManager.SendErrorText(this, messageOutput,
                "BOT_IDENTITY status=failure code=invalid_placement reason=use_here_or_race-spawn");
            return;
        }

        var result = _create(new BotIdentityCreationRequest(
            args[0],
            race,
            gender,
            args[3],
            level,
            placement));

        if (result?.Success == true && result.Character != null)
        {
            var bot = result.Character;
            CommandManager.SendNormalText(this, messageOutput,
                $"BOT_IDENTITY status=success code=created_and_admitted id={bot.Id} name={bot.Name} " +
                $"level={bot.Level} race={bot.Race} gender={bot.Gender} zone={bot.Transform.ZoneId}");
            return;
        }

        var status = result?.Status ?? BotIdentityCreationStatus.Failed;
        var reason = string.IsNullOrWhiteSpace(result?.Reason) ? "unspecified" : result.Reason;
        CommandManager.SendErrorText(this, messageOutput,
            $"BOT_IDENTITY status=failure code={ToToken(status)} reason={reason}");
    }

    internal static bool TryCaptureHere(Character character, out BotIdentityPlacement placement)
    {
        placement = default;
        var world = character?.ParentWorld;
        var transform = character?.Transform;
        if (world?.Template == null || transform == null)
            return false;

        // ParentWorld is the authoritative runtime instance for an admitted character.
        // Transform.InstanceId can briefly retain stale metadata while the live client is
        // entering or crossing a region, but its world-space position is already current.
        var position = transform.World.Position;
        var rotation = transform.World.Rotation;
        placement = new BotIdentityPlacement(
            BotIdentityPlacementMode.Here,
            world.Template.Id,
            world.Id,
            transform.ZoneId,
            position.X,
            position.Y,
            position.Z,
            rotation.X,
            rotation.Y,
            rotation.Z);
        return placement.IsValid;
    }

    private static bool TryParseNamedEnum<T>(string text, out T value) where T : struct, Enum
    {
        value = default;
        return !string.IsNullOrWhiteSpace(text) &&
               Enum.GetNames<T>().Any(name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase)) &&
               Enum.TryParse(text, true, out value) &&
               Enum.IsDefined(value);
    }

    private static string ToToken(BotIdentityCreationStatus status)
    {
        var name = status.ToString();
        var chars = new List<char>(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var current = name[index];
            if (index > 0 && char.IsUpper(current))
                chars.Add('_');
            chars.Add(char.ToLowerInvariant(current));
        }
        return new string(chars.ToArray());
    }
}
#endif
