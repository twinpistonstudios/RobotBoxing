using System;
using System.Collections.Generic;
using IronKnuckle.Data;
using UnityEngine;

namespace IronKnuckle.Core
{
    public class ProgressionService
    {
        public const int MaxPartLevel = 5;

        public GameDatabase Database { get; }
        public PlayerSaveData Save { get; private set; }

        public ProgressionService(GameDatabase database, PlayerSaveData save)
        {
            Database = database;
            Save = save ?? SaveSystem.CreateDefaultSave();
        }

        public RobotStats ComputePlayerTotalStats()
        {
            return ComputeStatsForLoadout(
                Save.equippedHeadId,
                Save.equippedTorsoId,
                Save.equippedLeftArmId,
                Save.equippedRightArmId,
                Save.equippedLegsId,
                Save.equippedCoreId,
                0,
                Save.equippedPassiveId);
        }

        public RobotStats ComputeStatsForLoadout(
            string headId, string torsoId, string lArmId, string rArmId, string legsId, string coreId,
            int levelOverride = 0, string passiveAbilityId = null)
        {
            string[] ids = { headId, torsoId, lArmId, rArmId, legsId, coreId };
            var total = new RobotStats();
            var brandCounts = new Dictionary<RobotBrand, int>();

            foreach (string id in ids)
            {
                var part = Database.GetPart(id);
                if (part != null)
                {
                    int lvl = levelOverride > 0 ? levelOverride : Save.GetPartLevel(id);
                    total += part.baseStats.ScaledForLevel(lvl);
                    if (!brandCounts.ContainsKey(part.brand)) brandCounts[part.brand] = 0;
                    brandCounts[part.brand]++;
                }
            }

            total += ComputeSetBonusStats(brandCounts);

            if (!string.IsNullOrEmpty(passiveAbilityId))
            {
                var passive = Database.GetAbility(passiveAbilityId);
                if (passive != null && passive.isPassive)
                {
                    total.maxHp += passive.passiveHpBonus;
                    total.overdriveGain += passive.passiveOverdriveBonus;
                }
            }
            return total;
        }

        public Dictionary<RobotBrand, int> ComputeBrandCounts(string headId, string torsoId, string lArmId, string rArmId, string legsId, string coreId)
        {
            string[] ids = { headId, torsoId, lArmId, rArmId, legsId, coreId };
            var counts = new Dictionary<RobotBrand, int>();
            foreach (string id in ids)
            {
                var part = Database.GetPart(id);
                if (part == null) continue;
                if (!counts.ContainsKey(part.brand)) counts[part.brand] = 0;
                counts[part.brand]++;
            }
            return counts;
        }

        public static RobotStats ComputeSetBonusStats(Dictionary<RobotBrand, int> brandCounts)
        {
            var bonus = new RobotStats();
            if (brandCounts == null) return bonus;

            foreach (var kv in brandCounts)
            {
                int count = kv.Value;
                if (count < 3) continue;
                bool fullSet = count >= 6;

                switch (kv.Key)
                {
                    case RobotBrand.Scrapworks:
                        bonus.maxHp += fullSet ? 60 : 25;
                        bonus.defense += fullSet ? 12 : 5;
                        break;
                    case RobotBrand.VoltTech:
                        bonus.speed += fullSet ? 22 : 10;
                        bonus.overdriveGain += fullSet ? 26 : 12;
                        break;
                    case RobotBrand.TitanForge:
                        bonus.attack += fullSet ? 32 : 14;
                        bonus.defense += fullSet ? 24 : 10;
                        break;
                    case RobotBrand.MantisLabs:
                        bonus.speed += fullSet ? 24 : 12;
                        bonus.critChance += fullSet ? 0.14f : 0.06f;
                        break;
                    case RobotBrand.PyroCore:
                        bonus.attack += fullSet ? 40 : 18;
                        bonus.stamina += fullSet ? 45 : 20;
                        break;
                    case RobotBrand.ApexCorp:
                        bonus.maxHp += fullSet ? 90 : 40;
                        bonus.attack += fullSet ? 40 : 18;
                        bonus.speed += fullSet ? 15 : 0;
                        break;
                }
            }
            return bonus;
        }

