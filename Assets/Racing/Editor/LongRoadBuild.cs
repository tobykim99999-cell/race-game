using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CircuitRacing.Editor
{
    public static class LongRoadBuild
    {
        [MenuItem("Racing/Build Long Road Windows")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play mode and save scene changes before building.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Install Unity Windows Build Support first.");

            string[] scenes = Enumerable.Range(0, 4).Select(CreateLongRoadScenes.ScenePath).ToArray();
            foreach (string path in scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new FileNotFoundException("Missing race scene", path);
                EditorSceneManager.OpenScene(path);
                var director = UnityEngine.Object.FindAnyObjectByType<RoadRaceDirector>();
                if (director == null || director.route.navigation == null || director.racers.Length != 4
                    || director.regions.Any(region => region == null || region.preview == null || !scenes.Any(scene => Path.GetFileNameWithoutExtension(scene) == region.sceneName)))
                    throw new InvalidOperationException("Race scene references are incomplete: " + path);
                foreach (var racer in director.racers)
                {
                    var sound = racer.GetComponent<RaceCarAudio>();
                    if (sound == null || sound.engineLoop == null || sound.tyreLoop == null || sound.roadLoop == null || sound.collisionClip == null)
                        throw new InvalidOperationException("Missing vehicle audio in " + path);
                }
            }

            string name = "LongRoad-Windows-x64-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string directory = Path.GetFullPath(Path.Combine("Builds", name));
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory("Captures");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(directory, "LongRoad.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4
            });
            var messages = report.steps.SelectMany(step => step.messages)
                .Where(message => message.type == LogType.Error || message.type == LogType.Exception || message.type == LogType.Warning)
                .Select(message => new { type = message.type.ToString(), message.content }).ToArray();
            string zip = null;
            if (report.summary.result == BuildResult.Succeeded)
            {
                Documentation(directory);
                zip = directory + ".zip";
                using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
                    foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        string relative = Path.GetRelativePath(directory, file);
                        if (relative.Split(Path.DirectorySeparatorChar).Any(part => part.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame") || part.EndsWith("_BurstDebugInformation_DoNotShip"))) continue;
                        archive.CreateEntryFromFile(file, name + "/" + relative.Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
                    }
            }
            File.WriteAllText("Captures/long-road-build-result.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                result = report.summary.result.ToString(),
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                buildSeconds = report.summary.totalTime.TotalSeconds,
                playerBytes = report.summary.totalSize,
                output = report.summary.outputPath,
                directory,
                zip,
                zipBytes = zip == null ? 0L : new FileInfo(zip).Length,
                scenes,
                messages
            }, Newtonsoft.Json.Formatting.Indented));
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows build failed. See Captures/long-road-build-result.json.");
            Debug.Log("LONG_ROAD_BUILD_COMPLETE: " + directory);
        }

        private static void Documentation(string directory)
        {
            string credits = Path.Combine(directory, "ThirdParty");
            Directory.CreateDirectory(credits);
            File.Copy("Assets/Racing/Art/Porsche911/ATTRIBUTION.md", Path.Combine(credits, "Porsche911.md"));
            File.Copy("Assets/Racing/Art/PolyHaven/SOURCES.md", Path.Combine(credits, "RoadMaterials.md"));
            File.Copy("Assets/Racing/Art/LongRoad/SOURCES.md", Path.Combine(credits, "LongRoadEnvironment.md"));
            File.Copy("Assets/Racing/Audio/ATTRIBUTION.md", Path.Combine(credits, "Audio.md"));
            File.WriteAllText(Path.Combine(directory, "README.txt"),
                "LONG ROAD - WINDOWS X64\n\n" +
                "Extract the entire ZIP, then run LongRoad.exe. Keep LongRoad_Data, UnityPlayer.dll and the other runtime files beside the executable. Unity Editor and Python are not required.\n\n" +
                "Choose Green Plains, Pine Forest, Alpine Pass or Red Desert, choose a time of day, then select START RACE. Each route is a point-to-point race against three opponents.\n\n" +
                "CONTROLS\nW / Up: accelerate\nS / Down: brake, then reverse\nA / D or arrows: steer\nSpace while turning: drift and earn energy\nShift: boost\nC: cockpit / chase camera\nR: recover at the latest checkpoint\nEscape: pause\n\n" +
                "PICKUPS\nGreen: +35 energy\nBlue: 5 seconds of free turbo\nAmber: 8 seconds of extra grip\n\n" +
                "The upper-right map shows live terrain and racer positions. The top-centre mirror shows traffic behind you. SFX volume is available in the pause menu.\n\n" +
                "ASSET CREDITS\nKeep the ThirdParty directory with redistributions. The Porsche model is by n.brizitskaya under CC BY 4.0. Environment assets and the engine recording are CC0; details and source links are included.\n");
        }

        public static void RunBatch()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Captures");
                File.WriteAllText("Captures/long-road-build-error.txt", exception.ToString());
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
