using System.Collections.Generic;
using UnityEngine;

namespace IronKnuckle.Data
{
    [CreateAssetMenu(menuName = "IronKnuckle/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public List<RobotPartData> parts = new List<RobotPartData>();
        public List<RobotPresetData> presets = new List<RobotPresetData>();
        public List<CombatAbilityData> abilities = new List<CombatAbilityData>();
        public List<ColorSchemeData> colorSchemes = new List<ColorSchemeData>();
        public List<OpponentData> opponents = new List<OpponentData>();

        [Header("Shared Robot Meshes & Materials")]
        public Mesh visorScreenMesh;
        public Mesh pelvisMesh;
        public Mesh shoulderPadMesh;
        public Mesh upperArmMesh;
        public Material robotBaseMaterial;
        public Material gunmetalBaseMaterial;
        public Material emissiveBaseMaterial;
        public Material visorBaseMaterial;

        [Header("Surface Finishes & Normal Map")]
        public Texture2D armorNormalMap;
        public Texture2D[] surfaceFinishTextures = new Texture2D[5];

        public static readonly string[] LeagueNames =
        {
            "Scrapyard Underground",
            "Neon Street Circuit",
            "Titan Foundry",
            "Apex World Championship"
        };

        public static readonly string[] FinishNames =
        {
            "Painted Matte",
            "Brushed Chrome",
            "Carbon Fiber",
            "Hazard Industrial",
            "Neon Circuit"
        };

        public static readonly int[] FinishCosts = { 0, 140, 200, 180, 280 };

        public static string GetBrandDisplayName(RobotBrand brand)
        {
            switch (brand)
            {
                case RobotBrand.Scrapworks: return "SCRAPWORKS";
                case RobotBrand.VoltTech: return "VOLT-TECH";
                case RobotBrand.TitanForge: return "TITANFORGE";
                case RobotBrand.MantisLabs: return "MANTIS LABS";
                case RobotBrand.PyroCore: return "PYROCORE";
                case RobotBrand.ApexCorp: return "APEX CORP";
                default: return brand.ToString().ToUpperInvariant();
            }
        }

        public static string GetBrandSetSummary(RobotBrand brand, int count)
        {
            string bonus;
            switch (brand)
            {
                case RobotBrand.Scrapworks: bonus = "+25 HP / +5 DEF (3pc)  |  +60 HP / +12 DEF (6pc)"; break;
                case RobotBrand.VoltTech: bonus = "+10 SPD / +12 OD (3pc)  |  +22 SPD / +26 OD (6pc)"; break;
                case RobotBrand.TitanForge: bonus = "+14 ATK / +10 DEF (3pc)  |  +32 ATK / +24 DEF (6pc)"; break;
                case RobotBrand.MantisLabs: bonus = "+12 SPD / +6% CRIT (3pc)  |  +24 SPD / +14% CRIT (6pc)"; break;
                case RobotBrand.PyroCore: bonus = "+18 ATK / +20 STM (3pc)  |  +40 ATK / +45 STM (6pc)"; break;
                case RobotBrand.ApexCorp: bonus = "+40 HP / +18 ATK (3pc)  |  +90 HP / +40 ATK / +15 SPD (6pc)"; break;
                default: bonus = ""; break;
            }
            return $"{GetBrandDisplayName(brand)} ({count}/6): {bonus}";
        }

        public Texture2D GetFinishTexture(int index)
        {
            if (surfaceFinishTextures == null || surfaceFinishTextures.Length == 0) return null;
            int idx = Mathf.Clamp(index, 0, surfaceFinishTextures.Length - 1);
            return surfaceFinishTextures[idx];
        }

        public RobotPartData GetPart(string id)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null && parts[i].id == id) return parts[i];
            }
            return null;
        }

        public List<RobotPartData> GetPartsBySlot(PartSlot slot)
        {
            var list = new List<RobotPartData>();
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null && parts[i].slot == slot) list.Add(parts[i]);
            }
            return list;
        }

        public RobotPresetData GetPreset(string id)
        {
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i] != null && presets[i].id == id) return presets[i];
            }
            return null;
        }

        public CombatAbilityData GetAbility(string id)
        {
            for (int i = 0; i < abilities.Count; i++)
            {
                if (abilities[i] != null && abilities[i].id == id) return abilities[i];
            }
            return null;
        }

        public ColorSchemeData GetColorScheme(string id)
        {
            for (int i = 0; i < colorSchemes.Count; i++)
            {
                if (colorSchemes[i] != null && colorSchemes[i].id == id) return colorSchemes[i];
            }
            return colorSchemes.Count > 0 ? colorSchemes[0] : null;
        }

        public OpponentData GetOpponent(string id)
        {
            for (int i = 0; i < opponents.Count; i++)
            {
                if (opponents[i] != null && opponents[i].id == id) return opponents[i];
            }
            return null;
        }

        public List<OpponentData> GetOpponentsInLeague(int leagueIndex)
        {
            var list = new List<OpponentData>();
            for (int i = 0; i < opponents.Count; i++)
            {
                if (opponents[i] != null && opponents[i].leagueIndex == leagueIndex) list.Add(opponents[i]);
            }
            list.Sort((a, b) => a.matchIndex.CompareTo(b.matchIndex));
            return list;
        }
    }
}
