#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>Services an already requested native shutdown when the world scheduler is stalled.</summary>
public sealed class BotPendingShutdownCommand : ICommand
{
    public string[] CommandNames { get; set; } = ["botpendingshutdown"];
    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }
    public string GetCommandLineHelp() => "pending";
    public string GetCommandHelpText() =>
        "Runs the existing pending native shutdown check, including its save gate. First request /scripts shutdown now.";

    public void Execute(Character character, string[] args, IMessageOutput output)
    {
        if (!ValidArguments(args))
        {
            CommandManager.SendDefaultHelpText(this, output);
            return;
        }
        var shutdown = SaveManager.Instance.ShutdownTask;
        if (shutdown == null)
        {
            CommandManager.SendErrorText(this, output, "No native shutdown is pending. Use /scripts shutdown now first.");
            return;
        }
        // Do not manufacture an exit, skip saving, or release another thread's
        // locks. This is the same task the native scheduler would have executed.
        shutdown.Execute();
        CommandManager.SendNormalText(this, output, "Pending native shutdown checked; its deadline and save gate remain authoritative.");
    }

    internal static bool ValidArguments(string[] args) => args is { Length: 1 } &&
        string.Equals(args[0], "pending", StringComparison.OrdinalIgnoreCase);
}
#endif
