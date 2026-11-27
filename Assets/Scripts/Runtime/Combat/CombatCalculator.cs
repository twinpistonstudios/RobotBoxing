using IronKnuckle.Data;
using UnityEngine;

namespace IronKnuckle.Combat
{
    public struct HitResolution
    {
        public float finalDamage;
        public float guardDamage;
        public bool wasBlocked;
        public bool wasParried;
        public bool wasDodged;
        public bool wasCrit;
        public bool wasCounter;
        public bool brokeGuard;
    }

    public static class CombatCalculator
    {
        public static float GetPunchBaseMultiplier(PunchType punch, CombatAbilityData ability = null)
        {
            if ((punch == PunchType.Special1 || punch == PunchType.Special2) && ability != null)
            {
                return Mathf.Max(1.2f, ability.damageMultiplier);
            }

            switch (punch)
            {
                case PunchType.LeftJab: return 0.85f;
                case PunchType.RightCross: return 1.15f;
                case PunchType.LeftHook: return 1.35f;
                case PunchType.RightHook: return 1.40f;
                case PunchType.Uppercut: return 1.70f;
                case PunchType.Special1: return 2.15f;
                case PunchType.Special2: return 2.35f;
                default: return 1.0f;
            }
        }

        public static float GetPunchStaminaCost(PunchType punch)
        {
            switch (punch)
            {
                case PunchType.LeftJab: return 8f;
                case PunchType.RightCross: return 12f;
                case PunchType.LeftHook: return 16f;
                case PunchType.RightHook: return 16f;
                case PunchType.Uppercut: return 24f;
                case PunchType.Special1:
                case PunchType.Special2: return 0f; // Uses Overdrive gauge
                default: return 10f;
            }
        }

        public static float GetWindupDuration(PunchType punch, float attackerSpeed)
        {
            float speedFactor = Mathf.Clamp(1.15f - attackerSpeed * 0.0045f, 0.58f, 1.15f);
            float baseTime;
            switch (punch)
            {
                case PunchType.LeftJab: baseTime = 0.16f; break;
                case PunchType.RightCross: baseTime = 0.24f; break;
                case PunchType.LeftHook:
                case PunchType.RightHook: baseTime = 0.34f; break;
                case PunchType.Uppercut: baseTime = 0.44f; break;
                case PunchType.Special1:
                case PunchType.Special2: baseTime = 0.48f; break;
                default: baseTime = 0.25f; break;
            }
            return baseTime * speedFactor;
        }

        public static bool CanDodgePunch(PunchType punch, FighterState defenderState)
        {
            if (defenderState != FighterState.SlipLeft && defenderState != FighterState.SlipRight)
                return false;

            // Straight punches & uppercuts can be slipped either way; hooks are best slipped opposite, but generous mobile timing allows slip either side except special AOEs
            if (punch == PunchType.Special2) return false;
            return true;
        }

        public static HitResolution ResolveHit(
            RobotStats attacker,
            RobotStats defender,
            PunchType punch,
            FighterState defenderState,
            bool defenderPerfectParryWindow,
            bool attackerCounterWindow,
            int attackerCombo,
            float defenderGuardStability,
            float passiveCounterBonus,
            float critRoll01,
            CombatAbilityData specialAbility = null)
        {
            var res = new HitResolution();

            if (CanDodgePunch(punch, defenderState))
            {
                res.wasDodged = true;
                return res;
            }

            if (defenderState == FighterState.Guarding && defenderPerfectParryWindow)
            {
                res.wasParried = true;
                return res;
            }

            float punchMult = GetPunchBaseMultiplier(punch, specialAbility);
            float comboMult = 1f + Mathf.Min(attackerCombo, 6) * 0.08f;
            float counterMult = attackerCounterWindow ? (1.65f + passiveCounterBonus) : 1f;
            bool isCrit = critRoll01 < Mathf.Clamp(attacker.critChance + (attackerCounterWindow ? 0.20f : 0f), 0f, 0.90f);
            float critMult = isCrit ? 1.50f : 1f;

            // Defense mitigation curve
            float defMitigation = 100f / (100f + Mathf.Max(0f, defender.defense) * 1.4f);
            float raw = Mathf.Max(4f, attacker.attack * punchMult * comboMult * counterMult * critMult * defMitigation);

            res.wasCrit = isCrit;
            res.wasCounter = attackerCounterWindow;

            if (defenderState == FighterState.Guarding)
            {
                res.wasBlocked = true;
                float guardBreakMult = 1f + (specialAbility != null ? specialAbility.guardBreakBonus : 0f);
                float guardDmg = (18f + punchMult * 14f) * guardBreakMult;
                res.guardDamage = guardDmg;

                if (defenderGuardStability - guardDmg <= 0f)
                {
                    res.brokeGuard = true;
                    res.finalDamage = Mathf.Round(raw * 0.65f);
                }
                else
                {
                    res.finalDamage = Mathf.Max(1f, Mathf.Round(raw * 0.24f));
                }
            }
            else
            {
                float stunnedBonus = (defenderState == FighterState.Stunned || defenderState == FighterState.GuardBroken) ? 1.25f : 1f;
                res.finalDamage = Mathf.Max(2f, Mathf.Round(raw * stunnedBonus));
            }

            return res;
        }
    }
}
