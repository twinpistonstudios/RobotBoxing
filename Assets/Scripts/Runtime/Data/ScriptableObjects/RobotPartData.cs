using UnityEngine;

namespace IronKnuckle.Data
{
    [CreateAssetMenu(menuName = "IronKnuckle/Robot Part Data", fileName = "Part_")]
    public class RobotPartData : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public PartSlot slot;
        public PartRarity rarity;
        public RobotBrand brand;
        public Mesh mesh;
        public RobotStats baseStats;
        public int buyCostScrap = 100;
        public int upgradeBaseScrap = 80;
        public int upgradeBaseShards = 5;
        public Vector3 meshScale = Vector3.one;

        public int GetUpgradeScrapCost(int currentLevel)
        {
            return Mathf.RoundToInt(upgradeBaseScrap * Mathf.Pow(1.55f, Mathf.Max(0, currentLevel - 1)));
        }

        public int GetUpgradeShardsCost(int currentLevel)
        {
            return Mathf.RoundToInt(upgradeBaseShards * Mathf.Pow(1.45f, Mathf.Max(0, currentLevel - 1)));
        }
    }
}
