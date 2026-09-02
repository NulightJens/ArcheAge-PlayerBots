using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using AAEmu.Commons.Utils;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Population.Identity;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Connections;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Duels;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.Game.Models.StaticValues;
using NLog;

namespace AAEmu.Game.Core.Managers.Bots;

public enum SpawnResult
{
    Ok,
    AlreadyActive,
    Online,
    LoadFailed
}

public class BotManager : Singleton<BotManager>, IBotManager
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentDictionary<uint, Character> ActiveBots = new();
    private readonly ConcurrentDictionary<uint, BotMovementState> _botStates = new();
    private readonly ConcurrentDictionary<uint, BotMovementBroadcaster> _broadcasters = new();
    private readonly ConcurrentDictionary<uint, global::AAEmu.Game.Models.Tasks.Task> _movementTasks = new();
    private readonly Func<uint, Character> _characterLoader;
    private readonly Func<uint, Character> _onlineLookup;
    private readonly Action<Character> _fullLoader;
    private readonly Action<Character> _onBotSpawn;
    private readonly Action<Character> _saveAndRemove;
    private readonly Action<Character> _leaveWorld;
    private readonly Action<Character> _setWorld;
    private readonly Func<Character, bool> _prepareCharacter;
    private readonly Action<Character> _spawn;
    private readonly Action<Character> _publishEquipmentVisibility;
    private readonly Action<Character> _teamLoginRebind;
    private readonly IWorldManager _worldManager;
    private readonly ICharacterManager _characterManager;
    private readonly ISkillManager _skillManager;
    private readonly IObjectIdManager _objectIdManager;
    private readonly ITaskManager _taskManager;
    private readonly IEnterWorldManager _enterWorldManager;
    private readonly IBotArchetypeManager _botArchetypeManager;
    private readonly IBotCombatManager _botCombatManager;
    private readonly IBotHost _botHost;
#if !PLAYERBOTS_AAEMU_3_0
    private readonly IBotIdentityFactory _botIdentityFactory;
    private readonly string _botIdentityFactoryUnavailableReason;
