using System;
using System.Numerics;
using AAEmu.Game.Bots.Body;
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
                var dest = ComputeChaseDestination(
                    bot.Transform.World.Position,
                    target.Transform.World.Position,
                    meleeRange,
                    bot.ParentWorld == null ? null : bot.ParentWorld.GetHeight);
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

            UseLearnedSkill(bot, state, target);

            return true;
        }

        internal static Vector3 ComputeChaseDestination(
            Vector3 botPosition,
            Vector3 targetPosition,
            float meleeRange,
            Func<float, float, float> groundHeight = null)
        {
            var offset = new Vector2(targetPosition.X - botPosition.X, targetPosition.Y - botPosition.Y);
            var planarDistance = offset.Length();
            if (!float.IsFinite(planarDistance) || planarDistance <= 0.0001f)
                return botPosition;

            var direction = offset / planarDistance;
            var stopDistance = MathF.Min(MathF.Max(0f, meleeRange), planarDistance);
            var destination = new Vector3(
                targetPosition.X - direction.X * stopDistance,
                targetPosition.Y - direction.Y * stopDistance,
                botPosition.Z);

            if (groundHeight == null)
                return destination;

            try
            {
                var surfaceZ = groundHeight(destination.X, destination.Y);
                if (float.IsFinite(surfaceZ) && surfaceZ > 0f)
                    destination.Z = surfaceZ;
            }
            catch
            {
                // The navigation boundary remains the final authority when height data is unavailable.
            }

            return destination;
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
                bot.CurrentTarget = target;
                bot.IsAutoAttack = true;
                bot.StartAutoSkill(skill);
                // ---- CRITICAL FIX: Set IsInBattle on both parties ----
                // This ensures regen drops to PersistentHpRegen (low) instead of out-of-combat HpRegen (high).
                bot.IsInBattle = true;
                target.IsInBattle = true;
            }
        }

        private static void UseLearnedSkill(Character bot, BotCombatState state, Unit target)
        {
            var now = DateTime.UtcNow;
            var template = LearnedSkillDecision.Select(bot, target, state, now, BotConfig.Instance);
            if (template == null)
                return;

            var skill = new Skill(template, bot);
            var caster = new SkillCasterUnit(bot.ObjId);
            var skillTarget = new SkillCastUnitTarget(target.ObjId);
            if (skill.Use(bot, caster, skillTarget, null, false, out _) != SkillResult.Success)
                return;

            state.LastSkillTime = now;
            bot.IsInBattle = true;
            target.IsInBattle = true;
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
