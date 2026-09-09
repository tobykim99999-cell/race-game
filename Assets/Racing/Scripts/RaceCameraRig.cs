using UnityEngine;
using UnityEngine.InputSystem;

namespace CircuitRacing
{
    [RequireComponent(typeof(Camera))]
    public sealed class RaceCameraRig : MonoBehaviour
    {
        public RaceCarController car;
        public Transform cockpitAnchor;
        public Transform chaseAnchor;
        public Transform lookTarget;
        public bool CockpitView { get; private set; }

        private Camera viewCamera;
        private Vector3 positionVelocity;
        private VehicleAbilities abilities;
        private readonly RaycastHit[] cameraHits = new RaycastHit[24];

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            viewCamera.nearClipPlane = 0.035f;
        }

        private void Start()
        {
            if (car != null) abilities = car.GetComponent<VehicleAbilities>();
            SnapToTarget();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                ToggleView();
        }

        private void LateUpdate()
        {
            if (car == null || cockpitAnchor == null || chaseAnchor == null || lookTarget == null) return;
            if (CockpitView)
            {
                // The cockpit camera stays attached to the rendered car pose to avoid clipping through its interior.
                transform.SetPositionAndRotation(cockpitAnchor.position, cockpitAnchor.rotation);
                viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, abilities != null && abilities.Boosting ? 79f : 72f, 1f - Mathf.Exp(-7f * Time.deltaTime));
                return;
            }

            Vector3 position = Vector3.SmoothDamp(transform.position, chaseAnchor.position,
                ref positionVelocity, 0.13f, Mathf.Infinity, Time.deltaTime);
            position = ConstrainCamera(position);
            Quaternion rotation = Quaternion.LookRotation(lookTarget.position - position, Vector3.up);
            transform.position = position;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-12f * Time.deltaTime));
            float fieldOfView = Mathf.Lerp(61f, 70f, Mathf.Clamp01(car.SpeedKph / 180f));
            if (abilities != null && abilities.Boosting) fieldOfView += 7f;
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, fieldOfView, 1f - Mathf.Exp(-7f * Time.deltaTime));
        }

        public void ToggleView()
        {
            CockpitView = !CockpitView;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            viewCamera.nearClipPlane = 0.035f;
            if (car == null || cockpitAnchor == null || chaseAnchor == null || lookTarget == null) return;
            positionVelocity = Vector3.zero;
            if (CockpitView)
            {
                transform.SetPositionAndRotation(cockpitAnchor.position, cockpitAnchor.rotation);
                viewCamera.fieldOfView = 72f;
            }
            else
            {
                Vector3 position = ConstrainCamera(chaseAnchor.position);
                transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookTarget.position - position, Vector3.up));
                viewCamera.fieldOfView = 61f;
            }
        }

        private Vector3 ConstrainCamera(Vector3 requested)
        {
            Vector3 origin = lookTarget.position;
            Vector3 delta = requested - origin;
            float distance = delta.magnitude;
            if (distance < 0.01f) return requested;
            int count = Physics.SphereCastNonAlloc(origin, 0.22f, delta / distance, cameraHits,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
            {
                if (cameraHits[i].collider.transform.IsChildOf(car.transform)) continue;
                nearest = Mathf.Min(nearest, Mathf.Max(0.25f, cameraHits[i].distance - 0.08f));
            }
            return origin + delta / distance * nearest;
        }
    }
}
