using System.Globalization;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
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
    internal static Func<Character, uint, Npc> NpcResolver { get; set; } =
        static (bot, objId) => bot?.ParentWorld?.GetNpc(objId);

    public string[] CommandNames { get; set; } = ["botbuff", "botbuffnpc"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() =>
        "<botId> <buffId|-buffId> [abLevel] | botbuffnpc <botId> <npcObjId> <buffId|-buffId> <abLevel>";

    public string GetCommandHelpText() =>
        "Applies a buff directly to a bot, or removes it when the buff id is negative. " +
        "The botbuffnpc alias applies the same operation to one exact living NPC object. " +
        "This command is intended for repeatable GM/development testing and does not require a selected target.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        // CommandManager resolves aliases to the configured primary command
        // before authorization and execution. Keep the exact-NPC qualifier as
        // a four-argument alias form so it shares botbuff's existing access
        // policy without introducing an unregistered command identity.
        if (args.Length == 4)
        {
            ExecuteNpc(args, messageOutput);
            return;
        }

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

        if (!TryChangeBuff(bot, bot, signedBuffId, abLevel: args.Length == 3 ? args[2] : null,
                targetLabel: $"bot '{bot.Name}'", out var result, out var error))
        {
            CommandManager.SendErrorText(this, messageOutput, error);
            return;
        }

        CommandManager.SendNormalText(this, messageOutput, result);
    }

    private void ExecuteNpc(string[] args, IMessageOutput messageOutput)
    {
        if (!BotCommandArgs.TryBotId(args, 0, out var botId, out _) ||
            !uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var npcObjId) || npcObjId == 0 ||
            !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var signedBuffId) || signedBuffId == 0 ||
            !uint.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var abLevel) || abLevel == 0)
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

        var npc = NpcResolver(bot, npcObjId);
        if (npc == null || npc.IsDead)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Living NPC object {npcObjId} was not found in bot {botId}'s world.");
            return;
        }

        if (!TryChangeBuff(npc, bot, signedBuffId, abLevel.ToString(CultureInfo.InvariantCulture),
                $"NPC object {npcObjId}", out var result, out var error))
        {
            CommandManager.SendErrorText(this, messageOutput, error);
            return;
        }

        CommandManager.SendNormalText(this, messageOutput, result);
    }

    internal static bool TryChangeBuff(
        Unit target,
        Unit casterUnit,
        int signedBuffId,
        string abLevel,
        string targetLabel,
        out string result,
        out string error)
    {
        result = null;
        error = null;
        var absoluteBuffId = Math.Abs((long)signedBuffId);
        if (target?.Buffs == null || casterUnit == null || absoluteBuffId is 0 or > uint.MaxValue)
        {
            error = "The supplied unit or buff identity is invalid.";
            return false;
        }

        var buffId = (uint)absoluteBuffId;
        if (signedBuffId < 0)
        {
            if (!target.Buffs.CheckBuff(buffId))
            {
                error = $"The supplied {targetLabel} does not have buff {buffId}.";
                return false;
            }

            target.Buffs.RemoveBuff(buffId);
            result = $"Removed buff {buffId} from {targetLabel}.";
            return true;
        }

        var parsedAbLevel = 1u;
        if (abLevel != null &&
            (!uint.TryParse(abLevel, NumberStyles.None, CultureInfo.InvariantCulture, out parsedAbLevel) || parsedAbLevel == 0))
        {
            error = "Ability level must be a positive integer.";
            return false;
        }

        var template = BuffTemplateResolver(buffId);
        if (template == null)
        {
            error = $"Unknown buff id {buffId}.";
            return false;
        }

        var caster = new SkillCasterUnit(casterUnit.ObjId);
        var buff = new Buff(target, casterUnit, caster, template, null, DateTime.UtcNow)
        {
            AbLevel = checked((ushort)Math.Min(parsedAbLevel, ushort.MaxValue))
        };
        target.Buffs.AddBuff(buff);
        result = $"Applied buff {buffId} to {targetLabel} (stealth={template.Stealth}, abLevel={parsedAbLevel}).";
        return true;
    }
}
