using UnityEngine;
using UnityEngine.InputSystem;

namespace CircuitRacing
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RaceCarController : MonoBehaviour
    {
        public WheelCollider[] wheels;
        public Transform[] wheelVisuals;
        public Transform steeringWheel;

        public float SpeedKph => body == null ? 0f : body.linearVelocity.magnitude * 3.6f;
        public float Steering { get; private set; }
        public Rigidbody Body => body;

        private Rigidbody body;
        private Vector3 resetPosition;
        private Quaternion resetRotation;
        private Quaternion steeringRestRotation;
        private float throttle;
        private float brake;
        private bool handbrake;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (wheels == null || wheels.Length != 4 || wheelVisuals == null || wheelVisuals.Length != 4)
            {
                Debug.LogError("Assign four wheels and wheel visuals, ordered front-left, front-right, rear-left, rear-right.", this);
                enabled = false;
                return;
            }
            body.centerOfMass = new Vector3(0f, -0.25f, 0.05f);
            resetPosition = transform.position;
            resetRotation = transform.rotation;
            if (steeringWheel != null) steeringRestRotation = steeringWheel.localRotation;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            throttle = 0f;
            brake = 0f;
            handbrake = false;
            float desiredSteering = 0f;
            if (keyboard != null && Time.timeScale > 0f)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) throttle = 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) brake = 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) desiredSteering -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) desiredSteering += 1f;
                handbrake = keyboard.spaceKey.isPressed;
                if (keyboard.rKey.wasPressedThisFrame) ResetCar();
            }
            Steering = Mathf.MoveTowards(Steering, desiredSteering, Time.deltaTime * 3.5f);

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheelVisuals[i] == null) continue;
                wheels[i].GetWorldPose(out Vector3 position, out Quaternion rotation);
                wheelVisuals[i].SetPositionAndRotation(position, rotation);
            }
            if (steeringWheel != null)
                steeringWheel.localRotation = steeringRestRotation * Quaternion.AngleAxis(-Steering * 115f, Vector3.forward);
            if (transform.position.y < -8f) ResetCar();
        }

        private void FixedUpdate()
        {
            float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
            float steeringAngle = Mathf.Lerp(30f, 12f, Mathf.Clamp01(SpeedKph / 150f));
            bool reversing = brake > 0f && forwardSpeed < 0.8f;
            bool stoppingReverse = throttle > 0f && forwardSpeed < -0.8f;
            float motor = reversing ? -650f : throttle * 1000f;
            float braking = (brake > 0f && !reversing) || stoppingReverse ? 2600f : 0f;
            if (SpeedKph > (reversing ? 35f : 185f) || stoppingReverse) motor = 0f;

            for (int i = 0; i < wheels.Length; i++)
            {
                bool rear = i >= 2;
                wheels[i].steerAngle = rear ? 0f : Steering * steeringAngle;
                wheels[i].motorTorque = rear ? motor : 0f;
                wheels[i].brakeTorque = rear && handbrake ? 3800f : braking;
                if (throttle == 0f && brake == 0f && !handbrake) wheels[i].brakeTorque = 40f;
            }
            body.AddForce(-transform.up * body.linearVelocity.sqrMagnitude * 1.3f);
        }

        public void ResetCar()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = resetPosition;
            body.rotation = resetRotation;
            Steering = 0f;
            if (TryGetComponent<TimeTrial>(out var trial)) trial.ResetTrial();
            body.WakeUp();
            var rig = FindAnyObjectByType<RaceCameraRig>();
            if (rig != null) rig.SnapToTarget();
        }
    }
}
