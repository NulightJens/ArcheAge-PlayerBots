using AAEmu.Game.Core.Managers;
using AAEmu.Game.Bots.Host;
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
            CommandManager.SendNormalText(this, messageOutput,
                $"Combat stats: level={bot.Level}, str={DiagnosticValue(() => bot.Str)}, " +
                $"spi={DiagnosticValue(() => bot.Spi)}, facets={DiagnosticValue(() => bot.Facets)}, " +
                $"melee_accuracy={DiagnosticValue(() => bot.MeleeAccuracy)}, dps={DiagnosticValue(() => bot.Dps)}, " +
                $"dps_inc={DiagnosticValue(() => bot.DpsInc)}, melee_damage_mul={DiagnosticValue(() => bot.MeleeDamageMul)}, " +
                $"learned_skills={bot.Skills?.Skills?.Count ?? 0}");
            CommandManager.SendNormalText(this, messageOutput, $"IsDead: {bot.IsDead}, IsInBattle: {bot.IsInBattle}");
            var isStealthed = bot.Buffs?.HasEffectsMatchingCondition(effect => effect.Template.Stealth) == true;
            CommandManager.SendNormalText(this, messageOutput, $"Stealthed: {isStealthed}");

            if (moveState != null)
            {
                CommandManager.SendNormalText(this, messageOutput, $"--- Movement State ---");
                CommandManager.SendNormalText(this, messageOutput, $"Destination: {moveState.Destination?.ToString() ?? "null"}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Travel route: mode={moveState.TravelMode}, " +
                    $"final={moveState.TravelDestination?.ToString() ?? "null"}, " +
                    $"remaining={moveState.TravelWaypointCount}, " +
                    $"distance={moveState.TravelRemainingDistance:F2}, speed={moveState.TravelSpeed:F2}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Travel steering: target={moveState.SteeringDestination?.ToString() ?? "null"}");
                var destinationSurface = moveState.Destination.HasValue
                    ? DiagnosticGroundHeight(bot, moveState.Destination.Value.X, moveState.Destination.Value.Y)
                    : null;
                CommandManager.SendNormalText(this, messageOutput,
                    $"Destination surface: z={moveState.Destination?.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "none"}, " +
                    $"heightmap={destinationSurface?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "none"}");
                CommandManager.SendNormalText(this, messageOutput,
                    $"Navigation decision: status={moveState.LastNavigationDecision?.Status.ToString() ?? "none"}, " +
                    $"reason={moveState.LastNavigationDecision?.Reason.ToString() ?? "none"}");
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
                var decisionTarget = combatState.Target ?? bot.CurrentTarget;
                var targetPosition = decisionTarget?.Transform?.World.Position;
                var targetSurface = targetPosition.HasValue
                    ? DiagnosticGroundHeight(bot, targetPosition.Value.X, targetPosition.Value.Y)
                    : null;
                CommandManager.SendNormalText(this, messageOutput,
                    $"Decision target: template={decisionTarget?.TemplateId.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}, " +
                    $"obj={decisionTarget?.ObjId.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}, " +
                    $"position={targetPosition?.ToString() ?? "none"}, " +
                    $"heightmap={targetSurface?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "none"}");
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
            string questGiverDebug = null;
            string questLifecycleDebug = null;
            if (runtime != null)
            {
                var questIntake = runtime.QuestIntakeController.Inspect();
                CommandManager.SendNormalText(this, messageOutput,
                    string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"Quest intake: state={QuestIntakeState(questIntake.State)}, " +
                        $"npc={Optional(questIntake.NpcTemplateId)}:{Optional(questIntake.NpcObjectId)}, " +
                        $"quest={Optional(questIntake.QuestId)}, main_story={Optional(questIntake.MainStory)}, " +
                        $"reason={questIntake.DecisionReason ?? "none"}, at={Timestamp(questIntake.DecisionAt)}, " +
                        $"last_accepted_at={Timestamp(questIntake.LastAcceptedAt)}, retry_at={Timestamp(questIntake.RetryAt)}, " +
                        $"accepted={questIntake.AcceptedCount}, rejected={questIntake.RejectedCount}"));
                questGiverDebug = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                    $"Quest intake giver: giver={questIntake.GiverKind?.ToString().ToLowerInvariant() ?? "none"}:" +
                    $"{Optional(questIntake.GiverTemplateId)}:{Optional(questIntake.GiverObjectId)}");
                var questLifecycle = runtime.QuestLifecycleController.Inspect();
                questLifecycleDebug = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"Quest lifecycle: state={questLifecycle.State.ToString().ToLowerInvariant()}, " +
                        $"quest={Optional(questLifecycle.QuestId)}, " +
                        $"objective={Optional(questLifecycle.ObjectiveTargetTemplateId)}:" +
                        $"{Optional(questLifecycle.ObjectiveTargetObjectId)} " +
                        $"item={Optional(questLifecycle.ObjectiveItemId)} " +
                        $"progress={Optional(questLifecycle.ObjectiveCurrent)}/" +
                        $"{Optional(questLifecycle.ObjectiveRequired)}, " +
                        $"report={questLifecycle.ReportKind?.ToString().ToLowerInvariant() ?? "none"}:" +
                        $"{Optional(questLifecycle.ReportTemplateId)}:{Optional(questLifecycle.ReportObjectId)}, " +
                        $"reward={Optional(questLifecycle.RewardIndex)}, " +
                        $"reason={questLifecycle.DecisionReason ?? "none"}, at={Timestamp(questLifecycle.DecisionAt)}, " +
                        $"progress_at={Timestamp(questLifecycle.ProgressObservedAt)}, " +
                        $"report_at={Timestamp(questLifecycle.ReportAttemptedAt)}, " +
                        $"completed_at={Timestamp(questLifecycle.CompletedAt)}, retry_at={Timestamp(questLifecycle.RetryAt)}, " +
                        $"completed={questLifecycle.CompletedCount}, suspended={questLifecycle.SuspensionCount}, " +
                        $"report_attempts={questLifecycle.ReportAttemptCount}");
                var runtimeMetrics = runtime.Metrics;
                CommandManager.SendNormalText(this, messageOutput,
                    string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"Runtime metrics: brain_steps={runtimeMetrics.BrainSteps}, " +
                        $"mover_steps={runtimeMetrics.MoverSteps}, errors={runtimeMetrics.Errors}"));
            }
            var hostMetrics = BotHost.Instance.Metrics;
            CommandManager.SendNormalText(this, messageOutput, $"Host metrics: bots={hostMetrics.LastTickBots}, active={hostMetrics.ActiveBots}, tick_ms_ema={hostMetrics.TickMsEma:F2}, max={hostMetrics.MaxTickMs:F2}, skipped={hostMetrics.SkippedTicks}, brain_steps={hostMetrics.BrainStepsTotal}, mover_steps={hostMetrics.MoverStepsTotal}");
            if (questGiverDebug != null)
                CommandManager.SendNormalText(this, messageOutput, questGiverDebug);
            if (questLifecycleDebug != null)
                CommandManager.SendNormalText(this, messageOutput, questLifecycleDebug);
        }

        private static string Timestamp(DateTimeOffset? value) =>
            value?.ToUniversalTime().ToString("O") ?? "none";

        private static string DiagnosticValue(Func<object> read)
        {
            try
            {
                return Convert.ToString(read(), System.Globalization.CultureInfo.InvariantCulture) ?? "unavailable";
            }
            catch
            {
                return "unavailable";
            }
        }

        private static float? DiagnosticGroundHeight(Character bot, float x, float y)
        {
            try
            {
                return bot.ParentWorld?.GetHeight(x, y);
            }
            catch
            {
                return null;
            }
        }

        private static string QuestIntakeState(Bots.Questing.BotQuestIntakeState state) =>
            state.ToString().ToLowerInvariant();

        private static string Optional(uint? value) =>
            value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";

        private static string Optional(int? value) =>
            value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";

        private static string Optional(bool? value) =>
            value.HasValue ? value.Value.ToString().ToLowerInvariant() : "none";
    }
}
