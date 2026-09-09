using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CircuitRacing
{
    public enum RoadRaceState { Ready, Countdown, Racing, Finished, Loading }

    [DefaultExecutionOrder(-100)]
    public sealed class RoadRaceDirector : MonoBehaviour
    {
        public RoadRegionDefinition region;
        public RoadRegionDefinition[] regions;
        public RoadRoute route;
        public RoadRaceProgress[] racers;
        public RoadLighting lighting;
        public RaceCameraRig cameraRig;
        public RoadRaceState State { get; private set; } = RoadRaceState.Ready;
        public RoadRaceProgress Player => racers[0];
        public float Elapsed => State == RoadRaceState.Racing ? Time.time - raceStarted : finalTime;
        public int Countdown => Mathf.Max(0, Mathf.CeilToInt(countdownUntil - Time.time));
        public float LoadingProgress { get; private set; }
        public int PlayerPlace => Standings().ToList().IndexOf(Player) + 1;
        private float countdownUntil;
        private float raceStarted;
        private float finalTime;
        private static bool autoStart;

        private void Awake()
        {
            Time.timeScale = 1f;
            foreach (var racer in racers) racer.GetComponent<RaceCarController>().ControlsEnabled = false;
        }

        private void Start()
        {
            lighting.Apply(RoadLaunchSettings.TimeOfDay);
            if (autoStart) { autoStart = false; BeginRace(); }
        }

        private void Update()
        {
            if (State == RoadRaceState.Countdown && Time.time >= countdownUntil)
            {
                State = RoadRaceState.Racing;
                raceStarted = Time.time;
                foreach (var racer in racers) racer.Car.ControlsEnabled = true;
            }
        }

        public void BeginRace()
        {
            if (State != RoadRaceState.Ready) return;
            lighting.Apply(RoadLaunchSettings.TimeOfDay);
            State = RoadRaceState.Countdown;
            countdownUntil = Time.time + 3f;
        }

        public RoadRaceProgress[] Standings()
        {
            return racers.OrderByDescending(racer => racer.Finished).ThenBy(racer => racer.Finished ? racer.FinishTime : -racer.Distance).ThenBy(racer => racer.racerIndex).ToArray();
        }

        public void OnFinisher(RoadRaceProgress racer)
        {
            if (racer != Player) return;
            finalTime = Time.time - raceStarted;
            State = RoadRaceState.Finished;
            foreach (var participant in racers) participant.Car.ControlsEnabled = false;
            FindAnyObjectByType<RoadRaceHud>()?.ShowFinish();
            Time.timeScale = 0f;
            string key = "CircuitRacing.LongRoad." + region.region;
            float best = PlayerPrefs.GetFloat(key, 0f);
            if (best <= 0f || finalTime < best) { PlayerPrefs.SetFloat(key, finalTime); PlayerPrefs.Save(); }
        }

        public void SetTimeOfDay(RoadTime time)
        {
            RoadLaunchSettings.TimeOfDay = time;
            if (State == RoadRaceState.Ready) lighting.Apply(time);
        }

        public void SelectRegion(RoadRegionDefinition selected)
        {
            if (selected == region || State == RoadRaceState.Loading) return;
            autoStart = false;
            StartCoroutine(LoadRegion(selected.sceneName));
        }

        public void Restart(bool startRace)
        {
            if (State == RoadRaceState.Loading) return;
            autoStart = startRace;
            StartCoroutine(LoadRegion(region.sceneName));
        }

        private IEnumerator LoadRegion(string sceneName)
        {
            State = RoadRaceState.Loading;
            Time.timeScale = 1f;
            var operation = SceneManager.LoadSceneAsync(sceneName);
            while (!operation.isDone)
            {
                LoadingProgress = Mathf.Clamp01(operation.progress / 0.9f);
                yield return null;
            }
        }
    }
}
