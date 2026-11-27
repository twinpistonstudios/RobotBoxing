using IronKnuckle.Core;
using IronKnuckle.Robot;
using UnityEngine;
using UnityEngine.UI;

namespace IronKnuckle.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("3D Showcase")]
        public RobotBuilder showcaseRobot;

        [Header("Top Bar")]
        public Text scrapText;
        public Text shardsText;
        public Text powerText;
        public Text recordText;

        [Header("Daily Mission")]
        public Text dailyMissionText;
        public Button claimDailyButton;
        public Text claimDailyLabel;

        [Header("Main Buttons")]
        public Button careerButton;
        public Button quickSparButton;
        public Button workshopButton;
        public Button skillLabButton;
        public Button settingsButton;

        [Header("Modals")]
        public LeagueSelectUI leagueSelectModal;
        public GraphicsSettingsUI settingsModal;

        public static int RequestedGarageTabOnOpen = -1;

        private void Start()
        {
            AudioManager.Instance?.PlayBGM("bgm_menu");
            RefreshShowcaseAndHeader();

            careerButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                leagueSelectModal?.Open();
            });

            quickSparButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                GameManager.Instance?.StartQuickSparring();
            });

            workshopButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                RequestedGarageTabOnOpen = 0;
                GameManager.Instance?.LoadGarage();
            });

            skillLabButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                RequestedGarageTabOnOpen = 3;
                GameManager.Instance?.LoadGarage();
            });

            claimDailyButton?.onClick.AddListener(OnClaimDaily);
            settingsButton?.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                settingsModal?.Open();
            });

            if (settingsModal != null)
            {
                settingsModal.onSaveReset = RefreshShowcaseAndHeader;
                settingsModal.gameObject.SetActive(false);
            }
            if (leagueSelectModal != null) leagueSelectModal.gameObject.SetActive(false);
        }

        public void RefreshShowcaseAndHeader()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Database == null) return;
            var save = gm.SaveData;

            if (showcaseRobot != null)
            {
                showcaseRobot.ApplyLoadout(gm.Database,
                    save.equippedHeadId, save.equippedTorsoId,
                    save.equippedLeftArmId, save.equippedRightArmId,
                    save.equippedLegsId, save.equippedCoreId,
                    save.equippedColorId, save.equippedFinishIndex);
                var vis = showcaseRobot.GetComponent<RobotVisualController>();
                vis?.Initialize(true);
            }

            var stats = gm.Progression.ComputePlayerTotalStats();
            if (scrapText != null) scrapText.text = save.scrapCredits.ToString();
            if (shardsText != null) shardsText.text = save.gearShards.ToString();
            if (powerText != null) powerText.text = $"PWR {stats.ComputePowerRating()}";
            if (recordText != null) recordText.text = $"WINS: {save.totalWins}  |  K.O.: {save.totalKOs}  |  STREAK: {save.winStreak}";

            RefreshDailyMission();
        }

        private void RefreshDailyMission()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var save = gm.SaveData;
            int progress = Mathf.Min(3, save.dailyMatchesPlayed);

            if (dailyMissionText != null)
            {
                dailyMissionText.text = save.dailyMissionClaimed
                    ? "DAILY BOUNTY: COMPLETED (+300 Scrap, +18 Shards)"
                    : $"DAILY BOUNTY: Fight 3 Matches ({progress}/3) -> +300 Scrap & +18 Shards";
            }

            if (claimDailyButton != null)
            {
                bool canClaim = gm.Progression.CanClaimDailyMission();
                claimDailyButton.interactable = canClaim;
                if (claimDailyLabel != null)
                {
                    claimDailyLabel.text = save.dailyMissionClaimed ? "CLAIMED" : (canClaim ? "CLAIM!" : $"{progress}/3");
                }
            }
        }

        private void OnClaimDaily()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.Progression.TryClaimDailyMission())
            {
                gm.PersistSave();
                AudioManager.Instance?.PlaySFX("sfx_upgrade");
                RefreshShowcaseAndHeader();
            }
        }

    }
}
