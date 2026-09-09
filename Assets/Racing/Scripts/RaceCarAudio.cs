using UnityEngine;

namespace CircuitRacing
{
    [RequireComponent(typeof(RaceCarController))]
    [DefaultExecutionOrder(-50)]
    public sealed class RaceCarAudio : MonoBehaviour
    {
        public AudioClip engineLoop;
        public AudioClip tyreLoop;
        public AudioClip roadLoop;
        public AudioClip collisionClip;
        public float MasterVolume => sharedVolume;
        private static float sharedVolume = 0.8f;

        private RaceCarController car;
        private RaceCameraRig cameraRig;
        private AudioSource engine;
        private AudioSource tyres;
        private AudioSource road;
        private AudioSource impacts;
        private AudioLowPassFilter engineFilter;
        private AudioSource[] sources;
        private bool paused;
        private int gear;
        private float rpm = 900f;
        private float lastImpact = -10f;
        private static readonly float[] GearSpeeds = { 0f, 35f, 65f, 105f, 145f, 190f };

        private void Awake()
        {
            car = GetComponent<RaceCarController>();
            sharedVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("CircuitRacing.SfxVolume", 0.8f));
        }

        private void Start()
        {
            if (engineLoop == null || tyreLoop == null || roadLoop == null || collisionClip == null)
            {
                Debug.LogError("Vehicle audio clips are missing. Run Racing/Install Vehicle Audio.", this);
                enabled = false;
                return;
            }
            cameraRig = FindAnyObjectByType<RaceCameraRig>();
            engine = Source("Engine Audio", engineLoop, true);
            tyres = Source("Tyre Audio", tyreLoop, true);
            road = Source("Road Audio", roadLoop, true);
            impacts = Source("Impact Audio", collisionClip, false);
            engineFilter = engine.gameObject.AddComponent<AudioLowPassFilter>();
            engineFilter.cutoffFrequency = 6500f;
            sources = new[] { engine, tyres, road, impacts };
            engine.volume = 0.2f * MasterVolume;
            engine.pitch = 0.7f;
            engine.Play();
            tyres.Play();
            road.Play();
        }

        private AudioSource Source(string name, AudioClip clip, bool loop)
        {
            var item = new GameObject(name);
            item.transform.SetParent(transform, false);
            var source = item.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            // Player-car audio remains audible across both camera distances without a Doppler jump on switching.
            source.spatialBlend = car.playerControlled ? 0f : 1f;
            source.minDistance = 3f;
            source.maxDistance = 65f;
            source.priority = car.playerControlled ? 64 : 160;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            return source;
        }

        private void LateUpdate()
        {
            if (sources == null) return;
            bool shouldPause = Time.timeScale <= 0f;
            if (shouldPause != paused)
            {
                paused = shouldPause;
                foreach (var source in sources)
                {
                    if (paused) source.Pause();
                    else source.UnPause();
                }
            }
            if (paused) return;

            float speed = car.SpeedKph;
            float torque = 0f;
            float slip = 0f;
            bool grounded = false;
            foreach (var wheel in car.wheels)
            {
                torque = Mathf.Max(torque, Mathf.Abs(wheel.motorTorque));
                if (!wheel.GetGroundHit(out WheelHit hit)) continue;
                grounded = true;
                slip = Mathf.Max(slip, Mathf.Max(Mathf.Abs(hit.sidewaysSlip), Mathf.Abs(hit.forwardSlip) * 0.65f));
            }
            float load = Mathf.Clamp01(torque / 1000f);
            if (gear < 4 && speed > GearSpeeds[gear + 1]) gear++;
            if (gear > 0 && speed < GearSpeeds[gear] - 4f) gear--;
            float revs = Mathf.InverseLerp(GearSpeeds[gear], GearSpeeds[gear + 1], speed);
            float targetRpm = 900f + revs * 4700f + load * 650f;
            float smoothing = 1f - Mathf.Exp(-7f * Time.deltaTime);
            rpm = Mathf.Lerp(rpm, targetRpm, smoothing);
            engine.pitch = Mathf.Lerp(0.7f, 2.2f, Mathf.InverseLerp(900f, 6500f, rpm));
            float inside = car.playerControlled && cameraRig != null && cameraRig.CockpitView ? 0.82f : 1f;
            float vehicleMix = car.playerControlled ? 1f : 0.45f;
            engine.volume = Mathf.Lerp(engine.volume, MasterVolume * vehicleMix * inside * (0.2f + load * 0.25f + revs * 0.06f), smoothing);
            engineFilter.cutoffFrequency = Mathf.Lerp(engineFilter.cutoffFrequency, inside < 1f ? 2200f : 6500f, smoothing);
            float skid = grounded ? Mathf.InverseLerp(0.16f, 0.65f, slip) * Mathf.InverseLerp(3f, 14f, speed) : 0f;
            tyres.volume = Mathf.Lerp(tyres.volume, skid * MasterVolume * vehicleMix * 0.35f, smoothing);
            tyres.pitch = Mathf.Lerp(0.9f, 1.2f, Mathf.Clamp01(speed / 160f));
            road.volume = Mathf.Lerp(road.volume, MasterVolume * vehicleMix * inside * Mathf.Clamp01(speed / 150f) * 0.14f, smoothing);
            road.pitch = Mathf.Lerp(0.75f, 1.4f, Mathf.Clamp01(speed / 180f));
            impacts.volume = MasterVolume * vehicleMix * 0.6f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (impacts == null || paused || Time.time - lastImpact < 0.18f) return;
            float strength = collision.relativeVelocity.magnitude;
            if (strength < 2.2f) return;
            lastImpact = Time.time;
            impacts.PlayOneShot(collisionClip, Mathf.Lerp(0.15f, 1f, Mathf.InverseLerp(2.2f, 15f, strength)));
        }

        public void SetVolume(float volume)
        {
            sharedVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat("CircuitRacing.SfxVolume", MasterVolume);
            PlayerPrefs.Save();
        }

        public void ResetSounds()
        {
            gear = 0;
            rpm = 900f;
            lastImpact = Time.time;
            if (sources == null) return;
            engine.pitch = 0.7f;
            tyres.volume = 0f;
            road.volume = 0f;
            impacts.Stop();
        }

        private void OnDisable()
        {
            if (sources == null) return;
            foreach (var source in sources) if (source != null) source.Stop();
        }
    }
}