#endif

    internal BotManager() : this(
        Character.Load,
        teamLoginRebind: character => TeamManager.Instance.UpdateAtLogin(character))
    {
    }

    public BotManager(
        IWorldManager worldManager,
        ICharacterManager characterManager,
        ISkillManager skillManager,
        IObjectIdManager objectIdManager,
        ITaskManager taskManager,
        IEnterWorldManager enterWorldManager,
        IBotArchetypeManager botArchetypeManager,
        IBotCombatManager botCombatManager,
        IBotHost botHost = null,
        IBotIdentityFactory botIdentityFactory = null)
    {
        _worldManager = worldManager ?? throw new ArgumentNullException(nameof(worldManager));
        _characterManager = characterManager ?? throw new ArgumentNullException(nameof(characterManager));
        _skillManager = skillManager ?? throw new ArgumentNullException(nameof(skillManager));
        _objectIdManager = objectIdManager ?? throw new ArgumentNullException(nameof(objectIdManager));
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        _enterWorldManager = enterWorldManager ?? throw new ArgumentNullException(nameof(enterWorldManager));
        _botArchetypeManager = botArchetypeManager ?? throw new ArgumentNullException(nameof(botArchetypeManager));
        _botCombatManager = botCombatManager ?? throw new ArgumentNullException(nameof(botCombatManager));
        _botHost = botHost;

        _characterLoader = Character.Load;
        _onlineLookup = id => _worldManager.GetCharacterById(id);
        _fullLoader = character => character.Load();
        _onBotSpawn = _botArchetypeManager.OnBotSpawn;
        _saveAndRemove = GameConnection.SaveAndRemoveFromWorld;
        _leaveWorld = character => _enterWorldManager.LeaveWorldTask(null, LeaveWorldTargetType.CharacterSelect, character);
        _setWorld = character =>
        {
            character.Transform.InstanceId = WorldManager.DefaultInstanceId;
            character.ParentWorld = _worldManager.GetWorld(WorldManager.DefaultInstanceId);
        };
        _prepareCharacter = PrepareCharacter;
        _spawn = character => character.Spawn();
        _publishEquipmentVisibility = BotEquipmentVisibility.PublishPublic;
        _teamLoginRebind = character => TeamManager.Instance.UpdateAtLogin(character);
#if !PLAYERBOTS_AAEMU_3_0
        _botIdentityFactory = botIdentityFactory ?? CreateIdentityFactory(
            _characterManager,
            _botArchetypeManager,
            out _botIdentityFactoryUnavailableReason);
#endif
    }

    internal BotManager(
        Func<uint, Character> characterLoader,
        Func<uint, Character> onlineLookup = null,
        Action<Character> fullLoader = null,
        Action<Character> onBotSpawn = null,
        Action<Character> saveAndRemove = null,
        Action<Character> leaveWorld = null,
        Action<Character> setWorld = null,
        Func<Character, bool> prepareCharacter = null,
        Action<Character> spawn = null,
        Action<Character> publishEquipmentVisibility = null,
        Action<Character> teamLoginRebind = null,
        IBotIdentityFactory botIdentityFactory = null)
    {
        _worldManager = null;
        _characterManager = null;
        _skillManager = null;
        _objectIdManager = null;
        _taskManager = null;
        _enterWorldManager = null;
        _botArchetypeManager = null;
        _botCombatManager = null;
        _botHost = null;
        _characterLoader = characterLoader ?? throw new ArgumentNullException(nameof(characterLoader));
        _onlineLookup = onlineLookup ?? (id => WorldManager.Instance.GetCharacterById(id));
        _fullLoader = fullLoader ?? (character => character.Load());
        _onBotSpawn = onBotSpawn ?? (character => BotArchetypeManager.Instance.OnBotSpawn(character));
        _saveAndRemove = saveAndRemove ?? GameConnection.SaveAndRemoveFromWorld;
        _leaveWorld = leaveWorld ?? (character => EnterWorldManager.Instance.LeaveWorldTask(null, LeaveWorldTargetType.CharacterSelect, character));
        _setWorld = setWorld ?? SetDefaultWorld;
        _prepareCharacter = prepareCharacter ?? PrepareCharacter;
        _spawn = spawn ?? (character => character.Spawn());
        _publishEquipmentVisibility = publishEquipmentVisibility ?? BotEquipmentVisibility.PublishPublic;
        _teamLoginRebind = teamLoginRebind ?? (_ => { });
#if !PLAYERBOTS_AAEMU_3_0
        _botIdentityFactory = botIdentityFactory;
        _botIdentityFactoryUnavailableReason = botIdentityFactory == null ? "identity_factory_not_injected" : null;
#endif
    }

#if !PLAYERBOTS_AAEMU_3_0
    public BotIdentityCreationResult CreateBot(BotIdentityCreationRequest request)
    {
        if (_botIdentityFactory == null)
        {
            return new BotIdentityCreationResult(
                BotIdentityCreationStatus.HostUnavailable,
                _botIdentityFactoryUnavailableReason ?? "identity_factory_unavailable");
        }

        try
        {
            return _botIdentityFactory.CreateAndAdmit(request);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "BotManager: server-owned bot identity creation failed unexpectedly");
            return new BotIdentityCreationResult(
                BotIdentityCreationStatus.Failed,
                $"unexpected_{exception.GetType().Name}");
        }
    }

    private IBotIdentityFactory CreateIdentityFactory(
        ICharacterManager characterManager,
        IBotArchetypeManager archetypeManager,
        out string unavailableReason)
    {
        unavailableReason = null;
        if (characterManager is not IBotIdentityAuthority authority)
        {
            unavailableReason = "aaemu12_identity_compatibility_patch_missing";
            return null;
        }
        if (archetypeManager is not IBotArchetypeCreationPlanStore creationPlans)
        {
            unavailableReason = "archetype_creation_plan_store_unavailable";
            return null;
        }

        try
        {
            var options = BotIdentityFactoryOptions.FromEnvironment();
            var roster = new JsonBotRosterStore(options.RosterPath, authority.CharacterExists);
            return new BotIdentityFactory(
                options,
                authority,
                creationPlans,
                roster,
                SpawnBot);
        }
        catch (Exception exception)
        {
            unavailableReason = $"identity_factory_configuration_{exception.GetType().Name}";
            Logger.Error(exception, "BotManager: server-owned bot identity factory configuration is invalid");
            return null;
        }
    }
