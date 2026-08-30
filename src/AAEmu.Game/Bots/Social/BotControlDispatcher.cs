using AAEmu.Commons.Utils;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Team;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Social;

public sealed class BotControlDispatcher : Singleton<BotControlDispatcher>
{
    private readonly IBotManager _bots;
    private readonly IBotHost _host;
    private readonly ITeamManager _teams;
    private readonly IBotCombatManager _combat;
    private readonly TimeProvider _timeProvider;

    internal BotControlDispatcher()
        : this(BotManager.Instance, BotHost.Instance, TeamManager.Instance, TimeProvider.System)
    {
    }

    public BotControlDispatcher(
        IBotManager bots,
        IBotHost host,
        ITeamManager teams,
        TimeProvider timeProvider,
        IBotCombatManager combat = null)
    {
        _bots = bots ?? throw new ArgumentNullException(nameof(bots));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _teams = teams ?? throw new ArgumentNullException(nameof(teams));
        _combat = combat ?? BotCombatManager.Instance;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public BotControlDispatchResult Dispatch(
        Character requester,
        uint botId,
        BotControlVerb verb,
        MemberRole role = MemberRole.Undecided)
    {
        ArgumentNullException.ThrowIfNull(requester);
        var bot = _bots.GetBot(botId);
        if (bot == null || !bot.IsBot)
            return Reject(BotControlStatus.UnknownBot, $"Bot {botId} is not active.");

        var runtime = _host.GetRuntime(botId);
        if (runtime == null || runtime.Retired)
            return Reject(BotControlStatus.Unavailable, $"Bot {botId} has no active runtime.");

        var botTeam = _teams.GetActiveTeamByUnit(bot.Id);
        var requesterTeam = _teams.GetActiveTeamByUnit(requester.Id);
        if (botTeam == null || requesterTeam == null || botTeam.Id != requesterTeam.Id ||
            botTeam.OwnerId != requester.Id || requester.IsBot)
            return Reject(BotControlStatus.Unauthorized, "Only the current human team owner can control this bot.");

        runtime.TeamHooks.Refresh(botTeam);
        if (!runtime.Social.IsAuthorized(requester.Id, botTeam.Id, botTeam.OwnerId))
            return Reject(BotControlStatus.Unauthorized, "Bot ownership changed before the command could be accepted.");

        if (verb == BotControlVerb.Role && !IsAssignableRole(role))
            return Reject(BotControlStatus.InvalidRole, "Role must be tank, healer, or attacker.");

        var targetObjId = 0u;
        if (verb == BotControlVerb.Attack)
        {
            if (requester.CurrentTarget is not Unit target || target.IsDead)
                return Reject(BotControlStatus.InvalidTarget, "Select a living target before using attack.");
            targetObjId = target.ObjId;
        }

        var command = new BotControlEvent(requester.Id, botTeam.Id, botTeam.OwnerId, verb, targetObjId, role);
        var engine = SelectEngine(runtime);
        if (engine == null)
            return Reject(BotControlStatus.Unavailable, $"Bot {botId} has no control engine.");

        // Inactive duel cleanup can shed the brain without retiring the runtime.
        // Party commands must wake that brain before queuing an event, otherwise
        // the command is accepted but no engine tick can ever consume it.
        _combat.StartListening(bot);
        if (!engine.EnqueueCommand(
            BotControlAction.ActionName,
            new BotEvent("party-control", command),
            _timeProvider.GetUtcNow().UtcDateTime))
            return Reject(BotControlStatus.Unavailable, $"Bot {botId} cannot queue control commands.");

        if (verb == BotControlVerb.Role)
            _teams.SetTeamMemberRole(bot, botTeam.Id, bot.Id, role);
        return new BotControlDispatchResult(BotControlStatus.Accepted, $"{verb} accepted for {bot.Name}.");
    }

    private static bool IsAssignableRole(MemberRole role)
    {
        return role is MemberRole.Tank or MemberRole.Healer or MemberRole.Attacker;
    }

    private static BotEngine SelectEngine(BotRuntime runtime)
    {
        var kind = runtime.CombatState.CurrentState is
            BotCombatStateType.Combat or BotCombatStateType.Dueling or BotCombatStateType.Searching
            ? BotEngineKind.Combat
            : BotEngineKind.NonCombat;
        return runtime.Engines[(int)kind];
    }

    private static BotControlDispatchResult Reject(BotControlStatus status, string message) => new(status, message);
}
