using UnityEngine;

namespace CircuitRacing
{
    public enum RoadPickupKind { Energy, Turbo, Grip }

    [DefaultExecutionOrder(20)]
    [RequireComponent(typeof(RaceCarController))]
    public sealed class VehicleAbilities : MonoBehaviour
    {
        public ParticleSystem[] jets;
        public Light[] jetLights;
        public float Energy { get; private set; } = 35f;
        public bool Boosting { get; private set; }
        public bool Drifting { get; private set; }
        public float GripSeconds => Mathf.Max(0f, gripUntil - Time.time);
        public float TurboSeconds => Mathf.Max(0f, turboUntil - Time.time);
        public string LastPickup { get; private set; }
        public float NoticeUntil { get; private set; }
        private RaceCarController car;
        private bool requested;
        private float turboUntil;
        private float gripUntil;

        private void Awake() => car = GetComponent<RaceCarController>();

        public void RequestBoost(bool value) => requested = value;

        private void FixedUpdate()
        {
            Vector3 velocity = car.Body.linearVelocity;
            float forward = Vector3.Dot(velocity, transform.forward);
            float lateral = Mathf.Abs(Vector3.Dot(velocity, transform.right));
            float angle = Mathf.Atan2(lateral, Mathf.Abs(forward)) * Mathf.Rad2Deg;
            Drifting = car.ControlsEnabled && car.GroundedWheelCount >= 2 && car.SpeedKph > 30f
                && forward > 5f && lateral > 1.6f && angle > 8f && angle < 42f && Mathf.Abs(car.Steering) > 0.12f;
            if (Drifting) Energy = Mathf.Min(100f, Energy + Time.fixedDeltaTime * Mathf.Lerp(8f, 18f, angle / 42f));
            bool freeTurbo = TurboSeconds > 0f;
            Boosting = car.ControlsEnabled && car.GroundedWheelCount >= 2 && car.SpeedKph > 4f
                && (requested || freeTurbo) && (Energy > 0f || freeTurbo);
            if (Boosting && !freeTurbo) Energy = Mathf.Max(0f, Energy - Time.fixedDeltaTime * 16f);
        }

        private void LateUpdate()
        {
            if (jets != null)
                foreach (var jet in jets)
                {
                    if (Boosting && !jet.isPlaying) jet.Play();
                    if (!Boosting && jet.isPlaying) jet.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            if (jetLights != null) foreach (var lamp in jetLights) lamp.enabled = Boosting;
        }

        public void Collect(RoadPickupKind kind)
        {
            if (!car.ControlsEnabled) return;
            switch (kind)
            {
                case RoadPickupKind.Energy: Energy = Mathf.Min(100f, Energy + 35f); LastPickup = "ENERGY +35"; break;
                case RoadPickupKind.Turbo: turboUntil = Time.time + 5f; LastPickup = "TURBO"; break;
                case RoadPickupKind.Grip: gripUntil = Time.time + 8f; LastPickup = "GRIP"; break;
            }
            NoticeUntil = Time.time + 2f;
        }

        public void ResetAfterRespawn()
        {
            Energy = Mathf.Max(0f, Energy - 15f);
            requested = false;
            Boosting = false;
            Drifting = false;
            turboUntil = 0f;
        }
    }
}
