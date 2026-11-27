using System;
using System.Collections.Generic;
using IronKnuckle.Core;
using IronKnuckle.Data;
using IronKnuckle.Robot;
using UnityEngine;
using UnityEngine.UI;

namespace IronKnuckle.UI
{
    public class GarageUI : MonoBehaviour
    {
        [Header("3D Preview & Studio Camera")]
        public RobotBuilder previewRobot;
        public RobotVisualController previewVisuals;
        public GarageCameraController cameraController;

        [Header("Header")]
        public Text scrapText;
        public Text shardsText;
        public Text statsSummaryText;
        public Text setBonusText;
        public Button backButton;
        public Button gfxButton;
        public Button fightNowButton;

        [Header("Preview Toolbar")]
        public GameObject previewBanner;
        public Text previewBannerText;
        public Button resetPreviewButton;
        public Button fullViewButton;
        public Button testPunchButton;
        public Button autoBestButton;
        public Button[] loadoutSlotButtons; // 3 build slots

        [Header("Main Category Tabs")]
        public Button[] categoryTabButtons; // 0=Parts, 1=Robots, 2=Paint & Finish, 3=Skills
        public GameObject slotFilterRow;
        public Button[] slotFilterButtons; // 6 slots

        [Header("Catalog Scroll & Modals")]
        public Transform listContainer;
        public GraphicsSettingsUI graphicsModal;
        public Sprite cardSprite;
        public Sprite buttonSprite;

        private int activeTab;
        private PartSlot activeSlot = PartSlot.Head;

        // Live preview state (lets player inspect ANY locked/unlocked part on the 3D robot!)
        private string previewHeadId;
        private string previewTorsoId;
        private string previewLeftArmId;
        private string previewRightArmId;
        private string previewLegsId;
        private string previewCoreId;
        private string previewColorId;
        private int previewFinishIndex;
        private string previewHighlightedItemId;

        private void Start()
        {
            AudioManager.Instance?.PlayBGM("bgm_menu");

            backButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                GameManager.Instance?.LoadMainMenu();
            });

            gfxButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                graphicsModal?.Open();
            });

            fightNowButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                GameManager.Instance?.StartQuickSparring();
            });

            resetPreviewButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                SyncPreviewToEquipped();
                RefreshPreviewRobot(null);
                RefreshAll();
            });

            fullViewButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                cameraController?.ToggleFullView(activeSlot);
            });

            testPunchButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_whoosh");
                previewVisuals?.TriggerDemoCombo();
            });

            autoBestButton?.onClick.AddListener(() =>
            {
                var gm = GameManager.Instance;
                if (gm == null) return;
                gm.Progression.AutoEquipBestOwnedBuild();
                gm.PersistSave();
                AudioManager.Instance?.PlaySFX("sfx_upgrade");
                SyncPreviewToEquipped();
                RefreshPreviewRobot(null);
                RefreshAll();
            });

            if (loadoutSlotButtons != null)
            {
                for (int i = 0; i < loadoutSlotButtons.Length; i++)
                {
                    int slotIdx = i;
                    loadoutSlotButtons[i]?.onClick.AddListener(() => OnSelectBuildSlot(slotIdx));
                }
            }

            if (categoryTabButtons != null)
            {
                for (int i = 0; i < categoryTabButtons.Length; i++)
                {
                    int idx = i;
                    categoryTabButtons[i].onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlaySFX("sfx_ui_click");
                        SelectTab(idx);
                    });
                }
            }

            if (slotFilterButtons != null)
            {
                for (int i = 0; i < slotFilterButtons.Length; i++)
                {
                    PartSlot slot = (PartSlot)i;
                    slotFilterButtons[i].onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlaySFX("sfx_ui_click");
                        activeSlot = slot;
                        cameraController?.FocusOnSlot(slot);
                        RefreshAll();
                    });
                }
            }

            if (MainMenuUI.RequestedGarageTabOnOpen >= 0)
            {
                activeTab = MainMenuUI.RequestedGarageTabOnOpen;
                MainMenuUI.RequestedGarageTabOnOpen = -1;
            }

            if (graphicsModal != null) graphicsModal.gameObject.SetActive(false);
            SyncPreviewToEquipped();
            RefreshPreviewRobot(null);
            SelectTab(activeTab);
        }

        private void SyncPreviewToEquipped()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var s = gm.SaveData;
            previewHeadId = s.equippedHeadId;
            previewTorsoId = s.equippedTorsoId;
            previewLeftArmId = s.equippedLeftArmId;
            previewRightArmId = s.equippedRightArmId;
            previewLegsId = s.equippedLegsId;
            previewCoreId = s.equippedCoreId;
            previewColorId = s.equippedColorId;
            previewFinishIndex = s.equippedFinishIndex;
            previewHighlightedItemId = "";
        }

        private bool IsPreviewingDifferentBuild()
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;
            var s = gm.SaveData;
            return previewHeadId != s.equippedHeadId || previewTorsoId != s.equippedTorsoId ||
                   previewLeftArmId != s.equippedLeftArmId || previewRightArmId != s.equippedRightArmId ||
                   previewLegsId != s.equippedLegsId || previewCoreId != s.equippedCoreId ||
                   previewColorId != s.equippedColorId || previewFinishIndex != s.equippedFinishIndex;
        }

        private void OnSelectBuildSlot(int slotIdx)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            AudioManager.Instance?.PlaySFX("sfx_ui_click");
            // Save current first if switching away, then load target slot
            gm.Progression.SaveCurrentToLoadoutSlot(gm.SaveData.activeLoadoutIndex);
            gm.Progression.LoadFromLoadoutSlot(slotIdx);
            gm.PersistSave();
            SyncPreviewToEquipped();
            RefreshPreviewRobot(null);
            RefreshAll();
        }

        public void SelectTab(int tabIndex)
        {
            activeTab = Mathf.Clamp(tabIndex, 0, 3);
            if (slotFilterRow != null) slotFilterRow.SetActive(activeTab == 0);
            if (activeTab == 0) cameraController?.FocusOnSlot(activeSlot);
            else cameraController?.FocusOnFullBody();
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshHeader();
            RefreshTabHighlights();
            RebuildCatalogList();
        }

        private void RefreshHeader()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var save = gm.SaveData;

            var equippedStats = gm.Progression.ComputePlayerTotalStats();
            var previewStats = gm.Progression.ComputeStatsForLoadout(
                previewHeadId, previewTorsoId, previewLeftArmId, previewRightArmId, previewLegsId, previewCoreId, 0, save.equippedPassiveId);

            bool isPrev = IsPreviewingDifferentBuild();
            if (previewBanner != null) previewBanner.SetActive(isPrev);
            if (previewBannerText != null && isPrev)
            {
                int diffPwr = previewStats.ComputePowerRating() - equippedStats.ComputePowerRating();
                string sign = diffPwr >= 0 ? "+" : "";
                previewBannerText.text = $"PREVIEW MODE  ({sign}{diffPwr} PWR)";
            }

            if (scrapText != null) scrapText.text = save.scrapCredits.ToString();
            if (shardsText != null) shardsText.text = save.gearShards.ToString();

            if (statsSummaryText != null)
            {
                int pwrDiff = previewStats.ComputePowerRating() - equippedStats.ComputePowerRating();
                string pwrTag = isPrev ? $" ({FormatDelta(pwrDiff)})" : "";
                statsSummaryText.text =
                    $"POWER: {previewStats.ComputePowerRating()}{pwrTag}\n" +
                    $"HP: {Mathf.RoundToInt(previewStats.maxHp)}   ATK: {Mathf.RoundToInt(previewStats.attack)}   SPD: {Mathf.RoundToInt(previewStats.speed)}\n" +
                    $"DEF: {Mathf.RoundToInt(previewStats.defense)}   STM: {Mathf.RoundToInt(previewStats.stamina)}   CRIT: {Mathf.RoundToInt(previewStats.critChance * 100f)}%";
            }

            if (setBonusText != null)
            {
                var counts = gm.Progression.ComputeBrandCounts(previewHeadId, previewTorsoId, previewLeftArmId, previewRightArmId, previewLegsId, previewCoreId);
                string activeSynergies = "";
                foreach (var kv in counts)
                {
                    if (kv.Value >= 3)
                    {
                        activeSynergies += $"★ {GameDatabase.GetBrandSetSummary(kv.Key, kv.Value)}\n";
                    }
                }
                if (string.IsNullOrEmpty(activeSynergies))
                {
                    // Show closest brand progress
                    RobotBrand topBrand = RobotBrand.Scrapworks;
                    int topCount = 0;
                    foreach (var kv in counts)
                    {
                        if (kv.Value > topCount) { topCount = kv.Value; topBrand = kv.Key; }
                    }
                    activeSynergies = $"SET SYNERGY: {GameDatabase.GetBrandDisplayName(topBrand)} {topCount}/3 (Equip 3 same-brand parts for bonus!)";
                }
                setBonusText.text = activeSynergies.TrimEnd('\n');
            }

            if (loadoutSlotButtons != null)
            {
                for (int i = 0; i < loadoutSlotButtons.Length; i++)
                {
                    var img = loadoutSlotButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (i == save.activeLoadoutIndex) ? UIThemeHelper.PrimaryCyan : UIThemeHelper.MutedSlate;
                }
            }
        }

        private void RefreshPreviewRobot(PartSlot? sparkSlot)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Database == null || previewRobot == null) return;

            previewRobot.ApplyLoadout(gm.Database,
                previewHeadId, previewTorsoId,
                previewLeftArmId, previewRightArmId,
                previewLegsId, previewCoreId,
                previewColorId, previewFinishIndex);

            if (previewVisuals == null) previewVisuals = previewRobot.GetComponent<RobotVisualController>();
            previewVisuals?.Initialize(true);
            previewVisuals?.TriggerPartSwapBounce();
            if (sparkSlot.HasValue) previewRobot.PlaySwapSparks(sparkSlot);
        }

        private void RefreshTabHighlights()
        {
            if (categoryTabButtons != null)
            {
                for (int i = 0; i < categoryTabButtons.Length; i++)
                {
                    var img = categoryTabButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (i == activeTab) ? UIThemeHelper.PrimaryCyan : UIThemeHelper.MutedSlate;
                }
            }
            if (slotFilterButtons != null)
            {
                for (int i = 0; i < slotFilterButtons.Length; i++)
                {
                    var img = slotFilterButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (i == (int)activeSlot) ? UIThemeHelper.AccentGold : UIThemeHelper.MutedSlate;
                }
            }
        }

        private void RebuildCatalogList()
        {
            if (listContainer == null) return;
            for (int i = listContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(listContainer.GetChild(i).gameObject);
            }

            var gm = GameManager.Instance;
            if (gm == null || gm.Database == null) return;

            switch (activeTab)
            {
                case 0: BuildPartsList(gm); break;
                case 1: BuildPresetsList(gm); break;
                case 2: BuildPaintAndFinishList(gm); break;
                case 3: BuildSkillsList(gm); break;
            }
        }

        private void PreviewSinglePart(RobotPartData part)
        {
            if (part == null) return;
            switch (part.slot)
            {
                case PartSlot.Head: previewHeadId = part.id; break;
                case PartSlot.Torso: previewTorsoId = part.id; break;
                case PartSlot.LeftArm: previewLeftArmId = part.id; break;
                case PartSlot.RightArm: previewRightArmId = part.id; break;
                case PartSlot.Legs: previewLegsId = part.id; break;
                case PartSlot.Core: previewCoreId = part.id; break;
            }
            previewHighlightedItemId = part.id;
            AudioManager.Instance?.PlaySFX("sfx_ui_click");
            RefreshPreviewRobot(part.slot);
            RefreshHeader();
            RebuildCatalogList();
        }

        private void BuildPartsList(GameManager gm)
        {
            List<RobotPartData> parts = gm.Database.GetPartsBySlot(activeSlot);
            string equippedId = gm.SaveData.GetEquippedPartId(activeSlot);
            var equippedPart = gm.Database.GetPart(equippedId);
            var eqStats = equippedPart != null
                ? equippedPart.baseStats.ScaledForLevel(gm.SaveData.GetPartLevel(equippedId))
                : new RobotStats();

            foreach (var part in parts)
            {
                bool owned = gm.Progression.IsPartOwned(part.id);
                bool equipped = equippedId == part.id;
                bool previewing = previewHighlightedItemId == part.id;
                int lvl = gm.SaveData.GetPartLevel(part.id);
                var pStats = part.baseStats.ScaledForLevel(lvl);

                Color border = previewing ? UIThemeHelper.PrimaryCyan : UIThemeHelper.GetRarityColor(part.rarity);
                var card = CreateCardRoot("Part_" + part.id, border, 184f);

                // Make card itself clickable to preview part live on the 3D robot!
                var cardBtn = card.gameObject.AddComponent<Button>();
                cardBtn.transition = Selectable.Transition.None;
                cardBtn.onClick.AddListener(() => PreviewSinglePart(part));

                string brandTag = GameDatabase.GetBrandDisplayName(part.brand);
                string lvlTag = owned ? $"Lv.{lvl}/{ProgressionService.MaxPartLevel}" : part.rarity.ToString().ToUpperInvariant();
                string title = $"{part.displayName}  [{brandTag} | {lvlTag}]" + (equipped ? "  ★ EQUIPPED" : (previewing ? "  👁 PREVIEWING" : ""));
                AddCardHeader(card.transform, title, UIThemeHelper.GetRarityColor(part.rarity));

                string deltaInfo =
                    $"HP {Mathf.RoundToInt(pStats.maxHp)} ({FormatDelta(pStats.maxHp - eqStats.maxHp)})  |  " +
                    $"ATK {Mathf.RoundToInt(pStats.attack)} ({FormatDelta(pStats.attack - eqStats.attack)})  |  " +
                    $"SPD {Mathf.RoundToInt(pStats.speed)} ({FormatDelta(pStats.speed - eqStats.speed)})  |  " +
                    $"DEF {Mathf.RoundToInt(pStats.defense)} ({FormatDelta(pStats.defense - eqStats.defense)})";
                AddCardBody(card.transform, $"{part.description}\n{deltaInfo}");

                var btnRow = AddCardButtonRow(card.transform);

                var prevBtn = UIThemeHelper.CreateButton("Preview", btnRow.transform, previewing ? "VIEWING ★" : "PREVIEW 3D",
                    previewing ? UIThemeHelper.PrimaryCyan : UIThemeHelper.MutedSlate, 18, buttonSprite);
                prevBtn.onClick.AddListener(() => PreviewSinglePart(part));

                if (!owned)
                {
                    bool canAfford = gm.SaveData.scrapCredits >= part.buyCostScrap;
                    var buyBtn = UIThemeHelper.CreateButton("Buy", btnRow.transform, $"BUY ({part.buyCostScrap} SCRAP)",
                        canAfford ? UIThemeHelper.AccentGreen : UIThemeHelper.MutedSlate, 19, buttonSprite);
                    buyBtn.interactable = canAfford;
                    buyBtn.onClick.AddListener(() =>
                    {
                        if (gm.Progression.TryBuyPart(part.id, true))
                        {
                            gm.Progression.SaveCurrentToLoadoutSlot(gm.SaveData.activeLoadoutIndex);
                            gm.PersistSave();
                            AudioManager.Instance?.PlaySFX("sfx_upgrade");
                            SyncPreviewToEquipped();
                            RefreshPreviewRobot(part.slot);
                            RefreshAll();
                        }
                    });
                }
                else
                {
                    if (!equipped)
                    {
                        var eqBtn = UIThemeHelper.CreateButton("Equip", btnRow.transform, "EQUIP", UIThemeHelper.PrimaryCyan, 19, buttonSprite);
                        eqBtn.onClick.AddListener(() =>
                        {
                            if (gm.Progression.TryEquipPart(part.id))
                            {
                                gm.Progression.SaveCurrentToLoadoutSlot(gm.SaveData.activeLoadoutIndex);
                                gm.PersistSave();
                                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                                SyncPreviewToEquipped();
                                RefreshPreviewRobot(part.slot);
                                RefreshAll();
                            }
                        });
                    }

                    if (lvl < ProgressionService.MaxPartLevel)
                    {
                        int sc = part.GetUpgradeScrapCost(lvl);
                        int sh = part.GetUpgradeShardsCost(lvl);
                        bool canUp = gm.SaveData.scrapCredits >= sc && gm.SaveData.gearShards >= sh;
                        var upBtn = UIThemeHelper.CreateButton("Upgrade", btnRow.transform, $"UPGRADE ({sc}S / {sh}G)",
                            canUp ? UIThemeHelper.AccentGold : UIThemeHelper.MutedSlate, 18, buttonSprite);
                        upBtn.interactable = canUp;
                        upBtn.onClick.AddListener(() =>
                        {
                            if (gm.Progression.TryUpgradePart(part.id))
                            {
                                gm.PersistSave();
                                AudioManager.Instance?.PlaySFX("sfx_upgrade");
                                RefreshPreviewRobot(part.slot);
                                RefreshAll();
                            }
                        });
                    }
                    else
                    {
                        var maxBtn = UIThemeHelper.CreateButton("Max", btnRow.transform, "MAX LV.5", UIThemeHelper.MutedSlate, 18, buttonSprite);
                        maxBtn.interactable = false;
                    }
                }
            }
        }

        private void BuildPresetsList(GameManager gm)
        {
            foreach (var preset in gm.Database.presets)
            {
                bool owned = gm.Progression.IsPresetOwned(preset.id);
                var pStats = gm.Progression.ComputeStatsForLoadout(
                    preset.headPartId, preset.torsoPartId, preset.leftArmPartId, preset.rightArmPartId, preset.legsPartId, preset.corePartId, 1);

                var card = CreateCardRoot("Preset_" + preset.id, UIThemeHelper.AccentGold, 184f);
                AddCardHeader(card.transform, $"{preset.displayName}  ({preset.archetype}) — PWR {pStats.ComputePowerRating()}", UIThemeHelper.AccentGold);
                AddCardBody(card.transform, $"{preset.tagline}\nHP {Mathf.RoundToInt(pStats.maxHp)} | ATK {Mathf.RoundToInt(pStats.attack)} | SPD {Mathf.RoundToInt(pStats.speed)} | DEF {Mathf.RoundToInt(pStats.defense)}");

                var btnRow = AddCardButtonRow(card.transform);
                var prevBtn = UIThemeHelper.CreateButton("Preview", btnRow.transform, "PREVIEW 3D", UIThemeHelper.MutedSlate, 19, buttonSprite);
                prevBtn.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("sfx_ui_click");
                    previewHeadId = preset.headPartId;
                    previewTorsoId = preset.torsoPartId;
                    previewLeftArmId = preset.leftArmPartId;
                    previewRightArmId = preset.rightArmPartId;
                    previewLegsId = preset.legsPartId;
                    previewCoreId = preset.corePartId;
                    previewColorId = preset.defaultColorSchemeId;
                    RefreshPreviewRobot(PartSlot.Torso);
                    RefreshHeader();
                });

                if (!owned)
                {
                    bool canBuy = gm.SaveData.scrapCredits >= preset.buyCostScrap;
                    var buyBtn = UIThemeHelper.CreateButton("BuyPreset", btnRow.transform, $"BUY ROBOT ({preset.buyCostScrap} SCRAP)",
                        canBuy ? UIThemeHelper.AccentGreen : UIThemeHelper.MutedSlate, 19, buttonSprite);
                    buyBtn.interactable = canBuy;
                    buyBtn.onClick.AddListener(() =>
                    {
                        if (gm.Progression.TryBuyPreset(preset.id, true))
                        {
                            gm.Progression.SaveCurrentToLoadoutSlot(gm.SaveData.activeLoadoutIndex);
                            gm.PersistSave();
                            AudioManager.Instance?.PlaySFX("sfx_upgrade");
                            SyncPreviewToEquipped();
                            RefreshPreviewRobot(PartSlot.Torso);
                            RefreshAll();
                        }
                    });
                }
                else
                {
                    var eqBtn = UIThemeHelper.CreateButton("EquipPreset", btnRow.transform, "EQUIP BUILD", UIThemeHelper.PrimaryCyan, 19, buttonSprite);
                    eqBtn.onClick.AddListener(() =>
                    {
                        if (gm.Progression.EquipPreset(preset.id))
                        {
                            gm.Progression.SaveCurrentToLoadoutSlot(gm.SaveData.activeLoadoutIndex);
                            gm.PersistSave();
                            AudioManager.Instance?.PlaySFX("sfx_ui_click");
                            SyncPreviewToEquipped();
                            RefreshPreviewRobot(PartSlot.Torso);
                            RefreshAll();
                        }
                    });
                }
            }
        }

        private void BuildPaintAndFinishList(GameManager gm)
        {
            // Surface Finishes Section
            var fHdr = UIThemeHelper.CreateText("FinHdr", listContainer, "SURFACE FINISHES (TAP TO PREVIEW)", 22, TextAnchor.MiddleLeft, UIThemeHelper.PrimaryCyan);
            fHdr.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

            for (int f = 0; f < GameDatabase.FinishNames.Length; f++)
            {
                int finIdx = f;
                bool owned = gm.Progression.IsFinishOwned(finIdx);
                bool eq = gm.SaveData.equippedFinishIndex == finIdx;
                int cost = GameDatabase.FinishCosts[finIdx];

                var card = CreateCardRoot("Finish_" + finIdx, UIThemeHelper.PrimaryCyan, 124f);
                AddCardHeader(card.transform, $"{GameDatabase.FinishNames[finIdx]}" + (eq ? "  ★ ACTIVE" : ""), UIThemeHelper.PrimaryCyan);
                var btnRow = AddCardButtonRow(card.transform);

                var prevBtn = UIThemeHelper.CreateButton("PreviewFin", btnRow.transform, "PREVIEW", UIThemeHelper.MutedSlate, 18, buttonSprite);
                prevBtn.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("sfx_ui_click");
                    previewFinishIndex = finIdx;
                    RefreshPreviewRobot(PartSlot.Torso);
                    RefreshHeader();
                });

                if (!owned)
                {
                    bool canBuy = gm.SaveData.scrapCredits >= cost;
                    var buyBtn = UIThemeHelper.CreateButton("BuyFin", btnRow.transform, $"BUY ({cost} SCRAP)",
                        canBuy ? UIThemeHelper.AccentGreen : UIThemeHelper.MutedSlate, 18, buttonSprite);
                    buyBtn.interactable = canBuy;
                    buyBtn.onClick.AddListener(() =>
                    {
                        if (gm.Progression.TryBuyFinish(finIdx, true))
                        {
                            gm.PersistSave();
                            AudioManager.Instance?.PlaySFX("sfx_upgrade");
                            SyncPreviewToEquipped();
                            RefreshPreviewRobot(PartSlot.Torso);
                            RefreshAll();
                        }
                    });
                }
                else
                {
                    var eqBtn = UIThemeHelper.CreateButton("EqFin", btnRow.transform, eq ? "APPLIED" : "APPLY FINISH",
                        eq ? UIThemeHelper.MutedSlate : UIThemeHelper.PrimaryCyan, 18, buttonSprite);
                    eqBtn.interactable = !eq;
                    eqBtn.onClick.AddListener(() =>
                    {
                        gm.Progression.TryEquipFinish(finIdx);
                        gm.PersistSave();
                        AudioManager.Instance?.PlaySFX("sfx_ui_click");
                        SyncPreviewToEquipped();
                        RefreshPreviewRobot(PartSlot.Torso);
                        RefreshAll();
                    });
                }
            }

            var cHdr = UIThemeHelper.CreateText("ColHdr", listContainer, "COLORWAYS", 22, TextAnchor.MiddleLeft, UIThemeHelper.AccentGold);
            cHdr.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

            foreach (var scheme in gm.Database.colorSchemes)
            {
                bool owned = gm.Progression.IsColorOwned(scheme.id);
                bool equipped = gm.SaveData.equippedColorId == scheme.id;
                var card = CreateCardRoot("Color_" + scheme.id, scheme.primaryColor, 128f);

                AddCardHeader(card.transform, scheme.displayName + (equipped ? "  ★ ACTIVE" : ""), scheme.visorColor);
                var btnRow = AddCardButtonRow(card.transform);

                var sw = UIThemeHelper.CreatePanel("Swatch", btnRow.transform, scheme.primaryColor, cardSprite);
                sw.gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;

                var prevBtn = UIThemeHelper.CreateButton("PrevCol", btnRow.transform, "PREVIEW", UIThemeHelper.MutedSlate, 18, buttonSprite);
                prevBtn.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("sfx_ui_click");
                    previewColorId = scheme.id;
                    RefreshPreviewRobot(PartSlot.Torso);
                    RefreshHeader();
                });

                if (!owned)
                {
                    bool canBuy = gm.SaveData.scrapCredits >= scheme.unlockCostScrap;
                    var buyBtn = UIThemeHelper.CreateButton("BuyColor", btnRow.transform, $"UNLOCK ({scheme.unlockCostScrap} SCRAP)",
                        canBuy ? UIThemeHelper.AccentGreen : UIThemeHelper.MutedSlate, 18, buttonSprite);
                    buyBtn.interactable = canBuy;
                    buyBtn.onClick.AddListener(() =>
                    {
                        if (gm.Progression.TryBuyColor(scheme.id, true))
                        {
                            gm.PersistSave();
                            AudioManager.Instance?.PlaySFX("sfx_upgrade");
                            SyncPreviewToEquipped();
                            RefreshPreviewRobot(PartSlot.Torso);
                            RefreshAll();
                        }
                    });
                }
                else
                {
                    var eqBtn = UIThemeHelper.CreateButton("EquipColor", btnRow.transform, equipped ? "EQUIPPED" : "APPLY PAINT",
                        equipped ? UIThemeHelper.MutedSlate : UIThemeHelper.PrimaryCyan, 18, buttonSprite);
                    eqBtn.interactable = !equipped;
                    eqBtn.onClick.AddListener(() =>
                    {
                        if (gm.Progression.TryEquipColor(scheme.id))
                        {
                            gm.PersistSave();
                            AudioManager.Instance?.PlaySFX("sfx_ui_click");
                            SyncPreviewToEquipped();
                            RefreshPreviewRobot(PartSlot.Torso);
                            RefreshAll();
                        }
                    });
                }
            }
        }

        private void BuildSkillsList(GameManager gm)
        {
            foreach (var ab in gm.Database.abilities)
            {
                bool owned = gm.Progression.IsAbilityOwned(ab.id);
                bool eq1 = gm.SaveData.equippedSpecial1Id == ab.id;
                bool eq2 = gm.SaveData.equippedSpecial2Id == ab.id;
                bool eqP = gm.SaveData.equippedPassiveId == ab.id;

                string badge = ab.isPassive ? "[PASSIVE MOD]" : $"[SPECIAL | {Mathf.RoundToInt(ab.overdriveCost)} OD]";
                if (eq1) badge += "  ★ SLOT 1";
                if (eq2) badge += "  ★ SLOT 2";
                if (eqP) badge += "  ★ ACTIVE MOD";

                var card = CreateCardRoot("Skill_" + ab.id, ab.accentColor, 166f);
                AddCardHeader(card.transform, $"{ab.displayName}  {badge}", ab.isPassive ? UIThemeHelper.AccentGreen : UIThemeHelper.AccentOrange);
                AddCardBody(card.transform, ab.description);

                var btnRow = AddCardButtonRow(card.transform);
                if (!owned)
                {
                    bool canBuy = gm.SaveData.scrapCredits >= ab.unlockCostScrap && gm.SaveData.gearShards >= ab.unlockCostShards;
                    var buyBtn = UIThemeHelper.CreateButton("UnlockSkill", btnRow.transform,
                        $"UNLOCK ({ab.unlockCostScrap}S / {ab.unlockCostShards}G)",
                        canBuy ? UIThemeHelper.AccentGreen : UIThemeHelper.MutedSlate, 19, buttonSprite);
                    buyBtn.interactable = canBuy;
                    buyBtn.onClick.AddListener(() =>
                    {
                        if (gm.Progression.TryUnlockAbility(ab.id))
                        {
                            gm.PersistSave();
                            AudioManager.Instance?.PlaySFX("sfx_upgrade");
                            RefreshAll();
                        }
                    });
                }
                else if (ab.isPassive)
                {
                    var eqBtn = UIThemeHelper.CreateButton("EquipPassive", btnRow.transform, eqP ? "EQUIPPED" : "EQUIP PASSIVE",
                        eqP ? UIThemeHelper.MutedSlate : UIThemeHelper.AccentGreen, 19, buttonSprite);
                    eqBtn.interactable = !eqP;
                    eqBtn.onClick.AddListener(() =>
                    {
                        gm.Progression.TryEquipAbility(ab.id, 0);
                        gm.PersistSave();
                        AudioManager.Instance?.PlaySFX("sfx_ui_click");
                        RefreshAll();
                    });
                }
                else
                {
                    var s1Btn = UIThemeHelper.CreateButton("Slot1", btnRow.transform, eq1 ? "SLOT 1 ★" : "SET SLOT 1",
                        eq1 ? UIThemeHelper.MutedSlate : UIThemeHelper.PrimaryCyan, 19, buttonSprite);
                    s1Btn.interactable = !eq1;
                    s1Btn.onClick.AddListener(() =>
                    {
                        gm.Progression.TryEquipAbility(ab.id, 0);
                        gm.PersistSave();
                        AudioManager.Instance?.PlaySFX("sfx_ui_click");
                        RefreshAll();
                    });

                    var s2Btn = UIThemeHelper.CreateButton("Slot2", btnRow.transform, eq2 ? "SLOT 2 ★" : "SET SLOT 2",
                        eq2 ? UIThemeHelper.MutedSlate : UIThemeHelper.AccentOrange, 19, buttonSprite);
                    s2Btn.interactable = !eq2;
                    s2Btn.onClick.AddListener(() =>
                    {
                        gm.Progression.TryEquipAbility(ab.id, 1);
                        gm.PersistSave();
                        AudioManager.Instance?.PlaySFX("sfx_ui_click");
                        RefreshAll();
                    });
                }
            }
        }

        private Image CreateCardRoot(string cardName, Color borderTint, float height)
        {
            var card = UIThemeHelper.CreatePanel(cardName, listContainer, UIThemeHelper.PanelCard, cardSprite);
            var le = card.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;

            var vlg = card.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 10, 10);
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(borderTint.r, borderTint.g, borderTint.b, 0.78f);
            outline.effectDistance = new Vector2(2f, -2f);
            return card;
        }

        private void AddCardHeader(Transform parent, string title, Color col)
        {
            var t = UIThemeHelper.CreateText("Title", parent, title, 21, TextAnchor.MiddleLeft, col);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        }

        private void AddCardBody(Transform parent, string body)
        {
            var t = UIThemeHelper.CreateText("Body", parent, body, 16, TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.96f), FontStyle.Normal);
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        }

        private GameObject AddCardButtonRow(Transform parent)
        {
            var row = UIThemeHelper.CreateUIObject("Buttons", parent);
            row.AddComponent<LayoutElement>().preferredHeight = 48f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            return row;
        }

        private string FormatDelta(float val)
        {
            int r = Mathf.RoundToInt(val);
            if (r > 0) return $"+{r}";
            return r.ToString();
        }
    }
}
