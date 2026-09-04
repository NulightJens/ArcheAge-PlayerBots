using System.Globalization;
using AAEmu.Commons.IO;

namespace AAEmu.Game.Bots.Population.Identity;

public sealed record BotIdentityFactoryOptions(uint ServerOwnedAccountId, string RosterPath)
{
    public const string AccountIdEnvironmentVariable = "AAEMU_PLAYERBOTS_ACCOUNT_ID";
    public const string RosterPathEnvironmentVariable = "AAEMU_PLAYERBOTS_ROSTER_PATH";

    public static BotIdentityFactoryOptions FromEnvironment()
    {
        var accountText = Environment.GetEnvironmentVariable(AccountIdEnvironmentVariable);
        _ = uint.TryParse(accountText, NumberStyles.None, CultureInfo.InvariantCulture, out var accountId);

        var rosterPath = Environment.GetEnvironmentVariable(RosterPathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(rosterPath))
        {
            rosterPath = Path.Combine(FileManager.AppPath, "Data", "PlayerBots", "bot-roster.json");
        }

        return new BotIdentityFactoryOptions(accountId, rosterPath);
    }
}
