using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using NLog;

namespace AAEmu.Game.Bots.Ops;

public sealed class BotAutoSpawnTask : AAEmu.Game.Models.Tasks.Task
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly uint[] _characterIds;
    private readonly BotCombatStateType? _state;
    private readonly IBotManager _botManager;
    private readonly IBotCombatManager _botCombatManager;
    private readonly Action<string> _log;

    public BotAutoSpawnTask(
        IEnumerable<uint> characterIds,
        string state,
        IBotManager botManager,
        IBotCombatManager botCombatManager,
        Action<string> log = null)
    {
        _characterIds = characterIds?.ToArray() ?? [];
        _botManager = botManager ?? throw new ArgumentNullException(nameof(botManager));
        _botCombatManager = botCombatManager ?? throw new ArgumentNullException(nameof(botCombatManager));
        _log = log ?? (message => Logger.Info(message));

        var normalizedState = state?.Trim().ToLowerInvariant();
        if (!BotConfig.TryParseAutoSpawnState(normalizedState, out _state))
        {
            Logger.Warn($"Invalid auto-spawn state '{state}', using idle.");
            _state = BotCombatStateType.Idle;
        }
    }

    public override void Execute()
    {
        foreach (var characterId in _characterIds)
        {
            var result = "Exception";
            var stateOutcome = "NotApplied";
            try
            {
                var spawnResult = _botManager.SpawnBot(characterId, out var bot);
                result = spawnResult.ToString();
                if (spawnResult == SpawnResult.Ok && bot != null)
                {
                    _botCombatManager.SetForcedState(bot, _state);
                    stateOutcome = _state?.ToString() ?? "Free";
                }
            }
            catch (Exception e)
            {
                result = "Exception";
                Logger.Error(e, $"BOT ev=autospawn id={characterId} result=Exception");
            }

            _log($"BOT ev=autospawn id={characterId} result={result} state={stateOutcome}");
        }
    }
}
