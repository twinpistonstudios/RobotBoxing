using System.Collections.Generic;
using IronKnuckle.Core;
using IronKnuckle.Data;
using UnityEngine;
using UnityEngine.UI;

namespace IronKnuckle.UI
{
    public class LeagueSelectUI : MonoBehaviour
    {
        public Button closeButton;
        public Button[] leagueTabButtons;
        public Transform cardsContainer;
        public Sprite cardSprite;
        public Sprite buttonSprite;
        public Sprite starFilled;
        public Sprite starEmpty;

        private int selectedLeague;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(() =>
                {
                    AudioManager.Instance?.PlaySFX("sfx_ui_click");
                    gameObject.SetActive(false);
                });
            }

            if (leagueTabButtons != null)
            {
                for (int i = 0; i < leagueTabButtons.Length; i++)
                {
                    int idx = i;
                    leagueTabButtons[i].onClick.AddListener(() =>
                    {
                        AudioManager.Instance?.PlaySFX("sfx_ui_click");
                        SelectLeague(idx);
                    });
                }
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            SelectLeague(selectedLeague);
        }

        public void SelectLeague(int leagueIndex)
        {
            selectedLeague = Mathf.Clamp(leagueIndex, 0, 3);
            if (leagueTabButtons != null)
            {
                for (int i = 0; i < leagueTabButtons.Length; i++)
                {
                    var img = leagueTabButtons[i].GetComponent<Image>();
                    if (img != null) img.color = (i == selectedLeague) ? UIThemeHelper.AccentGold : UIThemeHelper.MutedSlate;
                }
            }
            RebuildOpponentCards();
        }

        private void RebuildOpponentCards()
        {
            if (cardsContainer == null) return;
            for (int i = cardsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(cardsContainer.GetChild(i).gameObject);
            }

            var gm = GameManager.Instance;
            if (gm == null || gm.Database == null) return;

            List<OpponentData> opps = gm.Database.GetOpponentsInLeague(selectedLeague);
            foreach (var opp in opps)
            {
                BuildOpponentCard(opp, gm);
            }
        }

        private void BuildOpponentCard(OpponentData opp, GameManager gm)
        {
            bool unlocked = gm.Progression.IsOpponentUnlocked(opp);
            int stars = gm.SaveData.GetOpponentStars(opp.id);
            var oStats = gm.Progression.ComputeStatsForLoadout(
                opp.headPartId, opp.torsoPartId, opp.leftArmPartId, opp.rightArmPartId, opp.legsPartId, opp.corePartId, opp.partLevel);
            int pwr = oStats.ComputePowerRating();

            var cardImg = UIThemeHelper.CreatePanel("Card_" + opp.id, cardsContainer,
                opp.isBoss ? new Color(0.24f, 0.14f, 0.18f, 0.96f) : UIThemeHelper.PanelCard, cardSprite);
            var le = cardImg.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 420f;
            le.preferredHeight = 460f;

            var vlg = cardImg.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(18, 18, 18, 18);
            vlg.spacing = 10f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            string tag = opp.isBoss ? "LEAGUE CHAMPION BOSS" : $"MATCH {opp.matchIndex + 1}";
            var tagTxt = UIThemeHelper.CreateText("Tag", cardImg.transform, tag, 18, TextAnchor.MiddleCenter,
                opp.isBoss ? UIThemeHelper.AccentGold : UIThemeHelper.PrimaryCyan);
            tagTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            var nameTxt = UIThemeHelper.CreateText("Name", cardImg.transform, opp.fighterName.ToUpperInvariant(), 30, TextAnchor.MiddleCenter, Color.white);
            nameTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

            var titleTxt = UIThemeHelper.CreateText("Title", cardImg.transform, opp.title, 19, TextAnchor.MiddleCenter, new Color(0.75f, 0.82f, 0.92f));
            titleTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            var infoTxt = UIThemeHelper.CreateText("Info", cardImg.transform,
                $"STYLE: {opp.aiStyle}\nPOWER: {pwr}   |   LV.{opp.partLevel}\nHP: {Mathf.RoundToInt(oStats.maxHp)}   ATK: {Mathf.RoundToInt(oStats.attack)}",
                20, TextAnchor.MiddleCenter, new Color(0.86f, 0.92f, 1f), FontStyle.Normal);
            infoTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 96f;

            var quoteTxt = UIThemeHelper.CreateText("Quote", cardImg.transform, $"\"{opp.quote}\"", 17, TextAnchor.MiddleCenter, new Color(0.98f, 0.84f, 0.45f), FontStyle.Italic);
            quoteTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 48f;

            // Stars row
            var starRow = UIThemeHelper.CreateUIObject("Stars", cardImg.transform);
            starRow.AddComponent<LayoutElement>().preferredHeight = 42f;
            var hlg = starRow.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 12f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            for (int s = 1; s <= 3; s++)
            {
                var st = UIThemeHelper.CreatePanel("Star_" + s, starRow.transform, Color.white, s <= stars ? starFilled : starEmpty);
                st.rectTransform.sizeDelta = new Vector2(36f, 36f);
            }

            var rewardTxt = UIThemeHelper.CreateText("Reward", cardImg.transform,
                $"REWARD: +{opp.rewardScrap} SCRAP  |  +{opp.rewardShards} SHARDS", 18, TextAnchor.MiddleCenter, UIThemeHelper.AccentGreen);
            rewardTxt.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;

            var fightBtn = UIThemeHelper.CreateButton("FightBtn", cardImg.transform, unlocked ? "FIGHT!" : "LOCKED",
                unlocked ? (opp.isBoss ? UIThemeHelper.AccentOrange : UIThemeHelper.PrimaryCyan) : UIThemeHelper.MutedSlate, 26, buttonSprite);
            fightBtn.gameObject.AddComponent<LayoutElement>().preferredHeight = 68f;
            fightBtn.interactable = unlocked;
            fightBtn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySFX("sfx_ui_click");
                gm.StartCareerMatch(opp.id);
            });
        }
    }
}
