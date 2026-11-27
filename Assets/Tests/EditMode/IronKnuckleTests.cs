using System.IO;
using IronKnuckle.Combat;
using IronKnuckle.Core;
using IronKnuckle.Data;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;

namespace IronKnuckle.Tests
{
    public class IronKnuckleTests
    {
        private GameDatabase LoadDb()
        {
            var db = Resources.Load<GameDatabase>("GameDatabase");
            Assert.IsNotNull(db, "Resources/GameDatabase.asset must exist");
            return db;
        }

        [Test]
        public void Database_Contains72MultiSubmeshPartsPresetsAbilitiesColorsAndOpponents()
        {
            var db = LoadDb();
            Assert.AreEqual(72, db.parts.Count, "Expected 72 modular robot parts (12 per slot)");
            Assert.AreEqual(6, db.presets.Count, "Expected 6 pre-made robot presets");
            Assert.AreEqual(10, db.abilities.Count, "Expected 10 combat abilities (6 active + 4 passive)");
            Assert.AreEqual(8, db.colorSchemes.Count, "Expected 8 color schemes");
            Assert.AreEqual(12, db.opponents.Count, "Expected 12 career opponents across 4 leagues");
            Assert.AreEqual(5, db.surfaceFinishTextures.Length, "Expected 5 surface finish textures");

            Assert.IsNotNull(db.visorScreenMesh);
            Assert.IsNotNull(db.pelvisMesh);
            Assert.IsNotNull(db.shoulderPadMesh);
            Assert.IsNotNull(db.upperArmMesh);
            Assert.IsNotNull(db.robotBaseMaterial);
            Assert.IsNotNull(db.gunmetalBaseMaterial);
            Assert.IsNotNull(db.emissiveBaseMaterial);
            Assert.IsNotNull(db.visorBaseMaterial);
            Assert.IsNotNull(db.armorNormalMap);

            foreach (var p in db.parts)
            {
                Assert.IsNotNull(p, "Part entry null");
                Assert.IsNotNull(p.mesh, $"Part {p.id} missing mesh");
                Assert.GreaterOrEqual(p.mesh.subMeshCount, 3, $"Part {p.id} must have 3 submeshes (Armor/Gunmetal/Emissive)");
            }

            foreach (var preset in db.presets)
            {
                Assert.IsNotNull(db.GetPart(preset.headPartId), $"Preset {preset.id} invalid head");
                Assert.IsNotNull(db.GetPart(preset.torsoPartId), $"Preset {preset.id} invalid torso");
                Assert.IsNotNull(db.GetPart(preset.leftArmPartId), $"Preset {preset.id} invalid leftArm");
                Assert.IsNotNull(db.GetPart(preset.rightArmPartId), $"Preset {preset.id} invalid rightArm");
                Assert.IsNotNull(db.GetPart(preset.legsPartId), $"Preset {preset.id} invalid legs");
                Assert.IsNotNull(db.GetPart(preset.corePartId), $"Preset {preset.id} invalid core");
                Assert.IsNotNull(db.GetColorScheme(preset.defaultColorSchemeId), $"Preset {preset.id} invalid color");
            }

            foreach (var opp in db.opponents)
            {
                Assert.IsNotNull(db.GetPart(opp.headPartId), $"Opp {opp.id} invalid head");
                Assert.IsNotNull(db.GetPart(opp.torsoPartId), $"Opp {opp.id} invalid torso");
                Assert.IsNotNull(db.GetAbility(opp.specialAbilityId), $"Opp {opp.id} invalid special");
                Assert.IsNotNull(db.GetColorScheme(opp.colorSchemeId), $"Opp {opp.id} invalid color");
            }
        }

