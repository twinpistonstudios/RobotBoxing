using UnityEngine;

namespace IronKnuckle.Data
{
    [CreateAssetMenu(menuName = "IronKnuckle/Robot Preset Data", fileName = "Preset_")]
    public class RobotPresetData : ScriptableObject
    {
        public string id;
        public string displayName;
        public string tagline;
        public string archetype;
        public int buyCostScrap = 350;
        public bool isStarter;
        public string headPartId;
        public string torsoPartId;
        public string leftArmPartId;
        public string rightArmPartId;
        public string legsPartId;
        public string corePartId;
        public string defaultColorSchemeId;

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
