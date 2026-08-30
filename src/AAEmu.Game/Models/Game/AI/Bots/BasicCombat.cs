using System;
using System.Numerics;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.AI.Bots
{
    /// <summary>
    /// Fallback combat handler for bots that have no archetype or no specific implementation.
    /// Uses simple melee auto‑attack at melee range.
    /// For ranged (Primeval) we will have a separate handler later.
    /// </summary>
    public static class BasicCombat
    {
        public static bool Execute(Character bot, BotCombatState state, Unit target)
        {
            if (bot == null || target == null || state == null)
                return false;

            // Check if we are in melee range
            float dist = (float)bot.GetDistanceTo(target, true);
            float meleeRange = (float)BotConfig.Instance.AttackRange;

            // If too far, move closer
            if (dist > meleeRange)
            {
                var direction = target.Transform.World.Position - bot.Transform.World.Position;
                var dest = bot.Transform.World.Position + Vector3.Normalize(direction) * (dist - meleeRange);
                BotManager.Instance.SetBotDestinationIfChanged(bot, dest, run: true, tolerance: float.MaxValue);
                return true;
            }

            // Stop moving if we were moving
            BotManager.Instance.StopIfMoving(bot);

            // Face target
            if (state.LastFacingAngle == float.MinValue || Math.Abs(state.LastFacingAngle - (float)MathUtil.CalculateAngleFrom(bot.Transform.World.Position, target.Transform.World.Position)) > 0.01f)
            {
                FaceTarget(bot, target);
                state.LastFacingAngle = (float)MathUtil.CalculateAngleFrom(bot.Transform.World.Position, target.Transform.World.Position);
            }

            // Start auto‑attack if not already
            if (!bot.IsAutoAttack)
                StartAutoAttack(bot, target);

            return true;
        }

        private static void StartAutoAttack(Character bot, Unit target)
        {
            uint skillId = 2; // melee auto‑attack
            var template = SkillManager.Instance.GetSkillTemplate(skillId);
            if (template == null) return;
            var skill = new Skill(template);
            var caster = new SkillCasterUnit(bot.ObjId);
            var targetCaster = new SkillCastUnitTarget(target.ObjId);
            var result = skill.Use(bot, caster, targetCaster, null, false, out _);

            if (result == SkillResult.Success)
            {
                // ---- CRITICAL FIX: Set IsInBattle on both parties ----
                // This ensures regen drops to PersistentHpRegen (low) instead of out-of-combat HpRegen (high).
                bot.IsInBattle = true;
                target.IsInBattle = true;
            }
        }

        private static void FaceTarget(Character bot, Unit target)
        {
            var angle = MathUtil.CalculateAngleFrom(bot.Transform.World.Position, target.Transform.World.Position);
            var pos = bot.Transform.World.Position;
            BotManager.Instance.GetBroadcaster(bot.Id)?.SendFaceTarget(pos, (float)angle - 90, bot.IsInBattle);
            bot.Transform.FinalizeTransform();
        }
    }
}
