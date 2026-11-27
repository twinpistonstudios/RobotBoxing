using System.Collections;
using IronKnuckle.Combat;
using IronKnuckle.Core;
using IronKnuckle.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IronKnuckle.UI
{
    public class FightHUD : MonoBehaviour
    {
        [Header("Player Bars")]
        public Text playerNameText;
        public Image playerHpFill;
        public Image playerHpTrailFill;
        public Image playerStaminaFill;
        public Image playerOverdriveFill;
        public Image playerGuardFill;

        [Header("Opponent Bars")]
        public Text opponentNameText;
        public Image opponentHpFill;
        public Image opponentHpTrailFill;
        public Image opponentStaminaFill;
        public Image opponentOverdriveFill;

        [Header("Match Info")]
        public Text roundText;
        public Text timerText;
        public Text comboText;
        public Text bannerText;

        [Header("Action Buttons")]
        public Button slipLeftButton;
        public Button slipRightButton;
        public EventTrigger guardHoldTrigger;
        public Button jabButton;
        public Button hookButton;
        public Button uppercutButton;
        public Button special1Button;
        public Text special1Label;
        public Button special2Button;
        public Text special2Label;
        public Button pauseButton;

        [Header("Overlays")]
        public GameObject rebootPanel;
        public Text rebootTimerText;
        public Image rebootProgressFill;
        public Button rebootTapButton;

        public GameObject tutorialBanner;
        public Text tutorialHintText;
        public Button skipTutorialButton;

        public GameObject pausePanel;
        public Button resumeButton;
        public Button graphicsButton;
        public Button quitToMenuButton;
        public GraphicsSettingsUI graphicsModal;

        public GameObject resultPanel;
        public Text resultTitleText;
        public Text resultRewardsText;
        public Image[] resultStars;
        public Sprite starFilled;
        public Sprite starEmpty;
        public Button rematchButton;
        public Button workshopButton;
        public Button menuButton;

        public CombatManager combatManager;
        public PlayerInputController playerInput;

        private FighterController player;
        private FighterController opponent;
        private CombatAbilityData sp1;
        private CombatAbilityData sp2;
        private Coroutine bannerRoutine;

        private int tutStep;
        private bool tutActive;

        private void Start()
        {
            if (rebootPanel != null) rebootPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (graphicsModal != null) graphicsModal.gameObject.SetActive(false);
            if (resultPanel != null) resultPanel.SetActive(false);
            if (tutorialBanner != null) tutorialBanner.SetActive(false);
            if (bannerText != null) bannerText.gameObject.SetActive(false);

            WireButtons();
        }

        private void WireButtons()
        {
            slipLeftButton?.onClick.AddListener(() => playerInput?.TriggerSlipLeft());
            slipRightButton?.onClick.AddListener(() => playerInput?.TriggerSlipRight());
            jabButton?.onClick.AddListener(() => playerInput?.TriggerJab());
            hookButton?.onClick.AddListener(() => playerInput?.TriggerLeftHook());
            uppercutButton?.onClick.AddListener(() => playerInput?.TriggerUppercut());
            special1Button?.onClick.AddListener(() => playerInput?.TriggerSpecial1());
            special2Button?.onClick.AddListener(() => playerInput?.TriggerSpecial2());

            if (guardHoldTrigger != null)
            {
                var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                down.callback.AddListener(_ => playerInput?.SetGuardHeld(true));
                guardHoldTrigger.triggers.Add(down);

                var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                up.callback.AddListener(_ => playerInput?.SetGuardHeld(false));
                guardHoldTrigger.triggers.Add(up);
            }

            rebootTapButton?.onClick.AddListener(() => combatManager?.OnRebootTap());
            pauseButton?.onClick.AddListener(() => combatManager?.SetPaused(true));
            resumeButton?.onClick.AddListener(() => combatManager?.SetPaused(false));
            graphicsButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                graphicsModal?.Open();
            });
            quitToMenuButton?.onClick.AddListener(() => GameManager.Instance?.LoadMainMenu());

            rematchButton?.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene("Arena");
            });
            workshopButton?.onClick.AddListener(() => GameManager.Instance?.LoadGarage());
            menuButton?.onClick.AddListener(() => GameManager.Instance?.LoadMainMenu());
            skipTutorialButton?.onClick.AddListener(CompleteTutorial);
        }

        public void BindFighters(FighterController p, FighterController o, CombatAbilityData s1, CombatAbilityData s2)
        {
            player = p;
            opponent = o;
            sp1 = s1;
            sp2 = s2;

            if (playerNameText != null) playerNameText.text = p.FighterName;
            if (opponentNameText != null) opponentNameText.text = o.FighterName;

            if (special1Label != null) special1Label.text = s1 != null ? s1.displayName.ToUpperInvariant() : "NO SPECIAL";
            if (special2Label != null) special2Label.text = s2 != null ? s2.displayName.ToUpperInvariant() : "SLOT 2 EMPTY";
        }

        private void Update()
        {
            if (player == null || opponent == null) return;

            float pHp = player.CurrentHp / player.MaxHp;
            float oHp = opponent.CurrentHp / opponent.MaxHp;

            if (playerHpFill != null) playerHpFill.fillAmount = pHp;
            if (playerHpTrailFill != null) playerHpTrailFill.fillAmount = Mathf.Lerp(playerHpTrailFill.fillAmount, pHp, Time.deltaTime * 4f);
            if (playerStaminaFill != null)
            {
                playerStaminaFill.fillAmount = player.CurrentStamina / player.MaxStamina;
                playerStaminaFill.color = player.IsOverheated ? UIThemeHelper.AccentRed : UIThemeHelper.AccentGold;
            }
            if (playerOverdriveFill != null) playerOverdriveFill.fillAmount = player.Overdrive / 100f;
            if (playerGuardFill != null) playerGuardFill.fillAmount = player.GuardStability / player.MaxGuardStability;

            if (opponentHpFill != null) opponentHpFill.fillAmount = oHp;
            if (opponentHpTrailFill != null) opponentHpTrailFill.fillAmount = Mathf.Lerp(opponentHpTrailFill.fillAmount, oHp, Time.deltaTime * 4f);
            if (opponentStaminaFill != null) opponentStaminaFill.fillAmount = opponent.CurrentStamina / opponent.MaxStamina;
            if (opponentOverdriveFill != null) opponentOverdriveFill.fillAmount = opponent.Overdrive / 100f;

            if (special1Button != null)
                special1Button.interactable = sp1 != null && player.Overdrive >= sp1.overdriveCost;
            if (special2Button != null)
                special2Button.interactable = sp2 != null && player.Overdrive >= sp2.overdriveCost;

            if (combatManager != null)
            {
                if (roundText != null) roundText.text = $"ROUND {combatManager.CurrentRound}/3";
                if (timerText != null) timerText.text = Mathf.CeilToInt(combatManager.RoundTimer).ToString("00");
            }

            if (comboText != null)
            {
                comboText.gameObject.SetActive(player.ComboCount >= 2);
                comboText.text = $"{player.ComboCount}x COMBO!";
            }
        }

        public void ShowBanner(string message, float duration)
        {
            if (bannerText == null) return;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerCoroutine(message, duration));
        }

        private IEnumerator BannerCoroutine(string msg, float dur)
        {
            bannerText.gameObject.SetActive(true);
            bannerText.text = msg;
            yield return new WaitForSecondsRealtime(dur);
            bannerText.gameObject.SetActive(false);
            bannerRoutine = null;
        }

        public void ShowRebootPanel(bool show, float progress)
        {
            if (rebootPanel != null) rebootPanel.SetActive(show);
            if (rebootProgressFill != null) rebootProgressFill.fillAmount = progress;
        }

        public void UpdateRebootTimer(int countLeft, float progress)
        {
            if (rebootTimerText != null) rebootTimerText.text = $"SYSTEM DOWN! TAP REBOOT! ({countLeft})";
            if (rebootProgressFill != null) rebootProgressFill.fillAmount = progress;
        }

        public void ShowPausePanel(bool show)
        {
            if (pausePanel != null) pausePanel.SetActive(show);
        }

        public void ShowResultPanel(bool won, bool byKO, int stars, int scrap, int shards)
        {
            if (resultPanel == null) return;
            resultPanel.SetActive(true);
            if (resultTitleText != null)
            {
                resultTitleText.text = won ? (byKO ? "KNOCKOUT VICTORY!" : "VICTORY BY DECISION!") : "DEFEATED!";
                resultTitleText.color = won ? UIThemeHelper.AccentGold : UIThemeHelper.AccentRed;
            }
            if (resultRewardsText != null)
            {
                resultRewardsText.text = $"+{scrap} SCRAP CREDITS    |    +{shards} GEAR SHARDS";
            }
            if (resultStars != null)
            {
                for (int i = 0; i < resultStars.Length; i++)
                {
                    resultStars[i].sprite = (i < stars) ? starFilled : starEmpty;
                }
            }
        }

        public void StartOnboardingTutorial(PlayerInputController input)
        {
            if (tutorialBanner == null || input == null) return;
            tutActive = true;
            tutStep = 0;
            tutorialBanner.SetActive(true);
            UpdateTutorialPrompt();
            input.OnPlayerActionPerformed += HandleTutorialAction;
        }

        private void HandleTutorialAction(string action)
        {
            if (!tutActive) return;
            if (tutStep == 0 && (action == "JAB" || action == "CROSS")) { tutStep = 1; UpdateTutorialPrompt(); }
            else if (tutStep == 1 && (action == "HOOK" || action == "UPPER")) { tutStep = 2; UpdateTutorialPrompt(); }
            else if (tutStep == 2 && (action == "SLIP" || action == "GUARD")) { CompleteTutorial(); }
        }

        private void UpdateTutorialPrompt()
        {
            if (tutorialHintText == null) return;
            switch (tutStep)
            {
                case 0:
                    tutorialHintText.text = "TIP 1/3: TAP left/right or press JAB to throw fast punches!";
                    break;
                case 1:
                    tutorialHintText.text = "TIP 2/3: SWIPE across or UP (or tap HOOK/UPPER) for heavy damage!";
                    break;
                case 2:
                    tutorialHintText.text = "TIP 3/3: HOLD GUARD to block or tap SLIP L/R to dodge & COUNTER!";
                    break;
            }
        }

        private void CompleteTutorial()
        {
            tutActive = false;
            if (playerInput != null) playerInput.OnPlayerActionPerformed -= HandleTutorialAction;
            if (tutorialBanner != null) tutorialBanner.SetActive(false);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveData.tutorialCompleted = true;
                GameManager.Instance.PersistSave();
            }
        }
    }
}
