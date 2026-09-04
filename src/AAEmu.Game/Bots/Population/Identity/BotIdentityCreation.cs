using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;

namespace AAEmu.Game.Bots.Population.Identity;

public enum BotIdentityPlacementMode
{
    RaceSpawn,
    Here
}

public readonly record struct BotIdentityPlacement(
    BotIdentityPlacementMode Mode,
    uint WorldId,
    uint InstanceId,
    uint ZoneId,
    float X,
    float Y,
    float Z,
    float Roll,
    float Pitch,
    float Yaw)
{
    public static BotIdentityPlacement RaceSpawn => new(BotIdentityPlacementMode.RaceSpawn, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public bool IsValid => Mode switch
    {
        BotIdentityPlacementMode.RaceSpawn => true,
        BotIdentityPlacementMode.Here =>
            WorldId != 0 &&
            InstanceId != uint.MaxValue &&
            ZoneId != 0 &&
            float.IsFinite(X) &&
            float.IsFinite(Y) &&
            float.IsFinite(Z) &&
            float.IsFinite(Roll) &&
            float.IsFinite(Pitch) &&
            float.IsFinite(Yaw),
        _ => false
    };
}

public sealed record BotIdentityCreationRequest(
    string Name,
    Race Race,
    Gender Gender,
    string Archetype,
    int Level,
    BotIdentityPlacement Placement);

public sealed record BotArchetypeCreationPlan(
    string Name,
    AbilityType Ability1,
    AbilityType Ability2,
    AbilityType Ability3,
    bool IsFinal);

public interface IBotArchetypeCreationPlanStore
{
    bool TryResolveCreationPlan(string archetypeName, byte level, out BotArchetypeCreationPlan plan);
    void RegisterCreationPlan(uint characterId, BotArchetypeCreationPlan plan);
    void RollbackCreationPlan(uint characterId);
}

public enum BotIdentityAuthorityStatus
{
    Created,
    AccountUnavailable,
    AccountOnline,
    InvalidName,
    NameUnavailable,
    TemplateUnavailable,
    IdUnavailable,
    PlacementUnavailable,
    PersistenceFailed,
    Failed
}

public sealed record BotIdentityAuthorityRequest(
    uint ServerOwnedAccountId,
    string Name,
    Race Race,
    Gender Gender,
    byte Level,
    AbilityType Ability1,
    AbilityType Ability2,
    AbilityType Ability3,
    BotIdentityPlacement Placement);

public sealed record BotIdentityAuthorityResult(
    BotIdentityAuthorityStatus Status,
    string Reason,
    Character Character = null)
{
    public static BotIdentityAuthorityResult Created(Character character) =>
        new(BotIdentityAuthorityStatus.Created, "created", character);

    public static BotIdentityAuthorityResult Failure(BotIdentityAuthorityStatus status, string reason) =>
        new(status, reason);
}

/// <summary>AAEmu 1.2 boundary for native character creation and rollback.</summary>
public interface IBotIdentityAuthority
{
    byte MaxPlayerLevel { get; }
    bool CharacterExists(uint characterId);
    BotIdentityAuthorityResult CreateServerOwnedBot(BotIdentityAuthorityRequest request);
    bool RollbackServerOwnedBot(uint serverOwnedAccountId, uint characterId);
    void CompleteServerOwnedBotCreation(uint serverOwnedAccountId, uint characterId);
}

public enum BotIdentityCreationStatus
{
    CreatedAndAdmitted,
    ConfigurationUnavailable,
    HostUnavailable,
    InvalidName,
    InvalidRace,
    InvalidGender,
    InvalidArchetype,
    InvalidLevel,
    InvalidPlacement,
    AccountUnavailable,
    AccountOnline,
    DuplicateName,
    TemplateUnavailable,
    IdUnavailable,
    PersistenceFailed,
    RosterRegistrationFailed,
    ArchetypeRegistrationFailed,
    AdmissionFailed,
    RollbackFailed,
    Failed
}

public sealed record BotIdentityCreationResult(
    BotIdentityCreationStatus Status,
    string Reason,
    uint CharacterId = 0,
    Character Character = null)
{
    public bool Success => Status == BotIdentityCreationStatus.CreatedAndAdmitted;
}

public interface IBotIdentityFactory
{
    BotIdentityCreationResult CreateAndAdmit(BotIdentityCreationRequest request);
}
