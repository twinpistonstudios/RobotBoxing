using System;
using System.Collections.Generic;
using System.IO;
using IronKnuckle.Combat;
using IronKnuckle.Data;
using IronKnuckle.Robot;
using IronKnuckle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace IronKnuckle.Editor
{
    public static class RobotBoxingProjectBuilder
    {
        private const string DataRoot = "Assets/Data";
        private const string MaterialsRoot = "Assets/Art/Materials";
        private const string ScenesRoot = "Assets/Scenes";

        [MenuItem("Tools/Iron Knuckle/Rebuild Full Project")]
        public static void BuildAllMenu()
        {
            BuildAll();
            Debug.Log("[RobotBoxingProjectBuilder] Full project rebuild completed successfully.");
        }

        public static void BuildAllBatch()
        {
            try
            {
                BuildAll();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[RobotBoxingProjectBuilder] Batch build finished cleanly.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[RobotBoxingProjectBuilder] Batch build failed: " + ex);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildAll()
        {
            EnsureFolders();
            ConfigureAllTextureImporters();
            var db = CreateMaterialsAndDatabase();
            var volProfile = CreateOrUpdateVolumeProfile();
            BuildMainMenuScene(db, volProfile);
            BuildGarageScene(db, volProfile);
            BuildArenaScene(db, volProfile);
            ConfigureProjectSettings();
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolders()
        {
            string[] dirs =
            {
                "Assets/Resources",
                "Assets/Settings",
                DataRoot,
                DataRoot + "/Parts",
                DataRoot + "/Presets",
                DataRoot + "/Abilities",
                DataRoot + "/Colors",
                DataRoot + "/Opponents",
                MaterialsRoot,
                ScenesRoot
            };
            foreach (var d in dirs)
            {
                if (!AssetDatabase.IsValidFolder(d))
                {
                    string parent = Path.GetDirectoryName(d).Replace('\\', '/');
                    string folder = Path.GetFileName(d);
                    AssetDatabase.CreateFolder(parent, folder);
                }
            }
        }

        private static void ConfigureAllTextureImporters()
        {
            string[] uiGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/UI" });
            foreach (string guid in uiGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                bool changed = false;
                if (imp.textureType != TextureImporterType.Sprite)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    changed = true;
                }
                if (imp.spriteImportMode != SpriteImportMode.Single)
                {
                    imp.spriteImportMode = SpriteImportMode.Single;
                    changed = true;
                }
                if (path.Contains("ui_panel") || path.Contains("ui_button") || path.Contains("ui_bar"))
                {
                    Vector4 targetBorder = path.Contains("ui_bar") ? new Vector4(12, 12, 12, 12) : new Vector4(24, 24, 24, 24);
                    if (imp.spriteBorder != targetBorder)
                    {
                        imp.spriteBorder = targetBorder;
                        changed = true;
                    }
                }
                if (changed) imp.SaveAndReimport();
            }

            string normalPath = "Assets/Art/Textures/armor_stylized_normal.png";
            var nImp = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (nImp != null && nImp.textureType != TextureImporterType.NormalMap)
            {
                nImp.textureType = TextureImporterType.NormalMap;
                nImp.SaveAndReimport();
            }

            string visorPath = "Assets/Art/Textures/visor_faces_atlas.png";
            var vImp = AssetImporter.GetAtPath(visorPath) as TextureImporter;
            if (vImp != null && vImp.wrapMode != TextureWrapMode.Clamp)
            {
                vImp.wrapMode = TextureWrapMode.Clamp;
                vImp.SaveAndReimport();
            }
        }

        private static Mesh LoadMesh(string path)
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m != null) return m;
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in all)
            {
                if (obj is Mesh mesh) return mesh;
            }
            Debug.LogWarning("Mesh not found at: " + path);
            return null;
        }

        private static Sprite LoadUISprite(string fileName) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/" + fileName);

        private static Material CreateOrUpdateMaterial(string fileName, Shader shader, Action<Material> configure)
        {
            string path = MaterialsRoot + "/" + fileName;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            configure?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static VolumeProfile CreateOrUpdateVolumeProfile()
        {
            string path = "Assets/Settings/IronKnuckleVolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            if (!profile.TryGet<Tonemapping>(out var tone)) tone = profile.Add<Tonemapping>(true);
            tone.active = true;
            tone.mode.Override(TonemappingMode.ACES);

            if (!profile.TryGet<Bloom>(out var bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(false);

            if (!profile.TryGet<Vignette>(out var vig)) vig = profile.Add<Vignette>(true);
            vig.active = true;
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);

            if (!profile.TryGet<ColorAdjustments>(out var col)) col = profile.Add<ColorAdjustments>(true);
            col.active = true;
            col.contrast.Override(10f);
            col.saturation.Override(14f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static GameDatabase CreateMaterialsAndDatabase()
        {
            Shader toonShader = Shader.Find("IronKnuckle/StylizedRobotToon") ?? Shader.Find("Universal Render Pipeline/Lit");
            Shader visorShader = Shader.Find("IronKnuckle/VisorEmissive") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");

            var armorTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/armor_stylized_albedo.png");
            var armorNorm = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/armor_stylized_normal.png");
            var finishChrome = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/finish_chrome.png");
            var finishCarbon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/finish_carbon.png");
            var finishHazard = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/finish_hazard.png");
            var finishCircuit = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/finish_circuit.png");

            var hazardTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/hazard_stripes.png");
            var ringTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/ring_canvas.png");
            var garageFloorTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/garage_floor_grid.png");
            var visorTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/visor_faces_atlas.png");

            var matRobot = CreateOrUpdateMaterial("Mat_RobotBase.mat", toonShader, m =>
            {
                m.SetTexture("_BaseMap", armorTex);
                m.SetTexture("_BumpMap", armorNorm);
                m.SetFloat("_BumpScale", 0.9f);
                m.SetColor("_BaseColor", new Color(0.22f, 0.56f, 0.96f, 1f));
                m.SetColor("_ShadeTint", new Color(0.42f, 0.48f, 0.66f, 1f));
                m.SetFloat("_Steps", 3f);
                m.SetFloat("_MetallicSpec", 0.85f);
                m.SetFloat("_OutlineWidth", 0.011f);
            });

            var matGunmetal = CreateOrUpdateMaterial("Mat_GunmetalBase.mat", toonShader, m =>
            {
                m.SetTexture("_BaseMap", finishChrome);
                m.SetTexture("_BumpMap", armorNorm);
                m.SetFloat("_BumpScale", 0.7f);
                m.SetColor("_BaseColor", new Color(0.20f, 0.23f, 0.30f, 1f));
                m.SetColor("_ShadeTint", new Color(0.35f, 0.38f, 0.48f, 1f));
                m.SetFloat("_Steps", 3f);
                m.SetFloat("_MetallicSpec", 1.55f);
                m.SetFloat("_SpecSmoothness", 0.78f);
                m.SetFloat("_OutlineWidth", 0.009f);
            });

            var matEmissive = CreateOrUpdateMaterial("Mat_EmissiveBase.mat", toonShader, m =>
            {
                m.SetTexture("_BaseMap", armorTex);
                m.SetColor("_BaseColor", new Color(1f, 0.65f, 0.15f, 1f));
                m.SetColor("_EmissionColor", new Color(1.4f, 0.8f, 0.2f, 1f));
                m.SetFloat("_Steps", 2f);
                m.SetFloat("_OutlineWidth", 0.007f);
            });

            var matVisor = CreateOrUpdateMaterial("Mat_VisorBase.mat", visorShader, m =>
            {
                m.SetTexture("_BaseMap", visorTex);
                m.SetColor("_Color", new Color(0.15f, 0.92f, 1f, 1f));
                m.SetFloat("_EmissionStrength", 2.1f);
            });

            CreateOrUpdateMaterial("Mat_RingCanvas.mat", urpLit, m =>
            {
                m.SetTexture("_BaseMap", ringTex);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.30f);
            });

            CreateOrUpdateMaterial("Mat_GarageFloor.mat", urpLit, m =>
            {
                m.SetTexture("_BaseMap", garageFloorTex);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_Smoothness", 0.48f);
            });

            CreateOrUpdateMaterial("Mat_ArenaMetal.mat", toonShader, m =>
            {
                m.SetTexture("_BaseMap", armorTex);
                m.SetTexture("_BumpMap", armorNorm);
                m.SetColor("_BaseColor", new Color(0.18f, 0.22f, 0.30f, 1f));
                m.SetFloat("_OutlineWidth", 0.008f);
            });

            CreateOrUpdateMaterial("Mat_Hazard.mat", urpLit, m =>
            {
                m.SetTexture("_BaseMap", hazardTex);
                m.SetColor("_BaseColor", Color.white);
            });

            CreateOrUpdateMaterial("Mat_Billboard.mat", urpUnlit, m =>
            {
                m.SetColor("_BaseColor", new Color(0.10f, 0.65f, 1.0f, 1f));
            });

            CreateOrUpdateMaterial("Mat_RingRopeRed.mat", urpLit, m => m.SetColor("_BaseColor", new Color(0.92f, 0.20f, 0.22f, 1f)));
            CreateOrUpdateMaterial("Mat_RingRopeWhite.mat", urpLit, m => m.SetColor("_BaseColor", new Color(0.94f, 0.96f, 1f, 1f)));
            CreateOrUpdateMaterial("Mat_RingRopeBlue.mat", urpLit, m => m.SetColor("_BaseColor", new Color(0.18f, 0.56f, 0.98f, 1f)));

            string dbPath = "Assets/Resources/GameDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }

            db.visorScreenMesh = LoadMesh("Assets/Art/Models/Parts/head_visor_screen.obj");
            db.pelvisMesh = LoadMesh("Assets/Art/Models/Parts/pelvis_hip.obj");
            db.shoulderPadMesh = LoadMesh("Assets/Art/Models/Parts/shoulder_pad.obj");
            db.upperArmMesh = LoadMesh("Assets/Art/Models/Parts/upper_arm.obj");
            db.robotBaseMaterial = matRobot;
            db.gunmetalBaseMaterial = matGunmetal;
            db.emissiveBaseMaterial = matEmissive;
            db.visorBaseMaterial = matVisor;
            db.armorNormalMap = armorNorm;
            db.surfaceFinishTextures = new[] { armorTex, finishChrome, finishCarbon, finishHazard, finishCircuit };

            db.parts = CreateAllParts();
            db.presets = CreateAllPresets();
            db.abilities = CreateAllAbilities();
            db.colorSchemes = CreateAllColors();
            db.opponents = CreateAllOpponents();

            EditorUtility.SetDirty(db);
            return db;
        }

        private static T CreateOrLoadSO<T>(string assetPath) where T : ScriptableObject
        {
            var obj = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (obj == null)
            {
                obj = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(obj, assetPath);
            }
            return obj;
        }

        private static List<RobotPartData> CreateAllParts()
        {
            var list = new List<RobotPartData>();
            void AddPart(string id, string name, string desc, PartSlot slot, PartRarity rarity, RobotBrand brand, string objFile,
                float hp, float atk, float spd, float def, float stm, float crit, float od, int cost, int upScrap, int upShards)
            {
                var p = CreateOrLoadSO<RobotPartData>($"{DataRoot}/Parts/{id}.asset");
                p.id = id;
                p.displayName = name;
                p.description = desc;
                p.slot = slot;
                p.rarity = rarity;
                p.brand = brand;
                p.mesh = LoadMesh("Assets/Art/Models/Parts/" + objFile);
                p.meshScale = Vector3.one;
                p.baseStats = new RobotStats
                {
                    maxHp = hp, attack = atk, speed = spd, defense = def, stamina = stm, critChance = crit, overdriveGain = od
                };
                p.buyCostScrap = cost;
                p.upgradeBaseScrap = upScrap;
                p.upgradeBaseShards = upShards;
                EditorUtility.SetDirty(p);
                list.Add(p);
            }

            // 12 HEADS
            AddPart("head_classic", "Scrap Visor", "Reliable starter boxing helmet with reinforced jaw.", PartSlot.Head, PartRarity.Common, RobotBrand.Scrapworks, "head_classic.obj", 30, 3, 4, 4, 5, 0.03f, 2, 0, 70, 4);
            AddPart("head_crt_retro", "Retro CRT-Box", "Vintage arcade monitor chassis with crisp target tracking.", PartSlot.Head, PartRarity.Common, RobotBrand.Scrapworks, "head_crt_retro.obj", 36, 4, 3, 5, 6, 0.05f, 3, 110, 75, 4);
            AddPart("head_cage", "Iron Cage Mask", "Welded steel bar-grill built to absorb uppercuts.", PartSlot.Head, PartRarity.Common, RobotBrand.TitanForge, "head_cage.obj", 44, 4, 2, 7, 5, 0.02f, 2, 130, 80, 5);
            AddPart("head_cyclops", "Cyclops Optic", "Heavy targeting dome boosting critical precision.", PartSlot.Head, PartRarity.Rare, RobotBrand.VoltTech, "head_cyclops.obj", 42, 6, 4, 6, 6, 0.08f, 4, 160, 95, 6);
            AddPart("head_horned", "Bullhorn Helm", "Aggressive twin-horn chassis built for brawlers.", PartSlot.Head, PartRarity.Rare, RobotBrand.TitanForge, "head_horned.obj", 55, 8, 2, 7, 6, 0.05f, 3, 220, 110, 7);
            AddPart("head_mohawk", "Spark Mohawk", "Rebellious street circuit crest with overclocked audio-sensors.", PartSlot.Head, PartRarity.Rare, RobotBrand.PyroCore, "head_mohawk.obj", 44, 9, 7, 4, 7, 0.07f, 4, 240, 115, 7);
            AddPart("head_samurai", "Ronin Crest", "Aerodynamic battle helm with rapid reflex sensors.", PartSlot.Head, PartRarity.Epic, RobotBrand.MantisLabs, "head_samurai.obj", 48, 7, 11, 5, 8, 0.09f, 5, 340, 135, 9);
            AddPart("head_valkyrie", "Valkyrie Wing", "Winged aero-stabilizer head for lightning slips.", PartSlot.Head, PartRarity.Epic, RobotBrand.MantisLabs, "head_valkyrie.obj", 50, 8, 12, 6, 9, 0.08f, 5, 380, 145, 9);
            AddPart("head_tesla", "Tesla Dome", "Arc-capacitor crown accelerating Overdrive charge.", PartSlot.Head, PartRarity.Epic, RobotBrand.VoltTech, "head_tesla.obj", 54, 8, 6, 6, 9, 0.07f, 11, 410, 150, 10);
            AddPart("head_arachnid", "Arachnid Quad", "Four-lens predatory scanner locking weak points.", PartSlot.Head, PartRarity.Epic, RobotBrand.PyroCore, "head_arachnid.obj", 58, 11, 7, 7, 9, 0.11f, 6, 470, 165, 11);
            AddPart("head_overlord", "Dreadnought Helm", "Massive armored siege helm with heavy brow plating.", PartSlot.Head, PartRarity.Legendary, RobotBrand.TitanForge, "head_overlord.obj", 88, 12, 4, 13, 10, 0.06f, 6, 640, 185, 13);
            AddPart("head_apex", "Apex Crown", "Championship-grade command head with top sensors.", PartSlot.Head, PartRarity.Legendary, RobotBrand.ApexCorp, "head_apex.obj", 78, 13, 10, 10, 12, 0.12f, 9, 720, 195, 14);

            // 12 TORSOS
            AddPart("torso_scrap", "Barrel Chassis", "Welded steel drum torso for balanced rookie brawls.", PartSlot.Torso, PartRarity.Common, RobotBrand.Scrapworks, "torso_scrap.obj", 85, 5, 3, 8, 12, 0.01f, 2, 0, 80, 5);
            AddPart("torso_ribcage", "Piston Ribcage", "Open-frame pneumatic torso with lightweight ribs.", PartSlot.Torso, PartRarity.Common, RobotBrand.Scrapworks, "torso_ribcage.obj", 80, 7, 6, 6, 15, 0.03f, 3, 130, 85, 5);
            AddPart("torso_aero", "Aero Frame", "Sleek V-tapered frame built for dodging and speed.", PartSlot.Torso, PartRarity.Rare, RobotBrand.MantisLabs, "torso_aero.obj", 95, 7, 11, 7, 16, 0.03f, 4, 210, 105, 7);
            AddPart("torso_dozer", "Bulldozer Hull", "Broad street-sweeper chest plate with ramming bumper.", PartSlot.Torso, PartRarity.Rare, RobotBrand.TitanForge, "torso_dozer.obj", 132, 9, 1, 13, 13, 0.02f, 3, 230, 110, 7);
            AddPart("torso_anvil", "Anvil Plate", "Ultra-dense frontal plating that shrugs off hooks.", PartSlot.Torso, PartRarity.Rare, RobotBrand.TitanForge, "torso_anvil.obj", 145, 6, -2, 16, 12, 0.01f, 3, 260, 115, 7);
            AddPart("torso_turbine", "Twin-Jet Chest", "Dual intake turbines feeding stamina & rush speed.", PartSlot.Torso, PartRarity.Rare, RobotBrand.VoltTech, "torso_turbine.obj", 110, 9, 10, 8, 20, 0.04f, 7, 290, 125, 8);
            AddPart("torso_mantis", "Mantis Shell", "Lightweight kinetic frame tuned for counter combos.", PartSlot.Torso, PartRarity.Epic, RobotBrand.MantisLabs, "torso_mantis.obj", 115, 11, 13, 9, 18, 0.05f, 5, 420, 145, 10);
            AddPart("torso_shogun", "Shogun Lamellar", "Layered cyber-samurai chest armor with balanced poise.", PartSlot.Torso, PartRarity.Epic, RobotBrand.VoltTech, "torso_shogun.obj", 138, 12, 9, 13, 18, 0.05f, 7, 460, 150, 10);
            AddPart("torso_furnace", "Furnace Boiler", "Pressurized steam core torso with high stamina output.", PartSlot.Torso, PartRarity.Epic, RobotBrand.PyroCore, "torso_furnace.obj", 155, 13, 4, 15, 24, 0.03f, 6, 510, 160, 11);
            AddPart("torso_vault", "Bank Vault Door", "Impervious fortress bulkhead with massive armor HP.", PartSlot.Torso, PartRarity.Legendary, RobotBrand.TitanForge, "torso_vault.obj", 215, 10, -3, 24, 16, 0.02f, 5, 700, 195, 14);
            AddPart("torso_exo", "Quantum Exo", "High-energy field chassis boosting power & agility.", PartSlot.Torso, PartRarity.Legendary, RobotBrand.ApexCorp, "torso_exo.obj", 168, 17, 14, 15, 24, 0.07f, 10, 760, 205, 14);
            AddPart("torso_titan", "Titan Aegis", "Legend-tier reinforced championship armor.", PartSlot.Torso, PartRarity.Legendary, RobotBrand.ApexCorp, "torso_titan.obj", 195, 16, 8, 20, 22, 0.05f, 8, 840, 215, 15);

            // 12 LEFT ARMS & 12 RIGHT ARMS
            void AddArmPair(string slug, string baseName, string desc, PartRarity rarity, RobotBrand brand, string objFile,
                float hp, float atkL, float atkR, float spd, float def, float stm, float crit, float od, int cost, int upScrap, int upShards)
            {
                AddPart("larm_" + slug, baseName + " Left", desc, PartSlot.LeftArm, rarity, brand, objFile,
                    hp, atkL, spd, def, stm, crit, od, cost, upScrap, upShards);
                AddPart("rarm_" + slug, baseName + " Right", desc, PartSlot.RightArm, rarity, brand, objFile,
                    hp, atkR, spd, def, stm, crit + 0.01f, od, cost == 0 ? 0 : cost + 10, upScrap, upShards);
            }

            AddArmPair("riveted", "Riveted", "Standard steel-knuckle boxing glove.", PartRarity.Common, RobotBrand.Scrapworks, "glove_riveted.obj", 18, 10, 12, 5, 3, 6, 0.02f, 2, 0, 75, 4);
            AddArmPair("wreckball", "Wrecking Ball", "Heavy iron sphere fist with raw crushing momentum.", PartRarity.Common, RobotBrand.Scrapworks, "glove_wreckball.obj", 24, 14, 16, 1, 4, 5, 0.03f, 2, 120, 80, 5);
            AddArmPair("buzzguard", "Buzzguard", "Circular armored cuff built for tight parries.", PartRarity.Common, RobotBrand.TitanForge, "glove_buzzguard.obj", 25, 13, 15, 4, 6, 6, 0.03f, 3, 145, 85, 5);
            AddArmPair("shock", "Volt Shock", "Electrified knuckle coil that builds Overdrive.", PartRarity.Rare, RobotBrand.VoltTech, "glove_shock.obj", 22, 15, 17, 7, 3, 7, 0.05f, 7, 180, 100, 6);
            AddArmPair("piledriver", "Piledriver", "Heavy hydraulic ram fist for shattering guards.", PartRarity.Rare, RobotBrand.TitanForge, "glove_piledriver.obj", 30, 19, 22, 1, 6, 5, 0.03f, 3, 240, 115, 7);
            AddArmPair("jackhammer", "Jackhammer", "Pneumatic piston fist delivering rapid recoil strikes.", PartRarity.Rare, RobotBrand.PyroCore, "glove_jackhammer.obj", 28, 20, 22, 5, 5, 7, 0.05f, 4, 275, 120, 8);
            AddArmPair("mantis", "Mantis Blade", "Razor-fast light fist for lightning jab combos.", PartRarity.Epic, RobotBrand.MantisLabs, "glove_mantis.obj", 24, 17, 19, 13, 3, 9, 0.08f, 5, 370, 140, 9);
            AddArmPair("shield", "Riot Buckler", "Armored forearm buckler maximizing guard defense.", PartRarity.Epic, RobotBrand.TitanForge, "glove_shield.obj", 38, 18, 20, 4, 12, 7, 0.04f, 4, 410, 145, 10);
            AddArmPair("magma", "Magma Piston", "Superheated exhaust fist with explosive impact.", PartRarity.Epic, RobotBrand.PyroCore, "glove_magma.obj", 32, 24, 27, 5, 5, 8, 0.07f, 6, 460, 155, 11);
            AddArmPair("plasmaclaw", "Plasma Claw", "Triple plasma-knuckle gauntlet with lethal crits.", PartRarity.Epic, RobotBrand.VoltTech, "glove_plasmaclaw.obj", 28, 23, 25, 10, 4, 9, 0.11f, 8, 520, 165, 11);
            AddArmPair("gravity", "Gravity Orb", "Magnetic singularity fist pulling targets off-balance.", PartRarity.Legendary, RobotBrand.ApexCorp, "glove_gravity.obj", 38, 28, 30, 10, 7, 10, 0.10f, 10, 690, 190, 13);
            AddArmPair("titan", "Titan Mega", "Supreme championship power gauntlet.", PartRarity.Legendary, RobotBrand.ApexCorp, "glove_titan.obj", 42, 30, 33, 9, 8, 10, 0.10f, 8, 760, 200, 14);

            // 12 LEGS
            AddPart("legs_spring", "Spring Struts", "Bouncy coil legs for agile rookie footwork.", PartSlot.Legs, PartRarity.Common, RobotBrand.Scrapworks, "leg_spring.obj", 28, 2, 7, 4, 14, 0.01f, 2, 0, 70, 4);
            AddPart("legs_wheels", "Roller Skates", "Motorized wheel boots for quick lateral slips.", PartSlot.Legs, PartRarity.Common, RobotBrand.Scrapworks, "leg_wheels.obj", 30, 3, 11, 3, 16, 0.02f, 3, 115, 75, 5);
            AddPart("legs_sumo", "Sumo Pillars", "Wide heavy stance legs nearly impossible to knock down.", PartSlot.Legs, PartRarity.Common, RobotBrand.TitanForge, "leg_sumo.obj", 52, 4, 1, 11, 12, 0.01f, 2, 140, 85, 5);
            AddPart("legs_hover", "Hover Thrusters", "Micro-thruster calves enabling rapid slips.", PartSlot.Legs, PartRarity.Rare, RobotBrand.VoltTech, "leg_hover.obj", 34, 3, 14, 4, 18, 0.02f, 5, 190, 95, 6);
            AddPart("legs_tread", "Tread Stompers", "Wide armored tank boots anchoring heavy blows.", PartSlot.Legs, PartRarity.Rare, RobotBrand.TitanForge, "leg_tread.obj", 58, 5, 2, 12, 14, 0.01f, 2, 230, 110, 7);
            AddPart("legs_reverse", "Raptor Reverse", "Digitigrade mech joints built for explosive lunges.", PartSlot.Legs, PartRarity.Rare, RobotBrand.MantisLabs, "leg_reverse.obj", 38, 6, 15, 4, 19, 0.04f, 4, 270, 120, 7);
            AddPart("legs_strider", "Aero Striders", "Precision servo legs for lightning-fast evasion.", PartSlot.Legs, PartRarity.Epic, RobotBrand.MantisLabs, "leg_strider.obj", 44, 4, 18, 6, 22, 0.04f, 5, 360, 135, 9);
            AddPart("legs_hakama", "Shogun Hakama", "Armored cyber-skirts concealing reinforced knee servos.", PartSlot.Legs, PartRarity.Epic, RobotBrand.VoltTech, "leg_hakama.obj", 56, 7, 11, 11, 20, 0.04f, 6, 410, 145, 9);
            AddPart("legs_hydraulic", "Hydraulic Pistons", "Pressurized pistons adding drive to every punch.", PartSlot.Legs, PartRarity.Epic, RobotBrand.PyroCore, "leg_hydraulic.obj", 64, 9, 8, 12, 20, 0.03f, 4, 450, 150, 10);
            AddPart("legs_jetboot", "Rocket Jetboots", "Rear-mounted rocket boosters driving ferocious hooks.", PartSlot.Legs, PartRarity.Epic, RobotBrand.PyroCore, "leg_jetboot.obj", 54, 10, 14, 8, 24, 0.05f, 6, 510, 160, 11);
            AddPart("legs_quantum", "Quantum Struts", "Energy-stabilized combat legs with high Overdrive flow.", PartSlot.Legs, PartRarity.Legendary, RobotBrand.ApexCorp, "leg_quantum.obj", 68, 9, 17, 12, 28, 0.06f, 8, 650, 185, 13);
            AddPart("legs_apex", "Apex Greaves", "Pro-circuit carbon-titanium combat legs.", PartSlot.Legs, PartRarity.Legendary, RobotBrand.ApexCorp, "leg_apex.obj", 78, 11, 15, 15, 26, 0.05f, 7, 720, 195, 14);

            // 12 POWER CORES
            AddPart("core_battery", "Lead Cell", "Basic backup battery pack.", PartSlot.Core, PartRarity.Common, RobotBrand.Scrapworks, "core_battery.obj", 16, 2, 2, 2, 16, 0.01f, 5, 0, 65, 4);
            AddPart("core_v8pipes", "V8 Exhaust Pack", "Dual roaring exhaust stacks pumping stamina.", PartSlot.Core, PartRarity.Common, RobotBrand.Scrapworks, "core_v8pipes.obj", 20, 5, 3, 2, 22, 0.02f, 6, 115, 75, 5);
            AddPart("core_volt", "Twin Volt Coils", "Dual capacitor cylinders that rapidly charge specials.", PartSlot.Core, PartRarity.Rare, RobotBrand.VoltTech, "core_volt.obj", 22, 4, 5, 3, 20, 0.03f, 12, 170, 95, 6);
            AddPart("core_flywheel", "Kinetic Flywheel", "Spinning gyro core converting blocks into energy.", PartSlot.Core, PartRarity.Rare, RobotBrand.TitanForge, "core_flywheel.obj", 34, 6, 4, 8, 22, 0.02f, 9, 220, 105, 7);
            AddPart("core_jetpack", "Twin Thruster", "Dual-nozzle boost pack amplifying lunge speed.", PartSlot.Core, PartRarity.Rare, RobotBrand.MantisLabs, "core_jetpack.obj", 24, 6, 12, 3, 24, 0.04f, 9, 260, 115, 7);
            AddPart("core_teslatower", "Tesla Arc Tower", "High-voltage spire supercharging Special Attacks.", PartSlot.Core, PartRarity.Epic, RobotBrand.VoltTech, "core_teslatower.obj", 28, 7, 6, 4, 24, 0.05f, 17, 340, 130, 9);
            AddPart("core_plasma", "Plasma Heart", "High-output reactor boosting stamina and crit.", PartSlot.Core, PartRarity.Epic, RobotBrand.PyroCore, "core_plasma.obj", 35, 8, 7, 5, 28, 0.06f, 14, 370, 135, 9);
            AddPart("core_pods", "Shoulder Pods", "Twin heavy ordnance packs increasing raw punch power.", PartSlot.Core, PartRarity.Epic, RobotBrand.TitanForge, "core_pods.obj", 44, 13, 3, 9, 22, 0.05f, 10, 420, 145, 10);
            AddPart("core_fusion", "Fusion Stack", "Twin exhaust thermal engine for relentless combos.", PartSlot.Core, PartRarity.Epic, RobotBrand.PyroCore, "core_fusion.obj", 45, 11, 6, 8, 32, 0.05f, 15, 460, 155, 11);
            AddPart("core_halo", "Halo Ring Core", "Orbital gyro ring radiating constant defensive energy.", PartSlot.Core, PartRarity.Legendary, RobotBrand.MantisLabs, "core_halo.obj", 48, 9, 14, 10, 32, 0.08f, 16, 620, 180, 12);
            AddPart("core_quantum", "Quantum Wings", "Winged apex reactor maximizing Overdrive and speed.", PartSlot.Core, PartRarity.Legendary, RobotBrand.ApexCorp, "core_quantum.obj", 55, 12, 12, 10, 38, 0.08f, 20, 720, 195, 14);
            AddPart("core_angelwings", "Seraphim Wings", "Iconic championship wing-array with peak balanced power.", PartSlot.Core, PartRarity.Legendary, RobotBrand.ApexCorp, "core_angelwings.obj", 62, 15, 14, 12, 40, 0.09f, 22, 850, 215, 15);

            return list;
        }

        private static List<RobotPresetData> CreateAllPresets()
        {
            var list = new List<RobotPresetData>();
            void AddPreset(string id, string name, string tagline, string arch, int cost, bool starter,
                string h, string t, string la, string ra, string l, string c, string col)
            {
                var p = CreateOrLoadSO<RobotPresetData>($"{DataRoot}/Presets/{id}.asset");
                p.id = id;
                p.displayName = name;
                p.tagline = tagline;
                p.archetype = arch;
                p.buyCostScrap = cost;
                p.isStarter = starter;
                p.headPartId = h;
                p.torsoPartId = t;
                p.leftArmPartId = la;
                p.rightArmPartId = ra;
                p.legsPartId = l;
                p.corePartId = c;
                p.defaultColorSchemeId = col;
                EditorUtility.SetDirty(p);
                list.Add(p);
            }

            AddPreset("preset_rusty_jab", "Rusty Jab", "Scrapworks starter underdog (3pc+ Scrapworks Set).", "All-Rounder", 0, true,
                "head_classic", "torso_scrap", "larm_riveted", "rarm_riveted", "legs_spring", "core_battery", "color_cobalt");
            AddPreset("preset_volt_runner", "Volt Runner", "Full Volt-Tech circuit runner with shock knuckles.", "Speed / Overdrive", 620, false,
                "head_cyclops", "torso_turbine", "larm_shock", "rarm_shock", "legs_hover", "core_volt", "color_neon_cyan");
            AddPreset("preset_iron_anvil", "Iron Anvil", "TitanForge siege fortress built to shatter guards.", "Heavy Tank", 850, false,
                "head_horned", "torso_anvil", "larm_piledriver", "rarm_piledriver", "legs_tread", "core_flywheel", "color_hazard");
            AddPreset("preset_crimson_mantis", "Crimson Mantis", "Mantis Labs duelist with lethal counter crits.", "Counter Striker", 1350, false,
                "head_samurai", "torso_mantis", "larm_mantis", "rarm_mantis", "legs_strider", "core_jetpack", "color_crimson");
            AddPreset("preset_magma_forge", "Magma Forge", "PyroCore thermal powerhouse with endless stamina.", "Power Slugger", 1750, false,
                "head_arachnid", "torso_furnace", "larm_magma", "rarm_magma", "legs_hydraulic", "core_fusion", "color_magma");
            AddPreset("preset_apex_titan", "Apex Seraph", "Ultimate Apex Corp champion with Seraphim Wings.", "Apex Champion", 3200, false,
                "head_apex", "torso_titan", "larm_titan", "rarm_titan", "legs_apex", "core_angelwings", "color_gold");

            return list;
        }

        private static List<CombatAbilityData> CreateAllAbilities()
        {
            var list = new List<CombatAbilityData>();
            void AddActive(string id, string name, string desc, int scrap, int shards, float odCost, float dmgMult, float gb, float stun, Color col)
            {
                var a = CreateOrLoadSO<CombatAbilityData>($"{DataRoot}/Abilities/{id}.asset");
                a.id = id;
                a.displayName = name;
                a.description = desc;
                a.isPassive = false;
                a.unlockCostScrap = scrap;
                a.unlockCostShards = shards;
                a.overdriveCost = odCost;
                a.damageMultiplier = dmgMult;
                a.guardBreakBonus = gb;
                a.stunDuration = stun;
                a.hitCount = 1;
                a.accentColor = col;
                EditorUtility.SetDirty(a);
                list.Add(a);
            }

            void AddPassive(string id, string name, string desc, int scrap, int shards, float hpB, float stmRegenB, float counterB, float odB, Color col)
            {
                var a = CreateOrLoadSO<CombatAbilityData>($"{DataRoot}/Abilities/{id}.asset");
                a.id = id;
                a.displayName = name;
                a.description = desc;
                a.isPassive = true;
                a.unlockCostScrap = scrap;
                a.unlockCostShards = shards;
                a.passiveHpBonus = hpB;
                a.passiveStaminaRegenBonus = stmRegenB;
                a.passiveCounterDamageBonus = counterB;
                a.passiveOverdriveBonus = odB;
                a.accentColor = col;
                EditorUtility.SetDirty(a);
                list.Add(a);
            }

            AddActive("ability_rocket_upper", "Rocket Uppercut", "Explosive rising uppercut dealing 2.1x damage and brief stun.", 0, 0, 50f, 2.10f, 0.35f, 0.45f, new Color(1f, 0.55f, 0.1f));
            AddActive("ability_emp_hook", "EMP Shock Hook", "High-voltage arc hook that stuns the opponent for 1.1s.", 220, 12, 55f, 1.90f, 0.40f, 1.10f, new Color(0.2f, 0.9f, 1f));
            AddActive("ability_guard_crusher", "Guard Crusher", "Massive twin ram strike with +120% Guard Break power.", 280, 15, 50f, 2.05f, 1.20f, 0.50f, new Color(1f, 0.3f, 0.25f));
            AddActive("ability_hydraulic_flurry", "Piston Flurry", "Unstoppable rapid combo barrage dealing 2.45x damage.", 360, 20, 65f, 2.45f, 0.60f, 0.60f, new Color(0.95f, 0.4f, 0.95f));
            AddActive("ability_magma_haymaker", "Magma Haymaker", "Superheated knockout hook with massive 2.75x damage.", 480, 25, 75f, 2.75f, 0.85f, 0.85f, new Color(1f, 0.35f, 0.1f));
            AddActive("ability_quantum_overdrive", "Quantum Nova", "Apex finisher unleashing 3.2x devastating championship force.", 650, 35, 90f, 3.20f, 1.40f, 1.25f, new Color(1f, 0.82f, 0.2f));

            AddPassive("passive_titan_plating", "Titanium Sub-Frame", "Passive Mod: +65 Max Armor HP in every match.", 240, 12, 65f, 0f, 0f, 0f, new Color(0.35f, 0.85f, 0.55f));
            AddPassive("passive_coolant_vents", "Turbo Coolant Vents", "Passive Mod: +9 Stamina regeneration per second.", 260, 14, 0f, 9f, 0f, 0f, new Color(0.25f, 0.85f, 1f));
            AddPassive("passive_counter_chip", "Counter Reflex Chip", "Passive Mod: +35% extra damage on Counter-Punches.", 340, 18, 0f, 0f, 0.35f, 0f, new Color(1f, 0.7f, 0.2f));
            AddPassive("passive_dynamo_core", "Overdrive Dynamo", "Passive Mod: +18 Overdrive Gain rate from punches.", 400, 22, 0f, 0f, 0f, 18f, new Color(0.8f, 0.4f, 1f));

            return list;
        }

        private static List<ColorSchemeData> CreateAllColors()
        {
            var list = new List<ColorSchemeData>();
            void AddColor(string id, string name, Color pri, Color sec, Color acc, Color vis, int cost)
            {
                var c = CreateOrLoadSO<ColorSchemeData>($"{DataRoot}/Colors/{id}.asset");
                c.id = id;
                c.displayName = name;
                c.primaryColor = pri;
                c.secondaryColor = sec;
                c.accentColor = acc;
                c.visorColor = vis;
                c.unlockCostScrap = cost;
                EditorUtility.SetDirty(c);
                list.Add(c);
            }

            AddColor("color_cobalt", "Cobalt Striker", new Color(0.18f, 0.52f, 0.94f), new Color(0.18f, 0.22f, 0.28f), new Color(1f, 0.62f, 0.12f), new Color(0.15f, 0.92f, 1f), 0);
            AddColor("color_crimson", "Crimson Viper", new Color(0.86f, 0.16f, 0.22f), new Color(0.16f, 0.16f, 0.20f), new Color(1f, 0.82f, 0.22f), new Color(1f, 0.35f, 0.25f), 0);
            AddColor("color_hazard", "Hazard Yellow", new Color(0.95f, 0.72f, 0.12f), new Color(0.15f, 0.17f, 0.22f), new Color(0.95f, 0.26f, 0.18f), new Color(1f, 0.88f, 0.25f), 90);
            AddColor("color_neon_cyan", "Neon Cyber", new Color(0.12f, 0.82f, 0.88f), new Color(0.12f, 0.15f, 0.25f), new Color(0.95f, 0.25f, 0.85f), new Color(0.2f, 1f, 0.95f), 120);
            AddColor("color_emerald", "Emerald Venom", new Color(0.16f, 0.76f, 0.42f), new Color(0.15f, 0.19f, 0.22f), new Color(0.75f, 1f, 0.25f), new Color(0.35f, 1f, 0.45f), 140);
            AddColor("color_magma", "Magma Core", new Color(0.24f, 0.16f, 0.18f), new Color(0.12f, 0.10f, 0.12f), new Color(1f, 0.38f, 0.08f), new Color(1f, 0.52f, 0.12f), 180);
            AddColor("color_phantom", "Phantom White", new Color(0.90f, 0.93f, 0.97f), new Color(0.22f, 0.26f, 0.34f), new Color(0.20f, 0.75f, 1f), new Color(0.25f, 0.90f, 1f), 220);
            AddColor("color_gold", "Apex Champion Gold", new Color(0.98f, 0.76f, 0.18f), new Color(0.14f, 0.14f, 0.20f), new Color(0.25f, 0.92f, 1f), new Color(1f, 0.92f, 0.45f), 320);

            return list;
        }

        private static List<OpponentData> CreateAllOpponents()
        {
            var list = new List<OpponentData>();
            void AddOpp(string id, string name, string title, int league, int match, bool boss, AIStyle style, float diff,
                string h, string t, string la, string ra, string l, string c, int lvl, string col, string sp, int sc, int sh, string quote)
            {
                var o = CreateOrLoadSO<OpponentData>($"{DataRoot}/Opponents/{id}.asset");
                o.id = id;
                o.fighterName = name;
                o.title = title;
                o.leagueIndex = league;
                o.matchIndex = match;
                o.isBoss = boss;
                o.aiStyle = style;
                o.aiDifficulty = diff;
                o.headPartId = h;
                o.torsoPartId = t;
                o.leftArmPartId = la;
                o.rightArmPartId = ra;
                o.legsPartId = l;
                o.corePartId = c;
                o.partLevel = lvl;
                o.colorSchemeId = col;
                o.specialAbilityId = sp;
                o.rewardScrap = sc;
                o.rewardShards = sh;
                o.quote = quote;
                EditorUtility.SetDirty(o);
                list.Add(o);
            }

            AddOpp("opp_01_tin_man", "Tin Man", "Rookie Rustbucket", 0, 0, false, AIStyle.Swarmer, 0.20f,
                "head_crt_retro", "torso_scrap", "larm_riveted", "rarm_riveted", "legs_spring", "core_battery", 1, "color_hazard", "ability_rocket_upper", 130, 7, "Hope your bolts are tightened!");
            AddOpp("opp_02_sparky", "Sparky", "Garage Welder", 0, 1, false, AIStyle.OutBoxer, 0.28f,
                "head_cyclops", "torso_ribcage", "larm_shock", "rarm_riveted", "legs_wheels", "core_v8pipes", 1, "color_emerald", "ability_emp_hook", 160, 9, "Catch a face full of static!");
            AddOpp("opp_03_bulldozer", "Bulldozer", "Scrapyard Boss", 0, 2, true, AIStyle.Slugger, 0.38f,
                "head_cage", "torso_dozer", "larm_wreckball", "rarm_piledriver", "legs_sumo", "core_flywheel", 2, "color_crimson", "ability_guard_crusher", 240, 14, "Nothing survives the crusher!");

            AddOpp("opp_04_neon_kid", "Neon Kid", "Alley Dash", 1, 0, false, AIStyle.Swarmer, 0.45f,
                "head_mohawk", "torso_aero", "larm_shock", "rarm_shock", "legs_hover", "core_volt", 2, "color_neon_cyan", "ability_emp_hook", 270, 14, "Too fast for your optics!");
            AddOpp("opp_05_ronin_x", "Ronin-X", "Cyber Duelist", 1, 1, false, AIStyle.CounterPuncher, 0.52f,
                "head_samurai", "torso_shogun", "larm_mantis", "rarm_shock", "legs_hakama", "core_plasma", 2, "color_phantom", "ability_hydraulic_flurry", 320, 16, "Show me your rhythm.");
            AddOpp("opp_06_volt_king", "Volt King", "Street Circuit Boss", 1, 2, true, AIStyle.OutBoxer, 0.60f,
                "head_tesla", "torso_turbine", "larm_plasmaclaw", "rarm_piledriver", "legs_hover", "core_teslatower", 3, "color_cobalt", "ability_emp_hook", 420, 22, "Feel 10,000 volts in the ring!");

            AddOpp("opp_07_anvil_9", "Anvil-9", "Heavy Forged", 2, 0, false, AIStyle.Slugger, 0.66f,
                "head_horned", "torso_anvil", "larm_shield", "rarm_piledriver", "legs_tread", "core_pods", 3, "color_hazard", "ability_guard_crusher", 470, 24, "Try blocking this, lightweight!");
            AddOpp("opp_08_cinder", "Cinder", "Boiler Bruiser", 2, 1, false, AIStyle.Swarmer, 0.72f,
                "head_arachnid", "torso_furnace", "larm_jackhammer", "rarm_magma", "legs_jetboot", "core_fusion", 4, "color_magma", "ability_magma_haymaker", 540, 28, "The foundry is heating up!");
            AddOpp("opp_09_magma_lord", "Magma Lord", "Foundry Boss", 2, 2, true, AIStyle.Slugger, 0.80f,
                "head_overlord", "torso_vault", "larm_magma", "rarm_magma", "legs_hydraulic", "core_fusion", 4, "color_magma", "ability_magma_haymaker", 700, 35, "Melt in the furnace!");

            AddOpp("opp_10_phantom_0", "Phantom Zero", "Shadow Boxer", 3, 0, false, AIStyle.CounterPuncher, 0.85f,
                "head_valkyrie", "torso_mantis", "larm_mantis", "rarm_mantis", "legs_reverse", "core_halo", 4, "color_phantom", "ability_hydraulic_flurry", 780, 38, "Every opening is mine.");
            AddOpp("opp_11_aegis_prime", "Aegis Prime", "Grand Semifinalist", 3, 1, false, AIStyle.OutBoxer, 0.92f,
                "head_apex", "torso_exo", "larm_gravity", "rarm_titan", "legs_quantum", "core_quantum", 5, "color_crimson", "ability_guard_crusher", 940, 46, "Only perfection reaches the belt.");
            AddOpp("opp_12_omega_apex", "OMEGA APEX", "Undisputed World Champion", 3, 2, true, AIStyle.CounterPuncher, 1.0f,
                "head_apex", "torso_titan", "larm_titan", "rarm_titan", "legs_apex", "core_angelwings", 5, "color_gold", "ability_quantum_overdrive", 1500, 70, "Step into history — if you can stand!");

            return list;
        }

        private static EventSystem EnsureEventSystem()
        {
            var go = new GameObject("EventSystem");
            var es = go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            return es;
        }

        private static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void SetupLightingAndBackdrop(Color bgColor, Vector3 camPos, Vector3 camLookAt, VolumeProfile volProfile)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bgColor;
            cam.fieldOfView = 44f;
            camGo.transform.position = camPos;
            camGo.transform.LookAt(camLookAt);
            camGo.AddComponent<AudioListener>();
            var ucd = camGo.AddComponent<UniversalAdditionalCameraData>();
            ucd.renderPostProcessing = true;

            // Global Post-Processing Volume
            var volGo = new GameObject("GlobalVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = volProfile;

            // Key Light
            var keyGo = new GameObject("Key Light");
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.55f;
            key.color = new Color(1f, 0.96f, 0.90f);
            key.shadows = LightShadows.Soft;
            keyGo.transform.rotation = Quaternion.Euler(46f, -34f, 0f);

            // Cyan Rim Light
            var rimGo = new GameObject("Rim Light");
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.85f;
            rim.color = new Color(0.24f, 0.80f, 1f);
            rimGo.transform.rotation = Quaternion.Euler(22f, 145f, 0f);

            // Warm Amber Fill
            var fillGo = new GameObject("Warm Fill Light");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 14f;
            fill.intensity = 4.2f;
            fill.color = new Color(1f, 0.58f, 0.22f);
            fillGo.transform.position = new Vector3(2.8f, 2.4f, -1.6f);
        }

        private static GameObject SpawnArenaMesh(string objName, string meshPath, string matPath, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            var go = new GameObject(objName);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(rot);
            go.transform.localScale = scale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = LoadMesh(meshPath);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            return go;
        }

        private static void SpawnHangarEnvironment(Vector3 center)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "HangarFloor";
            floor.transform.position = new Vector3(center.x, -0.36f, center.z);
            floor.transform.localScale = new Vector3(2.5f, 1f, 2.5f);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/Mat_GarageFloor.mat");

            SpawnArenaMesh("Turntable", "Assets/Art/Models/Arena/garage_turntable.obj", MaterialsRoot + "/Mat_ArenaMetal.mat",
                center, Vector3.zero, Vector3.one);
            SpawnArenaMesh("HangarArch_Back", "Assets/Art/Models/Arena/garage_arch.obj", MaterialsRoot + "/Mat_ArenaMetal.mat",
                center + new Vector3(0f, -0.35f, 3.2f), Vector3.zero, Vector3.one);
            SpawnArenaMesh("HangarArch_Mid", "Assets/Art/Models/Arena/garage_arch.obj", MaterialsRoot + "/Mat_ArenaMetal.mat",
                center + new Vector3(0f, -0.35f, 0.5f), Vector3.zero, new Vector3(1.1f, 1.05f, 1f));
        }

        private static GraphicsSettingsUI BuildGraphicsModal(Transform safeRoot, Sprite panelSp, Sprite btnPrimary, Sprite btnSecondary)
        {
            var modal = UIThemeHelper.CreatePanel("GraphicsSettingsModal", safeRoot, new Color(0.05f, 0.07f, 0.12f, 0.97f), panelSp);
            modal.rectTransform.anchorMin = new Vector2(0.16f, 0.10f);
            modal.rectTransform.anchorMax = new Vector2(0.84f, 0.90f);
            modal.rectTransform.offsetMin = Vector2.zero;
            modal.rectTransform.offsetMax = Vector2.zero;

            var ui = modal.gameObject.AddComponent<GraphicsSettingsUI>();
            var vlg = modal.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 24, 24);
            vlg.spacing = 14f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            var title = UIThemeHelper.CreateText("Title", modal.transform, "GRAPHICS, DISPLAY & AUDIO", 30, TextAnchor.MiddleCenter, UIThemeHelper.AccentGold);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

            // Quality Presets
            var qRow = UIThemeHelper.CreateUIObject("QualityRow", modal.transform);
            qRow.AddComponent<LayoutElement>().preferredHeight = 56f;
            var qHlg = qRow.AddComponent<HorizontalLayoutGroup>();
            qHlg.spacing = 12f;
            qHlg.childControlWidth = true;
            qHlg.childControlHeight = true;

            string[] qNames = { "LOW (30 FPS)", "MEDIUM", "HIGH", "ULTRA (120 FPS)" };
            ui.qualityButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                ui.qualityButtons[i] = UIThemeHelper.CreateButton("Q_" + i, qRow.transform, qNames[i], UIThemeHelper.MutedSlate, 19, btnSecondary);
            }

            // Render Scale Slider
            ui.renderScaleSlider = CreateLabeledSlider(modal.transform, "RENDER SCALE: 100%", panelSp, 0.55f, 1.0f, out ui.renderScaleLabel);

            // FPS Cap Row
            var fRow = UIThemeHelper.CreateUIObject("FpsRow", modal.transform);
            fRow.AddComponent<LayoutElement>().preferredHeight = 50f;
            var fHlg = fRow.AddComponent<HorizontalLayoutGroup>();
            fHlg.spacing = 12f;
            fHlg.childControlWidth = true;
            fHlg.childControlHeight = true;
            ui.fpsButtons = new Button[3];
            string[] fpsLabels = { "30 FPS CAP", "60 FPS CAP", "120 FPS CAP" };
            for (int i = 0; i < 3; i++)
            {
                ui.fpsButtons[i] = UIThemeHelper.CreateButton("Fps_" + i, fRow.transform, fpsLabels[i], UIThemeHelper.MutedSlate, 19, btnSecondary);
            }

            // Feature Toggles Row
            var tRow = UIThemeHelper.CreateUIObject("TogglesRow", modal.transform);
            tRow.AddComponent<LayoutElement>().preferredHeight = 54f;
            var tHlg = tRow.AddComponent<HorizontalLayoutGroup>();
            tHlg.spacing = 10f;
            tHlg.childControlWidth = true;
            tHlg.childControlHeight = true;

            ui.postFxToggleBtn = UIThemeHelper.CreateButton("PostFx", tRow.transform, "POST-FX: ON", UIThemeHelper.AccentGreen, 17, btnSecondary);
            ui.bloomToggleBtn = UIThemeHelper.CreateButton("Bloom", tRow.transform, "BLOOM: ON", UIThemeHelper.AccentGreen, 17, btnSecondary);
            ui.outlinesToggleBtn = UIThemeHelper.CreateButton("Outlines", tRow.transform, "OUTLINES: ON", UIThemeHelper.AccentGreen, 17, btnSecondary);
            ui.shadowsToggleBtn = UIThemeHelper.CreateButton("Shadows", tRow.transform, "SHADOWS: ON", UIThemeHelper.AccentGreen, 17, btnSecondary);
            ui.fpsCounterToggleBtn = UIThemeHelper.CreateButton("FpsHud", tRow.transform, "FPS HUD: OFF", UIThemeHelper.MutedSlate, 17, btnSecondary);

            // Audio Sliders
            ui.sfxSlider = CreateLabeledSlider(modal.transform, "SFX VOLUME", panelSp, 0f, 1f, out _);
            ui.bgmSlider = CreateLabeledSlider(modal.transform, "MUSIC VOLUME", panelSp, 0f, 1f, out _);

            var guide = UIThemeHelper.CreateText("Guide", modal.transform,
                "CONTROLS: Tap L/R = Jab/Cross  |  Swipe Side = Hook  |  Swipe Up = Uppercut  |  Hold Guard / Slip Buttons",
                17, TextAnchor.MiddleCenter, new Color(0.78f, 0.88f, 1f), FontStyle.Normal);
            guide.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;

            var btnRow = UIThemeHelper.CreateUIObject("Buttons", modal.transform);
            btnRow.AddComponent<LayoutElement>().preferredHeight = 62f;
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 24f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            ui.resetSaveButton = UIThemeHelper.CreateButton("ResetSave", btnRow.transform, "RESET PROGRESS", UIThemeHelper.AccentRed, 21, btnSecondary);
            ui.closeButton = UIThemeHelper.CreateButton("Close", btnRow.transform, "APPLY & CLOSE", UIThemeHelper.PrimaryCyan, 23, btnPrimary);

            modal.gameObject.SetActive(false);
            return ui;
        }

        private static Slider CreateLabeledSlider(Transform parent, string label, Sprite barSprite, float minVal, float maxVal, out Text labelOut)
        {
            var row = UIThemeHelper.CreateUIObject("Slider_" + label, parent);
            row.AddComponent<LayoutElement>().preferredHeight = 46f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            labelOut = UIThemeHelper.CreateText("Lbl", row.transform, label, 20, TextAnchor.MiddleLeft, Color.white);
            labelOut.gameObject.AddComponent<LayoutElement>().preferredWidth = 280f;

            var bg = UIThemeHelper.CreatePanel("SliderBg", row.transform, new Color(0.18f, 0.22f, 0.30f), barSprite);
            bg.gameObject.AddComponent<LayoutElement>().preferredWidth = 680f;

            var slider = bg.gameObject.AddComponent<Slider>();
            var fillArea = UIThemeHelper.CreateUIObject("FillArea", bg.transform);
            UIThemeHelper.StretchFull(fillArea.GetComponent<RectTransform>(), 6f);
            var fill = UIThemeHelper.CreatePanel("Fill", fillArea.transform, UIThemeHelper.PrimaryCyan, barSprite);
            UIThemeHelper.StretchFull(fill.rectTransform);
            slider.fillRect = fill.rectTransform;

            var handle = UIThemeHelper.CreatePanel("Handle", bg.transform, UIThemeHelper.AccentGold, barSprite);
            handle.rectTransform.sizeDelta = new Vector2(28f, 40f);
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            return slider;
        }

        // ======================================================================
        // SCENE 1: MAIN MENU
        // ======================================================================
        private static void BuildMainMenuScene(GameDatabase db, VolumeProfile volProfile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLightingAndBackdrop(new Color(0.05f, 0.07f, 0.13f, 1f), new Vector3(0.95f, 1.85f, -3.9f), new Vector3(-0.35f, 1.15f, 0f), volProfile);
            EnsureEventSystem();

            SpawnHangarEnvironment(new Vector3(1.1f, 0f, 0f));

            var botGo = new GameObject("ShowcaseRobot");
            botGo.transform.position = new Vector3(1.1f, 0f, 0f);
            botGo.transform.rotation = Quaternion.Euler(0f, 155f, 0f);
            var builder = botGo.AddComponent<RobotBuilder>();
            var vis = botGo.AddComponent<RobotVisualController>();
            builder.ApplyLoadout(db, "head_classic", "torso_scrap", "larm_riveted", "rarm_riveted", "legs_spring", "core_battery", "color_cobalt", 0);
            vis.Initialize(true);

            var tt = botGo.AddComponent<GarageTurntableController>();
            tt.targetRoot = botGo.transform;
            tt.idleSpinSpeed = 14f;

            var canvas = CreateCanvas("MainMenuCanvas");
            var safeGo = UIThemeHelper.CreateUIObject("SafeArea", canvas.transform);
            UIThemeHelper.StretchFull(safeGo.GetComponent<RectTransform>());
            safeGo.AddComponent<SafeAreaFitter>();

            var panelSp = LoadUISprite("ui_panel_rounded.png");
            var btnPrimary = LoadUISprite("ui_button_primary.png");
            var btnSecondary = LoadUISprite("ui_button_secondary.png");

            var menuUI = canvas.gameObject.AddComponent<MainMenuUI>();
            menuUI.showcaseRobot = builder;

            var topBar = UIThemeHelper.CreatePanel("TopBar", safeGo.transform, UIThemeHelper.PanelDark, panelSp);
            var topRt = topBar.rectTransform;
            topRt.anchorMin = new Vector2(0f, 1f);
            topRt.anchorMax = new Vector2(1f, 1f);
            topRt.pivot = new Vector2(0.5f, 1f);
            topRt.sizeDelta = new Vector2(-48f, 88f);
            topRt.anchoredPosition = new Vector2(0f, -18f);

            var topHlg = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            topHlg.padding = new RectOffset(24, 24, 12, 12);
            topHlg.spacing = 24f;
            topHlg.childControlWidth = false;
            topHlg.childControlHeight = true;
            topHlg.childAlignment = TextAnchor.MiddleLeft;

            var logoIcon = UIThemeHelper.CreatePanel("Logo", topBar.transform, Color.white, LoadUISprite("ui_logo_badge.png"));
            logoIcon.gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;

            var title = UIThemeHelper.CreateText("GameTitle", topBar.transform, "IRON KNUCKLE: BOT BOXING", 34, TextAnchor.MiddleLeft, UIThemeHelper.AccentGold);
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 560f;

            menuUI.powerText = UIThemeHelper.CreateText("PowerTxt", topBar.transform, "PWR 240", 26, TextAnchor.MiddleCenter, UIThemeHelper.PrimaryCyan);
            menuUI.powerText.gameObject.AddComponent<LayoutElement>().preferredWidth = 180f;

            var coinIc = UIThemeHelper.CreatePanel("CoinIcon", topBar.transform, Color.white, LoadUISprite("icon_coin.png"));
            coinIc.gameObject.AddComponent<LayoutElement>().preferredWidth = 44f;
            menuUI.scrapText = UIThemeHelper.CreateText("ScrapTxt", topBar.transform, "350", 28, TextAnchor.MiddleLeft, UIThemeHelper.AccentGold);
            menuUI.scrapText.gameObject.AddComponent<LayoutElement>().preferredWidth = 130f;

            var shardIc = UIThemeHelper.CreatePanel("ShardIcon", topBar.transform, Color.white, LoadUISprite("icon_shard.png"));
            shardIc.gameObject.AddComponent<LayoutElement>().preferredWidth = 44f;
            menuUI.shardsText = UIThemeHelper.CreateText("ShardsTxt", topBar.transform, "15", 28, TextAnchor.MiddleLeft, UIThemeHelper.PrimaryCyan);
            menuUI.shardsText.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;

            menuUI.settingsButton = UIThemeHelper.CreateButton("SettingsBtn", topBar.transform, "GRAPHICS & AUDIO", UIThemeHelper.MutedSlate, 20, btnSecondary);
            menuUI.settingsButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 230f;

            var leftPanel = UIThemeHelper.CreatePanel("LeftMenu", safeGo.transform, new Color(0.08f, 0.11f, 0.18f, 0.88f), panelSp);
            var lRt = leftPanel.rectTransform;
            lRt.anchorMin = new Vector2(0f, 0.05f);
            lRt.anchorMax = new Vector2(0f, 0.86f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.sizeDelta = new Vector2(560f, 0f);
            lRt.anchoredPosition = new Vector2(36f, 0f);

            var lVlg = leftPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            lVlg.padding = new RectOffset(28, 28, 28, 28);
            lVlg.spacing = 18f;
            lVlg.childControlWidth = true;
            lVlg.childControlHeight = false;

            menuUI.recordText = UIThemeHelper.CreateText("Record", leftPanel.transform, "WINS: 0 | K.O.: 0 | STREAK: 0", 22, TextAnchor.MiddleLeft, new Color(0.78f, 0.86f, 0.96f));
            menuUI.recordText.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;

            menuUI.careerButton = UIThemeHelper.CreateButton("CareerBtn", leftPanel.transform, "CAREER CHAMPIONSHIP", UIThemeHelper.AccentOrange, 30, btnPrimary);
            menuUI.careerButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

            menuUI.quickSparButton = UIThemeHelper.CreateButton("QuickSparBtn", leftPanel.transform, "QUICK SPARRING MATCH", UIThemeHelper.PrimaryCyan, 27, btnPrimary);
            menuUI.quickSparButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 84f;

            menuUI.workshopButton = UIThemeHelper.CreateButton("WorkshopBtn", leftPanel.transform, "ROBOT BUILDER & SHOP", UIThemeHelper.AccentGold, 27, btnPrimary);
            menuUI.workshopButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 84f;

            menuUI.skillLabButton = UIThemeHelper.CreateButton("SkillLabBtn", leftPanel.transform, "COMBAT SKILL LAB", new Color(0.55f, 0.34f, 0.92f), 26, btnSecondary);
            menuUI.skillLabButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;

            var dailyCard = UIThemeHelper.CreatePanel("DailyBounty", leftPanel.transform, UIThemeHelper.PanelCard, panelSp);
            dailyCard.gameObject.AddComponent<LayoutElement>().preferredHeight = 148f;
            var dVlg = dailyCard.gameObject.AddComponent<VerticalLayoutGroup>();
            dVlg.padding = new RectOffset(16, 16, 12, 12);
            dVlg.spacing = 10f;
            dVlg.childControlWidth = true;
            dVlg.childControlHeight = false;

            menuUI.dailyMissionText = UIThemeHelper.CreateText("MissionTxt", dailyCard.transform, "DAILY BOUNTY: Fight 3 Matches (0/3)", 20, TextAnchor.MiddleLeft, Color.white);
            menuUI.dailyMissionText.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

            menuUI.claimDailyButton = UIThemeHelper.CreateButton("ClaimBtn", dailyCard.transform, "0/3", UIThemeHelper.AccentGreen, 22, btnSecondary);
            menuUI.claimDailyButton.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
            menuUI.claimDailyLabel = menuUI.claimDailyButton.GetComponentInChildren<Text>();

            BuildLeagueModal(safeGo.transform, menuUI, panelSp, btnPrimary, btnSecondary);
            menuUI.settingsModal = BuildGraphicsModal(safeGo.transform, panelSp, btnPrimary, btnSecondary);

            EditorSceneManager.SaveScene(scene, ScenesRoot + "/MainMenu.unity");
        }

        private static void BuildLeagueModal(Transform safeRoot, MainMenuUI menuUI, Sprite panelSp, Sprite btnPrimary, Sprite btnSecondary)
        {
            var modal = UIThemeHelper.CreatePanel("LeagueSelectModal", safeRoot, new Color(0.05f, 0.07f, 0.12f, 0.97f), panelSp);
            UIThemeHelper.StretchFull(modal.rectTransform, 32f);
            var leagueUI = modal.gameObject.AddComponent<LeagueSelectUI>();
            leagueUI.cardSprite = panelSp;
            leagueUI.buttonSprite = btnPrimary;
            leagueUI.starFilled = LoadUISprite("ui_star_filled.png");
            leagueUI.starEmpty = LoadUISprite("ui_star_empty.png");
            menuUI.leagueSelectModal = leagueUI;

            var header = UIThemeHelper.CreateText("Header", modal.transform, "CAREER LEAGUES — CHOOSE YOUR OPPONENT", 36, TextAnchor.MiddleLeft, UIThemeHelper.AccentGold);
            header.rectTransform.anchorMin = new Vector2(0f, 1f);
            header.rectTransform.anchorMax = new Vector2(0.8f, 1f);
            header.rectTransform.sizeDelta = new Vector2(-40f, 72f);
            header.rectTransform.anchoredPosition = new Vector2(20f, -45f);

            leagueUI.closeButton = UIThemeHelper.CreateButton("CloseBtn", modal.transform, "BACK", UIThemeHelper.AccentRed, 24, btnSecondary);
            var cRt = leagueUI.closeButton.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(1f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.sizeDelta = new Vector2(160f, 58f);
            cRt.anchoredPosition = new Vector2(-110f, -48f);

            var tabRow = UIThemeHelper.CreateUIObject("LeagueTabs", modal.transform);
            var trt = tabRow.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.sizeDelta = new Vector2(-64f, 64f);
            trt.anchoredPosition = new Vector2(0f, -124f);
            var tHlg = tabRow.AddComponent<HorizontalLayoutGroup>();
            tHlg.spacing = 14f;
            tHlg.childControlWidth = true;
            tHlg.childControlHeight = true;

            leagueUI.leagueTabButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                leagueUI.leagueTabButtons[i] = UIThemeHelper.CreateButton("Tab_" + i, tabRow.transform,
                    $"L{i + 1}: {GameDatabase.LeagueNames[i]}", UIThemeHelper.MutedSlate, 20, btnSecondary);
            }

            var cards = UIThemeHelper.CreateUIObject("CardsRow", modal.transform);
            var crRt = cards.GetComponent<RectTransform>();
            crRt.anchorMin = new Vector2(0.03f, 0.06f);
            crRt.anchorMax = new Vector2(0.97f, 0.78f);
            crRt.offsetMin = Vector2.zero;
            crRt.offsetMax = Vector2.zero;
            var chlg = cards.AddComponent<HorizontalLayoutGroup>();
            chlg.spacing = 24f;
            chlg.childAlignment = TextAnchor.MiddleCenter;
            chlg.childControlWidth = false;
            chlg.childControlHeight = false;
            leagueUI.cardsContainer = cards.transform;

            modal.gameObject.SetActive(false);
        }

        // ======================================================================
        // SCENE 2: GARAGE (DEEP MECHA WORKSHOP)
        // ======================================================================
        private static void BuildGarageScene(GameDatabase db, VolumeProfile volProfile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLightingAndBackdrop(new Color(0.06f, 0.08f, 0.14f, 1f), new Vector3(-0.6f, 1.78f, -3.75f), new Vector3(0.35f, 1.12f, 0f), volProfile);
            EnsureEventSystem();

            Vector3 botCenter = new Vector3(-1.15f, 0f, 0f);
            SpawnHangarEnvironment(botCenter);

            var botGo = new GameObject("PreviewRobot");
            botGo.transform.position = botCenter;
            botGo.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
            var builder = botGo.AddComponent<RobotBuilder>();
            var vis = botGo.AddComponent<RobotVisualController>();
            builder.ApplyLoadout(db, "head_classic", "torso_scrap", "larm_riveted", "rarm_riveted", "legs_spring", "core_battery", "color_cobalt", 0);
            vis.Initialize(true);

            var tt = botGo.AddComponent<GarageTurntableController>();
            tt.targetRoot = botGo.transform;

            var camCtrl = Camera.main.gameObject.AddComponent<GarageCameraController>();
            camCtrl.robotTarget = botGo.transform;
            camCtrl.FocusOnFullBody();

            var canvas = CreateCanvas("GarageCanvas");
            var safeGo = UIThemeHelper.CreateUIObject("SafeArea", canvas.transform);
            UIThemeHelper.StretchFull(safeGo.GetComponent<RectTransform>());
            safeGo.AddComponent<SafeAreaFitter>();

            var panelSp = LoadUISprite("ui_panel_rounded.png");
            var btnPrimary = LoadUISprite("ui_button_primary.png");
            var btnSecondary = LoadUISprite("ui_button_secondary.png");

            var garageUI = canvas.gameObject.AddComponent<GarageUI>();
            garageUI.previewRobot = builder;
            garageUI.previewVisuals = vis;
            garageUI.cameraController = camCtrl;
            garageUI.cardSprite = panelSp;
            garageUI.buttonSprite = btnPrimary;

            // Top Header
            var topBar = UIThemeHelper.CreatePanel("TopBar", safeGo.transform, UIThemeHelper.PanelDark, panelSp);
            var tRt = topBar.rectTransform;
            tRt.anchorMin = new Vector2(0f, 1f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.sizeDelta = new Vector2(-36f, 80f);
            tRt.anchoredPosition = new Vector2(0f, -12f);

            var tHlg = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tHlg.padding = new RectOffset(16, 16, 10, 10);
            tHlg.spacing = 14f;
            tHlg.childControlWidth = false;
            tHlg.childControlHeight = true;
            tHlg.childAlignment = TextAnchor.MiddleLeft;

            garageUI.backButton = UIThemeHelper.CreateButton("BackBtn", topBar.transform, "< MENU", UIThemeHelper.MutedSlate, 21, btnSecondary);
            garageUI.backButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 140f;

            var hdr = UIThemeHelper.CreateText("Title", topBar.transform, "MECHA BUILDER & SHOP", 28, TextAnchor.MiddleLeft, UIThemeHelper.AccentGold);
            hdr.gameObject.AddComponent<LayoutElement>().preferredWidth = 440f;

            var coinIc = UIThemeHelper.CreatePanel("CoinIcon", topBar.transform, Color.white, LoadUISprite("icon_coin.png"));
            coinIc.gameObject.AddComponent<LayoutElement>().preferredWidth = 40f;
            garageUI.scrapText = UIThemeHelper.CreateText("Scrap", topBar.transform, "350", 25, TextAnchor.MiddleLeft, UIThemeHelper.AccentGold);
            garageUI.scrapText.gameObject.AddComponent<LayoutElement>().preferredWidth = 120f;

            var shardIc = UIThemeHelper.CreatePanel("ShardIcon", topBar.transform, Color.white, LoadUISprite("icon_shard.png"));
            shardIc.gameObject.AddComponent<LayoutElement>().preferredWidth = 40f;
            garageUI.shardsText = UIThemeHelper.CreateText("Shards", topBar.transform, "15", 25, TextAnchor.MiddleLeft, UIThemeHelper.PrimaryCyan);
            garageUI.shardsText.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;

            garageUI.gfxButton = UIThemeHelper.CreateButton("GfxBtn", topBar.transform, "GFX", UIThemeHelper.MutedSlate, 21, btnSecondary);
            garageUI.gfxButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;

            garageUI.fightNowButton = UIThemeHelper.CreateButton("FightNow", topBar.transform, "FIGHT NOW!", UIThemeHelper.AccentOrange, 23, btnPrimary);
            garageUI.fightNowButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 200f;

            // Preview Mode Banner (above robot on left)
            var prevBanner = UIThemeHelper.CreatePanel("PreviewBanner", safeGo.transform, new Color(0.08f, 0.24f, 0.36f, 0.92f), panelSp);
            var pbRt = prevBanner.rectTransform;
            pbRt.anchorMin = new Vector2(0.02f, 0.82f);
            pbRt.anchorMax = new Vector2(0.39f, 0.89f);
            pbRt.offsetMin = Vector2.zero;
            pbRt.offsetMax = Vector2.zero;
            var pbHlg = prevBanner.gameObject.AddComponent<HorizontalLayoutGroup>();
            pbHlg.padding = new RectOffset(12, 12, 6, 6);
            pbHlg.spacing = 10f;
            pbHlg.childControlWidth = true;
            pbHlg.childControlHeight = true;
            garageUI.previewBanner = prevBanner.gameObject;
            garageUI.previewBannerText = UIThemeHelper.CreateText("PrevTxt", prevBanner.transform, "PREVIEW MODE", 18, TextAnchor.MiddleLeft, UIThemeHelper.PrimaryCyan);
            garageUI.resetPreviewButton = UIThemeHelper.CreateButton("ResetPrev", prevBanner.transform, "RESET", UIThemeHelper.AccentRed, 17, btnSecondary);
            garageUI.resetPreviewButton.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;
            prevBanner.gameObject.SetActive(false);

            // Studio Controls Strip (above bottom stats)
            var ctrlStrip = UIThemeHelper.CreateUIObject("StudioToolbar", safeGo.transform);
            var csRt = ctrlStrip.GetComponent<RectTransform>();
            csRt.anchorMin = new Vector2(0.02f, 0.27f);
            csRt.anchorMax = new Vector2(0.40f, 0.33f);
            csRt.offsetMin = Vector2.zero;
            csRt.offsetMax = Vector2.zero;
            var csHlg = ctrlStrip.AddComponent<HorizontalLayoutGroup>();
            csHlg.spacing = 8f;
            csHlg.childControlWidth = true;
            csHlg.childControlHeight = true;

            garageUI.fullViewButton = UIThemeHelper.CreateButton("FullView", ctrlStrip.transform, "FULL VIEW", UIThemeHelper.MutedSlate, 16, btnSecondary);
            garageUI.testPunchButton = UIThemeHelper.CreateButton("TestPunch", ctrlStrip.transform, "TEST PUNCH!", UIThemeHelper.AccentOrange, 16, btnPrimary);
            garageUI.autoBestButton = UIThemeHelper.CreateButton("AutoBest", ctrlStrip.transform, "AUTO-BEST", UIThemeHelper.AccentGreen, 16, btnSecondary);

            garageUI.loadoutSlotButtons = new Button[3];
            for (int b = 0; b < 3; b++)
            {
                garageUI.loadoutSlotButtons[b] = UIThemeHelper.CreateButton("Build_" + b, ctrlStrip.transform, $"B{b + 1}", UIThemeHelper.MutedSlate, 16, btnSecondary);
                garageUI.loadoutSlotButtons[b].gameObject.AddComponent<LayoutElement>().preferredWidth = 64f;
            }

            // Bottom-Left Stats & Set Synergy Card
            var statsPanel = UIThemeHelper.CreatePanel("StatsPanel", safeGo.transform, UIThemeHelper.PanelDark, panelSp);
            var sRt = statsPanel.rectTransform;
            sRt.anchorMin = new Vector2(0.02f, 0.03f);
            sRt.anchorMax = new Vector2(0.40f, 0.26f);
            sRt.offsetMin = Vector2.zero;
            sRt.offsetMax = Vector2.zero;
            var sVlg = statsPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            sVlg.padding = new RectOffset(14, 14, 10, 10);
            sVlg.spacing = 6f;
            sVlg.childControlWidth = true;
            sVlg.childControlHeight = false;

            garageUI.statsSummaryText = UIThemeHelper.CreateText("StatsText", statsPanel.transform, "POWER: 240", 19, TextAnchor.MiddleCenter, Color.white);
            garageUI.statsSummaryText.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

            garageUI.setBonusText = UIThemeHelper.CreateText("SetBonusText", statsPanel.transform, "SET SYNERGY: SCRAPWORKS 3/6", 15, TextAnchor.MiddleCenter, UIThemeHelper.AccentGold);
            garageUI.setBonusText.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

            // Right Catalog Panel
            var rightPanel = UIThemeHelper.CreatePanel("CatalogPanel", safeGo.transform, UIThemeHelper.PanelDark, panelSp);
            var rRt = rightPanel.rectTransform;
            rRt.anchorMin = new Vector2(0.42f, 0.03f);
            rRt.anchorMax = new Vector2(0.98f, 0.89f);
            rRt.offsetMin = Vector2.zero;
            rRt.offsetMax = Vector2.zero;

            var rVlg = rightPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            rVlg.padding = new RectOffset(18, 18, 16, 16);
            rVlg.spacing = 10f;
            rVlg.childControlWidth = true;
            rVlg.childControlHeight = false;

            var catRow = UIThemeHelper.CreateUIObject("CategoryTabs", rightPanel.transform);
            catRow.AddComponent<LayoutElement>().preferredHeight = 54f;
            var cHlg = catRow.AddComponent<HorizontalLayoutGroup>();
            cHlg.spacing = 10f;
            cHlg.childControlWidth = true;
            cHlg.childControlHeight = true;

            string[] catNames = { "PARTS (72)", "PRE-MADE BOTS", "PAINT & FINISH", "SKILL LAB" };
            garageUI.categoryTabButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                garageUI.categoryTabButtons[i] = UIThemeHelper.CreateButton("Cat_" + i, catRow.transform, catNames[i], UIThemeHelper.MutedSlate, 19, btnSecondary);
            }

            var slotRow = UIThemeHelper.CreateUIObject("SlotFilters", rightPanel.transform);
            slotRow.AddComponent<LayoutElement>().preferredHeight = 46f;
            var sHlg = slotRow.AddComponent<HorizontalLayoutGroup>();
            sHlg.spacing = 8f;
            sHlg.childControlWidth = true;
            sHlg.childControlHeight = true;
            garageUI.slotFilterRow = slotRow;

            string[] slotNames = { "HEAD", "TORSO", "L-ARM", "R-ARM", "LEGS", "CORE" };
            garageUI.slotFilterButtons = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                garageUI.slotFilterButtons[i] = UIThemeHelper.CreateButton("Slot_" + i, slotRow.transform, slotNames[i], UIThemeHelper.MutedSlate, 18, btnSecondary);
            }

            var scrollGo = UIThemeHelper.CreatePanel("ScrollView", rightPanel.transform, new Color(0.05f, 0.07f, 0.11f, 0.72f), panelSp);
            var scLe = scrollGo.gameObject.AddComponent<LayoutElement>();
            scLe.flexibleHeight = 1f;
            scLe.preferredHeight = 620f;
            var scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = UIThemeHelper.CreateUIObject("Viewport", scrollGo.transform);
            UIThemeHelper.StretchFull(viewport.GetComponent<RectTransform>(), 8f);
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = viewport.GetComponent<RectTransform>();

            var content = UIThemeHelper.CreateUIObject("Content", viewport.transform);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.sizeDelta = new Vector2(0f, 0f);
            var contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.spacing = 12f;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = cRt;
            garageUI.listContainer = content.transform;

            garageUI.graphicsModal = BuildGraphicsModal(safeGo.transform, panelSp, btnPrimary, btnSecondary);

            EditorSceneManager.SaveScene(scene, ScenesRoot + "/Garage.unity");
        }

        // ======================================================================
        // SCENE 3: BOXING ARENA (WITH STADIUM TOWERS, BILLBOARDS & VOLUME)
        // ======================================================================
        private static void BuildArenaScene(GameDatabase db, VolumeProfile volProfile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLightingAndBackdrop(new Color(0.03f, 0.04f, 0.09f, 1f), new Vector3(-1.35f, 2.25f, -3.55f), new Vector3(0f, 1.3f, 0.8f), volProfile);
            EnsureEventSystem();

            var camCtrl = Camera.main.gameObject.AddComponent<CombatCameraController>();

            SpawnArenaMesh("RingPlatform", "Assets/Art/Models/Arena/ring_platform.obj", MaterialsRoot + "/Mat_RingCanvas.mat",
                Vector3.zero, Vector3.zero, Vector3.one);

            Vector3[] postPositions =
            {
                new Vector3(-4.5f, 0f, -4.5f),
                new Vector3(4.5f, 0f, -4.5f),
                new Vector3(-4.5f, 0f, 4.5f),
                new Vector3(4.5f, 0f, 4.5f)
            };
            for (int i = 0; i < 4; i++)
            {
                SpawnArenaMesh("RingPost_" + i, "Assets/Art/Models/Arena/ring_post.obj", MaterialsRoot + "/Mat_Hazard.mat",
                    postPositions[i], Vector3.zero, Vector3.one);
            }

            float[] ropeHeights = { 0.48f, 0.88f, 1.28f };
            string[] ropeMats = { "/Mat_RingRopeBlue.mat", "/Mat_RingRopeWhite.mat", "/Mat_RingRopeRed.mat" };
            for (int r = 0; r < 3; r++)
            {
                SpawnArenaMesh($"Rope_Back_{r}", "Assets/Art/Models/Arena/ring_rope.obj", MaterialsRoot + ropeMats[r],
                    new Vector3(0f, ropeHeights[r], 4.5f), Vector3.zero, Vector3.one);
                SpawnArenaMesh($"Rope_Left_{r}", "Assets/Art/Models/Arena/ring_rope.obj", MaterialsRoot + ropeMats[r],
                    new Vector3(-4.5f, ropeHeights[r], 0f), new Vector3(0f, 90f, 0f), Vector3.one);
                SpawnArenaMesh($"Rope_Right_{r}", "Assets/Art/Models/Arena/ring_rope.obj", MaterialsRoot + ropeMats[r],
                    new Vector3(4.5f, ropeHeights[r], 0f), new Vector3(0f, 90f, 0f), Vector3.one);
            }

            SpawnArenaMesh("ArenaTruss_Back", "Assets/Art/Models/Arena/arena_truss.obj", MaterialsRoot + "/Mat_ArenaMetal.mat",
                new Vector3(0f, 4.8f, 4.8f), Vector3.zero, Vector3.one);
            SpawnArenaMesh("StadiumPillar_L", "Assets/Art/Models/Arena/arena_pillar.obj", MaterialsRoot + "/Mat_ArenaMetal.mat",
                new Vector3(-5.8f, -0.4f, 5.8f), Vector3.zero, Vector3.one);
            SpawnArenaMesh("StadiumPillar_R", "Assets/Art/Models/Arena/arena_pillar.obj", MaterialsRoot + "/Mat_ArenaMetal.mat",
                new Vector3(5.8f, -0.4f, 5.8f), Vector3.zero, Vector3.one);
            SpawnArenaMesh("Billboard_Back", "Assets/Art/Models/Arena/arena_billboard.obj", MaterialsRoot + "/Mat_Billboard.mat",
                new Vector3(0f, 3.8f, 6.2f), Vector3.zero, Vector3.one);

            var pGo = new GameObject("PlayerFighter");
            pGo.transform.position = new Vector3(0f, 0f, -0.75f);
            pGo.transform.rotation = Quaternion.identity;
            pGo.AddComponent<RobotBuilder>();
            pGo.AddComponent<RobotVisualController>();
            var playerCtrl = pGo.AddComponent<FighterController>();

            var oGo = new GameObject("OpponentFighter");
            oGo.transform.position = new Vector3(0f, 0f, 1.15f);
            oGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            oGo.AddComponent<RobotBuilder>();
            oGo.AddComponent<RobotVisualController>();
            var oppCtrl = oGo.AddComponent<FighterController>();

            camCtrl.playerTarget = pGo.transform;
            camCtrl.opponentTarget = oGo.transform;

            var sysGo = new GameObject("[CombatSystems]");
            var combatMgr = sysGo.AddComponent<CombatManager>();
            var inputCtrl = sysGo.AddComponent<PlayerInputController>();
            var aiCtrl = sysGo.AddComponent<EnemyAIController>();
            var vfxMgr = sysGo.AddComponent<CombatVFXManager>();

            inputCtrl.playerFighter = playerCtrl;
            aiCtrl.aiFighter = oppCtrl;

            combatMgr.playerSpawn = pGo.transform;
            combatMgr.opponentSpawn = oGo.transform;
            combatMgr.playerFighter = playerCtrl;
            combatMgr.opponentFighter = oppCtrl;
            combatMgr.playerInput = inputCtrl;
            combatMgr.enemyAI = aiCtrl;
            combatMgr.vfxManager = vfxMgr;
            combatMgr.cameraController = camCtrl;

            var popupCanvas = CreateCanvas("PopupCanvas");
            popupCanvas.sortingOrder = 5;
            vfxMgr.worldPopupCanvas = popupCanvas;
            vfxMgr.sparkSprite = LoadUISprite("ui_spark_burst.png");

            var hudCanvas = CreateCanvas("ArenaHUDCanvas");
            hudCanvas.sortingOrder = 10;
            var safeGo = UIThemeHelper.CreateUIObject("SafeArea", hudCanvas.transform);
            UIThemeHelper.StretchFull(safeGo.GetComponent<RectTransform>());
            safeGo.AddComponent<SafeAreaFitter>();

            var hud = hudCanvas.gameObject.AddComponent<FightHUD>();
            hud.combatManager = combatMgr;
            hud.playerInput = inputCtrl;
            combatMgr.fightHUD = hud;

            BuildArenaHUDLayout(safeGo.transform, hud);

            EditorSceneManager.SaveScene(scene, ScenesRoot + "/Arena.unity");
        }

        private static void BuildArenaHUDLayout(Transform safeRoot, FightHUD hud)
        {
            var panelSp = LoadUISprite("ui_panel_rounded.png");
            var btnPrimary = LoadUISprite("ui_button_primary.png");
            var btnSecondary = LoadUISprite("ui_button_secondary.png");
            var barBg = LoadUISprite("ui_bar_bg.png");
            var barFill = LoadUISprite("ui_bar_fill.png");

            var topArea = UIThemeHelper.CreateUIObject("TopHUD", safeRoot);
            var topRt = topArea.GetComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0.02f, 0.80f);
            topRt.anchorMax = new Vector2(0.98f, 0.98f);
            topRt.offsetMin = Vector2.zero;
            topRt.offsetMax = Vector2.zero;

            hud.playerNameText = UIThemeHelper.CreateText("PlayerName", topArea.transform, "IRON CHALLENGER", 22, TextAnchor.MiddleLeft, UIThemeHelper.PrimaryCyan);
            SetRect(hud.playerNameText.rectTransform, 0f, 0.72f, 0.38f, 1f);
            CreateMeterBar("PlayerHP", topArea.transform, 0f, 0.42f, 0.38f, 0.70f, UIThemeHelper.AccentGreen, barBg, barFill, out hud.playerHpFill, out hud.playerHpTrailFill);
            CreateMeterBar("PlayerStamina", topArea.transform, 0f, 0.22f, 0.32f, 0.38f, UIThemeHelper.AccentGold, barBg, barFill, out hud.playerStaminaFill, out _);
            CreateMeterBar("PlayerOD", topArea.transform, 0f, 0.03f, 0.26f, 0.18f, UIThemeHelper.PrimaryCyan, barBg, barFill, out hud.playerOverdriveFill, out _);
            CreateMeterBar("PlayerGuard", topArea.transform, 0.27f, 0.03f, 0.38f, 0.18f, new Color(0.65f, 0.82f, 1f), barBg, barFill, out hud.playerGuardFill, out _);

            hud.opponentNameText = UIThemeHelper.CreateText("OpponentName", topArea.transform, "OPPONENT", 22, TextAnchor.MiddleRight, UIThemeHelper.AccentOrange);
            SetRect(hud.opponentNameText.rectTransform, 0.62f, 0.72f, 1f, 1f);
            CreateMeterBar("OpponentHP", topArea.transform, 0.62f, 0.42f, 1f, 0.70f, UIThemeHelper.AccentRed, barBg, barFill, out hud.opponentHpFill, out hud.opponentHpTrailFill);
            CreateMeterBar("OpponentStamina", topArea.transform, 0.68f, 0.22f, 1f, 0.38f, UIThemeHelper.AccentGold, barBg, barFill, out hud.opponentStaminaFill, out _);
            CreateMeterBar("OpponentOD", topArea.transform, 0.74f, 0.03f, 1f, 0.18f, UIThemeHelper.AccentOrange, barBg, barFill, out hud.opponentOverdriveFill, out _);

            var clockPanel = UIThemeHelper.CreatePanel("ClockPanel", topArea.transform, UIThemeHelper.PanelDark, panelSp);
            SetRect(clockPanel.rectTransform, 0.43f, 0.16f, 0.57f, 0.96f);
            hud.roundText = UIThemeHelper.CreateText("Round", clockPanel.transform, "ROUND 1/3", 18, TextAnchor.UpperCenter, UIThemeHelper.AccentGold);
            SetRect(hud.roundText.rectTransform, 0f, 0.58f, 1f, 0.94f);
            hud.timerText = UIThemeHelper.CreateText("Timer", clockPanel.transform, "60", 40, TextAnchor.LowerCenter, Color.white);
            SetRect(hud.timerText.rectTransform, 0f, 0.06f, 1f, 0.62f);

            hud.pauseButton = UIThemeHelper.CreateButton("PauseBtn", topArea.transform, "II", UIThemeHelper.MutedSlate, 22, btnSecondary);
            SetRect(hud.pauseButton.GetComponent<RectTransform>(), 0.46f, -0.22f, 0.54f, 0.10f);

            hud.comboText = UIThemeHelper.CreateText("ComboText", safeRoot, "3x COMBO!", 42, TextAnchor.MiddleLeft, UIThemeHelper.AccentGold);
            SetRect(hud.comboText.rectTransform, 0.04f, 0.58f, 0.32f, 0.72f);
            hud.comboText.gameObject.SetActive(false);

            hud.bannerText = UIThemeHelper.CreateText("BannerText", safeRoot, "FIGHT!", 76, TextAnchor.MiddleCenter, UIThemeHelper.AccentGold);
            SetRect(hud.bannerText.rectTransform, 0.15f, 0.44f, 0.85f, 0.68f);
            var bOut = hud.bannerText.gameObject.AddComponent<Outline>();
            bOut.effectColor = Color.black;
            bOut.effectDistance = new Vector2(4f, -4f);

            var leftCluster = UIThemeHelper.CreateUIObject("DefenseCluster", safeRoot);
            SetRect(leftCluster.GetComponent<RectTransform>(), 0.02f, 0.03f, 0.34f, 0.28f);

            hud.slipLeftButton = UIThemeHelper.CreateButton("SlipL", leftCluster.transform, "SLIP L", UIThemeHelper.PanelCard, 22, btnSecondary);
            SetRect(hud.slipLeftButton.GetComponent<RectTransform>(), 0f, 0f, 0.31f, 0.62f);

            var guardBtn = UIThemeHelper.CreateButton("GuardHold", leftCluster.transform, "HOLD\nGUARD", UIThemeHelper.PrimaryCyan, 22, btnPrimary);
            SetRect(guardBtn.GetComponent<RectTransform>(), 0.34f, 0f, 0.66f, 0.76f);
            hud.guardHoldTrigger = guardBtn.gameObject.AddComponent<EventTrigger>();

            hud.slipRightButton = UIThemeHelper.CreateButton("SlipR", leftCluster.transform, "SLIP R", UIThemeHelper.PanelCard, 22, btnSecondary);
            SetRect(hud.slipRightButton.GetComponent<RectTransform>(), 0.69f, 0f, 1f, 0.62f);

            var rightCluster = UIThemeHelper.CreateUIObject("OffenseCluster", safeRoot);
            SetRect(rightCluster.GetComponent<RectTransform>(), 0.62f, 0.03f, 0.98f, 0.31f);

            hud.special1Button = UIThemeHelper.CreateButton("Special1", rightCluster.transform, "SPECIAL 1", UIThemeHelper.AccentOrange, 20, btnPrimary);
            SetRect(hud.special1Button.GetComponent<RectTransform>(), 0f, 0.60f, 0.48f, 1f);
            hud.special1Label = hud.special1Button.GetComponentInChildren<Text>();

            hud.special2Button = UIThemeHelper.CreateButton("Special2", rightCluster.transform, "SPECIAL 2", new Color(0.68f, 0.28f, 0.95f), 20, btnPrimary);
            SetRect(hud.special2Button.GetComponent<RectTransform>(), 0.52f, 0.60f, 1f, 1f);
            hud.special2Label = hud.special2Button.GetComponentInChildren<Text>();

            hud.jabButton = UIThemeHelper.CreateButton("JabBtn", rightCluster.transform, "JAB", UIThemeHelper.AccentGold, 24, btnPrimary);
            SetRect(hud.jabButton.GetComponent<RectTransform>(), 0f, 0f, 0.31f, 0.54f);

            hud.hookButton = UIThemeHelper.CreateButton("HookBtn", rightCluster.transform, "HOOK", UIThemeHelper.AccentOrange, 24, btnPrimary);
            SetRect(hud.hookButton.GetComponent<RectTransform>(), 0.345f, 0f, 0.655f, 0.54f);

            hud.uppercutButton = UIThemeHelper.CreateButton("UpperBtn", rightCluster.transform, "UPPER", UIThemeHelper.AccentRed, 24, btnPrimary);
            SetRect(hud.uppercutButton.GetComponent<RectTransform>(), 0.69f, 0f, 1f, 0.54f);

            var tutPanel = UIThemeHelper.CreatePanel("TutorialBanner", safeRoot, new Color(0.06f, 0.10f, 0.18f, 0.92f), panelSp);
            SetRect(tutPanel.rectTransform, 0.22f, 0.69f, 0.78f, 0.78f);
            hud.tutorialBanner = tutPanel.gameObject;
            hud.tutorialHintText = UIThemeHelper.CreateText("Hint", tutPanel.transform, "TIP 1/3: TAP to throw fast Jabs!", 21, TextAnchor.MiddleLeft, Color.white);
            SetRect(hud.tutorialHintText.rectTransform, 0.03f, 0.08f, 0.80f, 0.92f);
            hud.skipTutorialButton = UIThemeHelper.CreateButton("Skip", tutPanel.transform, "SKIP", UIThemeHelper.MutedSlate, 18, btnSecondary);
            SetRect(hud.skipTutorialButton.GetComponent<RectTransform>(), 0.82f, 0.14f, 0.97f, 0.86f);

            var rebPanel = UIThemeHelper.CreatePanel("RebootPanel", safeRoot, new Color(0.14f, 0.05f, 0.08f, 0.94f), panelSp);
            SetRect(rebPanel.rectTransform, 0.28f, 0.32f, 0.72f, 0.64f);
            hud.rebootPanel = rebPanel.gameObject;
            hud.rebootTimerText = UIThemeHelper.CreateText("TimerTxt", rebPanel.transform, "SYSTEM DOWN! TAP REBOOT!", 28, TextAnchor.MiddleCenter, UIThemeHelper.AccentGold);
            SetRect(hud.rebootTimerText.rectTransform, 0.05f, 0.68f, 0.95f, 0.92f);
            CreateMeterBar("RebootBar", rebPanel.transform, 0.08f, 0.48f, 0.92f, 0.62f, UIThemeHelper.AccentGreen, barBg, barFill, out hud.rebootProgressFill, out _);
            hud.rebootTapButton = UIThemeHelper.CreateButton("RebootTap", rebPanel.transform, "TAP REBOOT!", UIThemeHelper.AccentOrange, 32, btnPrimary);
            SetRect(hud.rebootTapButton.GetComponent<RectTransform>(), 0.16f, 0.08f, 0.84f, 0.42f);

            // Pause Modal with Graphics Settings
            var pauseP = UIThemeHelper.CreatePanel("PausePanel", safeRoot, new Color(0.05f, 0.07f, 0.12f, 0.95f), panelSp);
            SetRect(pauseP.rectTransform, 0.30f, 0.22f, 0.70f, 0.78f);
            hud.pausePanel = pauseP.gameObject;
            var pTitle = UIThemeHelper.CreateText("Title", pauseP.transform, "MATCH PAUSED", 34, TextAnchor.MiddleCenter, UIThemeHelper.AccentGold);
            SetRect(pTitle.rectTransform, 0.08f, 0.74f, 0.92f, 0.94f);
            hud.resumeButton = UIThemeHelper.CreateButton("Resume", pauseP.transform, "RESUME FIGHT", UIThemeHelper.AccentGreen, 25, btnPrimary);
            SetRect(hud.resumeButton.GetComponent<RectTransform>(), 0.12f, 0.52f, 0.88f, 0.70f);
            hud.graphicsButton = UIThemeHelper.CreateButton("Graphics", pauseP.transform, "GRAPHICS & AUDIO", UIThemeHelper.PrimaryCyan, 24, btnSecondary);
            SetRect(hud.graphicsButton.GetComponent<RectTransform>(), 0.12f, 0.30f, 0.88f, 0.48f);
            hud.quitToMenuButton = UIThemeHelper.CreateButton("Quit", pauseP.transform, "RETIRE TO MENU", UIThemeHelper.AccentRed, 24, btnSecondary);
            SetRect(hud.quitToMenuButton.GetComponent<RectTransform>(), 0.12f, 0.08f, 0.88f, 0.26f);

            hud.graphicsModal = BuildGraphicsModal(safeRoot, panelSp, btnPrimary, btnSecondary);

            var resP = UIThemeHelper.CreatePanel("ResultPanel", safeRoot, new Color(0.06f, 0.08f, 0.14f, 0.96f), panelSp);
            SetRect(resP.rectTransform, 0.20f, 0.14f, 0.80f, 0.86f);
            hud.resultPanel = resP.gameObject;
            hud.resultTitleText = UIThemeHelper.CreateText("ResultTitle", resP.transform, "KNOCKOUT VICTORY!", 44, TextAnchor.MiddleCenter, UIThemeHelper.AccentGold);
            SetRect(hud.resultTitleText.rectTransform, 0.05f, 0.74f, 0.95f, 0.94f);

            hud.resultStars = new Image[3];
            var starRow = UIThemeHelper.CreateUIObject("Stars", resP.transform);
            SetRect(starRow.GetComponent<RectTransform>(), 0.25f, 0.52f, 0.75f, 0.72f);
            var sHlg = starRow.AddComponent<HorizontalLayoutGroup>();
            sHlg.spacing = 24f;
            sHlg.childAlignment = TextAnchor.MiddleCenter;
            sHlg.childControlWidth = false;
            sHlg.childControlHeight = false;
            hud.starFilled = LoadUISprite("ui_star_filled.png");
            hud.starEmpty = LoadUISprite("ui_star_empty.png");
            for (int i = 0; i < 3; i++)
            {
                var st = UIThemeHelper.CreatePanel("Star_" + i, starRow.transform, Color.white, hud.starFilled);
                st.rectTransform.sizeDelta = new Vector2(88f, 88f);
                hud.resultStars[i] = st;
            }

            hud.resultRewardsText = UIThemeHelper.CreateText("Rewards", resP.transform, "+150 SCRAP | +8 SHARDS", 28, TextAnchor.MiddleCenter, UIThemeHelper.AccentGreen);
            SetRect(hud.resultRewardsText.rectTransform, 0.05f, 0.32f, 0.95f, 0.48f);

            var rBtns = UIThemeHelper.CreateUIObject("ResultButtons", resP.transform);
            SetRect(rBtns.GetComponent<RectTransform>(), 0.06f, 0.08f, 0.94f, 0.26f);
            var rbHlg = rBtns.AddComponent<HorizontalLayoutGroup>();
            rbHlg.spacing = 18f;
            rbHlg.childControlWidth = true;
            rbHlg.childControlHeight = true;

            hud.rematchButton = UIThemeHelper.CreateButton("Rematch", rBtns.transform, "REMATCH", UIThemeHelper.AccentOrange, 24, btnPrimary);
            hud.workshopButton = UIThemeHelper.CreateButton("Workshop", rBtns.transform, "WORKSHOP", UIThemeHelper.PrimaryCyan, 24, btnPrimary);
            hud.menuButton = UIThemeHelper.CreateButton("Menu", rBtns.transform, "MAIN MENU", UIThemeHelper.MutedSlate, 24, btnSecondary);
        }

        private static void CreateMeterBar(string name, Transform parent, float x0, float y0, float x1, float y1,
            Color fillCol, Sprite bgSprite, Sprite fillSprite, out Image mainFill, out Image trailFill)
        {
            var bg = UIThemeHelper.CreatePanel(name, parent, Color.white, bgSprite);
            SetRect(bg.rectTransform, x0, y0, x1, y1);

            var trail = UIThemeHelper.CreatePanel("Trail", bg.transform, new Color(1f, 1f, 1f, 0.65f), fillSprite);
            UIThemeHelper.StretchFull(trail.rectTransform, 3f);
            trail.type = Image.Type.Filled;
            trail.fillMethod = Image.FillMethod.Horizontal;
            trail.fillAmount = 1f;
            trailFill = trail;

            var fill = UIThemeHelper.CreatePanel("Fill", bg.transform, fillCol, fillSprite);
            UIThemeHelper.StretchFull(fill.rectTransform, 3f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            mainFill = fill;
        }

        private static void SetRect(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void ConfigureProjectSettings()
        {
            PlayerSettings.companyName = "IronKnuckleStudio";
            PlayerSettings.productName = "Iron Knuckle: Bot Boxing";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenesRoot + "/MainMenu.unity", true),
                new EditorBuildSettingsScene(ScenesRoot + "/Garage.unity", true),
                new EditorBuildSettingsScene(ScenesRoot + "/Arena.unity", true)
            };
        }
    }
}
