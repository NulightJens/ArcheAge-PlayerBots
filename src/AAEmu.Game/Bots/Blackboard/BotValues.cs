using System.Collections.Generic;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Bots.Blackboard;

public static class BotValues
{
    public static readonly ValueKey<List<uint>> NearbyNpcIds = new("nearby_npc_ids");
    public static readonly ValueKey<List<uint>> NearbyHostileNpcIds = new("nearby_hostile_npc_ids");
    public static readonly ValueKey<List<uint>> AttackerIds = new("attacker_ids");
    public static readonly ValueKey<float> NearestRealPlayerDistance = new("nearest_real_player_distance");
    public static readonly ValueKey<float> TargetFacingDelta = new("target_facing_delta");
    public static readonly ValueKey<List<AreaTrigger>> HostileAreaTriggersNearby = new("hostile_area_triggers_nearby");
}
