using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace CircuitRacing.Editor
{
    [InitializeOnLoad]
    public static class VehicleAudioSetup
    {
        private const string ScenePath = "Assets/Racing/Scenes/DualViewPractice.unity";
        private const string AudioPath = "Assets/Racing/Audio/";
        private const string BatchKey = "CircuitRacing.AudioBatch";
        private static int stage;
        private static double startedAt;
        private static int firstFrame;
        private static int lastFrame = -1;
        private static RaceCarController car;
        private static RaceCarAudio sound;
        private static RaceCameraRig rig;
        private static Keyboard keyboard;
        private static InputSettings.BackgroundBehavior previousBackground;
        private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private static float idlePitch;
        private static float drivingPitch;
        private static double recordedEnergy;
        private static float recordedPeak;
        private static int recordedSamples;
        private static double sourceEnergy;
        private static int sourceSamples;
        private static readonly float[] SourceBuffer = new float[1024];
        private static bool captureStarted;
        private static bool hadVolume;
        private static float previousVolume;
        private static System.Reflection.PropertyInfo editorMute;
        private static bool previousEditorMute;
        private static readonly List<object> Results = new List<object>();

        static VehicleAudioSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode && SessionState.GetInt(BatchKey, 0) != 0)
                    EditorApplication.update += Tick;
            };
        }

        [MenuItem("Racing/Install Vehicle Audio")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var controller = UnityEngine.Object.FindAnyObjectByType<RaceCarController>();
            if (controller == null) throw new InvalidOperationException("Open the racing scene first.");
            foreach (string file in new[] { "engine-loop", "tyre-loop", "road-loop", "collision" })
            {
                var importer = AssetImporter.GetAtPath(AudioPath + file + ".wav") as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Missing sound: " + file);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
            var audio = controller.GetComponent<RaceCarAudio>();
            if (audio == null) audio = controller.gameObject.AddComponent<RaceCarAudio>();
            audio.engineLoop = Clip("engine-loop");
            audio.tyreLoop = Clip("tyre-loop");
            audio.roadLoop = Clip("road-loop");
            audio.collisionClip = Clip("collision");
            foreach (var clip in new[] { audio.engineLoop, audio.tyreLoop, audio.roadLoop, audio.collisionClip })
            {
                clip.LoadAudioData();
                var samples = new float[clip.samples * clip.channels];
                Require(clip.GetData(samples, 0), "Unable to read audio data: " + clip.name);
                Require(samples.Any(value => Mathf.Abs(value) > 0.01f), "Silent audio clip: " + clip.name);
                Require(samples.Max(value => Mathf.Abs(value)) < 0.95f, "Clipped audio: " + clip.name);
            }
            EditorUtility.SetDirty(audio);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            EditorSceneManager.SaveScene(controller.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath);
                Install();
                Directory.CreateDirectory("Captures");
                AudioListener.volume = 0f;
                SessionState.SetInt(BatchKey, 1);
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + name + ".wav");

        private static void Tick()
        {
            try
            {
                int phase = SessionState.GetInt(BatchKey, 0);
                if (phase == 2 && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.update -= Tick;
                    SessionState.SetInt(BatchKey, 0);
                    Build();
                    return;
                }
                if (phase != 1 || !EditorApplication.isPlaying) return;
                EditorApplication.QueuePlayerLoopUpdate();
                if (sound == null)
                {
                    sound = UnityEngine.Object.FindAnyObjectByType<RaceCarAudio>();
                    car = UnityEngine.Object.FindAnyObjectByType<RaceCarController>();
                    rig = UnityEngine.Object.FindAnyObjectByType<RaceCameraRig>();
                    if (sound == null || sound.GetComponentsInChildren<AudioSource>().Length != 4)
                    {
                        sound = null;
                        return;
                    }
                    Time.captureFramerate = 60;
                    Require(AudioRenderer.Start(), "Unable to enter offline audio recording mode.");
                    captureStarted = true;
                    editorMute = typeof(EditorUtility).GetProperty("audioMasterMute", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (editorMute != null)
                    {
                        previousEditorMute = (bool)editorMute.GetValue(null);
                        editorMute.SetValue(null, false);
                    }
                    AudioListener.volume = 1f;
                    AudioListener.pause = false;
                    hadVolume = PlayerPrefs.HasKey("CircuitRacing.SfxVolume");
                    previousVolume = PlayerPrefs.GetFloat("CircuitRacing.SfxVolume", 0.8f);
                    sound.SetVolume(0.8f);
                    previousBackground = InputSystem.settings.backgroundBehavior;
                    previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>("AudioVerification");
                    startedAt = EditorApplication.timeSinceStartup;
                    firstFrame = Time.frameCount;
                    Require(UnityEngine.Object.FindObjectsByType<AudioListener>().Length == 1, "Expected one audio listener.");
                }
                if (EditorApplication.timeSinceStartup - startedAt > 90) throw new TimeoutException("Audio verification timed out.");
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                double elapsed = (Time.frameCount - firstFrame) / 60d;
                keyboard.MakeCurrent();
                Capture();
                var engine = car.transform.Find("Engine Audio").GetComponent<AudioSource>();
                if (stage == 0 && elapsed > 0.8)
                {
                    Require(engine.isPlaying && engine.volume > 0f, "Engine did not start at idle.");
                    idlePitch = engine.pitch;
                    Results.Add(new { stage = "idle", pitch = engine.pitch, volume = engine.volume, engine.isVirtual, clipState = engine.clip.loadState.ToString(), previousEditorMute });
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    stage = 1;
                }
                else if (stage == 1 && elapsed > 2.8)
                {
                    drivingPitch = engine.pitch;
                    Require(car.SpeedKph > 3f, "Vehicle did not accelerate during audio verification.");
                    Require(drivingPitch > idlePitch + 0.05f, "Engine pitch did not increase under acceleration.");
                    Results.Add(new { stage = "accelerating", speed = car.SpeedKph, pitch = engine.pitch, volume = engine.volume });
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C));
                    stage = 2;
                }
                else if (stage == 2 && elapsed > 3.8)
                {
                    Require(rig.CockpitView, "Cockpit switch failed.");
                    float cutoff = engine.GetComponent<AudioLowPassFilter>().cutoffFrequency;
                    Require(cutoff < 5000f, "Cockpit filter was not applied.");
                    Results.Add(new { stage = "cockpit", cutoff });
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                    stage = 3;
                }
                else if (stage == 3 && elapsed > 4.8)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    stage = 4;
                }
                else if (stage == 4 && elapsed > 5.4)
                {
                    Require(Time.timeScale == 0f && !engine.isPlaying, "Pause did not suspend audio.");
                    var slider = UnityEngine.Object.FindObjectsByType<Slider>().Single();
                    slider.value = 0f;
                    Require(sound.MasterVolume == 0f, "Volume slider is not connected.");
                    slider.value = 0.8f;
                    var resume = UnityEngine.Object.FindObjectsByType<Button>().Single(button => button.name == "Resume");
                    resume.onClick.Invoke();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Results.Add(new { stage = "pause_and_volume", passed = true });
                    stage = 5;
                }
                else if (stage == 5 && elapsed > 6.2)
                {
                    Require(engine.isPlaying, "Engine did not resume.");
                    car.ResetCar();
                    Results.Add(new { stage = "resume_and_reset", pitch = engine.pitch });
                    double rms = Math.Sqrt(recordedEnergy / Math.Max(1, recordedSamples));
                    double sourceRms = Math.Sqrt(sourceEnergy / Math.Max(1, sourceSamples));
                    Results.Add(new { stage = "recorded_output", samples = recordedSamples, rms, peak = recordedPeak, sourceRms, available = rms > 0.0001 || sourceRms > 0.0001 });
                    if (rms <= 0.0001 && sourceRms <= 0.0001)
                        Debug.LogWarning("Batch audio output is unavailable. Clip data and playback behavior passed; speaker playback remains unverified in this environment.");
                    File.WriteAllText("Captures/audio-check.json", Newtonsoft.Json.JsonConvert.SerializeObject(Results, Newtonsoft.Json.Formatting.Indented));
                    Cleanup();
                    SessionState.SetInt(BatchKey, 2);
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Capture()
        {
            // Batch mode does not produce rendered-frame capture counts; pull one fixed audio frame directly.
            int frames = AudioSettings.outputSampleRate / 60;
            using (var samples = new NativeArray<float>(frames * 2, Allocator.Temp))
            {
                Require(AudioRenderer.Render(samples), "Unable to capture the Unity audio mix.");
                for (int i = 0; i < samples.Length; i++)
                {
                    recordedEnergy += samples[i] * samples[i];
                    recordedPeak = Mathf.Max(recordedPeak, Mathf.Abs(samples[i]));
                }
                recordedSamples += samples.Length;
            }
            var engine = car.transform.Find("Engine Audio").GetComponent<AudioSource>();
            engine.GetOutputData(SourceBuffer, 0);
            foreach (float value in SourceBuffer) sourceEnergy += value * value;
            sourceSamples += SourceBuffer.Length;
        }

        private static void Cleanup()
        {
            AudioListener.volume = 0f;
            if (editorMute != null) editorMute.SetValue(null, previousEditorMute);
            if (captureStarted) AudioRenderer.Stop();
            Time.captureFramerate = 0;
            Time.timeScale = 1f;
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
                if (hadVolume) PlayerPrefs.SetFloat("CircuitRacing.SfxVolume", previousVolume);
                else PlayerPrefs.DeleteKey("CircuitRacing.SfxVolume");
                PlayerPrefs.Save();
                keyboard = null;
            }
        }

        private static void Build()
        {
            AudioListener.volume = 1f;
            AudioListener.pause = false;
            Directory.CreateDirectory("Builds/Circuit01-Audio");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Circuit01-Audio/Circuit01.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4
            });
            File.WriteAllText("Captures/audio-build-result.json", Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                result = report.summary.result.ToString(),
                errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings,
                output = report.summary.outputPath
            }, Newtonsoft.Json.Formatting.Indented));
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Fail(Exception exception)
        {
            Debug.LogException(exception);
            Results.Add(new { stage = "failure", error = exception.Message, recordedSamples, recordedEnergy, recordedPeak, listenerVolume = AudioListener.volume, listenerPause = AudioListener.pause, sampleRate = AudioSettings.outputSampleRate, dspTime = AudioSettings.dspTime });
            File.WriteAllText("Captures/audio-check.json", Newtonsoft.Json.JsonConvert.SerializeObject(Results, Newtonsoft.Json.Formatting.Indented));
            File.WriteAllText("Captures/audio-check-error.txt", exception.ToString());
            Cleanup();
            SessionState.SetInt(BatchKey, 0);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }
}
