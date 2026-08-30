using AAEmu.Game.Bots.Content;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Content.Strategies;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;

namespace AAEmu.Game.Bots.Host;

public sealed class BotRuntime
{
    public BotRuntime(
        Character bot,
        BotMovementState movementState,
        BotCombatState combatState,
        BotMovementBroadcaster broadcaster = null,
        BotMovementTask mover = null,
        BotCombatTask brain = null,
        BotBlackboard blackboard = null,
        BotConfig config = null)
    {
        Bot = bot ?? throw new ArgumentNullException(nameof(bot));
        MovementState = movementState ?? throw new ArgumentNullException(nameof(movementState));
        CombatState = combatState ?? throw new ArgumentNullException(nameof(combatState));
        Broadcaster = broadcaster;
        Mover = mover;
        Mover?.BindCombatState(combatState);
        Brain = brain;
        Blackboard = blackboard ?? new BotBlackboard();
        CombatState.BotId = bot.Id;
        KillCreditSubscription = new BotKillCreditSubscription(Bot, CombatState);
        Social = new BotSocialState(this);
        TeamHooks = new BotTeamHooks(this);

        var runtimeConfig = config ?? BotConfig.Instance;
        StuckWatch = new BotStuckWatch(movementState, runtimeConfig);
        if (runtimeConfig.UseEngine)
        {
            LegacyContent.Register();
            if (BotContentRegistry.TryCreateStrategy("legacy", out var legacyStrategy))
            {
                Engines[(int)BotEngineKind.Combat] = new BotEngine(
                    BotEngineKind.Combat,
                    runtimeConfig,
                    [new CombatBaseStrategy(), legacyStrategy]);
                Engines[(int)BotEngineKind.NonCombat] = new BotEngine(
                    BotEngineKind.NonCombat,
                    runtimeConfig,
                    [new BodyBaseStrategy(), legacyStrategy]);
            }
        }
    }

    public Character Bot { get; }
    public BotMovementState MovementState { get; }
    public BotCombatState CombatState { get; }
    public BotMovementBroadcaster Broadcaster { get; }
    public BotMovementTask Mover { get; set; }
    public BotCombatTask Brain { get; set; }
    public string AttachedRotationId { get; set; }
    public string RotationOverrideId { get; set; }
    public int AttachedRotationVersion { get; set; }
    public string AttachedRotationArchetype { get; set; }
    public BotBlackboard Blackboard { get; }
    public BotSocialState Social { get; }
    public BotTeamHooks TeamHooks { get; }
    public BotStuckWatch StuckWatch { get; }
    public BotEngine[] Engines { get; } = new BotEngine[3];
    public BotSchedule Schedule { get; } = new();
    public BotRuntimeMetrics Metrics { get; } = new();
    internal BotKillCreditSubscription KillCreditSubscription { get; }
    internal BotHostMetrics HostMetrics { get; set; }
    internal int Running;
    internal object SyncRoot { get; } = new();
    internal bool Retired { get; set; }
    internal bool MissingTransformLogged { get; set; }
}
