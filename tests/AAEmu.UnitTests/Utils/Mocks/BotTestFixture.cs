using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using AAEmu.Commons.Utils;
using AAEmu.Game.Bots.Content;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Utils.Mocks;

public static class BotTestFixture
{
    private static TaskManager _taskManager;
    private static FakeTimeProvider _timeProvider;
    private static readonly Dictionary<Type, object> s_singletonOverrides = [];

    public static TaskManager RegisterTaskManager()
    {
        ResetSingleton<TaskManager>();
        ResetBotSingletons();

        var taskManager = new TaskManager(Mock.Of<ITickManager>().Object);
        _taskManager = taskManager;
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        RebuildServiceProvider();
        return taskManager;
    }

    public static void RegisterSingletons(params object[] instances)
    {
        foreach (var instance in instances)
        {
            if (instance is BotManager)
                s_singletonOverrides[typeof(BotManager)] = instance;
            else if (instance is BotCombatManager)
                s_singletonOverrides[typeof(BotCombatManager)] = instance;
            else if (instance is BotArchetypeManager)
                s_singletonOverrides[typeof(BotArchetypeManager)] = instance;
        }

        ResetBotSingletons();
        RebuildServiceProvider();
    }

    private static void RebuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_taskManager);
        services.AddSingleton<ITaskManager>(_taskManager);
        services.AddSingleton(_timeProvider);
        services.AddSingleton<TimeProvider>(_timeProvider);
        services.AddSingleton(new SusManager(Mock.Of<IWorldManager>().Object));
        services.AddSingleton<BotHost>();
        services.AddSingleton<IBotHost>(sp => sp.GetRequiredService<BotHost>());
        if (s_singletonOverrides.TryGetValue(typeof(BotManager), out var botManager))
            services.AddSingleton((BotManager)botManager);
        if (s_singletonOverrides.TryGetValue(typeof(BotCombatManager), out var botCombatManager))
            services.AddSingleton((BotCombatManager)botCombatManager);
        if (s_singletonOverrides.TryGetValue(typeof(BotArchetypeManager), out var botArchetypeManager))
            services.AddSingleton((BotArchetypeManager)botArchetypeManager);
        SingletonContainer.ServiceProvider = services.BuildServiceProvider();
    }

    public static void ResetTaskManager()
    {
        SingletonContainer.ServiceProvider = null;
        ResetSingleton<TaskManager>();
        ResetSingleton<SusManager>();
        _taskManager = null;
        _timeProvider = null;
        s_singletonOverrides.Clear();
        ResetBotSingletons();
    }

    public static void ResetBotContentRegistry()
    {
        BotContentRegistry.ResetForTests();
        LegacyContent.ResetForTests();
    }

    private static void ResetBotSingletons()
    {
        var botHost = GetSingleton<BotHost>();
        if (botHost != null)
        {
            foreach (var runtime in botHost.GetRuntimeSnapshot())
                botHost.Unregister(runtime.Bot.Id);
        }

        ResetSingleton<BotHost>();
        ResetSingleton<BotManager>();
        ResetSingleton<BotCombatManager>();
        ResetSingleton<BotArchetypeManager>();
    }

    private static T GetSingleton<T>() where T : class
    {
        return typeof(Singleton<T>)
            .GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null) as T;
    }

    public static CharacterMock MakeBot(uint id, Vector3 position)
    {
        var bot = new CharacterMock { Id = id, ObjId = 1000 + id, Name = $"bot{id}" };
        bot.Transform.Local.SetPosition(position);
        return bot;
    }

    public static WorldInstance MakeWorld(uint instanceId = 1)
    {
        return new WorldInstance(new WorldTemplate { Id = instanceId, Name = $"world{instanceId}" }, 0, true, instanceId);
    }

    public static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            // In a standalone module checkout, source and fixture files live
            // beneath modules/archeage-playerbots rather than the AAEmu tree.
            var moduleSourceCandidate = Path.Combine(
                directory.FullName,
                "modules",
                "archeage-playerbots",
                "src",
                relativePath);
            if (File.Exists(moduleSourceCandidate))
                return moduleSourceCandidate;

            var moduleTestCandidate = Path.Combine(
                directory.FullName,
                "modules",
                "archeage-playerbots",
                "tests",
                relativePath);
            if (File.Exists(moduleTestCandidate))
                return moduleTestCandidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relativePath}.");
    }

    public static T GetPrivateField<T>(object target, string fieldName)
    {
        return (T)FindField(target.GetType(), fieldName)!
            .GetValue(target)!;
    }

    public static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FindField(target.GetType(), fieldName)!
            .SetValue(target, value);
    }

    public static void ResetSingleton<T>() where T : class
    {
        typeof(Singleton<T>)
            .GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, null);
    }

    public static ConcurrentDictionary<uint, T> GetDictionary<T>(object target, string fieldName)
    {
        return GetPrivateField<ConcurrentDictionary<uint, T>>(target, fieldName);
    }

    private static FieldInfo FindField(Type type, string fieldName)
    {
        while (type != null)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null)
                return field;
            type = type.BaseType;
        }

        throw new MissingFieldException(fieldName);
    }
}
