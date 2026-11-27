using System;
using System.IO;
using IronKnuckle.Data;
using UnityEngine;

namespace IronKnuckle.Core
{
    public static class SaveSystem
    {
        private const string SaveFileName = "iron_knuckle_save_v2.json";

        public static string DefaultSavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        public static PlayerSaveData CreateDefaultSave()
        {
            var data = new PlayerSaveData
            {
                scrapCredits = 350,
                gearShards = 15,
                equippedHeadId = "head_classic",
                equippedTorsoId = "torso_scrap",
                equippedLeftArmId = "larm_riveted",
                equippedRightArmId = "rarm_riveted",
                equippedLegsId = "legs_spring",
                equippedCoreId = "core_battery",
                equippedColorId = "color_cobalt",
                equippedFinishIndex = 0,
                equippedSpecial1Id = "ability_rocket_upper",
                equippedSpecial2Id = "",
                equippedPassiveId = ""
            };

            string[] starterParts =
            {
                "head_classic", "torso_scrap", "larm_riveted",
                "rarm_riveted", "legs_spring", "core_battery"
            };
            foreach (var p in starterParts)
            {
                data.ownedPartIds.Add(p);
                data.SetPartLevel(p, 1);
            }

            data.ownedPresetIds.Add("preset_rusty_jab");
            data.ownedAbilityIds.Add("ability_rocket_upper");
            data.ownedColorIds.Add("color_cobalt");
            data.ownedColorIds.Add("color_crimson");
            data.ownedFinishIndices.Add(0);

            for (int i = 0; i < 3; i++)
            {
                data.savedLoadouts.Add(new CustomLoadoutSlot { slotName = $"BUILD {i + 1}" });
            }

            return data;
        }

        public static void Save(PlayerSaveData data, string pathOverride = null)
        {
            if (data == null) return;
            string path = string.IsNullOrEmpty(pathOverride) ? DefaultSavePath : pathOverride;
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonUtility.ToJson(data, true);
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
        }

        public static PlayerSaveData Load(string pathOverride = null)
        {
            string path = string.IsNullOrEmpty(pathOverride) ? DefaultSavePath : pathOverride;
            if (!File.Exists(path)) return CreateDefaultSave();

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return CreateDefaultSave();
                var loaded = JsonUtility.FromJson<PlayerSaveData>(json);
                if (loaded == null || loaded.ownedPartIds == null || loaded.ownedPartIds.Count == 0)
                {
                    return CreateDefaultSave();
                }
                if (loaded.ownedFinishIndices == null || loaded.ownedFinishIndices.Count == 0)
                {
                    loaded.ownedFinishIndices = new System.Collections.Generic.List<int> { 0 };
                }
                if (loaded.savedLoadouts == null || loaded.savedLoadouts.Count < 3)
                {
                    loaded.savedLoadouts = new System.Collections.Generic.List<CustomLoadoutSlot>();
                    for (int i = 0; i < 3; i++) loaded.savedLoadouts.Add(new CustomLoadoutSlot { slotName = $"BUILD {i + 1}" });
                }
                return loaded;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SaveSystem failed to load save file, resetting to defaults: {e.Message}");
                return CreateDefaultSave();
            }
        }
    }
}
