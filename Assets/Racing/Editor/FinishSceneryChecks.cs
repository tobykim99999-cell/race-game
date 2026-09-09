using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CircuitRacing.Editor
{
    public static class FinishSceneryChecks
    {
        public static void RunBatch()
        {
            var results = new List<object>();
            bool passed = true;
            try
            {
                Directory.CreateDirectory("Captures");
                // Finish positions measured in the previously generated scenes.
                float[] originalFinishes = { 6276.0293f, 5992.3086f, 6707.5264f, 7276.873f };
                for (int region = 0; region < 4; region++)
                {
                    EditorSceneManager.OpenScene(CreateLongRoadScenes.ScenePath(region));
                    var race = Object.FindAnyObjectByType<RoadRaceDirector>();
                    var route = race.route;
                    Physics.SyncTransforms();
                    int roadSamples = 0, roadGaps = 0, terrainSamples = 0, terrainGaps = 0;
                    for (float d = route.FinishDistance - 200f; d <= route.FinishDistance + 2200f; d += 8f)
                        foreach (float lane in new[] { -7f, 0f, 7f })
                        {
                            Vector3 p = route.Sample(d, lane);
                            roadSamples++;
                            if (!Physics.RaycastAll(p + Vector3.up * 5f, Vector3.down, 10f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                                .Any(h => h.collider.name == "Asphalt" && Mathf.Abs(h.point.y - p.y) < 0.05f)) roadGaps++;
                        }
                    for (float d = route.FinishDistance - 100f; d <= route.FinishDistance + 2000f; d += 40f)
                        foreach (float lane in new[] { -200f, -80f, -24f, 24f, 80f, 200f })
                        {
                            Vector3 p = route.Sample(d, lane);
                            terrainSamples++;
                            if (!Physics.RaycastAll(p + Vector3.up * 500f, Vector3.down, 1000f,
                                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                                .Any(h => h.collider.name == "Terrain" || h.collider.name == "High snow"
                                    || h.collider.name == "Rocky slopes")) terrainGaps++;
                        }
                    var camera = race.cameraRig.GetComponent<Camera>();
                    float endSeparation = Vector3.Distance(route.Sample(route.FinishDistance), route.points.Last());
                    var lastSector = GameObject.Find("Road Sector " + ((route.points.Length - 2) / 40).ToString("00"));
                    int sceneryObjects = lastSector.GetComponentsInChildren<Transform>()
                        .Count(t => t.name == "Fir (scanned CC0)" || t.name == "Boulder");
                    int roadTreeIntrusions = 0;
                    for (float d = 4f; d < route.Length - 4f; d += 8f)
                        roadTreeIntrusions += Physics.OverlapBox(route.Sample(d) + Vector3.up * 5f,
                            new Vector3(12.25f, 6f, 4.5f), Quaternion.LookRotation(route.Direction(d), Vector3.up),
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            .Count(c => c is CapsuleCollider && c.name.StartsWith("Fir"));
                    var forestTrees = Object.FindObjectsByType<LODGroup>().Where(l => l.name == "Fir (scanned CC0)").ToArray();
                    int minTreesPer100m = 0;
                    if (region == 1)
                    {
                        int[] sections = new int[Mathf.FloorToInt(route.FinishDistance / 100f)];
                        foreach (var tree in forestTrees)
                        {
                            var treeBounds = tree.GetLODs()[0].renderers[0].bounds;
                            int segment = 0;
                            float d = route.Project(new Vector3(treeBounds.center.x, treeBounds.min.y, treeBounds.center.z), ref segment, out _);
                            int section = Mathf.FloorToInt(d / 100f);
                            if (section >= 0 && section < sections.Length) sections[section]++;
                        }
                        minTreesPer100m = sections.Min();
                    }
                    bool regionPassed = Mathf.Abs(route.FinishDistance - originalFinishes[region]) < 0.05f
                        && Mathf.Abs(race.region.distanceMetres - (route.FinishDistance - 24f)) < 0.05f
                        && endSeparation > camera.farClipPlane + 400f
                        && roadGaps == 0 && terrainGaps == 0 && sceneryObjects > 0 && roadTreeIntrusions == 0
                        && (region != 1 || minTreesPer100m >= 35);
                    passed &= regionPassed;
                    results.Add(new { region = race.region.displayName, passed = regionPassed,
                        finishDistance = route.FinishDistance, generatedLength = route.Length,
                        endSeparation, camera.farClipPlane, roadSamples, roadGaps, terrainSamples, terrainGaps, sceneryObjects,
                        treeCount = forestTrees.Length, minTreesPer100m, roadTreeIntrusions });

                    if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) continue;
                    foreach (var rival in race.racers.Skip(1)) rival.gameObject.SetActive(false);
                    var player = race.Player.transform;
                    foreach (bool cockpit in new[] { false, true })
                    {
                        float d = route.FinishDistance + (cockpit ? 2f : -45f);
                        player.SetPositionAndRotation(route.Sample(d, -2.8f) + Vector3.up * 0.85f,
                            Quaternion.LookRotation(route.Direction(d), Vector3.up));
                        Physics.SyncTransforms();
                        if (race.cameraRig.CockpitView != cockpit) race.cameraRig.ToggleView();
                        race.cameraRig.SnapToTarget();
                        race.lighting.reflection.transform.position = player.position + Vector3.up * 2f;
                        foreach (var time in new[] { RoadTime.Day, RoadTime.Night })
                        {
                            race.lighting.Apply(time);
                            LongRoadChecks.Capture(camera, "Captures/finish-" + (RoadRegion)region + "-"
                                + (cockpit ? "cockpit" : "chase") + "-" + time + ".png", 1280, 720);
                        }
                    }
                    if (region == 1)
                    {
                        race.cameraRig.ToggleView();
                        foreach (float d in new[] { 300f, 2200f, 4300f })
                        {
                            player.SetPositionAndRotation(route.Sample(d, -2.8f) + Vector3.up * 0.85f,
                                Quaternion.LookRotation(route.Direction(d), Vector3.up));
                            Physics.SyncTransforms(); race.cameraRig.SnapToTarget();
                            race.lighting.reflection.transform.position = player.position + Vector3.up * 2f;
                            race.lighting.Apply(RoadTime.Day);
                            LongRoadChecks.Capture(camera, "Captures/forest-density-" + d.ToString("0") + ".png", 1280, 720);
                        }
                    }
                }
                File.WriteAllText("Captures/finish-scenery-check.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                    new { passed, regions = results }, Newtonsoft.Json.Formatting.Indented));
                Debug.Log("FINISH_SCENERY_CHECKS_COMPLETE " + passed);
                EditorApplication.Exit(passed ? 0 : 1);
            }
            catch (Exception exception)
            {
                File.WriteAllText("Captures/finish-scenery-check.json", Newtonsoft.Json.JsonConvert.SerializeObject(
                    new { passed = false, regions = results, error = exception.ToString() }, Newtonsoft.Json.Formatting.Indented));
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
