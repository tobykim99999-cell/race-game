using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CircuitRacing.Editor
{
    [InitializeOnLoad]
    public static class VehicleStabilityChecks
    {
        private const string RunningKey = "CircuitRacing.StabilityChecks";
        private static readonly float[] Speeds = { 100f, 100f, 150f, 150f, 185f, 185f, 150f, 10f, 220f, 220f, 275f, 275f };
        private static readonly string[] Names = { "100-left", "100-right", "150-left", "150-right", "185-left", "185-right", "150-slalom", "low-speed-start", "220-left", "220-right", "275-left", "275-right" };
        private static readonly List<object> Results = new List<object>();
        private static RaceCarController car;
        private static Keyboard keyboard;
        private static InputSettings.BackgroundBehavior previousBackground;
        private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private static int testIndex;
        private static int firstFrame;
        private static int lastFrame = -1;
        private static double startedAt;
        private static float maximumTilt;
        private static float maximumSteer;
        private static float maximumHeading;
        private static bool launched;

        static VehicleStabilityChecks()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode && SessionState.GetBool(RunningKey, false)) Attach();
            };
        }

        public static void RunBatch()
        {
            try
            {
                Directory.CreateDirectory("Captures");
                EditorSceneManager.OpenScene("Assets/Racing/Scenes/DualViewPractice.unity");
                foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    if (root.GetComponent<RaceCarController>() == null) root.SetActive(false);
                var player = UnityEngine.Object.FindAnyObjectByType<RaceCarController>();
                player.transform.SetPositionAndRotation(new Vector3(0f, 0.9f, 0f), Quaternion.identity);
                player.GetComponent<TimeTrial>().enabled = false;
                var audio = player.GetComponent<RaceCarAudio>();
                if (audio != null) audio.enabled = false;
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Temporary Stability Test Floor";
                floor.transform.position = new Vector3(0f, -1f, 0f);
                floor.transform.localScale = new Vector3(2000f, 2f, 2000f);
                AudioListener.volume = 0f;
                SessionState.SetBool(RunningKey, true);
                Attach();
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void Attach()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying) return;
                EditorApplication.QueuePlayerLoopUpdate();
                if (car == null)
                {
                    car = UnityEngine.Object.FindAnyObjectByType<RaceCarController>();
                    if (car == null || car.Body == null) { car = null; return; }
                    Time.captureFramerate = 60;
                    AudioListener.volume = 0f;
                    previousBackground = InputSystem.settings.backgroundBehavior;
                    previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    keyboard = InputSystem.AddDevice<Keyboard>("StabilityVerification");
                    startedAt = EditorApplication.timeSinceStartup;
                    BeginCase();
                }
                if (EditorApplication.timeSinceStartup - startedAt > 180) throw new TimeoutException("Stability checks timed out.");
                if (lastFrame == Time.frameCount) return;
                lastFrame = Time.frameCount;
                keyboard.MakeCurrent();
                float elapsed = (Time.frameCount - firstFrame) / 60f;
                if (!launched && elapsed >= 0.75f)
                {
                    if (!car.wheels.All(wheel => wheel.isGrounded)) throw new InvalidOperationException("The test car did not settle on the floor.");
                    car.Body.linearVelocity = Vector3.forward * (Speeds[testIndex] / 3.6f);
                    launched = true;
                }
                if (!launched) return;
                bool right = testIndex == 6 ? (int)(elapsed - 0.75f) % 2 != 0 : testIndex % 2 == 1 && testIndex != 7;
                var direction = right ? Key.D : Key.A;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, direction));
                maximumTilt = Mathf.Max(maximumTilt, Vector3.Angle(car.transform.up, Vector3.up));
                maximumSteer = Mathf.Max(maximumSteer, Mathf.Abs(car.wheels[0].steerAngle));
                maximumHeading = Mathf.Max(maximumHeading, Vector3.Angle(Vector3.forward, Vector3.ProjectOnPlane(car.transform.forward, Vector3.up)));
                if (elapsed < 6.75f) return;
                Results.Add(new
                {
                    scenario = Names[testIndex], initialSpeedKph = Speeds[testIndex], maximumTiltDegrees = maximumTilt,
                    maximumSteerDegrees = maximumSteer, maximumHeadingDegrees = maximumHeading,
                    finalSpeedKph = car.SpeedKph, constraints = car.Body.constraints.ToString(),
                    stable = maximumTilt < 25f, turning = maximumHeading > 5f
                });
                if (maximumTilt >= 25f || maximumHeading <= 5f)
                    throw new InvalidOperationException("Handling check failed: " + Names[testIndex]);
                if (++testIndex == Speeds.Length) { Finish(null); return; }
                BeginCase();
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void BeginCase()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            car.ResetCar();
            maximumTilt = 0f;
            maximumSteer = 0f;
            maximumHeading = 0f;
            firstFrame = Time.frameCount;
            launched = false;
        }

        private static void Finish(Exception exception)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(RunningKey, false);
            Time.captureFramerate = 0;
            if (keyboard != null)
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
                keyboard = null;
            }
            if (exception != null) Results.Add(new { error = exception.ToString() });
            File.WriteAllText("Captures/stability-check.json", Newtonsoft.Json.JsonConvert.SerializeObject(Results, Newtonsoft.Json.Formatting.Indented));
            if (exception != null) Debug.LogException(exception);
            // Deliberately no build step: this runner only collects handling measurements.
            EditorApplication.Exit(exception == null ? 0 : 1);
        }
    }
}
