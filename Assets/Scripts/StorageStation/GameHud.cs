using UnityEngine;
using UnityEngine.UI;

namespace StorageStation
{
    [DefaultExecutionOrder(100)]
    public sealed class GameHud : MonoBehaviour
    {
        [Header("场景引用")]
        [SerializeField] private LevelController level;
        [SerializeField] private Font font;
        [SerializeField] private Text progressText;
        [SerializeField] private Text puzzleText;
        [SerializeField] private Text trackingText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text catText;
        [SerializeField] private Text leftHint;
        [SerializeField] private Text rightHint;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text panelTitle;
        [SerializeField] private Text panelDetail;
        [SerializeField] private Text panelButtonText;
        [SerializeField] private Button panelButton;

        private float messageUntil;
        private readonly float[] hintUntil = new float[2];

        // Called by the editor baker. Every Canvas, Text, Image and Button is saved in the scene.
        public void BuildVisual(LevelController owner)
        {
            level = owner;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasObject = new GameObject("GameCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            CreateText(canvasObject.transform, "Title", "收纳小站 · 双手原型", 34, FontStyle.Bold,
                new Color(0.16f, 0.1f, 0.22f), new Vector2(0f, 1f), new Vector2(32f, -30f), new Vector2(620f, 54f), TextAnchor.UpperLeft);
            progressText = CreateText(canvasObject.transform, "Progress", "已收纳 0/6", 28, FontStyle.Bold,
                new Color(0.2f, 0.12f, 0.28f), new Vector2(0f, 1f), new Vector2(34f, -88f), new Vector2(400f, 46f), TextAnchor.UpperLeft);
            puzzleText = CreateText(canvasObject.transform, "Puzzle", "双手拼合 0/2", 24, FontStyle.Normal,
                new Color(0.32f, 0.18f, 0.4f), new Vector2(0f, 1f), new Vector2(34f, -132f), new Vector2(400f, 42f), TextAnchor.UpperLeft);
            trackingText = CreateText(canvasObject.transform, "Tracking", "等待摄像头", 22, FontStyle.Normal,
                new Color(0.18f, 0.22f, 0.26f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(720f, 50f), TextAnchor.UpperCenter);
            messageText = CreateText(canvasObject.transform, "Message", "", 30, FontStyle.Bold,
                Color.white, new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(900f, 58f), TextAnchor.MiddleCenter);
            catText = CreateText(canvasObject.transform, "CatWarning", "", 26, FontStyle.Bold,
                new Color(0.85f, 0.12f, 0.18f), new Vector2(1f, 1f), new Vector2(-34f, -36f), new Vector2(460f, 58f), TextAnchor.UpperRight);
            leftHint = CreateText(canvasObject.transform, "LeftHint", "", 23, FontStyle.Bold,
                new Color(0.08f, 0.48f, 0.7f), new Vector2(0f, 0f), new Vector2(34f, 146f), new Vector2(580f, 52f), TextAnchor.LowerLeft);
            rightHint = CreateText(canvasObject.transform, "RightHint", "", 23, FontStyle.Bold,
                new Color(0.75f, 0.2f, 0.35f), new Vector2(1f, 0f), new Vector2(-34f, 146f), new Vector2(580f, 52f), TextAnchor.LowerRight);
            CreateText(canvasObject.transform, "Instructions", "整只手移动手套 · 悬停 2 秒拾取/放置 · 左右碎片需双手同时悬停 · 挥动手臂击中猫爪\nM 鼠标调试 · Tab 切换左/右手 · C 触发猫 · R 重开", 20, FontStyle.Normal,
                new Color(0.22f, 0.24f, 0.28f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1500f, 74f), TextAnchor.LowerCenter);

            BuildPanel(canvasObject.transform);
        }

        public void Initialize(LevelController owner)
        {
            if (owner != null) level = owner;
            var runtimeFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 28);
            if (runtimeFont == null) return;
            foreach (var text in GetComponentsInChildren<Text>(true)) text.font = runtimeFont;
        }

