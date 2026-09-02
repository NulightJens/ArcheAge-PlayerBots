using AAEmu.Game.Core.Managers;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Life;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands
{
    public class BotDebugCommand : ICommand
    {
        public string[] CommandNames { get; set; } = ["botdebug"];

        public void OnLoad()
        {
            CommandManager.Instance.Register(CommandNames, this);
        }

        public string GetCommandLineHelp()
        {
            return "<characterId>";
        }

        public string GetCommandHelpText()
        {
            return "Prints debug info for a bot's movement and combat state.";
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

            var moveState = BotManager.Instance.GetBotState(botId);
            var combatState = BotCombatManager.Instance.GetState(bot);

            CommandManager.SendNormalText(this, messageOutput, $"=== Bot '{bot.Name}' (Id: {bot.Id}, ObjId: {bot.ObjId}) ===");
            CommandManager.SendNormalText(this, messageOutput, $"Position: {bot.Transform.World.Position}");
            var transform = bot.Transform;
            var worldTransform = transform.World;
            var position = worldTransform.Position;
            var yaw = worldTransform.Rotation.Z;
            CommandManager.SendNormalText(this, messageOutput,
                string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"Transform: world={transform.WorldId}, instance={transform.InstanceId}, zone={transform.ZoneId}, " +
                    $"x={position.X:R}, y={position.Y:R}, z={position.Z:R}, yaw_rad={yaw:R}"));
            CommandManager.SendNormalText(this, messageOutput, $"HP: {bot.Hp}/{bot.MaxHp}, MP: {bot.Mp}/{bot.MaxMp}");
            CommandManager.SendNormalText(this, messageOutput, $"IsDead: {bot.IsDead}, IsInBattle: {bot.IsInBattle}");
            var isStealthed = bot.Buffs?.HasEffectsMatchingCondition(effect => effect.Template.Stealth) == true;
            CommandManager.SendNormalText(this, messageOutput, $"Stealthed: {isStealthed}");

            if (moveState != null)
            {
                CommandManager.SendNormalText(this, messageOutput, $"--- Movement State ---");
                CommandManager.SendNormalText(this, messageOutput, $"Destination: {moveState.Destination?.ToString() ?? "null"}");
                CommandManager.SendNormalText(this, messageOutput, $"IsRunning: {moveState.IsRunning}");
                CommandManager.SendNormalText(this, messageOutput, $"IsMoving: {moveState.IsMoving}");
                CommandManager.SendNormalText(this, messageOutput, $"IsFalling: {moveState.IsFalling}");
                CommandManager.SendNormalText(this, messageOutput, $"FallVelocity: {moveState.FallVelocity}");
                CommandManager.SendNormalText(this, messageOutput, $"FollowTarget: {moveState.FollowTarget?.Name ?? "null"}");
            }
            else
            {
                CommandManager.SendErrorText(this, messageOutput, "No movement state found.");
            }

            if (combatState != null)
            {
                CommandManager.SendNormalText(this, messageOutput, $"--- Combat State ---");
                CommandManager.SendNormalText(this, messageOutput,
                    $"State: {combatState.CurrentState}, Previous: {combatState.PreviousState}, Forced: {combatState.ForcedState?.ToString() ?? "null"}");
                CommandManager.SendNormalText(this, messageOutput, $"IsActive: {combatState.IsActive}");
                CommandManager.SendNormalText(this, messageOutput, $"IsResting: {combatState.IsResting}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Target: {combatState.Target?.Name ?? "null"}, CurrentTarget: {bot.CurrentTarget?.Name ?? "null"}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Duel: active={combatState.InDuel}, opponent={combatState.DuelOpponent?.Name ?? "null"}");
                var searchElapsed = combatState.IsSearching && combatState.SearchStartTime != default
                    ? Math.Max(0d, (DateTime.UtcNow - combatState.SearchStartTime).TotalSeconds)
                    : 0d;
                CommandManager.SendNormalText(this, messageOutput,
                    $"Search: active={combatState.IsSearching}, elapsed_s={searchElapsed:F2}, radius={combatState.SearchRadius:F2}, angle={combatState.SearchAngle:F2}, last_known={combatState.LastKnownTargetPosition?.ToString() ?? "null"}");
                CommandManager.SendNormalText(this, messageOutput, $"KillCount: {combatState.KillCount}");
                CommandManager.SendNormalText(this, messageOutput, $"ShouldRespawn: {combatState.ShouldRespawn}");
            }
            else
            {
                CommandManager.SendNormalText(this, messageOutput, "Combat state: not active.");
            }

            // Also check task status
            CommandManager.SendNormalText(this, messageOutput, $"Movement task running: {BotManager.Instance.IsMovementTaskRunning(botId)}");
            CommandManager.SendNormalText(this, messageOutput, $"Combat task running: {BotCombatManager.Instance.IsTaskRunning(botId)}");
            var runtime = BotHost.Instance.GetRuntime(botId);
            if (runtime != null)
            {
                var life = runtime.LifeController.Inspect();
                var transition = life.LastTransition;
                CommandManager.SendNormalText(this, messageOutput,
                    $"Life: state={life.Life.State}, entered_at={Timestamp(life.Life.EnteredAt)}, profile={life.ProfileId}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Life transition: event={transition?.Event.Kind.ToString() ?? "none"}, " +
                    $"outcome={transition?.Outcome.ToString() ?? "none"}, reason={transition?.Reason.ToString() ?? "none"}, " +
                    $"at={Timestamp(transition?.Event.At)}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Life decision: activity={life.Activity ?? "none"}, reason={life.DecisionReason ?? "none"}, " +
                    $"at={Timestamp(life.DecisionAt)}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Life recovery: state={RecoveryState(life.Recovery.State)}, " +
                    $"started_at={Timestamp(life.Recovery.StartedAt)}, completed_at={Timestamp(life.Recovery.CompletedAt)}, " +
                    $"observed_at={Timestamp(life.Recovery.ObservedAt)}, " +
                    $"resources={Availability(life.Recovery.ResourcesAvailable)}, " +
                    $"hp={Number(life.Recovery.Hp)}/{Number(life.Recovery.MaxHp)}, " +
                    $"mp={Number(life.Recovery.Mp)}/{Number(life.Recovery.MaxMp)}");
                var callback = !life.LogoutCallbackAt.HasValue
                    ? "not_requested"
                    : !life.LogoutSucceeded.HasValue
                        ? "pending"
                        : life.LogoutSucceeded.Value ? "succeeded" : "failed";
                CommandManager.SendNormalText(this, messageOutput,
                    $"Life logout: callback={callback}, requested_at={Timestamp(life.LogoutRequestedAt)}, " +
                    $"callback_at={Timestamp(life.LogoutCallbackAt)}, completed_at={Timestamp(life.LogoutCompletedAt)}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Life baseline: {Snapshot(life.ProgressionBaseline)}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Life completion: {Snapshot(life.ProgressionCompletion)}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Life delta: {Delta(life.ProgressionDelta)}");
                var questIntake = runtime.QuestIntakeController.Inspect();
                CommandManager.SendNormalText(this, messageOutput,
                    string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"Quest intake: state={QuestIntakeState(questIntake.State)}, " +
                        $"npc={Optional(questIntake.NpcTemplateId)}:{Optional(questIntake.NpcObjectId)}, " +
                        $"quest={Optional(questIntake.QuestId)}, main_story={Optional(questIntake.MainStory)}, " +
                        $"reason={questIntake.DecisionReason ?? "none"}, at={Timestamp(questIntake.DecisionAt)}, " +
                        $"last_accepted_at={Timestamp(questIntake.LastAcceptedAt)}, retry_at={Timestamp(questIntake.RetryAt)}, " +
                        $"accepted={questIntake.AcceptedCount}, rejected={questIntake.RejectedCount}"));
                var runtimeMetrics = runtime.Metrics;
                CommandManager.SendNormalText(this, messageOutput,
                    string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"Runtime metrics: brain_steps={runtimeMetrics.BrainSteps}, " +
                        $"mover_steps={runtimeMetrics.MoverSteps}, errors={runtimeMetrics.Errors}"));
            }
            var hostMetrics = BotHost.Instance.Metrics;
            CommandManager.SendNormalText(this, messageOutput, $"Host metrics: bots={hostMetrics.LastTickBots}, active={hostMetrics.ActiveBots}, tick_ms_ema={hostMetrics.TickMsEma:F2}, max={hostMetrics.MaxTickMs:F2}, skipped={hostMetrics.SkippedTicks}, brain_steps={hostMetrics.BrainStepsTotal}, mover_steps={hostMetrics.MoverStepsTotal}");
        }

        private static string Timestamp(DateTimeOffset? value) =>
            value?.ToUniversalTime().ToString("O") ?? "none";

        private static string Snapshot(BotLifeProgressionSnapshot? snapshot)
        {
            if (!snapshot.HasValue)
                return "pending";

            var value = snapshot.Value;
            return
                $"captured_at={Timestamp(value.CapturedAt)}, level={Number(value.Level)}, experience={Number(value.Experience)}, " +
                $"hp={Number(value.Hp)}/{Number(value.MaxHp)}, mp={Number(value.Mp)}/{Number(value.MaxMp)}, " +
                $"bag_slots={Number(value.OccupiedBagSlots)}, bag_units={Number(value.BagItemUnits)}, " +
                $"inventory={(value.InventoryAvailable ? "available" : "unavailable")}, " +
                $"summary={value.InventorySummary}, fingerprint={value.InventoryFingerprint}";
        }

        private static string Delta(BotLifeProgressionDelta? delta)
        {
            if (!delta.HasValue)
                return "pending";

            var value = delta.Value;
            return
                $"level={Signed(value.Level)}, experience={Signed(value.Experience)}, " +
                $"hp={Signed(value.Hp)}, max_hp={Signed(value.MaxHp)}, mp={Signed(value.Mp)}, max_mp={Signed(value.MaxMp)}, " +
                $"bag_slots={Signed(value.OccupiedBagSlots)}, bag_units={Signed(value.BagItemUnits)}, " +
                $"inventory_changed={Boolean(value.InventoryChanged)}";
        }

        private static string Number(long? value) =>
            value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable";

        private static string Signed(long? value) =>
            value?.ToString("+0;-0;+0", System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable";

        private static string Boolean(bool? value) =>
            value.HasValue ? value.Value.ToString().ToLowerInvariant() : "unavailable";

        private static string Availability(bool? value) =>
            value.HasValue ? value.Value ? "available" : "unavailable" : "pending";

        private static string RecoveryState(BotLifeRecoveryState state) => state switch
        {
            BotLifeRecoveryState.NotRequired => "not_required",
            BotLifeRecoveryState.Pending => "pending",
            BotLifeRecoveryState.Completed => "completed",
            _ => "unavailable"
        };

        private static string QuestIntakeState(Bots.Questing.BotQuestIntakeState state) =>
            state.ToString().ToLowerInvariant();

        private static string Optional(uint? value) =>
            value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";

        private static string Optional(bool? value) =>
            value.HasValue ? value.Value.ToString().ToLowerInvariant() : "none";
    }
}
