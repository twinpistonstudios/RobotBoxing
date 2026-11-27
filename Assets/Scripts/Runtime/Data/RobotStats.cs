using System;
using UnityEngine;

namespace IronKnuckle.Data
{
    [Serializable]
    public struct RobotStats
    {
        public float maxHp;
        public float attack;
        public float speed;
        public float defense;
        public float stamina;
        [Range(0f, 0.75f)] public float critChance;
        public float overdriveGain;

        public RobotStats ScaledForLevel(int level)
        {
            int clamped = Mathf.Clamp(level, 1, 5);
            float mult = 1f + 0.14f * (clamped - 1);
            return new RobotStats
            {
                maxHp = Mathf.Round(maxHp * mult),
                attack = Mathf.Round(attack * mult),
                speed = Mathf.Round(speed * (1f + 0.08f * (clamped - 1))),
                defense = Mathf.Round(defense * mult),
                stamina = Mathf.Round(stamina * (1f + 0.10f * (clamped - 1))),
                critChance = Mathf.Clamp(critChance + 0.015f * (clamped - 1), 0f, 0.65f),
                overdriveGain = overdriveGain * (1f + 0.08f * (clamped - 1))
            };
        }

        public static RobotStats operator +(RobotStats a, RobotStats b)
        {
            return new RobotStats
            {
                maxHp = a.maxHp + b.maxHp,
                attack = a.attack + b.attack,
                speed = a.speed + b.speed,
                defense = a.defense + b.defense,
                stamina = a.stamina + b.stamina,
                critChance = Mathf.Clamp(a.critChance + b.critChance, 0f, 0.75f),
                overdriveGain = a.overdriveGain + b.overdriveGain
            };
        }

        public int ComputePowerRating()
        {
            float score = maxHp * 0.45f + attack * 3.1f + speed * 2.2f + defense * 2.4f + stamina * 1.1f + critChance * 250f + overdriveGain * 1.5f;
            return Mathf.RoundToInt(score);
        }
    }
}
