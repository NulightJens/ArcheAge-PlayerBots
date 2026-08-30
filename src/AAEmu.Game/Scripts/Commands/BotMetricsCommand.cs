using System.Globalization;
using System.Text.Json;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public sealed class BotMetricsCommand : ICommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string[] CommandNames { get; set; } = ["botmetrics"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp() => "[snapshot|reset|activity <0-100>]";

    public string GetCommandHelpText() =>
        "Emits or resets the versioned PlayerBots live scale metrics window; activity changes are runtime-only.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var verb = args.Length == 0 ? "snapshot" : args[0].Trim().ToLowerInvariant();
        switch (verb)
        {
            case "snapshot":
                SendSnapshot(messageOutput);
                return;
            case "reset":
                BotHost.Instance.Metrics.Reset();
                TickManager.Instance.Metrics.Reset();
                CommandManager.SendNormalText(this, messageOutput,
                    $"T021_METRICS_RESET {{\"schemaVersion\":\"{BotScaleMetricsEnvelope.CurrentSchemaVersion}\",\"capturedAtUtc\":\"{DateTime.UtcNow:O}\"}}");
                return;
            case "activity":
                if (args.Length != 2 ||
                    !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ||
                    percent < 0 || percent > 100)
                {
                    CommandManager.SendErrorText(this, messageOutput, "Activity must be a number from 0 through 100.");
                    return;
                }

                BotConfig.Instance.ActivityPercent = percent;
                CommandManager.SendNormalText(this, messageOutput,
                    $"T021_ACTIVITY {{\"activityPercent\":{percent.ToString(CultureInfo.InvariantCulture)},\"persistence\":\"runtime-only\"}}");
                return;
            default:
                CommandManager.SendDefaultHelpText(this, messageOutput);
                return;
        }
    }

    private void SendSnapshot(IMessageOutput messageOutput)
    {
        var snapshot = BotScaleMetricsEnvelope.Capture(
            BotHost.Instance.Metrics,
            TickManager.Instance.Metrics,
            BotConfig.Instance,
            BotHost.Instance.RuntimeCount);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        CommandManager.SendNormalText(this, messageOutput, $"T021_METRICS {json}");
    }
}
