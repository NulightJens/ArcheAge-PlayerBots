using System.Globalization;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Applies or removes a buff on a bot without requiring a client-side selection.
/// This keeps repeatable physical tests controllable from the server console/API.
/// </summary>
public sealed class BotBuffCommand : ICommand
{
    internal static Func<uint, BuffTemplate> BuffTemplateResolver { get; set; } =
        static id => SkillManager.Instance.GetBuffTemplate(id);

    public string[] CommandNames { get; set; } = ["botbuff"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "<botId> <buffId|-buffId> [abLevel]";

    public string GetCommandHelpText() =>
        "Applies a buff directly to a bot, or removes it when the buff id is negative. " +
        "This command is intended for repeatable GM/development testing and does not require a selected target.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (!BotCommandArgs.TryBotId(args, 0, out var botId, out _) || args.Length is < 2 or > 3 ||
            !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var signedBuffId) ||
            signedBuffId == 0)
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var bot = BotManager.Instance.GetBot(botId);
        if (bot == null)
        {
            BotCommandArgs.SendUnknownBot(this, messageOutput, botId);
            return;
        }

        var absoluteBuffId = Math.Abs((long)signedBuffId);
        if (absoluteBuffId > uint.MaxValue)
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var buffId = (uint)absoluteBuffId;
        if (signedBuffId < 0)
        {
            if (!bot.Buffs.CheckBuff(buffId))
            {
                CommandManager.SendErrorText(this, messageOutput,
                    $"Bot '{bot.Name}' does not have buff {buffId}.");
                return;
            }

            bot.Buffs.RemoveBuff(buffId);
            CommandManager.SendNormalText(this, messageOutput,
                $"Removed buff {buffId} from bot '{bot.Name}'.");
            return;
        }

        var abLevel = 1u;
        if (args.Length == 3 &&
            (!uint.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out abLevel) || abLevel == 0))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var template = BuffTemplateResolver(buffId);
        if (template == null)
        {
            CommandManager.SendErrorText(this, messageOutput, $"Unknown buff id {buffId}.");
            return;
        }

        var caster = new SkillCasterUnit(bot.ObjId);
        var buff = new Buff(bot, bot, caster, template, null, DateTime.UtcNow) { AbLevel = abLevel };
        bot.Buffs.AddBuff(buff);
        CommandManager.SendNormalText(this, messageOutput,
            $"Applied buff {buffId} to bot '{bot.Name}' (stealth={template.Stealth}, abLevel={abLevel}).");
    }
}
