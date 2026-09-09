using UnityEngine;

namespace CircuitRacing
{
    public sealed class RoadPickup : MonoBehaviour
    {
        public RoadPickupKind kind;
        public Transform visual;
        private Vector3 origin;
        private bool collected;

        private void Awake() { if (visual != null) origin = visual.localPosition; }

        private void Update()
        {
            if (visual == null) return;
            visual.Rotate(0f, 65f * Time.deltaTime, 0f, Space.Self);
            visual.localPosition = origin + Vector3.up * (Mathf.Sin(Time.time * 2.3f + transform.position.z) * 0.16f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || other.attachedRigidbody == null) return;
            var car = other.attachedRigidbody.GetComponent<RaceCarController>();
            if (car == null || !car.ControlsEnabled) return;
            var abilities = car.GetComponent<VehicleAbilities>();
            if (abilities == null) return;
            collected = true;
            abilities.Collect(kind);
            gameObject.SetActive(false);
        }
    }
}
