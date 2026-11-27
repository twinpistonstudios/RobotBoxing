using UnityEngine;

namespace IronKnuckle.Data
{
    [CreateAssetMenu(menuName = "IronKnuckle/Opponent Data", fileName = "Opponent_")]
    public class OpponentData : ScriptableObject
    {
        public string id;
        public string fighterName;
        public string title;
        public int leagueIndex;
        public int matchIndex;
        public bool isBoss;
        public AIStyle aiStyle;
        [Range(0.1f, 1.0f)] public float aiDifficulty = 0.3f;

        [Header("Loadout")]
        public string headPartId;
        public string torsoPartId;
        public string leftArmPartId;
        public string rightArmPartId;
        public string legsPartId;
        public string corePartId;
        [Range(1, 5)] public int partLevel = 1;
        public string colorSchemeId;
        public string specialAbilityId;

        [Header("Rewards")]
        public int rewardScrap = 150;
        public int rewardShards = 8;
        [TextArea] public string quote = "Ready to scrap!";

        public string GetPartIdForSlot(PartSlot slot)
        {
            switch (slot)
            {
                case PartSlot.Head: return headPartId;
                case PartSlot.Torso: return torsoPartId;
                case PartSlot.LeftArm: return leftArmPartId;
                case PartSlot.RightArm: return rightArmPartId;
                case PartSlot.Legs: return legsPartId;
                case PartSlot.Core: return corePartId;
                default: return headPartId;
            }
        }
    }
}
