using UnityEngine;

namespace IronKnuckle.Data
{
    [CreateAssetMenu(menuName = "IronKnuckle/Color Scheme Data", fileName = "Color_")]
    public class ColorSchemeData : ScriptableObject
    {
        public string id;
        public string displayName;
        public Color primaryColor = new Color(0.2f, 0.55f, 0.95f, 1f);
        public Color secondaryColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        public Color accentColor = new Color(1.0f, 0.65f, 0.1f, 1f);
        public Color visorColor = new Color(0.15f, 0.92f, 1.0f, 1f);
        public int unlockCostScrap = 80;
    }
}
