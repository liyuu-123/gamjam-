using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WrongTurn.TurnBased
{
    public sealed class GridTurnHud : MonoBehaviour
    {
        [Header("Roots")]
        public GameObject TitleRoot;
        public GameObject GameRoot;
        public GameObject PauseRoot;
        public GameObject MemoryRoot;
        public GameObject EndRoot;

        [Header("Title")]
        public Text TitleStageText;
        public Text TitleDescription;
        public Button StartButton;
        public Button[] MapButtons;
        public Text[] MapButtonLabels;
        public Image TitlePortrait;
        public Image TitlePortraitGlow;

        [Header("HUD")]
        public Image Portrait;
        public Text HpText;
        public Text ShadowText;
        public Text LightText;
        public Text TurnText;
        public Text StageText;
        public Text ObjectiveText;
        public Text ToastText;
        public Text ControlHint;
        public Text BossText;
        public GameObject BossBarRoot;
        public Image BossFill;
        public Image TurnGlow;
        public Image[] HpCells;
        public Image[] ShadowCells;
        public Image[] LightCells;

        [Header("Pause")]
        public Button ResumeButton;
        public Button PauseRestartButton;
        public Button PauseMapButton;

        [Header("Memory")]
        public Text MemoryTitle;
        public Button[] MemoryButtons;
        public Text[] MemoryButtonTitles;
        public Text[] MemoryButtonDescriptions;

        [Header("End")]
        public Text EndTitle;
        public Text EndDescription;
        public Button EndRestartButton;
        public Button EndMapButton;

        private static readonly Color CellOn = new Color32(92, 231, 239, 255);
        private static readonly Color CellOff = new Color32(24, 47, 58, 220);
        private static Font dynamicFont;
        private float toastTimer;
        private float animationTime;

        private void Awake()
        {
            EnsureFonts();
            HideAllRoots();
        }

        private void Update()
        {
            animationTime += Time.unscaledDeltaTime;
            if (TitlePortrait != null)
            {
                var scale = 1f + Mathf.Sin(animationTime * 1.8f) * 0.012f;
                TitlePortrait.rectTransform.localScale = Vector3.one * scale;
            }
            if (TitlePortraitGlow != null)
            {
                var glow = TitlePortraitGlow.color;
                glow.a = 0.16f + Mathf.Sin(animationTime * 1.5f) * 0.045f;
                TitlePortraitGlow.color = glow;
            }
            if (TurnGlow != null)
            {
                var glow = TurnGlow.color;
                glow.a = 0.22f + Mathf.Sin(animationTime * 3.2f) * 0.07f;
                TurnGlow.color = glow;
            }
        }

        public void Bind(GridTurnController controller)
        {
            EnsureFonts();
            if (StartButton != null)
                StartButton.onClick.AddListener(controller.StartSelectedStage);
            if (MapButtons != null)
            {
                for (var i = 0; i < MapButtons.Length; i++)
                {
                    var index = i;
                    if (MapButtons[i] != null)
                        MapButtons[i].onClick.AddListener(() =>
                            controller.SelectStageForTitle(index));
                }
            }

            if (ResumeButton != null)
                ResumeButton.onClick.AddListener(controller.ResumeGame);
            if (PauseRestartButton != null)
                PauseRestartButton.onClick.AddListener(controller.RestartStage);
            if (PauseMapButton != null)
                PauseMapButton.onClick.AddListener(controller.ReturnToTitle);

            if (MemoryButtons != null)
            {
                for (var i = 0; i < MemoryButtons.Length; i++)
                {
                    var index = i;
                    if (MemoryButtons[i] != null)
                        MemoryButtons[i].onClick.AddListener(() =>
                            controller.SelectMemory(index));
                }
            }

            if (EndRestartButton != null)
                EndRestartButton.onClick.AddListener(controller.RestartStage);
            if (EndMapButton != null)
                EndMapButton.onClick.AddListener(controller.ReturnToTitle);
        }

        public void ShowTitle(int selectedStage, string[] stageNames)
        {
            HideAllRoots();
            TitleRoot.SetActive(true);
            if (TitleStageText != null)
                TitleStageText.text = "进入 " + stageNames[Mathf.Clamp(selectedStage, 0,
                    stageNames.Length - 1)];
            if (TitleDescription != null)
            {
                TitleDescription.text =
                    "方向斩立即结算；近战红格可走开，远程敌人准备一回合后开枪。\n" +
                    "虚影可穿墙探路，但身体仍会留在黑暗里。";
            }
            UpdateMapButtons(selectedStage);
        }

        public void ShowGame()
        {
            HideAllRoots();
            GameRoot.SetActive(true);
        }

        public void ShowPause(bool show)
        {
            if (PauseRoot != null)
                PauseRoot.SetActive(show);
            if (GameRoot != null)
                GameRoot.SetActive(!show);
        }

        public void ShowMemory(GridTurnSession session)
        {
            HideAllRoots();
            GameRoot.SetActive(true);
            MemoryRoot.SetActive(true);
            var choices = session.ActiveMemoryChoices;
            if (MemoryTitle != null)
                MemoryTitle.text = "触碰记忆冰晶";
            for (var i = 0; i < MemoryButtons.Length; i++)
            {
                var active = i < choices.Count;
                MemoryButtons[i].gameObject.SetActive(active);
                if (!active)
                    continue;
                MemoryButtonTitles[i].text = choices[i].Title + " / " + choices[i].Short;
                MemoryButtonDescriptions[i].text = choices[i].Description;
            }
        }

        public void HideMemory()
        {
            if (MemoryRoot != null)
                MemoryRoot.SetActive(false);
        }

        public void ShowEnd(bool won)
        {
            HideAllRoots();
            EndRoot.SetActive(true);
            if (EndTitle != null)
                EndTitle.text = won ? "走出错误之路" : "灯火熄灭";
            if (EndDescription != null)
            {
                EndDescription.text = won
                    ? "你带着记忆和旧路离开了三境。"
                    : "所有生命都已耗尽。重新进入迷宫，或者换一张地图。";
            }
        }

        public void ShowToast(string text, Color color)
        {
            if (ToastText == null)
                return;
            ToastText.text = text;
            ToastText.color = color;
            ToastText.canvasRenderer.SetAlpha(1f);
            toastTimer = 2.6f;
        }

        public void Refresh(GridTurnSession session, string stageName, int selectedStage)
        {
            GameRoot.SetActive(true);
            if (session == null)
                return;

            if (Portrait != null && Portrait.sprite == null)
                Portrait.enabled = false;

            if (StageText != null)
                StageText.text = (selectedStage + 1) + " / 3  " + stageName;
            if (TurnText != null)
            {
                TurnText.text = session.Phase == GridTurnPhase.WorldTurn
                    ? "世界回合"
                    : session.SoulActive ? "虚影回合" : "玩家回合";
            }
            if (HpText != null)
                HpText.text = "生命 " + session.PlayerHp + " / " +
                              GridTurnSession.PlayerMaxHp;
            if (ShadowText != null)
                ShadowText.text = "影子 " + session.Shadow + " 格";
            if (LightText != null)
                LightText.text = "微光 " + session.Light + " 格";
            if (ObjectiveText != null)
            {
                ObjectiveText.text = session.ExitOpen
                    ? "Boss 已倒下，前往打开的地图出口。"
                    : session.BossActive ? "Boss 已苏醒。观察红色危险格。" :
                    "探索四向视野，寻找冰晶和 Boss。";
            }
            if (ControlHint != null)
            {
                ControlHint.text =
                    "鼠标选向攻击 · 近战红格可躲避 · 远程蓄力一回合后发射可见弹体";
            }

            FillCells(HpCells, session.PlayerHp);
            FillCells(ShadowCells, session.Shadow);
            FillCells(LightCells, session.Light, new Color32(251, 184, 69, 255));

            var boss = session.Boss;
            var showBoss = boss != null && boss.Alive &&
                           (session.BossActive || boss.Hp < boss.MaxHp);
            if (BossBarRoot != null)
                BossBarRoot.SetActive(showBoss);
            if (showBoss && BossFill != null)
            {
                BossFill.fillAmount = boss.MaxHp <= 0
                    ? 0f
                    : boss.Hp / (float)boss.MaxHp;
                BossFill.color = boss.IsBossSecondPhase
                    ? new Color(1f, 0.20f, 0.10f, 0.96f)
                    : new Color(1f, 0.54f, 0.18f, 0.96f);
            }
            if (BossText != null && showBoss)
            {
                BossText.text = "守门者  " + boss.Hp + " / " + boss.MaxHp +
                                (boss.IsBossSecondPhase ? "   二阶段" : "");
            }

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                var alpha = Mathf.Clamp01(toastTimer);
                ToastText.canvasRenderer.SetAlpha(alpha);
            }
        }

        public void UpdateMapButtons(int selectedStage)
        {
            if (MapButtons == null)
                return;
            for (var i = 0; i < MapButtons.Length; i++)
            {
                var image = MapButtons[i].targetGraphic as Image;
                if (image != null)
                {
                    image.color = i == selectedStage
                        ? new Color(0.52f, 0.80f, 0.95f, 0.95f)
                        : new Color(0.28f, 0.33f, 0.48f, 0.85f);
                }
            }
        }

        private void HideAllRoots()
        {
            if (TitleRoot != null) TitleRoot.SetActive(false);
            if (GameRoot != null) GameRoot.SetActive(false);
            if (PauseRoot != null) PauseRoot.SetActive(false);
            if (MemoryRoot != null) MemoryRoot.SetActive(false);
            if (EndRoot != null) EndRoot.SetActive(false);
        }

        private static void FillCells(Image[] cells, int amount, Color? activeColor = null)
        {
            if (cells == null)
                return;
            var color = activeColor ?? CellOn;
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null)
                    cells[i].color = i < amount ? color : CellOff;
            }
        }

        private void EnsureFonts()
        {
            if (dynamicFont == null)
            {
                dynamicFont = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial"
                }, 24);
            }
            if (dynamicFont == null)
                return;
            foreach (var text in GetComponentsInChildren<Text>(true))
                text.font = dynamicFont;
        }
    }
}
