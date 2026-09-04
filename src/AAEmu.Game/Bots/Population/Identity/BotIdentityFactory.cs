using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Bots.Population.Identity;

/// <summary>Creates an AAEmu character, records its bot policy, and admits it.</summary>
public sealed class BotIdentityFactory : IBotIdentityFactory
{
    public delegate SpawnResult AdmitBot(uint characterId, out Character character);

    private readonly object _gate = new();
    private readonly BotIdentityFactoryOptions _options;
    private readonly IBotIdentityAuthority _authority;
    private readonly IBotArchetypeCreationPlanStore _archetypes;
    private readonly IBotRosterStore _roster;
    private readonly AdmitBot _admit;

    public BotIdentityFactory(
        BotIdentityFactoryOptions options,
        IBotIdentityAuthority authority,
        IBotArchetypeCreationPlanStore archetypes,
        IBotRosterStore roster,
        AdmitBot admit)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _archetypes = archetypes ?? throw new ArgumentNullException(nameof(archetypes));
        _roster = roster ?? throw new ArgumentNullException(nameof(roster));
        _admit = admit ?? throw new ArgumentNullException(nameof(admit));
    }

    public BotIdentityCreationResult CreateAndAdmit(BotIdentityCreationRequest request)
    {
        if (request == null)
            return Failure(BotIdentityCreationStatus.Failed, "request_missing");
        if (_options.ServerOwnedAccountId == 0 || string.IsNullOrWhiteSpace(_options.RosterPath))
            return Failure(BotIdentityCreationStatus.ConfigurationUnavailable, "server_owned_account_not_configured");
        if (string.IsNullOrWhiteSpace(request.Name))
            return Failure(BotIdentityCreationStatus.InvalidName, "name_missing");
        if (!Enum.IsDefined(request.Race) || request.Race == Race.None)
            return Failure(BotIdentityCreationStatus.InvalidRace, "race_unsupported");
        if (!Enum.IsDefined(request.Gender))
            return Failure(BotIdentityCreationStatus.InvalidGender, "gender_unsupported");
        if (!request.Placement.IsValid)
            return Failure(BotIdentityCreationStatus.InvalidPlacement, "placement_invalid");
        if (_authority.MaxPlayerLevel == 0 || request.Level < 1 || request.Level > _authority.MaxPlayerLevel)
            return Failure(BotIdentityCreationStatus.InvalidLevel, $"level_out_of_range_1_{_authority.MaxPlayerLevel}");

        var level = (byte)request.Level;
        if (!_archetypes.TryResolveCreationPlan(request.Archetype, level, out var plan))
            return Failure(BotIdentityCreationStatus.InvalidArchetype, "archetype_unsupported");

        lock (_gate)
            return CreateCore(request, level, plan);
    }

    private BotIdentityCreationResult CreateCore(
        BotIdentityCreationRequest request,
        byte level,
        BotArchetypeCreationPlan plan)
    {
        var authorityResult = _authority.CreateServerOwnedBot(new BotIdentityAuthorityRequest(
            _options.ServerOwnedAccountId,
            request.Name,
            request.Race,
            request.Gender,
            level,
            plan.Ability1,
            plan.Ability2,
            plan.Ability3,
            request.Placement));

        if (authorityResult == null)
            return Failure(BotIdentityCreationStatus.Failed, "host_result_missing");
        if (authorityResult.Status != BotIdentityAuthorityStatus.Created || authorityResult.Character == null)
            return MapAuthorityFailure(authorityResult);

        var created = authorityResult.Character;
        var archetypeAttempted = false;
        var rosterAttempted = false;
        var stage = "archetype";

        try
        {
            archetypeAttempted = true;
            _archetypes.RegisterCreationPlan(created.Id, plan);

            stage = "roster";
            rosterAttempted = true;
            _roster.Create(new BotRosterEntry(
                new BotIdentity(created.Id),
                enabled: true,
                profile: plan.Name,
                homeZoneId: created.Transform.ZoneId));

            stage = "admission";
            var admission = _admit(created.Id, out var admitted);
            if (admission != SpawnResult.Ok || admitted == null)
            {
                return Rollback(created.Id, archetypeAttempted, rosterAttempted,
                    BotIdentityCreationStatus.AdmissionFailed,
                    $"admission_{admission.ToString().ToLowerInvariant()}");
            }

            var completionReason = "created_and_admitted";
            try
            {
                _authority.CompleteServerOwnedBotCreation(_options.ServerOwnedAccountId, created.Id);
            }
            catch
            {
                // Admission succeeded, so keep the live identity and its process-local guard.
                completionReason = "created_and_admitted_rollback_guard_retained";
            }

            return new BotIdentityCreationResult(
                BotIdentityCreationStatus.CreatedAndAdmitted,
                completionReason,
                admitted.Id,
                admitted);
        }
        catch (Exception exception)
        {
            var status = stage switch
            {
                "archetype" => BotIdentityCreationStatus.ArchetypeRegistrationFailed,
                "roster" => BotIdentityCreationStatus.RosterRegistrationFailed,
                _ => BotIdentityCreationStatus.AdmissionFailed
            };
            var reason = $"{stage}_failed";
            return Rollback(created.Id, archetypeAttempted, rosterAttempted, status,
                $"{reason}:{exception.GetType().Name}");
        }
    }

    private BotIdentityCreationResult Rollback(
        uint characterId,
        bool archetypeRegistered,
        bool rosterRegistered,
        BotIdentityCreationStatus originalStatus,
        string reason)
    {
        var rollbackOk = true;
        try
        {
            if (rosterRegistered)
                rollbackOk &= _roster.RemoveForCreationRollback(new BotIdentity(characterId));
        }
        catch
        {
            rollbackOk = false;
        }

        try
        {
            if (archetypeRegistered)
                _archetypes.RollbackCreationPlan(characterId);
        }
        catch
        {
            rollbackOk = false;
        }

        try
        {
            rollbackOk &= _authority.RollbackServerOwnedBot(_options.ServerOwnedAccountId, characterId);
        }
        catch
        {
            rollbackOk = false;
        }

        return rollbackOk
            ? new BotIdentityCreationResult(originalStatus, reason)
            : new BotIdentityCreationResult(BotIdentityCreationStatus.RollbackFailed, $"{reason};rollback_failed", characterId);
    }

    private static BotIdentityCreationResult MapAuthorityFailure(BotIdentityAuthorityResult result)
    {
        var status = result.Status switch
        {
            BotIdentityAuthorityStatus.AccountUnavailable => BotIdentityCreationStatus.AccountUnavailable,
            BotIdentityAuthorityStatus.AccountOnline => BotIdentityCreationStatus.AccountOnline,
            BotIdentityAuthorityStatus.InvalidName => BotIdentityCreationStatus.InvalidName,
            BotIdentityAuthorityStatus.NameUnavailable => BotIdentityCreationStatus.DuplicateName,
            BotIdentityAuthorityStatus.TemplateUnavailable => BotIdentityCreationStatus.TemplateUnavailable,
            BotIdentityAuthorityStatus.IdUnavailable => BotIdentityCreationStatus.IdUnavailable,
            BotIdentityAuthorityStatus.PlacementUnavailable => BotIdentityCreationStatus.InvalidPlacement,
            BotIdentityAuthorityStatus.PersistenceFailed => BotIdentityCreationStatus.PersistenceFailed,
            _ => BotIdentityCreationStatus.Failed
        };
        return Failure(status, string.IsNullOrWhiteSpace(result.Reason) ? "host_creation_failed" : result.Reason);
    }

    private static BotIdentityCreationResult Failure(BotIdentityCreationStatus status, string reason) =>
        new(status, reason);
}
