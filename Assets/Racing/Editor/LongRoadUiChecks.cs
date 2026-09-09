using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CircuitRacing.Editor
{
    [InitializeOnLoad]
    public static class LongRoadUiChecks
    {
        private const string Running = "CircuitRacing.LongRoadUiChecks";
        private static int firstFrame, stage, lastFrame = -1;
        private static double started;
        private static RoadRaceDirector director;
        private static float boostedAt;
        private static Camera rearCamera;
        private static RenderTexture previousTexture;
        private static Color32[] previousPixels;
        private static Camera mapCamera;
        private static RenderTexture mapTexture;
        private static Color32[] mapPixels;
        private static int mapRegion = 1;
        private static readonly List<string> Checks = new List<string>();
        private static readonly List<string> Errors = new List<string>();

        static LongRoadUiChecks()
        {
            EditorApplication.delayCall += () => { if (Application.isBatchMode && SessionState.GetBool(Running, false)) Attach(); };
        }

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(CreateLongRoadScenes.ScenePath(0));
            AudioListener.volume = 0;
            SessionState.SetBool(Running, true); Attach(); EditorApplication.isPlaying = true;
        }

        private static void Attach()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }

        private static void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(message);
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            Checks.Add(name);
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
                lastFrame = Time.frameCount; EditorApplication.QueuePlayerLoopUpdate();
                Time.captureFramerate = 60; AudioListener.volume = 0;
                if (started == 0) started = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - started > 180) throw new TimeoutException("UI capture timed out.");
                if (director == null)
                {
                    director = Object.FindAnyObjectByType<RoadRaceDirector>();
                    if (director == null || director.Player.Car == null || director.State == RoadRaceState.Loading) { director = null; return; }
                    director.SetTimeOfDay(RoadTime.Day); firstFrame = Time.frameCount;
                }
                if (Time.frameCount < firstFrame + 12) return;
                var car = director.Player.Car;
                if (stage == 0)
                {
                    Capture("long-road-menu", 1600, 900);
                    Click("START RACE"); stage = 1;
                }
                else if (stage == 1 && director.State == RoadRaceState.Racing)
                {
                    rearCamera = car.GetComponentsInChildren<Camera>().Single(c => c.name == "Rear View Camera");
                    previousTexture = rearCamera.targetTexture;
                    previousPixels = MirrorPixels(previousTexture);
                    Check(previousTexture.IsCreated() && previousTexture.width == 864 && previousTexture.height == 224, "Rear-view texture created at the expected resolution");
                    Check(Vector3.Dot(rearCamera.transform.forward, -car.transform.forward) > 0.99f, "Rear-view camera faces behind the player");
                    Check(Object.FindObjectsByType<AudioListener>().Length == 1, "Rear view does not add an audio listener");
                    var mirror = Object.FindAnyObjectByType<RaceRearView>();
                    Check(mirror.GetComponent<RawImage>().uvRect.width == -1f, "Rear-view display is horizontally mirrored");
                    var map = Object.FindAnyObjectByType<RoadRouteGraphic>();
                    var terrainMap = Object.FindAnyObjectByType<RoadMinimap>();
                    var mapRect = terrainMap.GetComponent<RectTransform>();
                    Check(mapRect.anchorMin == Vector2.one && mapRect.anchorMax == Vector2.one, "Minimap is anchored in the upper-right corner");
                    mapCamera = terrainMap.ViewCamera;
                    mapTexture = mapCamera.targetTexture;
                    mapPixels = MirrorPixels(mapTexture);
                    Check(mapCamera.orthographic && Vector3.Dot(mapCamera.transform.forward, Vector3.down) > 0.99f, "Minimap uses a scheduled overhead orthographic camera");
                    Check(Vector3.Dot(mapCamera.transform.up, Vector3.forward) > 0.99f, "Minimap remains north-up");
                    Check(mapTexture.IsCreated() && mapTexture.width == 536 && mapTexture.height == 448, "Minimap renders into a reusable 536 x 448 texture");
                    Check(mapCamera.orthographicSize * 2f == 280f, "Minimap world extent matches its 50 m scale");
                    var mesh = map.canvasRenderer.GetMesh();
                    Check(mesh != null && mesh.vertexCount > 0, "Minimap vehicle markers are rendered");
                    LayoutCheck();
                    car.playerControlled = false; car.SetAiInput(0, 1, 0, false, true);
                    car.Body.linearVelocity = car.transform.forward * 27f; boostedAt = Time.time; stage = 2;
                }
                else if (stage == 2 && Time.time > boostedAt + 1.8f)
                {
                    var pixels = MirrorPixels(previousTexture, "Captures/long-road-rear-view.png");
                    int changed = pixels.Where((pixel, index) => Mathf.Abs(pixel.r - previousPixels[index].r) + Mathf.Abs(pixel.g - previousPixels[index].g) + Mathf.Abs(pixel.b - previousPixels[index].b) > 15).Count();
                    Check(changed > pixels.Length / 200, "Rear-view pixels update as the car moves");
                    Check(rearCamera.targetTexture == previousTexture, "Rear-view texture is reused across frames");
                    var updatedMap = MirrorPixels(mapTexture, "Captures/terrain-minimap-Plains.png");
                    int changedMap = updatedMap.Where((pixel, index) => Mathf.Abs(pixel.r - mapPixels[index].r) + Mathf.Abs(pixel.g - mapPixels[index].g) + Mathf.Abs(pixel.b - mapPixels[index].b) > 15).Count();
                    Check(changedMap > updatedMap.Length / 200, "Terrain minimap pixels update during driving");
                    Check(mapCamera.targetTexture == mapTexture, "Minimap texture is reused across frames");
                    CheckMapPixels(updatedMap, "Plains daytime");
                    Capture("long-road-race-boost", 1600, 900);
                    director.cameraRig.ToggleView(); stage = 3; firstFrame = Time.frameCount;
                }
                else if (stage == 3)
                {
                    Check(rearCamera.enabled && Vector3.Dot(rearCamera.transform.forward, -car.transform.forward) > 0.99f, "Rear view remains active when switching to the cockpit");
                    Capture("long-road-cockpit", 1280, 720);
                    mapPixels = MirrorPixels(mapTexture);
                    Click("II"); stage = 4; firstFrame = Time.frameCount;
                }
                else if (stage == 4)
                {
                    Check(!rearCamera.enabled, "Paused rear-view camera stops rendering");
                    Check(MirrorPixels(mapTexture).SequenceEqual(mapPixels), "Minimap texture remains frozen while paused");
                    Capture("long-road-pause", 1280, 720);
                    Click("RESUME");
                    if (Time.timeScale != 1f) throw new InvalidOperationException("Resume button failed.");
                    stage = 5; firstFrame = Time.frameCount;
                }
                else if (stage == 5)
                {
                    Check(rearCamera.enabled, "Rear-view rendering resumes with the race");
                    Check(!MirrorPixels(mapTexture).SequenceEqual(mapPixels), "Minimap refresh resumes with the race");
                    director.lighting.Apply(RoadTime.Night);
                    stage = 6; firstFrame = Time.frameCount;
                }
                else if (stage == 6)
                {
                    MirrorPixels(previousTexture, "Captures/long-road-rear-view-night.png");
                    CheckMapPixels(MirrorPixels(mapTexture, "Captures/terrain-minimap-Plains-night.png"), "Plains night");
                    Capture("long-road-cockpit-night", 1280, 720);
                    director.SelectRegion(director.regions[1]); director = null;
                    stage = 7;
                }
                else if (stage == 7)
                {
                    Check(previousTexture == null && rearCamera == null, "Region change destroys the previous mirror camera and texture");
                    Check(mapTexture == null && mapCamera == null, "Region change destroys the previous minimap camera and texture");
                    Check(Object.FindObjectsByType<Camera>().Where(c => c.name == "Rear View Camera").All(c => !c.enabled), "Region selection does not render a rear view");
                    Click("START RACE"); stage = 8;
                }
                else if (stage == 8 && director.State == RoadRaceState.Racing)
                {
                    Check(Object.FindObjectsByType<Camera>().Count(c => c.name == "Rear View Camera" && c.enabled && c.targetTexture.IsCreated()) == 1, "Next region starts with one working rear-view camera");
                    mapCamera = Object.FindAnyObjectByType<RoadMinimap>().ViewCamera;
                    mapTexture = mapCamera.targetTexture;
                    // Place the test-only formation along the route to inspect each biome's overhead detail.
                    foreach (var racer in director.racers)
                    {
                        racer.Car.ControlsEnabled = false;
                        float d = (mapRegion == 2 ? 2400f : 750f) - racer.racerIndex * 8f;
                        racer.Car.Body.position = director.route.Sample(d, racer.lane) + Vector3.up * 0.85f;
                        racer.Car.Body.rotation = Quaternion.LookRotation(director.route.Direction(d), Vector3.up);
                        racer.Car.Body.linearVelocity = Vector3.zero;
                        racer.Car.Body.angularVelocity = Vector3.zero;
                    }
                    director.cameraRig.SnapToTarget();
                    stage = 9; firstFrame = Time.frameCount;
                }
                else if (stage == 9)
                {
                    CheckMapPixels(MirrorPixels(mapTexture, "Captures/terrain-minimap-" + director.region.region + ".png"), director.region.displayName + " daytime");
                    Capture("terrain-map-" + director.region.region, 1600, 900);
                    director.lighting.Apply(RoadTime.Night);
                    stage = 10; firstFrame = Time.frameCount;
                }
                else if (stage == 10)
                {
                    CheckMapPixels(MirrorPixels(mapTexture, "Captures/terrain-minimap-" + director.region.region + "-night.png"), director.region.displayName + " night");
                    if (++mapRegion == 4) { Finish(null); return; }
                    previousTexture = Object.FindAnyObjectByType<RaceRearView>().GetComponent<RawImage>().texture as RenderTexture;
                    rearCamera = director.Player.Car.GetComponentsInChildren<Camera>().Single(c => c.name == "Rear View Camera");
                    director.SelectRegion(director.regions[mapRegion]); director = null;
                    stage = 7;
                }
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Click(string name)
        {
            Object.FindObjectsByType<Button>().First(button => button.name == name).onClick.Invoke();
        }

        private static void LayoutCheck()
        {
            var canvas = Object.FindAnyObjectByType<Canvas>().GetComponent<RectTransform>();
            var graphics = canvas.GetComponentsInChildren<Graphic>();
            var names = new[] { "Rear View Frame", "Route Map", "II", "00:00.00" };
            var bounds = names.Select(name => RectTransformUtility.CalculateRelativeRectTransformBounds(canvas, graphics.First(g => g.name == name).rectTransform)).ToArray();
            for (int i = 0; i < bounds.Length; i++)
                for (int j = i + 1; j < bounds.Length; j++)
                    Check(!bounds[i].Intersects(bounds[j]), names[i] + " does not overlap " + names[j]);
        }

        private static Color32[] MirrorPixels(RenderTexture texture, string path = null)
        {
            var old = RenderTexture.active;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); image.Apply();
                if (path != null) File.WriteAllBytes(path, image.EncodeToPNG());
                return image.GetPixels32();
            }
            finally { RenderTexture.active = old; Object.DestroyImmediate(image); }
        }

        private static void CheckMapPixels(Color32[] pixels, string name)
        {
            int darkest = 765, lightest = 0;
            long total = 0;
            for (int i = 0; i < pixels.Length; i += 13)
            {
                int value = pixels[i].r + pixels[i].g + pixels[i].b;
                darkest = Mathf.Min(darkest, value); lightest = Mathf.Max(lightest, value); total += value;
            }
            Check(lightest - darkest > 100 && total / (float)(pixels.Length / 13) > 65f, name + " terrain map contains visible scene detail");
        }

        private static void Capture(string name, int width, int height)
        {
            var camera = director.cameraRig.GetComponent<Camera>();
            var canvas = Object.FindAnyObjectByType<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = camera.nearClipPlane + 0.005f;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = width / 1600f;
            Canvas.ForceUpdateCanvases();
            LongRoadChecks.Capture(camera, "Captures/" + name + ".png", width, height);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        }

        private static void Finish(Exception exception)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log; SessionState.SetBool(Running, false);
            Time.captureFramerate = 0; Time.timeScale = 1;
            File.WriteAllText("Captures/long-road-ui-check.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = exception == null && Errors.Count == 0, checks = Checks, runtimeErrors = Errors, error = exception?.ToString() }, Newtonsoft.Json.Formatting.Indented));
            if (exception != null) Debug.LogException(exception);
            EditorApplication.Exit(exception == null && Errors.Count == 0 ? 0 : 1);
        }
    }
}
