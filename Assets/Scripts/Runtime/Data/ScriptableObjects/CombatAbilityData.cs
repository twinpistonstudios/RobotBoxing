using UnityEngine;

namespace IronKnuckle.Data
{
    [CreateAssetMenu(menuName = "IronKnuckle/Combat Ability Data", fileName = "Ability_")]
    public class CombatAbilityData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public bool isPassive;
        public int unlockCostScrap = 200;
        public int unlockCostShards = 12;

        [Header("Active Special Parameters")]
        public float overdriveCost = 50f;
        public float damageMultiplier = 1.85f;
        public float guardBreakBonus = 0.5f;
        public float stunDuration = 0.7f;
        public int hitCount = 1;
        public Color accentColor = new Color(1f, 0.55f, 0.1f, 1f);

        [Header("Passive Mod Parameters")]
        public float passiveHpBonus;
        public float passiveStaminaRegenBonus;
        public float passiveCounterDamageBonus;
        public float passiveOverdriveBonus;
    }
}
