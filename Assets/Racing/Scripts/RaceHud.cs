using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CircuitRacing
{
    public sealed class RaceHud : MonoBehaviour
    {
        public RaceCarController car;
        public RaceCameraRig cameraRig;
        public TextMesh dashboardSpeed;
        private bool paused;
        private Text speed;
        private Text view;
        private Text lap;
        private Text timing;
        private Text pauseHeading;
        private GameObject pausePanel;
        private TimeTrial trial;
        private Font font;

        private void Awake()
        {
            Application.runInBackground = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            trial = car.GetComponent<TimeTrial>();
            var canvasObject = new GameObject("Driving HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvas = canvasObject.transform;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Race UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Panel(canvas, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(245f, 82f));
            Label(canvas, "CIRCUIT 01", new Vector2(0f, 1f), new Vector2(48f, -38f), new Vector2(208f, 35f), 24);
            view = Label(canvas, "CHASE", new Vector2(0f, 1f), new Vector2(49f, -78f), new Vector2(208f, 25f), 17);
            view.color = new Color(0.54f, 0.83f, 0.91f);
            Panel(canvas, new Vector2(1f, 0f), new Vector2(-255f, 145f), new Vector2(225f, 115f));
            speed = Label(canvas, "000", new Vector2(1f, 0f), new Vector2(-234f, 149f), new Vector2(186f, 83f), 70);
            speed.alignment = TextAnchor.MiddleRight;
            Label(canvas, "KM/H", new Vector2(1f, 0f), new Vector2(-106f, 65f), new Vector2(66f, 25f), 17);
            Panel(canvas, new Vector2(1f, 1f), new Vector2(-282f, -30f), new Vector2(252f, 116f));
            lap = Label(canvas, "LAP 01 / 03", new Vector2(1f, 1f), new Vector2(-262f, -39f), new Vector2(218f, 30f), 21);
            timing = Label(canvas, "00:00.00", new Vector2(1f, 1f), new Vector2(-262f, -74f), new Vector2(218f, 62f), 20);

            pausePanel = new GameObject("Pause", typeof(RectTransform), typeof(Image));
            pausePanel.transform.SetParent(canvas, false);
            var stretch = pausePanel.GetComponent<RectTransform>();
            stretch.anchorMin = Vector2.zero; stretch.anchorMax = Vector2.one; stretch.offsetMin = Vector2.zero; stretch.offsetMax = Vector2.zero;
            pausePanel.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.025f, 0.88f);
            pauseHeading = Label(pausePanel.transform, "PAUSED", new Vector2(0.5f, 0.5f), new Vector2(-155f, 180f), new Vector2(310f, 55f), 38);
            Button(pausePanel.transform, "Resume", 92f, () => SetPaused(false));
            Button(pausePanel.transform, "Restart", 28f, () => { car.ResetCar(); SetPaused(false); });
            Button(pausePanel.transform, "Switch Camera", -36f, () => cameraRig.ToggleView());
            Button(pausePanel.transform, "Quit", -100f, () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
            pausePanel.SetActive(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetPaused(!paused);
            if (dashboardSpeed != null && car != null) dashboardSpeed.text = Mathf.RoundToInt(car.SpeedKph).ToString("000");
            speed.text = Mathf.RoundToInt(car.SpeedKph).ToString("000");
            view.text = cameraRig.CockpitView ? "COCKPIT" : "CHASE";
            if (trial != null)
            {
                lap.text = trial.Finished ? "FINISH" : "LAP " + Mathf.Min(trial.CompletedLaps + 1, 3).ToString("00") + " / 03";
                timing.text = TimeTrial.Format(trial.LapTime) + "\nBEST  " + (trial.BestLap > 0f ? TimeTrial.Format(trial.BestLap) : "--:--.--");
            }
        }

        private void OnDisable() => Time.timeScale = 1f;

        private void SetPaused(bool value)
        {
            paused = value;
            Time.timeScale = value ? 0f : 1f;
            pauseHeading.text = trial != null && trial.Finished ? "FINISHED" : "PAUSED";
            pausePanel.SetActive(value);
        }

        private RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private void Panel(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var panel = Rect(parent, "Telemetry", anchor, position, size).gameObject.AddComponent<Image>();
            panel.color = new Color(0.035f, 0.055f, 0.06f, 0.86f);
            panel.raycastTarget = false;
        }

        private Text Label(Transform parent, string content, Vector2 anchor, Vector2 position, Vector2 size, int fontSize)
        {
            var text = Rect(parent, content, anchor, position, size).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.text = content; text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            return text;
        }

        private void Button(Transform parent, string title, float y, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(parent, title, new Vector2(0.5f, 0.5f), new Vector2(-155f, y), new Vector2(310f, 48f));
            rect.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.23f, 0.26f);
            var button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(action);
            var text = Label(rect, title, new Vector2(0f, 1f), Vector2.zero, new Vector2(310f, 48f), 22);
            text.alignment = TextAnchor.MiddleCenter;
        }
    }
}
