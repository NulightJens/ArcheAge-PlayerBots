using System.Linq;
using System.Collections.Generic;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands
{
    public class BotArchetypeCommand : ICommand
    {
        public string[] CommandNames { get; set; } = ["botarchetype", "botclass"];

        public void OnLoad()
        {
            CommandManager.Instance.Register(CommandNames, this);
        }

        public string GetCommandLineHelp()
        {
            return "<botId> [force|reroll]";
        }

        public string GetCommandHelpText()
        {
            return "Displays the assigned archetype (class) for a bot.\n" +
                   "  force  : Re-equip best gear and re-learn skills (keeps current archetype).\n" +
                   "  reroll : Force a new random archetype assignment (based on abilities), then re-equip/re-learn.\n" +
                   "Aliases: /botclass <id>";
        }

        public void Execute(Character character, string[] args, IMessageOutput messageOutput)
        {
            if (!BotCommandArgs.TryBotId(args, 0, out var botId, out _))
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

            var action = args.Length > 1 ? args[1].ToLowerInvariant() : "";

            if (action == "force")
            {
                BotArchetypeManager.Instance.ForceReevaluate(bot);
                CommandManager.SendNormalText(this, messageOutput, $"Archetype re-evaluation forced for bot '{bot.Name}'.");
            }
            else if (action == "reroll")
            {
                BotArchetypeManager.Instance.RerollArchetype(bot);
                CommandManager.SendNormalText(this, messageOutput, $"Archetype re-rolled for bot '{bot.Name}'.");
            }
            else if (!string.IsNullOrEmpty(action))
            {
                CommandManager.SendErrorText(this, messageOutput, "Unknown archetype action. Use: force or reroll.");
                return;
            }

            // Display current state
            var state = BotArchetypeManager.Instance.GetState(bot);
            if (state == null || !state.IsInitialized)
            {
                CommandManager.SendErrorText(this, messageOutput, $"Bot '{bot.Name}' has no archetype state (spawn it first).");
                return;
            }

            var archetypeName = state.ArchetypeName ?? "None (unassigned)";
            var planned = state.PlannedArchetype ?? "None";
            var skillCount = bot.Skills?.Skills?.Count ?? 0;
            var passiveCount = bot.Skills?.PassiveBuffs?.Count ?? 0;

            CommandManager.SendNormalText(this, messageOutput,
                $"Bot '{bot.Name}' (Id: {bot.Id}) | Archetype: {archetypeName} | Planned: {planned} | Level: {bot.Level} | Skills: {skillCount} | Passives: {passiveCount}");

            if (!string.IsNullOrEmpty(state.ArchetypeName))
            {
                // Count only real equipment slots (0..18)
                var realEquipmentSlots = new HashSet<byte> { 0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18 };
                var equipped = bot.Inventory?.Equipment?.Items?
                    .Select((item, index) => new { item, index })
                    .Where(x => x.item != null && realEquipmentSlots.Contains((byte)x.index))
                    .Count() ?? 0;

                var bagCount = bot.Inventory?.Bag?.Items?.Count(i => i != null) ?? 0;
                CommandManager.SendNormalText(this, messageOutput,
                    $"  Equipment: {equipped} items equipped, Bag: {bagCount} items in bag.");
            }
            else
            {
                CommandManager.SendErrorText(this, messageOutput,
                    $"  No archetype assigned yet. Waiting for level {(state.PlannedArchetype != null ? "abilities to unlock" : "a valid starting ability")}.");
            }
        }
    }
}
