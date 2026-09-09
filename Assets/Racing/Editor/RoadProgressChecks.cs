using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CircuitRacing.Editor
{
    public static class RoadProgressChecks
    {
        private static readonly List<object> Results = new List<object>();
        private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly MethodInfo ProgressStep = typeof(RoadRaceProgress).GetMethod("FixedUpdate", PrivateInstance);
        private static int failures;
        private static GameObject vehicle;
        private static RoadRoute route;
        private static RoadRaceDirector director;

        public static void RunBatch()
        {
            try
            {
                Directory.CreateDirectory("Captures");
                vehicle = AssetDatabase.LoadAssetAtPath<GameObject>(CreateLongRoadScenes.Root + "/RoadPorsche.prefab");
                for (int region = 0; region < 4; region++)
                {
                    EditorSceneManager.OpenScene(CreateLongRoadScenes.ScenePath(region));
                    route = Object.FindAnyObjectByType<RoadRoute>();
                    director = Object.FindAnyObjectByType<RoadRaceDirector>();
                    string name = director.region.displayName;
                    ReplayLeader(name + ": centre line at 235 km/h", d => 0f, d => 0.85f);
                    ReplayLeader(name + ": right shoulder over checkpoint", d => Mathf.Abs(d - 240f) < 20f ? 10.5f : 0f, d => 0.85f);
                    ReplayLeader(name + ": left shoulder over checkpoint", d => Mathf.Abs(d - 240f) < 20f ? -10.5f : 0f, d => 0.85f);
                    ReplayLeader(name + ": crest hop over checkpoint", d => 0f, d => Mathf.Abs(d - 440f) < 12f ? 3.5f : 0.85f);
                    SafetyChecks(name);
                }
                Complete(null);
            }
            catch (Exception exception) { Complete(exception); }
        }

        private static RoadRaceProgress NewRacer(int index, float distance, float lane)
        {
            var item = Object.Instantiate(vehicle);
            item.name = "Progress replay " + index;
            item.transform.SetPositionAndRotation(route.Sample(distance, lane) + Vector3.up * 0.85f, Quaternion.LookRotation(route.Direction(distance), Vector3.up));
            var car = item.GetComponent<RaceCarController>();
            typeof(RaceCarController).GetMethod("Awake", PrivateInstance).Invoke(car, null);
            var progress = item.GetComponent<RoadRaceProgress>();
            progress.director = director;
            progress.racerIndex = index;
            progress.racerName = index == 0 ? "YOU" : "AI " + index;
            progress.lane = lane;
            typeof(RoadRaceProgress).GetMethod("Awake", PrivateInstance).Invoke(progress, null);
            typeof(RoadRaceProgress).GetMethod("Start", PrivateInstance).Invoke(progress, null);
            return progress;
        }

        private static void Step(RoadRaceProgress racer, float distance, float lane, float height = 0.85f)
        {
            racer.Car.Body.position = route.Sample(distance, lane) + Vector3.up * height;
            racer.Car.Body.rotation = Quaternion.LookRotation(route.Direction(distance), Vector3.up);
            ProgressStep.Invoke(racer, null);
        }

        private static void ReplayLeader(string name, Func<float, float> lane, Func<float, float> height)
        {
            var racers = new[] { NewRacer(0, 24f, 0f), NewRacer(1, 20f, -2.8f), NewRacer(2, 16f, 2.8f), NewRacer(3, 12f, 0f) };
            director.racers = racers;
            typeof(RoadRaceDirector).GetProperty("State").GetSetMethod(true).Invoke(director, new object[] { RoadRaceState.Racing });
            int wrongRankSamples = 0;
            float firstWrongAt = 0f;
            float firstRecordedDistance = 0f;
            for (float distance = 24f; distance < 1000f; distance += 235f / 3.6f * 0.02f)
            {
                Step(racers[0], distance, lane(distance), height(distance));
                for (int i = 1; i < racers.Length; i++)
                    Step(racers[i], Mathf.Max(24f - i * 4f, 24f - i * 4f + (distance - 24f) * (0.74f - i * 0.02f)), racers[i].lane);
                if (director.PlayerPlace == 1) continue;
                if (wrongRankSamples++ == 0) { firstWrongAt = distance; firstRecordedDistance = racers[0].Distance; }
            }
            bool passed = wrongRankSamples == 0 && racers[0].LastCheckpoint >= 840f && Mathf.Abs(racers[0].Distance - Project(racers[0])) < 0.1f;
            if (!passed) failures++;
            Results.Add(new
            {
                name, passed, wrongRankSamples, firstWrongAt, firstRecordedDistance,
                finalPlace = director.PlayerPlace,
                recordedDistance = racers[0].Distance,
                actualDistance = Project(racers[0]),
                racers[0].NextCheckpoint,
                rivalDistances = racers.Skip(1).Select(r => r.Distance).ToArray()
            });
            foreach (var racer in racers) Object.DestroyImmediate(racer.gameObject);
        }

        private static float Project(RoadRaceProgress racer)
        {
            int segment = 0;
            return route.Project(racer.Car.Body.position, ref segment, out _);
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) failures++;
            Results.Add(new { name, passed = condition });
        }

        private static void SafetyChecks(string name)
        {
            var player = NewRacer(0, 24f, 0f);
            var rival = NewRacer(1, 20f, -2.8f);
            director.racers = new[] { player, rival };
            for (float d = 24f; d < 200f; d += 1.3f) { Step(player, d, 0f); Step(rival, Mathf.Max(20f, d - 20f), -2.8f); }
            float beforeGate = player.Distance;
            for (float d = 200f; d < 212f; d += 1.3f) Step(player, d, 0f);
            for (float d = rival.Distance; d < 226f; d += 1.3f) Step(rival, d, -2.8f);
            Check(director.PlayerPlace == 2 && player.Distance > beforeGate, name + ": a real overtake changes the rank");

            float beforeReverse = player.Distance;
            Step(player, beforeReverse - 10f, 0f);
            Check(player.Distance < beforeReverse - 9f && player.NextCheckpoint == 240f, name + ": reversing reduces distance without granting checkpoints");
            float checkpoint = player.LastCheckpoint;
            player.Car.ResetCar();
            Check(player.LastCheckpoint == checkpoint && Mathf.Abs(player.Distance - Project(player)) < 0.1f && player.Distance < checkpoint,
                name + ": respawn immediately updates ranking distance");

            Step(player, route.FinishDistance + 2f, 0f);
            Check(!player.Finished && player.NextCheckpoint == 240f && player.Distance < checkpoint,
                name + ": teleport to finish is rejected and recovered");

            for (float d = player.Distance; d < 270f; d += 1.3f)
                Step(player, d, d > 218f ? 18f : 0f);
            Check(!player.Finished && player.LastCheckpoint == checkpoint && player.NextCheckpoint == 240f,
                name + ": bypassing the shoulder does not grant a checkpoint");
            Step(player, 272f, 18f);
            Check(player.Distance < checkpoint && Mathf.Abs(Project(player) - player.Distance) < 0.1f,
                name + ": a missed checkpoint triggers recovery instead of a permanent stall");
            for (float d = player.Distance; d < 470f; d += 1.3f) Step(player, d, 0f);
            Check(player.NextCheckpoint == 640f && player.Distance > 468f, name + ": race can continue after missed-checkpoint recovery");

            // Finish with a non-player racer so the production callback does not save a synthetic personal best.
            for (float d = rival.Distance; d < route.FinishDistance + 3f; d += 1.3f)
            {
                float side = Mathf.Abs(d - 440f) < 20f ? -10.5f : Mathf.Abs(d - 640f) < 20f ? 10.5f : 0f;
                Step(rival, d, side, Mathf.Abs(d - 840f) < 12f ? 3.5f : 0.85f);
            }
            Check(rival.Finished && Mathf.Abs(rival.Distance - route.FinishDistance) < 0.01f && director.Standings()[0] == rival,
                name + ": ordered checkpoints including shoulders and hops allow a valid finish");
            Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(rival.gameObject);
        }

        private static void Complete(Exception exception)
        {
            bool passed = failures == 0 && exception == null;
            File.WriteAllText("Captures/road-progress-check.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { passed, failures, checks = Results, error = exception?.ToString() }, Newtonsoft.Json.Formatting.Indented));
            if (exception != null) Debug.LogException(exception);
            Debug.Log("ROAD_PROGRESS_CHECKS_COMPLETE " + passed);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
