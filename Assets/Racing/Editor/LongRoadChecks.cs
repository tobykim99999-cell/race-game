using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CircuitRacing.Editor
{
    [InitializeOnLoad]
    public static class LongRoadChecks
    {
        private const string Running = "CircuitRacing.LongRoadChecks";
        private static readonly List<object> Results = new List<object>();
        private static RoadRaceDirector director;
        private static RaceCarController car;
        private static VehicleAbilities abilities;
        private static int stage, stageFrame, frame = -1, region;
        private static double started;
        private static float startEnergy, maxEnergy, maxTilt, maxSeparation;
        private static bool sawDrift, sawJets;
        private static int recoveries;
        private static float[] lastDistances;
        private static VehicleAbilities[] racerAbilities;
        private static float[] topSpeeds, pickupNotices, boostSeconds;
        private static int[] pickupCounts;
        private static bool[] activeBoosts;
        private static readonly List<string> Errors = new List<string>();

        static LongRoadChecks()
        {
            EditorApplication.delayCall += () => { if (Application.isBatchMode && SessionState.GetBool(Running, false)) Attach(); };
        }

        public static void RunBatch()
        {
            Directory.CreateDirectory("Captures");
            EditorSceneManager.OpenScene(CreateLongRoadScenes.ScenePath(0));
            AudioListener.volume = 0;
            SessionState.SetBool(Running, true);
            Attach();
            EditorApplication.isPlaying = true;
        }

        private static void Attach()
        {
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
        }

        private static void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Errors.Add(message);
        }

        private static void Check(bool condition, string name, object values = null)
        {
            Results.Add(new { check = name, passed = condition, values });
            if (!condition) throw new InvalidOperationException("Check failed: " + name);
        }

        private static void Next(int value) { stage = value; stageFrame = Time.frameCount; }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying) return;
                EditorApplication.QueuePlayerLoopUpdate();
                if (frame == Time.frameCount) return;
                frame = Time.frameCount;
                if (started == 0) started = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - started > 1200) throw new TimeoutException("Long road verification timed out.");
                Time.captureFramerate = 60;
                AudioListener.volume = 0;
                if (director == null)
                {
                    director = Object.FindAnyObjectByType<RoadRaceDirector>();
                    if (director == null || director.Player.Car == null || director.State == RoadRaceState.Loading) { director = null; return; }
                    car = director.Player.Car;
                    abilities = car.GetComponent<VehicleAbilities>();
                    if (stage == 20) { BeginFullRace(); return; }
                    if (stage == 90) { Check(director.State == RoadRaceState.Ready, "Return to region selection"); director.Restart(true); director = null; Next(91); return; }
                    if (stage == 91) { Check(director.State == RoadRaceState.Countdown, "Restart starts a new countdown"); Finish(null); return; }
                    Graph();
                    foreach (var rival in director.racers.Skip(1)) { rival.GetComponent<RoadRaceAi>().enabled = false; rival.Car.gameObject.SetActive(false); }
                    car.playerControlled = false;
                    director.BeginRace();
                    Next(1);
                }
                float elapsed = (Time.frameCount - stageFrame) / 60f;
                switch (stage)
                {
                    case 1:
                        if (director.State != RoadRaceState.Racing) return;
                        Check(car.GroundedWheelCount == 4, "Car settles on generated asphalt");
                        startEnergy = abilities.Energy;
                        car.Body.linearVelocity = car.transform.forward * (75f / 3.6f);
                        car.SetAiInput(0.65f, 1f, 0f, true, false);
                        maxEnergy = startEnergy; Next(2); break;
                    case 2:
                        sawDrift |= abilities.Drifting;
                        maxEnergy = Mathf.Max(maxEnergy, abilities.Energy);
                        maxTilt = Mathf.Max(maxTilt, Vector3.Angle(car.transform.up, Vector3.up));
                        if (elapsed < 2.4f) return;
                        Check(sawDrift && maxEnergy > startEnergy + 1f, "Physical handbrake drift earns energy", new { startEnergy, maxEnergy, sawDrift, maxTilt });
                        car.ResetCar(); car.SetAiInput(0, 0, 1, false, false); Next(3); break;
                    case 3:
                        if (elapsed < 0.75f) return;
                        car.Body.linearVelocity = car.transform.forward * 25f;
                        car.SetAiInput(0, 1, 0, false, true); startEnergy = abilities.Energy; Next(4); break;
                    case 4:
                        sawJets |= abilities.Boosting && abilities.jets.All(j => j.isPlaying) && abilities.jetLights.All(l => l.enabled);
                        if (elapsed < 0.65f) return;
                        Check(sawJets && abilities.Energy < startEnergy - 5f && car.SpeedKph > 101f, "Stronger boost consumes energy, accelerates and emits jets", new { startEnergy, remaining = abilities.Energy, sawJets, speed = car.SpeedKph });
                        car.SetAiInput(0, 0, 1, false, false);
                        startEnergy = abilities.Energy; abilities.Collect(RoadPickupKind.Energy);
                        Check(Mathf.Abs(abilities.Energy - Mathf.Min(100f, startEnergy + 35f)) < 0.01f, "Energy pickup refill");
                        abilities.Collect(RoadPickupKind.Turbo); abilities.Collect(RoadPickupKind.Grip); startEnergy = abilities.Energy; Next(5); break;
                    case 5:
                        car.SetAiInput(0, 1, 0, false, false);
                        if (elapsed < 0.5f) return;
                        Check(abilities.Boosting && Mathf.Abs(abilities.Energy - startEnergy) < 0.05f && abilities.GripSeconds > 7f, "Turbo is free; Grip timer runs");
                        car.SetAiInput(0, 0, 1, false, false);
                        var hud = Object.FindAnyObjectByType<RoadRaceHud>(); hud.SetPaused(true); startEnergy = director.Elapsed; Next(6); break;
                    case 6:
                        if (elapsed < 0.4f) return;
                        Check(Time.timeScale == 0f && Mathf.Abs(director.Elapsed - startEnergy) < 0.01f, "Pause freezes race clock");
                        Object.FindAnyObjectByType<RoadRaceHud>().SetPaused(false);
                        car.ResetCar(); Next(7); break;
                    case 7:
                        if (elapsed < 0.5f) return;
                        Check(!abilities.Boosting && abilities.TurboSeconds == 0f && car.SpeedKph < 2f, "Recovery stops boost and resets physics");
                        float checkpoint = director.Player.NextCheckpoint;
                        Vector3 goal = director.route.Sample(director.route.FinishDistance + 2f);
                        car.Body.position = goal + Vector3.up;
                        car.Body.linearVelocity = Vector3.zero;
                        startEnergy = checkpoint; Next(8); break;
                    case 8:
                        if (elapsed < 0.2f) return;
                        Check(!director.Player.Finished && director.Player.NextCheckpoint == startEnergy, "Skipping checkpoints cannot finish");
                        car.ResetCar();
                        var pickup = Object.FindObjectsByType<RoadPickup>().First(p => p.kind == RoadPickupKind.Energy);
                        // Exercise the trigger without teleporting the racer past unvalidated gates.
                        pickup.transform.position = car.Body.position;
                        startEnergy = abilities.Energy;
                        Next(9); break;
                    case 9:
                        if (elapsed < 0.2f) return;
                        Check(abilities.Energy > startEnergy + 30f, "Driving into a pickup triggers collection");
                        Next(20); director = null; SceneManager.LoadScene("LongRoadPlains"); break;
                    case 21:
                        if (elapsed > 650f) throw new TimeoutException("AI did not finish " + director.region.displayName + ": " + string.Join(", ", director.racers.Select(r => r.racerName + "=" + r.Distance.ToString("0"))));
                        for (int i = 0; i < director.racers.Length; i++)
                        {
                            var racer = director.racers[i];
                            maxTilt = Mathf.Max(maxTilt, Vector3.Angle(racer.transform.up, Vector3.up));
                            maxSeparation = Mathf.Max(maxSeparation, racer.RoadSeparation);
                            if (racer.Distance < lastDistances[i] - 25f)
                            {
                                recoveries++;
                                Debug.Log("AI_RECOVERY " + racer.racerName + " from " + lastDistances[i].ToString("0") + " to " + racer.Distance.ToString("0"));
                            }
                            lastDistances[i] = racer.Distance;
                            topSpeeds[i] = Mathf.Max(topSpeeds[i], racer.Car.SpeedKph);
                            if (racerAbilities[i].Boosting)
                            {
                                boostSeconds[i] += Time.deltaTime;
                                activeBoosts[i] |= racerAbilities[i].TurboSeconds <= 0f;
                            }
                            if (racerAbilities[i].NoticeUntil > pickupNotices[i])
                            { pickupCounts[i]++; pickupNotices[i] = racerAbilities[i].NoticeUntil; }
                        }
                        if (Time.frameCount % 1800 == 0) Debug.Log("ROAD_TEST_PROGRESS " + director.region.displayName + " " + string.Join(", ", director.racers.Select(r => r.racerName + ":" + r.Distance.ToString("0") + "m/" + r.Car.SpeedKph.ToString("0") + "kph")));
                        if (director.State != RoadRaceState.Finished) return;
                        Check(director.racers.All(r => r.Finished), director.region.displayName + " four racers finish", new { director.Elapsed, maxTilt, maxSeparation, recoveries, racers = director.racers.Select(r => new { r.racerName, r.Finished, r.Distance, r.FinishTime }).ToArray() });
                        Check(maxTilt < 25f && recoveries <= 2, director.region.displayName + " AI stability", new { maxTilt, maxSeparation, recoveries });
                        Check(Enumerable.Range(1, 3).All(i => topSpeeds[i] > 145f && pickupCounts[i] > 0 && activeBoosts[i]),
                            director.region.displayName + " opponents race faster, collect pickups and actively boost",
                            new { topSpeeds, pickupCounts, boostSeconds, activeBoosts });
                        if (++region < 4)
                        {
                            var target = director.regions[region];
                            Next(20); director.SelectRegion(target); director = null;
                        }
                        else { Next(90); director.Restart(false); director = null; }
                        break;
                }
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Graph()
        {
            Check(director.route.Length > 5900 && Vector3.Distance(director.route.points[0], director.route.points.Last()) > 5600, "Open long distance route");
            Check(director.racers.Length == 4 && director.route.navigation != null && director.regions.All(r => r != null), "Race references and road navigation");
            Check(Object.FindObjectsByType<RoadPickup>().Length >= 50, "Pickup placement");
            var model = car.transform.Find("Porsche911");
            Check(model != null && car.wheelVisuals.All(w => w != null), "Detailed Porsche and animated wheels");
            director.lighting.Apply(RoadTime.Night);
            Check(Object.FindObjectsByType<Light>().Where(l => l.name.StartsWith("Headlamp")).All(l => l.enabled) && RenderSettings.fogDensity > 0, "Night headlights and environment");
            director.lighting.Apply(RoadTime.Day);
            Check(Object.FindObjectsByType<Light>().Where(l => l.name.StartsWith("Headlamp")).All(l => !l.enabled), "Day headlight state");
            var sampled = new HashSet<RoadTime>();
            for (int i = 0; i < 30; i++) { director.lighting.Apply(RoadTime.Random); sampled.Add(director.lighting.CurrentTime); }
            Check(sampled.Count == 3, "Random time includes day, sunset and night");
            director.SetTimeOfDay(RoadTime.Day);
            director.cameraRig.ToggleView();
            Check(director.cameraRig.CockpitView && Vector3.Distance(director.cameraRig.transform.position, director.cameraRig.cockpitAnchor.position) < 0.01f, "Cockpit camera");
            director.cameraRig.ToggleView();
            Check(!director.cameraRig.CockpitView, "Chase camera");
        }

        private static void BeginFullRace()
        {
            car.playerControlled = false;
            var agent = car.gameObject.AddComponent<NavMeshAgent>();
            agent.enabled = false; agent.radius = 1.15f; agent.height = 1.4f; agent.baseOffset = 0.65f; agent.acceleration = 30; agent.angularSpeed = 180;
            car.gameObject.AddComponent<RoadRaceAi>().cruiseSpeed = 92;
            director.SetTimeOfDay((RoadTime)(region % 3 + 1));
            maxTilt = maxSeparation = 0; recoveries = 0; lastDistances = new float[4];
            racerAbilities = director.racers.Select(r => r.GetComponent<VehicleAbilities>()).ToArray();
            topSpeeds = new float[4]; pickupNotices = new float[4]; boostSeconds = new float[4];
            pickupCounts = new int[4]; activeBoosts = new bool[4];
            director.BeginRace(); Next(21);
        }

        private static void Finish(Exception exception)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= Log;
            SessionState.SetBool(Running, false); Time.captureFramerate = 0; Time.timeScale = 1;
            if (exception != null) Results.Add(new { error = exception.ToString() });
            Results.Add(new { runtimeErrors = Errors });
            File.WriteAllText("Captures/long-road-check.json", Newtonsoft.Json.JsonConvert.SerializeObject(Results, Newtonsoft.Json.Formatting.Indented));
            if (exception != null) Debug.LogException(exception);
            Debug.Log("LONG_ROAD_CHECKS_COMPLETE " + (exception == null && Errors.Count == 0));
            EditorApplication.Exit(exception == null && Errors.Count == 0 ? 0 : 1);
        }

        public static void CaptureBatch()
        {
            try
            {
                Directory.CreateDirectory("Captures");
                for (int region = 0; region < 4; region++)
                {
                    EditorSceneManager.OpenScene(CreateLongRoadScenes.ScenePath(region));
                    var race = Object.FindAnyObjectByType<RoadRaceDirector>();
                    var player = race.Player.GetComponent<RaceCarController>();
                    float distance = region == 2 ? 2400 : 750;
                    player.transform.SetPositionAndRotation(race.route.Sample(distance, -2.8f) + Vector3.up * 0.85f, Quaternion.LookRotation(race.route.Direction(distance), Vector3.up));
                    var camera = race.cameraRig.GetComponent<Camera>();
                    camera.transform.position = player.transform.position - player.transform.forward * 9f + Vector3.up * 3.5f;
                    camera.transform.LookAt(race.route.Sample(distance + 25f) + Vector3.up * 1f);
                    race.lighting.reflection.transform.position = player.transform.position + Vector3.up * 2;
                    race.lighting.Apply(RoadTime.Day);
                    string preview = CreateLongRoadScenes.Root + "/Previews/" + (RoadRegion)region + ".png";
                    Capture(camera, preview, 1280, 720);
                    AssetDatabase.ImportAsset(preview);
                    var definition = race.region;
                    definition.preview = AssetDatabase.LoadAssetAtPath<Texture2D>(preview); EditorUtility.SetDirty(definition);
                    race.lighting.Apply(RoadTime.Night);
                    Capture(camera, "Captures/long-road-" + (RoadRegion)region + "-night.png", 1280, 720);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("LONG_ROAD_PREVIEWS_COMPLETE"); EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void Capture(Camera camera, string path, int width, int height)
        {
            var render = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
            try
            {
                camera.targetTexture = render;
                if (RenderPipelineManager.currentPipeline == null) camera.Render();
                Canvas.ForceUpdateCanvases();
                for (int i = 0; i < 3; i++) RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = render });
                RenderTexture.active = render;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
            finally { camera.targetTexture = oldTarget; RenderTexture.active = oldActive; render.Release(); Object.DestroyImmediate(render); }
        }
    }
}