#endif

    public Character SpawnBot(uint characterId)
    {
        var result = SpawnBot(characterId, out var bot);
        return result == SpawnResult.Ok ? bot : null;
    }

    public SpawnResult SpawnBot(uint characterId, out Character bot)
    {
        var started = Stopwatch.GetTimestamp();
        var success = false;
        try
        {
            var result = SpawnBotCore(characterId, out bot);
            success = result == SpawnResult.Ok;
            return result;
        }
        finally
        {
            (_botHost ?? BotHost.Instance).Metrics.RecordSpawn(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                success);
        }
    }

    private SpawnResult SpawnBotCore(uint characterId, out Character bot)
    {
        bot = null;
        if (ActiveBots.ContainsKey(characterId))
        {
            Logger.Warn($"BotManager: bot with character id {characterId} is already active");
            return SpawnResult.AlreadyActive;
        }

        if (_onlineLookup(characterId) != null)
        {
            Logger.Warn($"BotManager: character id {characterId} is already online");
            return SpawnResult.Online;
        }

        // Step 1: Load base character data (stats, faction, money, transform, inventory)
        var character = _characterLoader(characterId);
        if (character == null)
        {
            Logger.Error($"BotManager: no character found with id {characterId}");
            return SpawnResult.LoadFailed;
        }

        character.IsBot = true;

        // Step 2: Put it in the default world
        _setWorld(character);

        // Step 3: Full load (skills, quests, abilities, portals, friends, mates, action slots)
        _fullLoader(character);
        character.VisualOptions = new CharacterVisualOptions
        {
            Stp = new byte[6]
        };

        // Connection is intentionally left null here, this character has no network session.

        // Step 4: Assign an ObjId (reuse a previous one if this bot already spawned this server session)
        lock (Character.UsedCharacterObjIds)
        {
            if (Character.UsedCharacterObjIds.TryGetValue(character.Id, out var oldObjId))
            {
                character.ObjId = oldObjId;
            }
            else
            {
                character.ObjId = (_objectIdManager ?? ObjectIdManager.Instance).GetNextId();
                Character.UsedCharacterObjIds.TryAdd(character.Id, character.ObjId);
            }
        }

        // Step 6: Apply starting race/gender buffs, same as a real login
        var resurrectOnSpawn = _prepareCharacter(character);

        BotMovementTask moveTask = null;
        var stateAdded = false;
        var broadcasterAdded = false;
        var movementTaskAdded = false;
        var runtimeAdded = false;
        var activeBotAdded = false;

        void RollbackSpawn()
        {
            if (runtimeAdded)
                (_botHost ?? BotHost.Instance).Unregister(character.Id);
            if (movementTaskAdded)
                _movementTasks.TryRemove(character.Id, out _);
            if (moveTask != null)
                moveTask.Cancelled = true;
            if (broadcasterAdded)
                _broadcasters.TryRemove(character.Id, out _);
            if (stateAdded)
                _botStates.TryRemove(character.Id, out _);
            if (activeBotAdded)
                ActiveBots.TryRemove(character.Id, out _);

            (_botCombatManager ?? BotCombatManager.Instance).StopListening(character);
            (_botArchetypeManager ?? BotArchetypeManager.Instance).RemoveState(character.Id);
            _saveAndRemove(character);
        }

        try
        {
            // Step 7: Build the bot's complete archetype before its first world-visible snapshot.
            // Applying skills, passives, buffs, and gear emits normal gameplay packets; suppress
            // those transient packets so nearby clients only receive the final initialized state.
            var previousBroadcastSuppression = character.SuppressBroadcastPackets;
            character.SuppressBroadcastPackets = true;
            try
            {
                _onBotSpawn(character);
            }
            finally
            {
                character.SuppressBroadcastPackets = previousBroadcastSuppression;
            }

            // Step 8: Spawn once with the fully initialized state.
            // Spawn() = ParentWorld.AddObject(this) + Show()
            _spawn(character);
            _publishEquipmentVisibility(character);

            if (resurrectOnSpawn)
            {
                character.BroadcastPacket(new SCCharacterResurrectedPacket(
                    character.ObjId,
                    character.Transform.World.Position.X,
                    character.Transform.World.Position.Y,
                    character.Transform.World.Position.Z,
                    character.Transform.World.Rotation.Z
                ), true);
            }

            // Step 9: Create movement state, broadcaster, and start the movement task
            var state = new BotMovementState();
            if (!_botStates.TryAdd(character.Id, state))
            {
                RollbackSpawn();
                return SpawnResult.AlreadyActive;
            }
            stateAdded = true;

            var broadcaster = new BotMovementBroadcaster(character);
            if (!_broadcasters.TryAdd(character.Id, broadcaster))
            {
                RollbackSpawn();
                return SpawnResult.AlreadyActive;
            }
            broadcasterAdded = true;

            moveTask = new BotMovementTask(character, state, broadcaster, task => RemoveMovementTask(character.Id, task));
            if (!_movementTasks.TryAdd(character.Id, moveTask))
            {
                RollbackSpawn();
                return SpawnResult.AlreadyActive;
            }
            movementTaskAdded = true;

            var runtime = new BotRuntime(
                character,
                state,
                new BotCombatState { BotId = character.Id },
                broadcaster,
                moveTask,
                blackboard: WorldValues.Create(character, metrics: (_botHost ?? BotHost.Instance).Metrics));
            (_botHost ?? BotHost.Instance).Register(runtime);
            runtimeAdded = true;

            // Start the combat listener (for duel auto-accept, etc.)
            (_botCombatManager ?? BotCombatManager.Instance).StartListening(character);

            if (!ActiveBots.TryAdd(character.Id, character))
            {
                RollbackSpawn();
                return SpawnResult.AlreadyActive;
            }
            activeBotAdded = true;

            // Connectionless bots do not receive CSNotifyInGamePacket, so mirror the
            // real-login team rebind only after the runtime hooks and active-bot entry
            // both exist. UpdateAtLogin replaces TeamMember.Character and its team-
            // changed notification initializes the new runtime's BotSocialState.
            _teamLoginRebind(character);

            Logger.Info($"BotManager: spawned bot '{character.Name}' (Id: {character.Id}, ObjId: {character.ObjId}) at zone {character.Transform.ZoneId}");

            bot = character;
            return SpawnResult.Ok;
        }
        catch
        {
            RollbackSpawn();
            throw;
        }
    }

    /// <summary>
    /// Logs out a single active bot, saving its state the same way a real player logout does.
    /// </summary>
    public bool DespawnBot(uint characterId)
    {
        var started = Stopwatch.GetTimestamp();
        var success = false;
        try
        {
            success = DespawnBotCore(characterId);
            return success;
        }
        finally
        {
            (_botHost ?? BotHost.Instance).Metrics.RecordDespawn(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                success);
        }
    }

    private bool DespawnBotCore(uint characterId)
    {
        if (!ActiveBots.TryRemove(characterId, out var character))
        {
            Logger.Warn($"BotManager: no active bot found with character id {characterId}");
            return false;
        }

        character.DisabledSetPosition = true;
        if (_movementTasks.TryRemove(characterId, out var moveTask))
            moveTask.Cancel();
        (_botCombatManager ?? BotCombatManager.Instance).StopListening(character);
        (_botArchetypeManager ?? BotArchetypeManager.Instance).RemoveState(characterId);
        _botStates.TryRemove(characterId, out _);
        _broadcasters.TryRemove(characterId, out _);

        _leaveWorld(character);
        Logger.Info($"BotManager: despawned bot '{character.Name}' (Id: {character.Id})");
        return true;
    }

    /// <summary>
    /// Logs out every currently active bot, called on server shutdown.
    /// </summary>
    public void DespawnAllBots()
    {
        foreach (var characterId in ActiveBots.Keys.ToList())
        {
            if (ActiveBots.TryGetValue(characterId, out var character) && character.IsInDuel &&
                DuelManager.Instance.TryGetDuel(characterId, out var duel))
            {
                DuelManager.Instance.DuelStop(duel.Challenger.Id, DuelDetType.Draw);
            }

            DespawnBot(characterId);
        }
    }

    public void Stop()
    {
        DespawnAllBots();
        var host = _botHost ?? BotHost.Instance;
        host.Metrics.RecordShutdownCleanup(ActiveBots.Count, host.RuntimeCount);
        Logger.Info($"BOT ev=shutdown_cleanup remaining_bots={ActiveBots.Count} remaining_runtimes={host.RuntimeCount}");
    }

    /// <summary>
    /// Moves a bot to an exact position (teleport). Updates position, broadcasts a stopping packet,
    /// and clears any pending movement destination.
    /// </summary>
    public void MoveBotTo(Character bot, float x, float y, float z)
    {
        if (bot == null)
            return;

        // Get the broadcaster for this bot
        if (!_broadcasters.TryGetValue(bot.Id, out var broadcaster))
        {
            Logger.Warn($"MoveBotTo: no broadcaster found for bot {bot.Name}");
            return;
        }

        // Send teleport packet via broadcaster
        broadcaster.SendTeleport(new Vector3(x, y, z), bot.IsInBattle);

        // Clear any pending destination and reset movement flags
        if (_botStates.TryGetValue(bot.Id, out var state))
            ResetMovementState(state);

        Logger.Trace($"BOT id={bot.Id} obj={bot.ObjId} ev=teleport pos=({x}, {y}, {z})");
    }

    public List<Character> GetAllBots()
    {
        return ActiveBots.Values.ToList();
    }

    /// <summary>
    /// Force stops the bot and clears all movement state.
    /// </summary>
    public void StopImmediately(Character bot)
    {
        if (bot == null || !_botStates.TryGetValue(bot.Id, out var state))
            return;

        ResetMovementState(state);

        StopAndClear(bot, bot.Transform.World.Position);

        Logger.Trace($"BOT id={bot.Id} obj={bot.ObjId} ev=stop_immediately");
    }

    /// <summary>
    /// Makes a bot follow a target character.
    /// </summary>
    public void SetFollowTarget(Character bot, Character target, float followDistance = 2.0f)
    {
        if (bot == null || target == null || !_botStates.TryGetValue(bot.Id, out var state))
            return;
        state.FollowTarget = target;
        state.FollowDistance = followDistance;
        state.Destination = null; // override any manual destination
        Logger.Trace($"BOT id={bot.Id} obj={bot.ObjId} ev=follow target={target.Id}");
    }

    /// <summary>
    /// Stops the bot from following.
    /// </summary>
    public void StopFollow(Character bot)
    {
        if (bot == null || !_botStates.TryGetValue(bot.Id, out var state))
            return;
        state.FollowTarget = null;
        StopImmediately(bot);
    }

    /// <summary>
    /// Sets a destination for the bot to walk/run toward. The movement task will handle continuous updates.
    /// </summary>
    public void SetBotDestination(Character bot, float x, float y, float z, bool run = true)
    {
        if (bot == null || !_botStates.TryGetValue(bot.Id, out var state))
            return;

        state.Destination = new Vector3(x, y, z);
        state.IsRunning = run;
        state.FallVelocity = 0; // reset fall when starting to move
        Logger.Trace($"BOT id={bot.Id} obj={bot.ObjId} ev=destination pos=({x}, {y}, {z}) run={run}");
    }

    internal bool SetBotDestinationIfChanged(Character bot, Vector3 destination, bool run = true, float tolerance = 0.5f)
    {
        if (bot == null || !_botStates.TryGetValue(bot.Id, out var state))
            return false;

        if (state.Destination is { } current && Vector3.Distance(current, destination) <= tolerance)
            return false;

        SetBotDestination(bot, destination.X, destination.Y, destination.Z, run);
        return true;
    }

    internal bool StopIfMoving(Character bot)
    {
        if (bot == null || !_botStates.TryGetValue(bot.Id, out var state) || state.Destination == null)
            return false;

        StopImmediately(bot);
        return true;
    }

    /// <summary>
    /// Stops the bot's current movement by clearing its destination.
    /// </summary>
    public void StopBot(Character bot)
    {
        if (bot == null || !_botStates.TryGetValue(bot.Id, out var state))
            return;
        state.Destination = null;
        Logger.Trace($"BOT id={bot.Id} obj={bot.ObjId} ev=stop");
    }

    public Character GetBot(uint characterId)
    {
        ActiveBots.TryGetValue(characterId, out var character);
        return character;
    }

    // Helper to get movement state (for debugging, etc.)
    public BotMovementState GetBotState(uint characterId)
    {
        _botStates.TryGetValue(characterId, out var state);
        return state;
    }

    public bool IsMovementTaskRunning(uint characterId)
    {
        return _movementTasks.TryGetValue(characterId, out var task) && !task.Cancelled &&
               (_botHost ?? BotHost.Instance).GetRuntime(characterId)?.Mover == task;
    }

    /// <summary>
    /// Gets the movement broadcaster for a bot.
    /// </summary>
    public BotMovementBroadcaster GetBroadcaster(uint characterId)
    {
        _broadcasters.TryGetValue(characterId, out var broadcaster);
        return broadcaster;
    }

    private void RemoveMovementTask(uint characterId, BotMovementTask task)
    {
        if (_movementTasks.TryGetValue(characterId, out var current) && ReferenceEquals(current, task))
            _movementTasks.TryRemove(characterId, out _);
    }

    private static void ResetMovementState(BotMovementState state)
    {
        state.Destination = null;
        state.IsMoving = false;
        state.IsFalling = false;
        state.FallVelocity = 0;
        state.JumpRequested = false;
        state.IsJumping = false;
        state.JumpVerticalVelocity = 0;
    }

    private void StopAndClear(Character bot, Vector3 pos)
    {
        if (!_broadcasters.TryGetValue(bot.Id, out var broadcaster))
            return;

        broadcaster.SendStop(pos, bot.IsInBattle);
        bot.Transform.FinalizeTransform();
    }

    private static void SetDefaultWorld(Character character)
    {
        character.Transform.InstanceId = WorldManager.DefaultInstanceId;
        character.ParentWorld = WorldManager.Instance.GetWorld(WorldManager.DefaultInstanceId);
    }

    private bool PrepareCharacter(Character character)
    {
        var characterManager = _characterManager ?? CharacterManager.Instance;
        var skillManager = _skillManager ?? SkillManager.Instance;
        var template = characterManager.GetTemplate(character.Race, character.Gender);
        if (template != null)
        {
            foreach (var buff in template.Buffs)
            {
                var buffTemplate = skillManager.GetBuffTemplate(buff);
                var casterObj = new SkillCasterUnit(character.ObjId);
                character.Buffs.AddBuff(new Buff(character, character, casterObj, buffTemplate, null, DateTime.UtcNow) { Passive = true });
            }
        }

        character.Buffs.AddBuff((uint)BuffConstants.LoggedOn, character);
#if !PLAYERBOTS_AAEMU_3_0
        character.Buffs.LoadActiveBuffs(character);
#endif
        character.CheckWantedThreshold();
        character.UpdateGearBonuses(null, null);
        character.RestoreSavedHpMp();
        var resurrectOnSpawn = false;
        if (character.Hp <= 0)
        {
            character.Hp = Math.Max(1, character.MaxHp / 10);
            character.Mp = Math.Max(1, character.MaxMp / 10);
            resurrectOnSpawn = true;
        }

        character.Breath = character.LungCapacity;
        character.OnZoneChange(0, character.Transform.ZoneId);
        return resurrectOnSpawn;
    }
}