        private void Update()
        {
            if (level == null || progressText == null) return;
            var placed = 0;
            foreach (var item in level.Items)
            {
                if (item.IsPlaced) placed++;
            }
            progressText.text = "已收纳 " + placed + "/" + level.Items.Count;
            puzzleText.text = "双手拼合 " + (level.Puzzle == null ? 0 : level.Puzzle.IsComplete ? 2 : 0) + "/2";

            if (level.Input != null && level.Input.DebugMode)
            {
                trackingText.text = "鼠标调试 | Tab 切换手 | 当前双手均有效";
            }
            else if (level.Runner != null)
            {
                trackingText.text = level.Runner.Status + " | 左手 " + (level.Input.Left.Valid ? "✓" : "—") +
                                    "  右手 " + (level.Input.Right.Valid ? "✓" : "—");
            }

            if (Time.unscaledTime >= messageUntil) messageText.text = "";
            if (Time.unscaledTime >= hintUntil[0]) leftHint.text = "";
            if (Time.unscaledTime >= hintUntil[1]) rightHint.text = "";
        }

        public void ShowMessage(string text, Color color, float duration = 1.1f)
        {
            messageText.text = text;
            messageText.color = color;
            messageUntil = Time.unscaledTime + duration;
        }

        public void ShowHint(HandSide side, string text, Color color, float duration = 1.0f)
        {
            var textComponent = side == HandSide.Left ? leftHint : rightHint;
            textComponent.text = (side == HandSide.Left ? "左手：" : "右手：") + text;
            textComponent.color = color;
            hintUntil[(int)side] = Time.unscaledTime + duration;
        }

        public void SetCatWarning(string text)
        {
            if (catText != null) catText.text = text;
        }

        public void ShowWon()
        {
            panel.SetActive(true);
            panelTitle.text = "收纳完成！";
            panelDetail.text = "三只包、手机与双手拼合全部完成";
            panelButtonText.text = "再玩一次";
            panelButton.onClick.RemoveAllListeners();
            panelButton.onClick.AddListener(level.RestartLevel);
        }

        public void ShowCameraError(string error)
        {
            panel.SetActive(true);
            panelTitle.text = "摄像头未就绪";
            panelDetail.text = error + "\n也可按 M 使用鼠标调试";
            panelButtonText.text = "重试摄像头";
            panelButton.onClick.RemoveAllListeners();
            panelButton.onClick.AddListener(level.RetryCamera);
        }

        public void HidePanel()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void BuildPanel(Transform parent)
        {
            panel = new GameObject("StatePanel");
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(700f, 340f);
            var background = panel.AddComponent<Image>();
            background.sprite = Geometry.GetSprite(PrimitiveShape.Square);
            background.color = new Color(0.16f, 0.08f, 0.22f, 0.96f);

            panelTitle = CreateText(panel.transform, "PanelTitle", "", 46, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(620f, 74f), TextAnchor.MiddleCenter);
            panelDetail = CreateText(panel.transform, "PanelDetail", "", 25, FontStyle.Normal,
                new Color(0.9f, 0.86f, 0.94f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(610f, 100f), TextAnchor.MiddleCenter);

            var buttonObject = new GameObject("PanelButton");
            buttonObject.transform.SetParent(panel.transform, false);
            var buttonRect = buttonObject.AddComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 42f);
            buttonRect.sizeDelta = new Vector2(250f, 66f);
            var image = buttonObject.AddComponent<Image>();
            image.sprite = Geometry.GetSprite(PrimitiveShape.Square);
            image.color = new Color(0.2f, 0.72f, 0.52f);
            panelButton = buttonObject.AddComponent<Button>();
            panelButton.targetGraphic = image;
            panelButtonText = CreateText(buttonObject.transform, "ButtonText", "", 26, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 60f), TextAnchor.MiddleCenter);
            panel.SetActive(false);
        }

        private Text CreateText(Transform parent, string name, string content, int size, FontStyle style,
            Color color, Vector2 anchor, Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.text = content;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
