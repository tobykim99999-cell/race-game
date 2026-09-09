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
        public bool playerControlled = true;
        public bool ControlsEnabled { get; set; } = true;
        public int GroundedWheelCount { get; private set; }
        public float SteeringLimitDegrees { get; private set; } = 30f;
        public float Wheelbase => wheelbase;

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
        private float desiredSteering;
        private float wheelbase;
        private readonly WheelHit[] wheelHits = new WheelHit[4];
        private readonly bool[] groundedWheels = new bool[4];
        private readonly float[] sidewaysStiffness = new float[4];
        private VehicleAbilities abilities;
        private Vector3 aiInput;
        private bool aiDrift;
        private bool aiBoost;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (wheels == null || wheels.Length != 4 || wheelVisuals == null || wheelVisuals.Length != 4)
            {
                Debug.LogError("Assign four wheels and wheel visuals, ordered front-left, front-right, rear-left, rear-right.", this);
                enabled = false;
                return;
            }
            body.centerOfMass = new Vector3(0f, -0.35f, 0.05f);
            Vector3 frontAxle = (wheels[0].transform.localPosition + wheels[1].transform.localPosition) * 0.5f;
            Vector3 rearAxle = (wheels[2].transform.localPosition + wheels[3].transform.localPosition) * 0.5f;
            wheelbase = Mathf.Max(1f, Vector3.Distance(frontAxle, rearAxle));
            foreach (var wheel in wheels) wheel.forceAppPointDistance = 0.12f;
            for (int i = 0; i < wheels.Length; i++) sidewaysStiffness[i] = wheels[i].sidewaysFriction.stiffness;
            abilities = GetComponent<VehicleAbilities>();
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
            desiredSteering = 0f;
            if (playerControlled && keyboard != null && Time.timeScale > 0f && ControlsEnabled)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) throttle = 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) brake = 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) desiredSteering -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) desiredSteering += 1f;
                handbrake = keyboard.spaceKey.isPressed;
                if (keyboard.rKey.wasPressedThisFrame) ResetCar();
            }
            if (!playerControlled && ControlsEnabled)
            {
                desiredSteering = aiInput.x;
                throttle = aiInput.y;
                brake = aiInput.z;
                handbrake = aiDrift;
            }
            if (abilities != null)
                abilities.RequestBoost(ControlsEnabled && (playerControlled
                    ? keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) : aiBoost));
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
            float speedSquared = body.linearVelocity.sqrMagnitude;
            // Bicycle-model curvature: a = v^2 * tan(steer) / wheelbase. Keep full keyboard lock within a manageable lateral demand.
            float steeringAngle = Mathf.Min(30f, Mathf.Atan(8.5f * wheelbase / Mathf.Max(1f, speedSquared)) * Mathf.Rad2Deg);
            if (abilities != null && abilities.GripSeconds > 0f) steeringAngle = Mathf.Min(30f, steeringAngle * 1.08f);
            SteeringLimitDegrees = steeringAngle;
            float steeringRate = Mathf.Lerp(4.5f, 1.8f, Mathf.Clamp01(SpeedKph / 120f));
            Steering = Mathf.MoveTowards(Steering, desiredSteering, steeringRate * Time.fixedDeltaTime);
            bool reversing = playerControlled && brake > 0f && forwardSpeed < 0.8f;
            bool stoppingReverse = throttle > 0f && forwardSpeed < -0.8f;
            float motor = reversing ? -650f : throttle * 1325f;
            bool boost = abilities != null && abilities.Boosting;
            if (boost && !reversing) motor *= 1.9f;
            float braking = (brake > 0f && !reversing) || stoppingReverse ? 2600f : 0f;
            if (SpeedKph > (reversing ? 35f : boost ? 275f : 220f) || stoppingReverse) motor = 0f;
            if (!ControlsEnabled) { motor = 0f; braking = 4500f; }
            bool driftInput = abilities != null && handbrake && SpeedKph > 30f && GroundedWheelCount >= 2;

            Vector3 groundNormal = Vector3.zero;
            int groundedCount = 0;
            for (int i = 0; i < wheels.Length; i++)
            {
                bool rear = i >= 2;
                wheels[i].steerAngle = rear ? 0f : Steering * steeringAngle;
                wheels[i].motorTorque = rear ? motor : 0f;
                wheels[i].brakeTorque = rear && handbrake ? driftInput ? 1100f : 3800f : braking;
                if (ControlsEnabled && throttle == 0f && brake == 0f && !handbrake) wheels[i].brakeTorque = 40f;
                var friction = wheels[i].sidewaysFriction;
                float grip = abilities != null && abilities.GripSeconds > 0f ? 1.12f : 1f;
                float desiredGrip = sidewaysStiffness[i] * grip * (rear && driftInput ? 0.48f : 1f);
                friction.stiffness = Mathf.MoveTowards(friction.stiffness, desiredGrip, Time.fixedDeltaTime * 3f);
                wheels[i].sidewaysFriction = friction;
                groundedWheels[i] = wheels[i].GetGroundHit(out wheelHits[i]);
                if (groundedWheels[i])
                {
                    groundedCount++;
                    groundNormal += wheelHits[i].normal;
                }
            }
            ApplyAntiRoll(0, 1);
            ApplyAntiRoll(2, 3);
            GroundedWheelCount = groundedCount;
            groundNormal.Normalize();
            if (groundedCount >= 2 && Vector3.Dot(transform.up, groundNormal) > 0.25f)
            {
                body.AddForce(-groundNormal * speedSquared * 1.3f);
                if (boost && ControlsEnabled && !reversing && throttle > 0f && brake <= 0f && SpeedKph < 275f)
                    body.AddForce(Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized * (5f * throttle), ForceMode.Acceleration);
                float slipAngle = Mathf.Atan2(Mathf.Abs(Vector3.Dot(body.linearVelocity, transform.right)), Mathf.Max(1f, Mathf.Abs(forwardSpeed))) * Mathf.Rad2Deg;
                if (driftInput && slipAngle < 30f) body.AddTorque(groundNormal * Steering * body.mass * 0.3f);
            }
        }

        public void SetAiInput(float steer, float acceleration, float braking, bool drift, bool boost)
        {
            aiInput = new Vector3(Mathf.Clamp(steer, -1f, 1f), Mathf.Clamp01(acceleration), Mathf.Clamp01(braking));
            aiDrift = drift;
            aiBoost = boost;
        }

        public void SetRecoveryPose(Vector3 position, Quaternion rotation)
        {
            resetPosition = position;
            resetRotation = rotation;
        }

        private void ApplyAntiRoll(int left, int right)
        {
            if (!groundedWheels[left] || !groundedWheels[right]) return;
            Vector3 normal = (wheelHits[left].normal + wheelHits[right].normal).normalized;
            if (Vector3.Dot(transform.up, normal) < 0.25f) return;
            float leftTravel = SuspensionTravel(left);
            float rightTravel = SuspensionTravel(right);
            float limit = body.mass * Physics.gravity.magnitude * 0.18f;
            float force = Mathf.Clamp((leftTravel - rightTravel) * 4500f, -limit, limit);
            body.AddForceAtPosition(-normal * force, wheels[left].transform.position);
            body.AddForceAtPosition(normal * force, wheels[right].transform.position);
        }

        private float SuspensionTravel(int index)
        {
            var wheel = wheels[index];
            float contactY = wheel.transform.InverseTransformPoint(wheelHits[index].point).y;
            return Mathf.Clamp01((-contactY - wheel.radius) / Mathf.Max(0.01f, wheel.suspensionDistance));
        }

        public void ResetCar()
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = resetPosition;
            body.rotation = resetRotation;
            Steering = 0f;
            desiredSteering = 0f;
            if (TryGetComponent<TimeTrial>(out var trial)) trial.ResetTrial();
            if (TryGetComponent<RaceCarAudio>(out var audio)) audio.ResetSounds();
            if (abilities != null) abilities.ResetAfterRespawn();
            if (TryGetComponent<RoadRaceProgress>(out var progress)) progress.AfterRespawn();
            body.WakeUp();
            var rig = FindAnyObjectByType<RaceCameraRig>();
            if (rig != null && rig.car == this) rig.SnapToTarget();
        }
    }
}