        [Test]
        public void CombatCalculator_ScalingParryDodgeCounterGuardBreakWork()
        {
            var atk = new RobotStats { maxHp = 200, attack = 50, speed = 25, defense = 20, stamina = 100, critChance = 0f, overdriveGain = 10 };
            var def = new RobotStats { maxHp = 200, attack = 40, speed = 20, defense = 25, stamina = 100, critChance = 0f, overdriveGain = 10 };

            var jab = CombatCalculator.ResolveHit(atk, def, PunchType.LeftJab, FighterState.Idle, false, false, 0, 100f, 0f, 0.99f);
            var upper = CombatCalculator.ResolveHit(atk, def, PunchType.Uppercut, FighterState.Idle, false, false, 0, 100f, 0f, 0.99f);
            Assert.Greater(upper.finalDamage, jab.finalDamage, "Uppercut must deal more damage than Jab");

            var dodged = CombatCalculator.ResolveHit(atk, def, PunchType.RightCross, FighterState.SlipLeft, false, false, 0, 100f, 0f, 0.99f);
            Assert.IsTrue(dodged.wasDodged);
            Assert.AreEqual(0f, dodged.finalDamage);

            var parried = CombatCalculator.ResolveHit(atk, def, PunchType.LeftHook, FighterState.Guarding, true, false, 0, 100f, 0f, 0.99f);
            Assert.IsTrue(parried.wasParried);
            Assert.AreEqual(0f, parried.finalDamage);

            var blocked = CombatCalculator.ResolveHit(atk, def, PunchType.RightCross, FighterState.Guarding, false, false, 0, 100f, 0f, 0.99f);
            Assert.IsTrue(blocked.wasBlocked);
            Assert.False(blocked.brokeGuard);
            Assert.Less(blocked.finalDamage, jab.finalDamage);

            var guardBreak = CombatCalculator.ResolveHit(atk, def, PunchType.Uppercut, FighterState.Guarding, false, false, 0, 15f, 0f, 0.99f);
            Assert.IsTrue(guardBreak.brokeGuard);

            var counter = CombatCalculator.ResolveHit(atk, def, PunchType.RightCross, FighterState.Idle, false, true, 0, 100f, 0.35f, 0.99f);
            var normal = CombatCalculator.ResolveHit(atk, def, PunchType.RightCross, FighterState.Idle, false, false, 0, 100f, 0f, 0.99f);
            Assert.IsTrue(counter.wasCounter);
            Assert.Greater(counter.finalDamage, normal.finalDamage * 1.8f);
        }

        [Test]
        public void ProgressionService_SetBonusesFinishesCustomLoadoutsAndSaveRoundTrip()
        {
            var db = LoadDb();
            var save = SaveSystem.CreateDefaultSave();
            save.scrapCredits = 3000;
            save.gearShards = 80;
            var svc = new ProgressionService(db, save);

            int basePower = svc.ComputePlayerTotalStats().ComputePowerRating();

            // Buy and equip 3 TitanForge parts -> activates 3pc set bonus!
            Assert.IsTrue(svc.TryBuyPart("head_horned", true));
            Assert.IsTrue(svc.TryBuyPart("torso_anvil", true));
            Assert.IsTrue(svc.TryBuyPart("larm_piledriver", true));
            var counts = svc.ComputeBrandCounts(save.equippedHeadId, save.equippedTorsoId, save.equippedLeftArmId, save.equippedRightArmId, save.equippedLegsId, save.equippedCoreId);
            Assert.GreaterOrEqual(counts[RobotBrand.TitanForge], 3);
            Assert.Greater(svc.ComputePlayerTotalStats().ComputePowerRating(), basePower);

            // Surface Finish buy & equip
            Assert.IsTrue(svc.TryBuyFinish(1, true));
            Assert.AreEqual(1, save.equippedFinishIndex);

            // Save to Loadout Slot 1, switch to Slot 0, switch back to Slot 1
            svc.SaveCurrentToLoadoutSlot(1);
            Assert.IsTrue(svc.LoadFromLoadoutSlot(0));
            Assert.AreEqual("head_classic", save.equippedHeadId);
            Assert.IsTrue(svc.LoadFromLoadoutSlot(1));
            Assert.AreEqual("head_horned", save.equippedHeadId);

            // Graphics preset test
            GraphicsSettingsManager.ApplyPresetToData(save, 0);
            Assert.AreEqual(30, save.targetFps);
            Assert.IsFalse(save.bloomEnabled);

            // Save & Load JSON roundtrip
            string tempFile = Path.Combine(Application.temporaryCachePath, "test_ik_save_v2.json");
            if (File.Exists(tempFile)) File.Delete(tempFile);
            SaveSystem.Save(save, tempFile);
            var reloaded = SaveSystem.Load(tempFile);
            Assert.AreEqual("head_horned", reloaded.equippedHeadId);
            Assert.AreEqual(1, reloaded.equippedFinishIndex);
            Assert.AreEqual(30, reloaded.targetFps);
            File.Delete(tempFile);
        }

