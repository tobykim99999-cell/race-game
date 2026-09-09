using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CircuitRacing
{
    public sealed class RoadRaceHud : MonoBehaviour
    {
        public RoadRaceDirector director;
        private Font font;
        private GameObject selection;
        private GameObject pause;
        private GameObject results;
        private GameObject racing;
        private Text speed, position, time, distance, energy, notice, countdown, standings, resultText, weather;
        private Image energyFill, distanceFill;
        private Sprite barSprite;
        private bool paused;
        private RoadRaceState previous = (RoadRaceState)(-1);
        private readonly Color accent = new Color(0.25f, 0.92f, 0.77f);

        private void Start()
        {
            Application.runInBackground = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            barSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            var canvas = new GameObject("Long Road HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Road UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            racing = Layer(canvas.transform, "Race", Color.clear);
            Plate(racing.transform, "Standings", new Vector2(28, -28), new Vector2(248, 222), new Vector2(0, 1));
            position = Label(racing.transform, "1 / 4", new Vector2(48, -44), new Vector2(208, 48), 34, new Vector2(0, 1));
            standings = Label(racing.transform, "", new Vector2(48, -106), new Vector2(208, 125), 20, new Vector2(0, 1));
            standings.lineSpacing = 1.4f;
            Plate(racing.transform, "Rear View Frame", new Vector2(-220, -28), new Vector2(440, 120), new Vector2(0.5f, 1));
            var mirror = Rect(racing.transform, "Rear View", new Vector2(-216, -32), new Vector2(432, 112), new Vector2(0.5f, 1));
            mirror.gameObject.AddComponent<RaceRearView>().car = director.Player.GetComponent<RaceCarController>();
            time = Label(racing.transform, "00:00.00", new Vector2(-214, -158), new Vector2(170, 40), 30, new Vector2(0.5f, 1));
            time.alignment = TextAnchor.MiddleLeft;
            distance = Label(racing.transform, "", new Vector2(-26, -166), new Vector2(240, 28), 18, new Vector2(0.5f, 1));
            distance.alignment = TextAnchor.MiddleRight;
            distanceFill = Bar(racing.transform, new Vector2(-214, -210), new Vector2(428, 5), new Vector2(0.5f, 1), accent);
            Command(racing.transform, "II", new Vector2(-372, -28), new Vector2(54, 48), () => SetPaused(true), new Vector2(1, 1));
            Plate(racing.transform, "Telemetry", new Vector2(-308, 226), new Vector2(280, 198), new Vector2(1, 0));
            speed = Label(racing.transform, "000", new Vector2(-285, 220), new Vector2(230, 82), 68, new Vector2(1, 0));
            speed.alignment = TextAnchor.MiddleRight;
            Label(racing.transform, "KM/H", new Vector2(-117, 132), new Vector2(65, 24), 17, new Vector2(1, 0));
            energy = Label(racing.transform, "", new Vector2(-284, 94), new Vector2(230, 25), 18, new Vector2(1, 0));
            energyFill = Bar(racing.transform, new Vector2(-284, 56), new Vector2(230, 9), new Vector2(1, 0), accent);
            notice = Label(racing.transform, "", new Vector2(-270, 168), new Vector2(540, 60), 25, new Vector2(0.5f, 0));
            notice.alignment = TextAnchor.MiddleCenter;
            notice.color = accent;
            countdown = Label(racing.transform, "", new Vector2(-150, 60), new Vector2(300, 120), 82, new Vector2(0.5f, 0.5f));
            countdown.alignment = TextAnchor.MiddleCenter;
            Plate(racing.transform, "Route Map", new Vector2(-304, -28), new Vector2(276, 252), new Vector2(1, 1));
            var map = Rect(racing.transform, "Terrain Map", new Vector2(-300, -32), new Vector2(268, 224), new Vector2(1, 1));
            var terrainMap = map.gameObject.AddComponent<RoadMinimap>();
            terrainMap.director = director;
            map.gameObject.AddComponent<RectMask2D>();
            var markers = Rect(map, "Vehicle Markers", Vector2.zero, new Vector2(268, 224), new Vector2(0, 1)).gameObject.AddComponent<RoadRouteGraphic>();
            markers.minimap = terrainMap;
            markers.raycastTarget = false;
            var north = Label(racing.transform, "N", new Vector2(-54, -40), new Vector2(16, 20), 14, new Vector2(1, 1));
            north.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1, -1);
            var scale = Rect(racing.transform, "Map Scale", new Vector2(-288, -269), new Vector2(40, 2), new Vector2(1, 1)).gameObject.AddComponent<Image>();
            scale.raycastTarget = false;
            Label(racing.transform, "50 m", new Vector2(-240, -258), new Vector2(40, 20), 12, new Vector2(1, 1));
            var regionLabel = Label(racing.transform, director.region.displayName.ToUpperInvariant(), new Vector2(-194, -258), new Vector2(158, 20), 12, new Vector2(1, 1));
            regionLabel.alignment = TextAnchor.MiddleRight;

            selection = Layer(canvas.transform, "Region Selection", new Color(0.015f, 0.022f, 0.026f, 0.79f));
            Label(selection.transform, "LONG ROAD", new Vector2(-518, 300), new Vector2(620, 55), 40);
            weather = Label(selection.transform, "", new Vector2(-518, 240), new Vector2(900, 32), 20);
            for (int i = 0; i < director.regions.Length; i++)
            {
                RoadRegionDefinition region = director.regions[i];
                float x = -518 + i * 264;
                var item = Rect(selection.transform, region.displayName, new Vector2(x, 177), new Vector2(244, 228), Vector2.one * 0.5f);
                var background = item.gameObject.AddComponent<Image>();
                background.color = region == director.region ? new Color(0.07f, 0.29f, 0.25f) : new Color(0.07f, 0.085f, 0.09f);
                var button = item.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => director.SelectRegion(region));
                var preview = Rect(item, "Preview", new Vector2(0, 0), new Vector2(244, 140), new Vector2(0, 1)).gameObject.AddComponent<RawImage>();
                preview.texture = region.preview;
                preview.raycastTarget = false;
                Label(item, region.displayName.ToUpperInvariant(), new Vector2(14, -152), new Vector2(216, 29), 21, new Vector2(0, 1));
                var details = Label(item, (region.distanceMetres / 1000f).ToString("0.0") + " KM" + (region == director.region ? "   /   SELECTED" : ""), new Vector2(14, -190), new Vector2(216, 26), 16, new Vector2(0, 1));
                details.color = region == director.region ? accent : new Color(0.7f, 0.75f, 0.75f);
            }
            Label(selection.transform, "TIME OF DAY", new Vector2(-518, -102), new Vector2(230, 28), 18);
            for (int i = 0; i < 4; i++)
            {
                RoadTime value = (RoadTime)i;
                Command(selection.transform, value.ToString().ToUpperInvariant(), new Vector2(-518 + i * 144, -148), new Vector2(132, 42), () => director.SetTimeOfDay(value));
            }
            Command(selection.transform, "START RACE", new Vector2(246, -143), new Vector2(280, 54), director.BeginRace, null, true);
            Command(selection.transform, "QUIT", new Vector2(406, -230), new Vector2(120, 42), Quit);

            pause = Layer(canvas.transform, "Paused", new Color(0.015f, 0.022f, 0.026f, 0.92f));
            Label(pause.transform, "PAUSED", new Vector2(-180, 290), new Vector2(360, 60), 38);
            Command(pause.transform, "RESUME", new Vector2(-180, 203), new Vector2(360, 48), () => SetPaused(false));
            Command(pause.transform, "RESTART RACE", new Vector2(-180, 140), new Vector2(360, 48), () => director.Restart(true));
            Command(pause.transform, "REGIONS", new Vector2(-180, 77), new Vector2(360, 48), () => director.Restart(false));
            Command(pause.transform, "CAMERA", new Vector2(-180, 14), new Vector2(360, 48), () => director.cameraRig.ToggleView());
            Label(pause.transform, "SFX", new Vector2(-180, -63), new Vector2(70, 26), 18);
            Volume(pause.transform);
            Command(pause.transform, "QUIT", new Vector2(-180, -175), new Vector2(360, 48), Quit);

            results = Layer(canvas.transform, "Results", new Color(0.015f, 0.022f, 0.026f, 0.90f));
            Label(results.transform, "FINISH", new Vector2(-300, 250), new Vector2(600, 58), 42);
            resultText = Label(results.transform, "", new Vector2(-300, 160), new Vector2(600, 280), 24);
            resultText.lineSpacing = 1.45f;
            Command(results.transform, "RACE AGAIN", new Vector2(-300, -173), new Vector2(280, 52), () => director.Restart(true), null, true);
            Command(results.transform, "REGIONS", new Vector2(20, -173), new Vector2(280, 52), () => director.Restart(false));
            pause.SetActive(false);
            results.SetActive(false);
            foreach (var child in canvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        }

        private void Update()
        {
            if (selection == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetPaused(!paused);
            if (previous != director.State)
            {
                previous = director.State;
                selection.SetActive(previous == RoadRaceState.Ready);
                results.SetActive(previous == RoadRaceState.Finished);
                racing.SetActive(previous == RoadRaceState.Racing || previous == RoadRaceState.Countdown);
                if (previous == RoadRaceState.Finished) ShowFinish();
                if (previous == RoadRaceState.Loading) { pause.SetActive(false); selection.SetActive(true); }
            }
            weather.text = director.State == RoadRaceState.Loading ? "LOADING  " + (director.LoadingProgress * 100f).ToString("0") + "%"
                : director.region.displayName.ToUpperInvariant() + "   /   " + RoadLaunchSettings.TimeOfDay.ToString().ToUpperInvariant() + "   /   4 RACERS";
            var player = director.Player;
            var abilities = player.GetComponent<VehicleAbilities>();
            speed.text = player.Car.SpeedKph.ToString("000");
            position.text = director.PlayerPlace + " / " + director.racers.Length;
            time.text = FormatTime(director.Elapsed);
            float remaining = Mathf.Max(0f, director.route.FinishDistance - player.Distance);
            distance.text = (remaining / 1000f).ToString("0.00") + " KM TO FINISH";
            distanceFill.fillAmount = player.Distance / director.route.FinishDistance;
            energy.text = (abilities.Boosting ? "BOOST" : "ENERGY") + "  " + abilities.Energy.ToString("0") + "%";
            energyFill.fillAmount = abilities.Energy / 100f;
            notice.text = abilities.Drifting ? "DRIFT  + ENERGY" : Time.time < abilities.NoticeUntil ? abilities.LastPickup : "";
            if (abilities.TurboSeconds > 0) notice.text += "   TURBO " + abilities.TurboSeconds.ToString("0.0");
            if (abilities.GripSeconds > 0) notice.text += "   GRIP " + abilities.GripSeconds.ToString("0.0");
            countdown.text = director.State == RoadRaceState.Countdown ? director.Countdown.ToString() : director.State == RoadRaceState.Racing && director.Elapsed < 1f ? "GO" : "";
            standings.text = "";
            int index = 1;
            foreach (var racer in director.Standings()) standings.text += index++ + "  " + racer.racerName + (racer == player ? "  <" : "") + "\n";
        }

        public void ShowFinish()
        {
            if (results == null || resultText == null) return;
            selection.SetActive(false);
            pause.SetActive(false);
            racing.SetActive(false);
            results.SetActive(true);
            resultText.text = director.region.displayName.ToUpperInvariant() + "   /   " + FormatTime(director.Elapsed) + "\n\n";
            int place = 1;
            foreach (var racer in director.Standings())
                resultText.text += place++ + ".  " + racer.racerName + "     " + (racer.Finished ? FormatTime(racer.FinishTime) : (100f * racer.Distance / director.route.FinishDistance).ToString("0") + "%") + "\n";
        }

        public void SetPaused(bool value)
        {
            if (director.State != RoadRaceState.Racing && director.State != RoadRaceState.Countdown) return;
            paused = value;
            Time.timeScale = paused ? 0f : 1f;
            pause.SetActive(paused);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (barSprite != null) Destroy(barSprite);
        }
        public static string FormatTime(float value) => TimeSpan.FromSeconds(Mathf.Max(0f, value)).ToString(@"mm\:ss\.ff");

        private RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private GameObject Layer(Transform parent, string name, Color color)
        {
            var rect = Rect(parent, name, Vector2.zero, Vector2.zero, Vector2.zero);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (color.a > 0) rect.gameObject.AddComponent<Image>().color = color;
            return rect.gameObject;
        }

        private void Plate(Transform parent, string name, Vector2 pos, Vector2 size, Vector2 anchor)
        {
            var plate = Rect(parent, name, pos, size, anchor).gameObject.AddComponent<Image>();
            plate.color = new Color(0.015f, 0.022f, 0.026f, 0.72f);
            plate.raycastTarget = false;
        }

        private Text Label(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, Vector2? anchor = null)
        {
            var label = Rect(parent, text, pos, size, anchor ?? Vector2.one * 0.5f).gameObject.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private void Command(Transform parent, string text, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action, Vector2? anchor = null, bool primary = false)
        {
            var rect = Rect(parent, text, pos, size, anchor ?? Vector2.one * 0.5f);
            rect.gameObject.AddComponent<Image>().color = primary ? accent : new Color(0.16f, 0.19f, 0.2f);
            rect.gameObject.AddComponent<Button>().onClick.AddListener(action);
            var label = Label(rect, text, Vector2.zero, size, 19, new Vector2(0, 1));
            label.alignment = TextAnchor.MiddleCenter;
            if (primary) label.color = new Color(0.025f, 0.08f, 0.07f);
        }

        private Image Bar(Transform parent, Vector2 pos, Vector2 size, Vector2 anchor, Color color)
        {
            var rect = Rect(parent, "Track", pos, size, anchor);
            rect.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.35f, 0.35f);
            var fill = Rect(rect, "Fill", Vector2.zero, size, new Vector2(0, 1)).gameObject.AddComponent<Image>();
            fill.color = color;
            fill.sprite = barSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            return fill;
        }

        private void Volume(Transform parent)
        {
            var rect = Rect(parent, "SFX Volume", new Vector2(-100, -70), new Vector2(280, 22), Vector2.one * 0.5f);
            rect.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.3f, 0.31f);
            var handle = Rect(rect, "Handle", Vector2.zero, new Vector2(16, 30), new Vector2(0, 1));
            var image = handle.gameObject.AddComponent<Image>();
            image.color = accent;
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.handleRect = handle;
            slider.targetGraphic = image;
            var audio = director.Player.GetComponent<RaceCarAudio>();
            slider.value = audio.MasterVolume;
            slider.onValueChanged.AddListener(audio.SetVolume);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
