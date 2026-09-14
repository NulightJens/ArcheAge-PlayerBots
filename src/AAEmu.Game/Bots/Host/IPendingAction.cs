#if !PLAYERBOTS_AAEMU_3_0
#nullable enable
using AAEmu.Game.Models.Game.Skills.Static;
namespace AAEmu.Game.Bots.Host;

public interface IPendingAction
{
    bool Finished { get; }
    string? Failure { get; }
    bool? ClientAccepted { get; }
    SkillResult? NativeResult { get; }
}
#endif
