#if PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Connections;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Char.Templates;
using AAEmu.Game.Models.Game.Team;

namespace AAEmu.Game.Core.Managers
{
    public interface ILoadable
    {
        void Load();
    }

    public interface IExperienceManager
    {
        int GetExpForLevel(byte level, bool mate = false);
    }

    public interface IDuelManager
    {
        void DuelRequest(Character challenger, uint challengedId);
        void DuelCancel(uint challengerId, ErrorMessageType errorMessage);
    }

    public interface ITeamManager
    {
        Team GetActiveTeamByUnit(uint unitId);
        void SetTeamMemberRole(Character unit, uint teamId, uint memberId, MemberRole role);
    }

    public interface ITickManager
    {
        ServerTickMetrics Metrics { get; }
    }
}

namespace AAEmu.Game.Core.Managers.Id
{
    public interface IObjectIdManager
    {
        uint GetNextId();
    }
}

namespace AAEmu.Game.Core.Managers.UnitManagers
{
    public interface ICharacterManager
    {
        CharacterTemplate GetTemplate(Race race, Gender gender);
    }
}

namespace AAEmu.Game.Core.Managers.World
{
    public enum LeaveWorldTargetType : byte
    {
        CharacterSelect = 1,
        ServerSelect = 2
    }

    public interface IEnterWorldManager
    {
        void LeaveWorldTask(GameConnection connection, LeaveWorldTargetType leaveWorldTarget, Character activeChar);
    }
}
#endif