        public bool IsPartOwned(string partId) => Save.ownedPartIds.Contains(partId);
        public bool IsPresetOwned(string presetId) => Save.ownedPresetIds.Contains(presetId);
        public bool IsAbilityOwned(string abilityId) => Save.ownedAbilityIds.Contains(abilityId);
        public bool IsColorOwned(string colorId) => Save.ownedColorIds.Contains(colorId);
        public bool IsFinishOwned(int finishIndex) => Save.ownedFinishIndices != null && Save.ownedFinishIndices.Contains(finishIndex);

        public bool TryBuyPart(string partId, bool autoEquip = true)
        {
            var part = Database.GetPart(partId);
            if (part == null || IsPartOwned(partId)) return false;
            if (Save.scrapCredits < part.buyCostScrap) return false;

            Save.scrapCredits -= part.buyCostScrap;
            Save.ownedPartIds.Add(partId);
            Save.SetPartLevel(partId, 1);
            if (autoEquip) Save.SetEquippedPartId(part.slot, partId);
            return true;
        }

        public bool TryEquipPart(string partId)
        {
            var part = Database.GetPart(partId);
            if (part == null || !IsPartOwned(partId)) return false;
            Save.SetEquippedPartId(part.slot, partId);
            return true;
        }

        public bool TryUpgradePart(string partId)
        {
            var part = Database.GetPart(partId);
            if (part == null || !IsPartOwned(partId)) return false;
            int currentLvl = Save.GetPartLevel(partId);
            if (currentLvl >= MaxPartLevel) return false;

            int scrapCost = part.GetUpgradeScrapCost(currentLvl);
            int shardCost = part.GetUpgradeShardsCost(currentLvl);
            if (Save.scrapCredits < scrapCost || Save.gearShards < shardCost) return false;

            Save.scrapCredits -= scrapCost;
            Save.gearShards -= shardCost;
            Save.SetPartLevel(partId, currentLvl + 1);
            return true;
        }

        public bool TryBuyPreset(string presetId, bool autoEquip = true)
        {
            var preset = Database.GetPreset(presetId);
            if (preset == null || IsPresetOwned(presetId)) return false;
            if (Save.scrapCredits < preset.buyCostScrap) return false;

            Save.scrapCredits -= preset.buyCostScrap;
            Save.ownedPresetIds.Add(presetId);

            foreach (PartSlot slot in Enum.GetValues(typeof(PartSlot)))
            {
                string pId = preset.GetPartIdForSlot(slot);
                if (!string.IsNullOrEmpty(pId) && !IsPartOwned(pId))
                {
                    Save.ownedPartIds.Add(pId);
                    Save.SetPartLevel(pId, 1);
                }
            }

            if (!string.IsNullOrEmpty(preset.defaultColorSchemeId) && !IsColorOwned(preset.defaultColorSchemeId))
            {
                Save.ownedColorIds.Add(preset.defaultColorSchemeId);
            }

            if (autoEquip) EquipPreset(presetId);
            return true;
        }

        public bool EquipPreset(string presetId)
        {
            var preset = Database.GetPreset(presetId);
            if (preset == null || !IsPresetOwned(presetId)) return false;

            foreach (PartSlot slot in Enum.GetValues(typeof(PartSlot)))
            {
                string pId = preset.GetPartIdForSlot(slot);
                if (!string.IsNullOrEmpty(pId) && IsPartOwned(pId))
                {
                    Save.SetEquippedPartId(slot, pId);
                }
            }

            if (!string.IsNullOrEmpty(preset.defaultColorSchemeId) && IsColorOwned(preset.defaultColorSchemeId))
            {
                Save.equippedColorId = preset.defaultColorSchemeId;
            }
            return true;
        }

        public bool TryBuyColor(string colorId, bool autoEquip = true)
        {
            var scheme = Database.GetColorScheme(colorId);
            if (scheme == null || IsColorOwned(colorId)) return false;
            if (Save.scrapCredits < scheme.unlockCostScrap) return false;

            Save.scrapCredits -= scheme.unlockCostScrap;
            Save.ownedColorIds.Add(colorId);
            if (autoEquip) Save.equippedColorId = colorId;
            return true;
        }

