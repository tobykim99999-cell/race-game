using UnityEngine;

namespace CircuitRacing
{
    public sealed class RoadRaceProgress : MonoBehaviour
    {
        public RoadRaceDirector director;
        public string racerName;
        public int racerIndex;
        public float lane;
        public float Distance { get; private set; }
        public float LastCheckpoint { get; private set; }
        public float NextCheckpoint { get; private set; } = 40f;
        public bool Finished { get; private set; }
        public float FinishTime { get; private set; }
        public float RoadSeparation { get; private set; }
        public RaceCarController Car { get; private set; }
        private Vector3 previousPosition;
        private int segment;
        private float offRoadSince;

        private void Awake() => Car = GetComponent<RaceCarController>();
        private void Start() => previousPosition = transform.position;

        private void FixedUpdate()
        {
            if (director == null || director.State != RoadRaceState.Racing || Finished) return;
            RoadRoute route = director.route;
            Vector3 position = Car.Body.position;
            float projected = route.Project(position, ref segment, out float separation);
            RoadSeparation = separation;
            // Rank by position on the route; checkpoint validation must not freeze a racer's distance.
            Distance = Mathf.Min(projected, route.FinishDistance);
            Vector3 gate = route.Sample(NextCheckpoint);
            Vector3 forward = route.Direction(NextCheckpoint);
            float before = Vector3.Dot(previousPosition - gate, forward);
            float after = Vector3.Dot(position - gate, forward);
            if (before < 0f && after >= 0f && Vector3.Distance(previousPosition, position) < 30f)
            {
                Vector3 crossing = Vector3.Lerp(previousPosition, position, -before / (after - before));
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                // Include the four-metre shoulders and the car's half-width, with clearance for short crest hops.
                if (Mathf.Abs(Vector3.Dot(crossing - gate, right)) <= route.width * 0.5f + 5f && Mathf.Abs(crossing.y - gate.y) < 6f)
                {
                    LastCheckpoint = NextCheckpoint;
                    if (NextCheckpoint >= route.FinishDistance - 0.1f)
                    {
                        Finished = true;
                        Distance = route.FinishDistance;
                        FinishTime = director.Elapsed;
                        Car.ControlsEnabled = false;
                        director.OnFinisher(this);
                    }
                    else
                    {
                        NextCheckpoint = Mathf.Min(route.FinishDistance, NextCheckpoint + 200f);
                        float recovery = Mathf.Max(8f, LastCheckpoint - 12f - racerIndex * 7f);
                        Car.SetRecoveryPose(route.Sample(recovery, lane) + Vector3.up * 0.85f, Quaternion.LookRotation(route.Direction(recovery), Vector3.up));
                    }
                }
            }
            previousPosition = position;
            if (!Finished && projected > NextCheckpoint + 30f)
            {
                // A real missed gate or teleport must recover instead of leaving the race unwinnable.
                Car.ResetCar();
                return;
            }
            if (separation > 22f)
            {
                if (offRoadSince <= 0f) offRoadSince = Time.time;
                if (Time.time - offRoadSince > 3f || separation > 65f) Car.ResetCar();
            }
            else offRoadSince = 0f;
        }

        public void AfterRespawn()
        {
            previousPosition = Car.Body.position;
            offRoadSince = 0f;
            segment = 0;
            if (director != null)
            {
                Distance = Mathf.Min(director.route.Project(previousPosition, ref segment, out float separation), director.route.FinishDistance);
                RoadSeparation = separation;
            }
        }
    }
}
