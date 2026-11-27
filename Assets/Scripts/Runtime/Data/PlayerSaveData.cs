using System;
using System.Collections.Generic;

namespace IronKnuckle.Data
{
    [Serializable]
    public class StringIntPair
    {
        public string key;
        public int value;
    }

    [Serializable]
    public class CustomLoadoutSlot
    {
        public string slotName = "BUILD 1";
        public string headId = "head_classic";
        public string torsoId = "torso_scrap";
        public string leftArmId = "larm_riveted";
        public string rightArmId = "rarm_riveted";
        public string legsId = "legs_spring";
        public string coreId = "core_battery";
        public string colorId = "color_cobalt";
        public int finishIndex = 0;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public int scrapCredits = 350;
        public int gearShards = 15;

        public List<string> ownedPartIds = new List<string>();
        public List<StringIntPair> partLevels = new List<StringIntPair>();
        public List<string> ownedPresetIds = new List<string>();
        public List<string> ownedAbilityIds = new List<string>();
        public List<string> ownedColorIds = new List<string>();
        public List<int> ownedFinishIndices = new List<int> { 0 };

        public string equippedHeadId = "head_classic";
        public string equippedTorsoId = "torso_scrap";
        public string equippedLeftArmId = "larm_riveted";
        public string equippedRightArmId = "rarm_riveted";
        public string equippedLegsId = "legs_spring";
        public string equippedCoreId = "core_battery";
        public string equippedColorId = "color_cobalt";
        public int equippedFinishIndex = 0;

        public List<CustomLoadoutSlot> savedLoadouts = new List<CustomLoadoutSlot>();
        public int activeLoadoutIndex = 0;

        public string equippedSpecial1Id = "ability_rocket_upper";
        public string equippedSpecial2Id = "";
        public string equippedPassiveId = "";

        public List<string> completedOpponentIds = new List<string>();
        public List<StringIntPair> opponentStars = new List<StringIntPair>();
        public int winStreak;
        public int totalWins;
        public int totalKOs;

        public int dailyMatchesPlayed;
        public int dailyKOsScored;
        public bool dailyMissionClaimed;
        public bool tutorialCompleted;

        public float sfxVolume = 0.85f;
        public float bgmVolume = 0.55f;

        // Graphics Settings
        public int graphicsQuality = 2; // 0=Low, 1=Medium, 2=High, 3=Ultra
        public float renderScale = 1.0f;
        public bool postProcessingEnabled = true;
        public bool bloomEnabled = true;
        public bool outlinesEnabled = true;
        public bool shadowsEnabled = true;
        public int targetFps = 60;
        public bool showFpsCounter = false;

        public string GetEquippedPartId(PartSlot slot)
        {
            switch (slot)
            {
                case PartSlot.Head: return equippedHeadId;
                case PartSlot.Torso: return equippedTorsoId;
                case PartSlot.LeftArm: return equippedLeftArmId;
                case PartSlot.RightArm: return equippedRightArmId;
                case PartSlot.Legs: return equippedLegsId;
                case PartSlot.Core: return equippedCoreId;
                default: return equippedHeadId;
            }
        }

        public void SetEquippedPartId(PartSlot slot, string partId)
        {
            switch (slot)
            {
                case PartSlot.Head: equippedHeadId = partId; break;
                case PartSlot.Torso: equippedTorsoId = partId; break;
                case PartSlot.LeftArm: equippedLeftArmId = partId; break;
                case PartSlot.RightArm: equippedRightArmId = partId; break;
                case PartSlot.Legs: equippedLegsId = partId; break;
                case PartSlot.Core: equippedCoreId = partId; break;
            }
        }

        public int GetPartLevel(string partId)
        {
            for (int i = 0; i < partLevels.Count; i++)
            {
                if (partLevels[i].key == partId) return partLevels[i].value < 1 ? 1 : partLevels[i].value;
            }
            return 1;
        }

        public void SetPartLevel(string partId, int level)
        {
            for (int i = 0; i < partLevels.Count; i++)
            {
                if (partLevels[i].key == partId)
                {
                    partLevels[i].value = level;
                    return;
                }
            }
            partLevels.Add(new StringIntPair { key = partId, value = level });
        }

        public int GetOpponentStars(string opponentId)
        {
            for (int i = 0; i < opponentStars.Count; i++)
            {
                if (opponentStars[i].key == opponentId) return opponentStars[i].value;
            }
            return 0;
        }

        public void SetOpponentStars(string opponentId, int stars)
        {
            for (int i = 0; i < opponentStars.Count; i++)
            {
                if (opponentStars[i].key == opponentId)
                {
                    if (stars > opponentStars[i].value) opponentStars[i].value = stars;
                    return;
                }
            }
            opponentStars.Add(new StringIntPair { key = opponentId, value = stars });
        }
    }
}
