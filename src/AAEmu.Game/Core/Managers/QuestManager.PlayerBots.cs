using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Quests.Templates;

namespace AAEmu.Game.Core.Managers;

/// <summary>
/// Read-only quest indexes used by PlayerBots. The indexes are built once, on
/// first use after AAEmu has loaded its quest templates, rather than rescanning
/// every quest during each bot brain tick.
/// </summary>
public partial class QuestManager
{
    private readonly object _playerBotNpcQuestIndexLock = new();
    private Dictionary<uint, QuestTemplate[]> _playerBotNpcQuestStartIndex;
    private Dictionary<uint, QuestTemplate[]> _playerBotNpcQuestReportIndex;

    public IReadOnlyList<QuestTemplate> GetPlayerBotNpcQuestStarts(uint npcTemplateId)
    {
        EnsurePlayerBotNpcQuestIndexes();
        return _playerBotNpcQuestStartIndex?.GetValueOrDefault(npcTemplateId) ?? [];
    }

    public IReadOnlyList<QuestTemplate> GetPlayerBotNpcQuestReports(uint npcTemplateId)
    {
        EnsurePlayerBotNpcQuestIndexes();
        return _playerBotNpcQuestReportIndex?.GetValueOrDefault(npcTemplateId) ?? [];
    }

    private void EnsurePlayerBotNpcQuestIndexes()
    {
        if (_playerBotNpcQuestStartIndex != null)
            return;

        lock (_playerBotNpcQuestIndexLock)
        {
            if (_playerBotNpcQuestStartIndex != null || !_loaded)
                return;

            var starts = new Dictionary<uint, Dictionary<uint, QuestTemplate>>();
            var reports = new Dictionary<uint, Dictionary<uint, QuestTemplate>>();

            foreach (var quest in _questTemplates.Values)
            {
                foreach (var component in quest.Components.Values)
                {
                    foreach (var act in component.ActTemplates)
                    {
                        if (component.KindId == QuestComponentKind.Start &&
                            TryGetExactNpcStarter(act, out var starterNpcId))
                            AddRelation(starts, starterNpcId, quest);
                        if (act is QuestActConReportNpc reportNpc)
                            AddRelation(reports, reportNpc.NpcId, quest);
                    }
                }
            }

            _playerBotNpcQuestStartIndex = Freeze(starts);
            _playerBotNpcQuestReportIndex = Freeze(reports);
        }
    }

    private static bool TryGetExactNpcStarter(QuestActTemplate act, out uint npcTemplateId)
    {
        npcTemplateId = act switch
        {
            QuestActConAcceptNpc accept => accept.NpcId,
            _ => 0
        };
        return npcTemplateId != 0;
    }

    private static void AddRelation(
        IDictionary<uint, Dictionary<uint, QuestTemplate>> index,
        uint npcTemplateId,
        QuestTemplate quest)
    {
        if (!index.TryGetValue(npcTemplateId, out var quests))
        {
            quests = new Dictionary<uint, QuestTemplate>();
            index.Add(npcTemplateId, quests);
        }

        quests.TryAdd(quest.Id, quest);
    }

    private static Dictionary<uint, QuestTemplate[]> Freeze(
        IDictionary<uint, Dictionary<uint, QuestTemplate>> source)
    {
        return source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Values.OrderBy(quest => quest.Id).ToArray());
    }
}
