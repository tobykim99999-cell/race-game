using UnityEngine;
using UnityEngine.AI;

namespace CircuitRacing
{
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(RaceCarController), typeof(RoadRaceProgress), typeof(NavMeshAgent))]
    public sealed class RoadRaceAi : MonoBehaviour
    {
        public float cruiseSpeed = 180f;
        private RaceCarController car;
        private RoadRaceProgress progress;
        private NavMeshAgent agent;
        private VehicleAbilities abilities;
        private float nextDestination;
        private float lastMovement;
        private float lastDistance;
        private float drivingLane;
        private RoadPickup[] pickups;
        private float[] pickupDistances, pickupLanes;
        private float nextPickupChoice, pickupLane;
        private bool chasingPickup;

        private void Start()
        {
            car = GetComponent<RaceCarController>();
            progress = GetComponent<RoadRaceProgress>();
            abilities = GetComponent<VehicleAbilities>();
            agent = GetComponent<NavMeshAgent>();
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.updateUpAxis = false;
            agent.enabled = true;
            lastMovement = Time.time;
            drivingLane = progress.lane;
            pickups = FindObjectsByType<RoadPickup>();
            pickupDistances = new float[pickups.Length]; pickupLanes = new float[pickups.Length];
            for (int i = 0; i < pickups.Length; i++)
            {
                int segment = 0;
                pickupDistances[i] = progress.director.route.Project(pickups[i].transform.position, ref segment, out _);
                Vector3 right = Vector3.Cross(Vector3.up, progress.director.route.Direction(pickupDistances[i])).normalized;
                pickupLanes[i] = Vector3.Dot(pickups[i].transform.position - progress.director.route.Sample(pickupDistances[i]), right);
            }
        }

        private void Update()
        {
            if (progress.director == null || !car.ControlsEnabled || progress.Finished)
            {
                car.SetAiInput(0f, 0f, 1f, false, false);
                if (agent.isOnNavMesh) agent.isStopped = true;
                lastMovement = Time.time;
                return;
            }
            RoadRoute route = progress.director.route;
            if (progress.Distance < lastDistance - 20f)
            {
                // A checkpoint recovery must also reset the stuck-car timer.
                // Otherwise the AI resets again while driving back to its old distance.
                lastDistance = progress.Distance;
                lastMovement = Time.time;
                drivingLane = progress.lane;
                chasingPickup = false;
                nextPickupChoice = Time.time + 1f;
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit reset, 6f, NavMesh.AllAreas)) agent.Warp(reset.position);
            }
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit recovery, 6f, NavMesh.AllAreas)) agent.Warp(recovery.position);
                else { car.ResetCar(); return; }
            }
            agent.isStopped = false;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit current, 5f, NavMesh.AllAreas)) agent.nextPosition = current.position;

            float curvature = 0f, cornerLimit = float.PositiveInfinity;
            for (int d = 0; d <= 240; d += 20)
            {
                float bend = route.Curvature(progress.Distance + d);
                if (d <= 180) curvature = Mathf.Max(curvature, bend);
                float cornerSpeedSquared = (abilities != null && abilities.GripSeconds > 0f ? 5f : 4.4f)
                    / Mathf.Max(0.0002f, bend);
                cornerLimit = Mathf.Min(cornerLimit, Mathf.Sqrt(cornerSpeedSquared + 2f * 4.5f * Mathf.Max(0, d - 40)) * 3.6f);
            }
            bool boostReady = abilities != null && (abilities.TurboSeconds > 0f || abilities.Energy > (abilities.Boosting ? 2f : 18f))
                && curvature < 0.0012f && car.SpeedKph > 62f && progress.RoadSeparation < 6f;
            float targetSpeed = Mathf.Min(cruiseSpeed + (boostReady ? 35f : 0f), cornerLimit);
            if (Time.time >= nextPickupChoice)
            {
                chasingPickup = false;
                float best = float.PositiveInfinity;
                for (int i = 0; i < pickups.Length; i++)
                {
                    if (pickups[i] == null || !pickups[i].gameObject.activeInHierarchy) continue;
                    float ahead = pickupDistances[i] - progress.Distance;
                    if (ahead < -3f || ahead > 160f || Mathf.Abs(pickupLanes[i]) > 5f || !LaneClear(route, pickupLanes[i], 40f)) continue;
                    float value = pickups[i].kind == RoadPickupKind.Energy && abilities.Energy < 65f ? 22f
                        : pickups[i].kind == RoadPickupKind.Turbo && curvature < 0.0018f ? 18f
                        : pickups[i].kind == RoadPickupKind.Grip && curvature > 0.001f ? 20f : 0f;
                    float score = ahead + Mathf.Abs(pickupLanes[i] - drivingLane) * 3f - value;
                    if (score >= best) continue;
                    best = score; pickupLane = pickupLanes[i]; chasingPickup = true;
                }
                nextPickupChoice = Time.time + 0.2f;
            }
            float desiredLane = chasingPickup && LaneClear(route, pickupLane, 30f) ? pickupLane : progress.lane;
            foreach (var other in progress.director.racers)
            {
                if (other == progress || !other.gameObject.activeInHierarchy) continue;
                Vector3 relative = transform.InverseTransformPoint(other.transform.position);
                if (relative.z <= -7f || relative.z > 55f) continue;
                // Keep a braking gap until the actual car has cleared the lane.
                // Choosing an overtaking lane does not instantly move the chassis.
                if (relative.z > 0f && Mathf.Abs(relative.x) < 2.8f)
                {
                    float leadSpeed = other.Car.SpeedKph / 3.6f;
                    float followingSpeed = Mathf.Sqrt(leadSpeed * leadSpeed + 2f * 5f * Mathf.Max(0f, relative.z - 12f)) * 3.6f;
                    targetSpeed = Mathf.Min(targetSpeed, followingSpeed);
                }
                float otherLane = Vector3.Dot(other.transform.position - route.Sample(other.Distance), Vector3.Cross(Vector3.up, route.Direction(other.Distance)).normalized);
                if (Mathf.Abs(otherLane - desiredLane) > 2.6f || other.Car.SpeedKph > targetSpeed + 3f) continue;
                float passingLane = otherLane <= 0f ? 3.8f : -3.8f;
                if (LaneClear(route, passingLane, 40f)) desiredLane = passingLane;
                if (relative.z > 0f && relative.z < 18f && Mathf.Abs(relative.x) < 2.6f)
                    targetSpeed = Mathf.Min(targetSpeed, Mathf.Max(12f, other.Car.SpeedKph - 8f));
            }
            if (Mathf.Abs(desiredLane - drivingLane) > 0.5f)
            {
                boostReady = false;
                targetSpeed = Mathf.Min(targetSpeed, cruiseSpeed);
            }
            drivingLane = Mathf.MoveTowards(drivingLane, desiredLane, Time.deltaTime * 2f);
            float currentLane = Vector3.Dot(transform.position - route.Sample(progress.Distance),
                Vector3.Cross(Vector3.up, route.Direction(progress.Distance)).normalized);
            float headingError = Vector3.Angle(Vector3.ProjectOnPlane(transform.forward, Vector3.up),
                Vector3.ProjectOnPlane(route.Direction(progress.Distance + 15f), Vector3.up));
            if (Mathf.Abs(currentLane - drivingLane) > 2.5f || headingError > 10f)
            { boostReady = false; targetSpeed = Mathf.Min(targetSpeed, 110f); }
            if (Mathf.Abs(currentLane) > 7f)
            { boostReady = false; targetSpeed = Mathf.Min(targetSpeed, 75f); }
            float lookAhead = Mathf.Clamp(12f + car.SpeedKph * 0.22f, 16f, 58f);
            Vector3 target = route.Sample(Mathf.Min(route.FinishDistance + 30f, progress.Distance + lookAhead), drivingLane);
            agent.speed = targetSpeed / 3.6f;
            if (Time.time >= nextDestination)
            {
                agent.SetDestination(target);
                nextDestination = Time.time + 0.1f;
            }
            // Steer toward the route's moving lookahead point. A cached NavMesh
            // corner can lag behind a fast car when it changes pickup lanes.
            Vector3 local = transform.InverseTransformPoint(target);
            float desiredAngle = Mathf.Atan2(2f * car.Wheelbase * local.x, Mathf.Max(1f, local.x * local.x + local.z * local.z)) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(desiredAngle / Mathf.Max(0.2f, car.SteeringLimitDegrees), -1f, 1f);
            float error = targetSpeed - car.SpeedKph;
            float throttle = Mathf.Clamp01(error / 13f + 0.42f);
            float brake = Mathf.Clamp01(-error / 16f);
            if (brake > 0.05f) throttle = 0f;
            // Rivals spend collected energy on the same boost skill as the player.
            bool boost = boostReady && Mathf.Abs(steer) < 0.65f && brake < 0.05f;
            car.SetAiInput(steer, throttle, brake, false, boost);

            if (progress.Distance > lastDistance + 3f)
            {
                lastDistance = progress.Distance;
                lastMovement = Time.time;
            }
            if (Time.time - lastMovement > 5f)
            {
                car.ResetCar();
                agent.Warp(route.Sample(Mathf.Max(8f, progress.LastCheckpoint - 12f - progress.racerIndex * 7f), progress.lane));
                lastDistance = progress.LastCheckpoint - 20f;
                lastMovement = Time.time;
            }
        }

        private bool LaneClear(RoadRoute route, float lane, float ahead)
        {
            foreach (var other in progress.director.racers)
            {
                if (other == progress || !other.gameObject.activeInHierarchy) continue;
                float separation = other.Distance - progress.Distance;
                if (separation < -14f || separation > ahead) continue;
                float otherLane = Vector3.Dot(other.transform.position - route.Sample(other.Distance),
                    Vector3.Cross(Vector3.up, route.Direction(other.Distance)).normalized);
                if (Mathf.Abs(otherLane - lane) < 2.8f) return false;
            }
            return true;
        }
    }
}
