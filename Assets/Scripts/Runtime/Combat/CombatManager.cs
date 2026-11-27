using System.Collections;
using IronKnuckle.Core;
using IronKnuckle.Data;
using IronKnuckle.Robot;
using IronKnuckle.UI;
using UnityEngine;

namespace IronKnuckle.Combat
{
    public class CombatManager : MonoBehaviour
    {
        public Transform playerSpawn;
        public Transform opponentSpawn;
        public FighterController playerFighter;
        public FighterController opponentFighter;
        public PlayerInputController playerInput;
        public EnemyAIController enemyAI;
        public CombatVFXManager vfxManager;
        public CombatCameraController cameraController;
        public FightHUD fightHUD;

        public int CurrentRound { get; private set; } = 1;
        public float RoundTimer { get; private set; } = 60f;
        public bool MatchActive { get; private set; }
        public bool IsPaused { get; private set; }

        private OpponentData activeOpponentData;
        private bool awaitingKnockdownRecovery;
        private int knockdownTapProgress;
        private const int KnockdownTapsRequired = 12;

        private void Start()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM("bgm_fight");
            SetupFighters();
            StartCoroutine(MatchFlowRoutine());
        }

        private void SetupFighters()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Database == null) return;
            var db = gm.Database;
            var save = gm.SaveData;

            activeOpponentData = gm.GetActiveOpponent();

            // Build Player Robot
            var pBuilder = playerFighter.GetComponent<RobotBuilder>();
            pBuilder.ApplyLoadout(db,
                save.equippedHeadId, save.equippedTorsoId,
                save.equippedLeftArmId, save.equippedRightArmId,
                save.equippedLegsId, save.equippedCoreId,
                save.equippedColorId, save.equippedFinishIndex);

            var pStats = gm.Progression.ComputePlayerTotalStats();
            var sp1 = db.GetAbility(save.equippedSpecial1Id);
            var sp2 = db.GetAbility(save.equippedSpecial2Id);
            var passive = db.GetAbility(save.equippedPassiveId);

            playerFighter.InitializeFighter("IRON CHALLENGER", true, pStats, sp1, sp2, passive);
            playerFighter.Opponent = opponentFighter;

            // Build Opponent Robot
            var oBuilder = opponentFighter.GetComponent<RobotBuilder>();
            if (activeOpponentData != null)
            {
                oBuilder.ApplyLoadout(db,
                    activeOpponentData.headPartId, activeOpponentData.torsoPartId,
                    activeOpponentData.leftArmPartId, activeOpponentData.rightArmPartId,
                    activeOpponentData.legsPartId, activeOpponentData.corePartId,
                    activeOpponentData.colorSchemeId);

                var oStats = gm.Progression.ComputeStatsForLoadout(
                    activeOpponentData.headPartId, activeOpponentData.torsoPartId,
                    activeOpponentData.leftArmPartId, activeOpponentData.rightArmPartId,
                    activeOpponentData.legsPartId, activeOpponentData.corePartId,
                    activeOpponentData.partLevel);

                var oSpecial = db.GetAbility(activeOpponentData.specialAbilityId);
                opponentFighter.InitializeFighter(activeOpponentData.fighterName.ToUpperInvariant(), false, oStats, oSpecial, null, null);
                enemyAI.Configure(activeOpponentData.aiStyle, activeOpponentData.aiDifficulty);
            }
            else
            {
                opponentFighter.InitializeFighter("SCRAPBOT", false, pStats, null, null, null);
            }

            opponentFighter.Opponent = playerFighter;

            playerFighter.OnAttackLanded += HandleAttackLanded;
            opponentFighter.OnAttackLanded += HandleAttackLanded;
            playerFighter.OnKnockedDown += HandleFighterKnockedDown;
            opponentFighter.OnKnockedDown += HandleFighterKnockedDown;
            playerFighter.OnOverheated += HandleOverheated;
            opponentFighter.OnOverheated += HandleOverheated;

            if (fightHUD != null)
            {
                fightHUD.BindFighters(playerFighter, opponentFighter, sp1, sp2);
            }
        }

        private void Update()
        {
            if (!MatchActive || IsPaused) return;

            float dt = Time.deltaTime;
            playerFighter.TickCombat(dt);
            opponentFighter.TickCombat(dt);

            if (!awaitingKnockdownRecovery)
            {
                RoundTimer -= dt;
                if (RoundTimer <= 0f)
                {
                    RoundTimer = 0f;
                    StartCoroutine(HandleRoundTimeout());
                }
            }
        }

        private IEnumerator MatchFlowRoutine()
        {
            MatchActive = false;
            playerInput.inputEnabled = false;
            enemyAI.aiEnabled = false;

            CurrentRound = 1;
            RoundTimer = 60f;
            fightHUD?.ShowBanner($"ROUND {CurrentRound}", 1.1f);
            yield return new WaitForSeconds(1.1f);

            AudioManager.Instance?.PlaySFX("sfx_bell_single", 1f, 0.02f);
            fightHUD?.ShowBanner("FIGHT!", 0.85f);
            MatchActive = true;
            playerInput.inputEnabled = true;
            enemyAI.aiEnabled = true;

            if (GameManager.Instance != null && !GameManager.Instance.SaveData.tutorialCompleted)
            {
                fightHUD?.StartOnboardingTutorial(playerInput);
            }
        }

        private void HandleAttackLanded(FighterController attacker, FighterController defender, PunchType punch, HitResolution res)
        {
            Vector3 hitPos = defender.Builder != null && defender.Builder.HeadPivot != null
                ? defender.Builder.HeadPivot.position + Vector3.up * 0.1f
                : defender.transform.position + Vector3.up * 1.5f;

            if (res.wasDodged)
            {
                AudioManager.Instance?.PlaySFX("sfx_dodge");
                vfxManager?.SpawnFloatingText(hitPos, "SLIP! COUNTER!", new Color(0.25f, 0.95f, 1f), 1.05f);
                return;
            }

            if (res.wasParried)
            {
                AudioManager.Instance?.PlaySFX("sfx_parry");
                vfxManager?.SpawnImpactSparks(hitPos, new Color(0.4f, 0.95f, 1f), 20);
                vfxManager?.SpawnFloatingText(hitPos, "PARRY!", new Color(0.3f, 1f, 0.9f), 1.25f);
                vfxManager?.TriggerHitstop(0.08f, 0.05f);
                cameraController?.AddTrauma(0.35f, 3f);
                return;
            }

            if (res.wasBlocked)
            {
                AudioManager.Instance?.PlaySFX(res.brokeGuard ? "sfx_hit_heavy" : "sfx_block");
                vfxManager?.SpawnImpactSparks(hitPos, res.brokeGuard ? new Color(1f, 0.3f, 0.2f) : new Color(0.6f, 0.8f, 1f), 12);
                vfxManager?.SpawnFloatingText(hitPos, res.brokeGuard ? "GUARD BREAK!" : $"BLOCK -{Mathf.RoundToInt(res.finalDamage)}",
                    res.brokeGuard ? new Color(1f, 0.35f, 0.2f) : new Color(0.75f, 0.85f, 1f), res.brokeGuard ? 1.25f : 0.85f);
                cameraController?.AddTrauma(res.brokeGuard ? 0.5f : 0.16f);
                return;
            }

            bool isHeavy = res.wasCrit || res.wasCounter || punch == PunchType.Uppercut || punch == PunchType.Special1 || punch == PunchType.Special2;
            AudioManager.Instance?.PlaySFX(res.wasCrit ? "sfx_hit_crit" : (isHeavy ? "sfx_hit_heavy" : "sfx_hit_light"));

            Color sparkCol = res.wasCrit ? new Color(1f, 0.85f, 0.15f) : (res.wasCounter ? new Color(0.2f, 0.95f, 1f) : new Color(1f, 0.55f, 0.15f));
            vfxManager?.SpawnImpactSparks(hitPos, sparkCol, isHeavy ? 24 : 14);

            string label = $"-{Mathf.RoundToInt(res.finalDamage)}";
            if (res.wasCounter) label = $"COUNTER! {label}";
            else if (res.wasCrit) label = $"CRIT! {label}";
            vfxManager?.SpawnFloatingText(hitPos, label, sparkCol, isHeavy ? 1.25f : 0.95f);

            if (isHeavy)
            {
                vfxManager?.TriggerHitstop(res.wasCrit ? 0.085f : 0.055f, 0.08f);
                cameraController?.AddTrauma(0.55f, 4.5f);
            }
            else
            {
                cameraController?.AddTrauma(0.22f, 1.5f);
            }
        }

        private void HandleOverheated(FighterController fighter)
        {
            AudioManager.Instance?.PlaySFX("sfx_overheat");
            Vector3 pos = fighter.transform.position + Vector3.up * 1.9f;
            vfxManager?.SpawnFloatingText(pos, "OVERHEAT!", new Color(1f, 0.4f, 0.2f), 1.1f);
        }

        private void HandleFighterKnockedDown(FighterController downedFighter)
        {
            if (!MatchActive) return;
            AudioManager.Instance?.PlaySFX("sfx_ko_drop");
            cameraController?.AddTrauma(0.8f, 6f);

            if (downedFighter.State == FighterState.KO)
            {
                StartCoroutine(FinishMatchRoutine(downedFighter != playerFighter, true));
            }
            else
            {
                StartCoroutine(KnockdownCountRoutine(downedFighter));
            }
        }

        private IEnumerator KnockdownCountRoutine(FighterController downed)
        {
            awaitingKnockdownRecovery = true;
            playerInput.inputEnabled = false;
            enemyAI.aiEnabled = false;

            fightHUD?.ShowBanner("KNOCKDOWN!", 1.2f);
            yield return new WaitForSeconds(1.0f);

            if (downed == playerFighter)
            {
                knockdownTapProgress = 0;
                fightHUD?.ShowRebootPanel(true, 0f);
                float count = 10f;
                while (count > 0f && knockdownTapProgress < KnockdownTapsRequired)
                {
                    count -= Time.deltaTime * 1.25f;
                    fightHUD?.UpdateRebootTimer(Mathf.CeilToInt(count), (float)knockdownTapProgress / KnockdownTapsRequired);
                    yield return null;
                }
                fightHUD?.ShowRebootPanel(false, 0f);

                if (knockdownTapProgress >= KnockdownTapsRequired)
                {
                    downed.RecoverFromKnockdown(0.32f);
                    fightHUD?.ShowBanner("REBOOTED!", 0.9f);
                    yield return new WaitForSeconds(0.9f);
                }
                else
                {
                    downed.ReceiveHit(new HitResolution { finalDamage = 999f }, null);
                    StartCoroutine(FinishMatchRoutine(false, true));
                    yield break;
                }
            }
            else
            {
                // AI stands back up on 1st knockdown after brief count
                for (int c = 1; c <= 4; c++)
                {
                    fightHUD?.ShowBanner($"COUNT: {c}", 0.55f);
                    yield return new WaitForSeconds(0.55f);
                }
                downed.RecoverFromKnockdown(0.30f);
                fightHUD?.ShowBanner("OPPONENT UP!", 0.85f);
                yield return new WaitForSeconds(0.85f);
            }

            awaitingKnockdownRecovery = false;
            playerInput.inputEnabled = true;
            enemyAI.aiEnabled = true;
        }

        public void OnRebootTap()
        {
            if (!awaitingKnockdownRecovery) return;
            knockdownTapProgress++;
            AudioManager.Instance?.PlaySFX("sfx_ui_click", 0.9f);
        }

        private IEnumerator HandleRoundTimeout()
        {
            MatchActive = false;
            playerInput.inputEnabled = false;
            enemyAI.aiEnabled = false;
            AudioManager.Instance?.PlaySFX("sfx_bell_single");

            if (CurrentRound < 3)
            {
                fightHUD?.ShowBanner($"END OF ROUND {CurrentRound}", 1.4f);
                yield return new WaitForSeconds(1.5f);
                CurrentRound++;
                RoundTimer = 60f;
                playerFighter.ResetForNewRound(false);
                opponentFighter.ResetForNewRound(false);
                fightHUD?.ShowBanner($"ROUND {CurrentRound}", 1.0f);
                yield return new WaitForSeconds(1.0f);
                AudioManager.Instance?.PlaySFX("sfx_bell_single");
                fightHUD?.ShowBanner("FIGHT!", 0.7f);
                MatchActive = true;
                playerInput.inputEnabled = true;
                enemyAI.aiEnabled = true;
            }
            else
            {
                float pPct = playerFighter.CurrentHp / playerFighter.MaxHp;
                float oPct = opponentFighter.CurrentHp / opponentFighter.MaxHp;
                bool playerWon = pPct >= oPct;
                fightHUD?.ShowBanner("TIME UP! DECISION...", 1.5f);
                yield return new WaitForSeconds(1.5f);
                StartCoroutine(FinishMatchRoutine(playerWon, false));
            }
        }

        private IEnumerator FinishMatchRoutine(bool playerWon, bool byKO)
        {
            MatchActive = false;
            awaitingKnockdownRecovery = false;
            playerInput.inputEnabled = false;
            enemyAI.aiEnabled = false;

            if (byKO)
            {
                AudioManager.Instance?.PlaySFX("sfx_bell_ko");
                cameraController?.SetKOOrbit(true);
                vfxManager?.TriggerHitstop(0.20f, 0.15f);
                fightHUD?.ShowBanner("K.O.!", 1.8f);
            }
            else
            {
                fightHUD?.ShowBanner(playerWon ? "DECISION WIN!" : "DEFEAT", 1.5f);
            }

            if (playerWon)
            {
                playerFighter.SetVictoryPose();
                AudioManager.Instance?.PlaySFX("sfx_crowd_cheer");
            }
            else
            {
                opponentFighter.SetVictoryPose();
            }

            yield return new WaitForSecondsRealtime(1.8f);
            cameraController?.SetKOOrbit(false);

            int stars = 0;
            if (playerWon)
            {
                float hpPct = playerFighter.CurrentHp / playerFighter.MaxHp;
                stars = hpPct >= 0.65f ? 3 : (hpPct >= 0.30f ? 2 : 1);
            }

            int scrapEarned = 0;
            int shardsEarned = 0;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.Progression.RecordMatchResult(playerWon, byKO, stars, gm.IsQuickSparring ? null : activeOpponentData, out scrapEarned, out shardsEarned);
                gm.PersistSave();
            }

            fightHUD?.ShowResultPanel(playerWon, byKO, stars, scrapEarned, shardsEarned);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            fightHUD?.ShowPausePanel(paused);
        }
    }
}