        [Test]
        public void Scenes_HaveValidCamerasVolumesAndNewInputSystemUIModule()
        {
            string[] scenes =
            {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Garage.unity",
                "Assets/Scenes/Arena.unity"
            };

            foreach (var scenePath in scenes)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Assert.IsNotNull(Camera.main, $"MainCamera missing in {scenePath}");
                var es = Object.FindAnyObjectByType<EventSystem>();
                Assert.IsNotNull(es, $"EventSystem missing in {scenePath}");
                Assert.IsNotNull(es.GetComponent<InputSystemUIInputModule>(), $"InputSystemUIInputModule missing in {scenePath}");
                var vol = Object.FindAnyObjectByType<Volume>();
                Assert.IsNotNull(vol, $"URP Post-Processing Volume missing in {scenePath}");
                Assert.IsNotNull(vol.sharedProfile, $"VolumeProfile missing in {scenePath}");
            }
        }

        [Test]
        public void FighterController_CombatStateMachineTicksAndDealsDamage()
        {
            var db = LoadDb();
            var pGo = new GameObject("TestPlayer");
            var oGo = new GameObject("TestOpponent");
            try
            {
                var pBuilder = pGo.AddComponent<IronKnuckle.Robot.RobotBuilder>();
                pBuilder.ApplyLoadout(db, "head_classic", "torso_scrap", "larm_riveted", "rarm_riveted", "legs_spring", "core_battery", "color_cobalt");
                pGo.AddComponent<IronKnuckle.Robot.RobotVisualController>();
                var pCtrl = pGo.AddComponent<FighterController>();

                var oBuilder = oGo.AddComponent<IronKnuckle.Robot.RobotBuilder>();
                oBuilder.ApplyLoadout(db, "head_classic", "torso_scrap", "larm_riveted", "rarm_riveted", "legs_spring", "core_battery", "color_crimson");
                oGo.AddComponent<IronKnuckle.Robot.RobotVisualController>();
                var oCtrl = oGo.AddComponent<FighterController>();

                var stats = new RobotStats { maxHp = 200, attack = 40, speed = 25, defense = 10, stamina = 100, critChance = 0f, overdriveGain = 10 };
                pCtrl.InitializeFighter("P1", true, stats, null, null, null);
                oCtrl.InitializeFighter("CPU", false, stats, null, null, null);
                pCtrl.Opponent = oCtrl;
                oCtrl.Opponent = pCtrl;

                Assert.IsTrue(pCtrl.TryStartPunch(PunchType.RightCross, 0.1f));
                Assert.AreEqual(FighterState.Windup, pCtrl.State);

                for (int i = 0; i < 30; i++)
                {
                    pCtrl.TickCombat(0.02f);
                    oCtrl.TickCombat(0.02f);
                }

                Assert.Less(oCtrl.CurrentHp, oCtrl.MaxHp, "Opponent should have taken damage from landed punch");
                Assert.Greater(pCtrl.Overdrive, 0f, "Player should gain Overdrive after landing punch");
                Assert.AreEqual(FighterState.Idle, pCtrl.State);
            }
            finally
            {
                Object.DestroyImmediate(pGo);
                Object.DestroyImmediate(oGo);
            }
        }
    }
}