        public bool TryEquipColor(string colorId)
        {
            if (!IsColorOwned(colorId)) return false;
            Save.equippedColorId = colorId;
            return true;
        }

        public bool TryBuyFinish(int finishIndex, bool autoEquip = true)
        {
            if (finishIndex < 0 || finishIndex >= GameDatabase.FinishCosts.Length) return false;
            if (IsFinishOwned(finishIndex)) return false;
            int cost = GameDatabase.FinishCosts[finishIndex];
            if (Save.scrapCredits < cost) return false;

            Save.scrapCredits -= cost;
            Save.ownedFinishIndices.Add(finishIndex);
            if (autoEquip) Save.equippedFinishIndex = finishIndex;
            return true;
        }

        public bool TryEquipFinish(int finishIndex)
        {
            if (!IsFinishOwned(finishIndex)) return false;
            Save.equippedFinishIndex = finishIndex;
            return true;
        }

        public void SaveCurrentToLoadoutSlot(int slotIndex)
        {
            if (Save.savedLoadouts == null) Save.savedLoadouts = new List<CustomLoadoutSlot>();
            while (Save.savedLoadouts.Count <= slotIndex)
            {
                Save.savedLoadouts.Add(new CustomLoadoutSlot { slotName = $"BUILD {Save.savedLoadouts.Count + 1}" });
            }
            var s = Save.savedLoadouts[slotIndex];
            s.headId = Save.equippedHeadId;
            s.torsoId = Save.equippedTorsoId;
            s.leftArmId = Save.equippedLeftArmId;
            s.rightArmId = Save.equippedRightArmId;
            s.legsId = Save.equippedLegsId;
            s.coreId = Save.equippedCoreId;
            s.colorId = Save.equippedColorId;
            s.finishIndex = Save.equippedFinishIndex;
            Save.activeLoadoutIndex = slotIndex;
        }

        public bool LoadFromLoadoutSlot(int slotIndex)
        {
            if (Save.savedLoadouts == null || slotIndex < 0 || slotIndex >= Save.savedLoadouts.Count) return false;
            var s = Save.savedLoadouts[slotIndex];
            if (IsPartOwned(s.headId)) Save.equippedHeadId = s.headId;
            if (IsPartOwned(s.torsoId)) Save.equippedTorsoId = s.torsoId;
            if (IsPartOwned(s.leftArmId)) Save.equippedLeftArmId = s.leftArmId;
            if (IsPartOwned(s.rightArmId)) Save.equippedRightArmId = s.rightArmId;
            if (IsPartOwned(s.legsId)) Save.equippedLegsId = s.legsId;
            if (IsPartOwned(s.coreId)) Save.equippedCoreId = s.coreId;
            if (IsColorOwned(s.colorId)) Save.equippedColorId = s.colorId;
            if (IsFinishOwned(s.finishIndex)) Save.equippedFinishIndex = s.finishIndex;
            Save.activeLoadoutIndex = slotIndex;
            return true;
        }

        public void AutoEquipBestOwnedBuild()
        {
            foreach (PartSlot slot in Enum.GetValues(typeof(PartSlot)))
            {
                var candidates = Database.GetPartsBySlot(slot);
                RobotPartData best = null;
                int bestPwr = -1;
                foreach (var p in candidates)
                {
                    if (!IsPartOwned(p.id)) continue;
                    int pwr = p.baseStats.ScaledForLevel(Save.GetPartLevel(p.id)).ComputePowerRating();
                    if (pwr > bestPwr)
                    {
                        bestPwr = pwr;
                        best = p;
                    }
                }
                if (best != null) Save.SetEquippedPartId(slot, best.id);
            }
        }

