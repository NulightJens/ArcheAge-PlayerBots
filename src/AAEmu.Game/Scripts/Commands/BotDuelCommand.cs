using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands
{
    public class BotDuelCommand : ICommand
    {
        public string[] CommandNames { get; set; } = ["botduel"];
        internal static Func<IDuelManager> DuelManagerResolver { get; set; } = () => DuelManager.Instance;

        public void OnLoad()
        {
            CommandManager.Instance.Register(CommandNames, this);
        }

        public string GetCommandLineHelp()
        {
            return "<botId1> <botId2>";
        }

        public string GetCommandHelpText()
        {
            return "Triggers a duel between two bots. The first bot challenges the second, and the second automatically accepts.";
        }

        public void Execute(Character character, string[] args, IMessageOutput messageOutput)
        {
            if (!BotCommandArgs.TryBotId(args, 0, out var botId1, out _) ||
                !BotCommandArgs.TryBotId(args, 1, out var botId2, out _))
            {
                CommandManager.SendDefaultHelpText(this, messageOutput);
                return;
            }

            var bot1 = BotManager.Instance.GetBot(botId1);
            var bot2 = BotManager.Instance.GetBot(botId2);

            if (bot1 == null)
            {
                BotCommandArgs.SendUnknownBot(this, messageOutput, botId1);
                return;
            }
            if (bot2 == null)
            {
                BotCommandArgs.SendUnknownBot(this, messageOutput, botId2);
                return;
            }
            if (bot1 == bot2)
            {
                CommandManager.SendErrorText(this, messageOutput, "Cannot duel oneself.");
                return;
            }

            if (bot1.IsDead || bot2.IsDead)
            {
                CommandManager.SendErrorText(this, messageOutput, "Cannot duel a dead bot.");
                return;
            }
            if (bot1.Transform.InstanceId != bot2.Transform.InstanceId)
            {
                CommandManager.SendErrorText(this, messageOutput, "Bots must be in the same instance to duel.");
                return;
            }
            if (bot1.IsInDuel || bot2.IsInDuel)
            {
                CommandManager.SendErrorText(this, messageOutput, "Both bots must be out of a duel first.");
                return;
            }
            if (bot1.Expedition != null || bot2.Expedition != null)
            {
                CommandManager.SendErrorText(this, messageOutput, "Both bots must be out of an expedition to duel.");
                return;
            }

            // ---- Check if either bot is already in a duel ----
            var state1 = BotCombatManager.Instance.GetState(bot1);
            var state2 = BotCombatManager.Instance.GetState(bot2);

            if (state1 != null && state1.InDuel)
            {
                CommandManager.SendErrorText(this, messageOutput, $"Bot '{bot1.Name}' is already in a duel with '{state1.DuelOpponent?.Name ?? "unknown"}'.");
                return;
            }
            if (state2 != null && state2.InDuel)
            {
                CommandManager.SendErrorText(this, messageOutput, $"Bot '{bot2.Name}' is already in a duel with '{state2.DuelOpponent?.Name ?? "unknown"}'.");
                return;
            }

            // Initiate duel request from bot1 to bot2 (DuelManager handles it)
            DuelManagerResolver().DuelRequest(bot1, bot2.Id);
            CommandManager.SendNormalText(this, messageOutput, $"Bot '{bot1.Name}' challenged '{bot2.Name}' to a duel.");
        }
    }
}