        public bool TryUnlockAbility(string abilityId)
        {
            var ab = Database.GetAbility(abilityId);
            if (ab == null || IsAbilityOwned(abilityId)) return false;
            if (Save.scrapCredits < ab.unlockCostScrap || Save.gearShards < ab.unlockCostShards) return false;

            Save.scrapCredits -= ab.unlockCostScrap;
            Save.gearShards -= ab.unlockCostShards;
            Save.ownedAbilityIds.Add(abilityId);

            if (ab.isPassive && string.IsNullOrEmpty(Save.equippedPassiveId))
            {
                Save.equippedPassiveId = abilityId;
            }
            else if (!ab.isPassive)
            {
                if (string.IsNullOrEmpty(Save.equippedSpecial1Id)) Save.equippedSpecial1Id = abilityId;
                else if (string.IsNullOrEmpty(Save.equippedSpecial2Id) && Save.equippedSpecial1Id != abilityId) Save.equippedSpecial2Id = abilityId;
            }
            return true;
        }

        public bool TryEquipAbility(string abilityId, int activeSlotIndex = 0)
        {
            var ab = Database.GetAbility(abilityId);
            if (ab == null || !IsAbilityOwned(abilityId)) return false;

            if (ab.isPassive) Save.equippedPassiveId = abilityId;
            else if (activeSlotIndex == 0)
            {
                if (Save.equippedSpecial2Id == abilityId) Save.equippedSpecial2Id = Save.equippedSpecial1Id;
                Save.equippedSpecial1Id = abilityId;
            }
            else
            {
                if (Save.equippedSpecial1Id == abilityId) Save.equippedSpecial1Id = Save.equippedSpecial2Id;
                Save.equippedSpecial2Id = abilityId;
            }
            return true;
        }

        public bool IsOpponentUnlocked(OpponentData opp)
        {
            if (opp == null) return false;
            if (opp.leagueIndex == 0 && opp.matchIndex == 0) return true;

            var allInLeague = Database.GetOpponentsInLeague(opp.leagueIndex);
            if (opp.matchIndex > 0)
            {
                for (int i = 0; i < allInLeague.Count; i++)
                {
                    if (allInLeague[i].matchIndex == opp.matchIndex - 1)
                        return Save.completedOpponentIds.Contains(allInLeague[i].id);
                }
            }
            else if (opp.leagueIndex > 0)
            {
                var prevLeague = Database.GetOpponentsInLeague(opp.leagueIndex - 1);
                if (prevLeague.Count > 0)
                    return Save.completedOpponentIds.Contains(prevLeague[prevLeague.Count - 1].id);
            }
            return true;
        }

        public void RecordMatchResult(bool playerWon, bool byKO, int stars, OpponentData opponent, out int scrapEarned, out int shardsEarned)
        {
            Save.dailyMatchesPlayed++;
            if (playerWon)
            {
                Save.totalWins++;
                Save.winStreak++;
                if (byKO)
                {
                    Save.totalKOs++;
                    Save.dailyKOsScored++;
                }

                float streakMult = 1f + Mathf.Min(Save.winStreak - 1, 5) * 0.1f;
                int baseScrap = opponent != null ? opponent.rewardScrap : 110;
                int baseShards = opponent != null ? opponent.rewardShards : 6;
                int koBonus = byKO ? Mathf.RoundToInt(baseScrap * 0.25f) : 0;

                scrapEarned = Mathf.RoundToInt((baseScrap + koBonus) * streakMult);
                shardsEarned = baseShards + (byKO ? 2 : 0);

                if (opponent != null)
                {
                    if (!Save.completedOpponentIds.Contains(opponent.id))
                        Save.completedOpponentIds.Add(opponent.id);
                    Save.SetOpponentStars(opponent.id, stars);
                }
            }
            else
            {
                Save.winStreak = 0;
                scrapEarned = opponent != null ? Mathf.Max(40, opponent.rewardScrap / 4) : 45;
                shardsEarned = 2;
            }

            Save.scrapCredits += scrapEarned;
            Save.gearShards += shardsEarned;
        }

        public bool CanClaimDailyMission() => !Save.dailyMissionClaimed && Save.dailyMatchesPlayed >= 3;

        public bool TryClaimDailyMission()
        {
            if (!CanClaimDailyMission()) return false;
            Save.dailyMissionClaimed = true;
            Save.scrapCredits += 300;
            Save.gearShards += 18;
            return true;
        }
    }
}
