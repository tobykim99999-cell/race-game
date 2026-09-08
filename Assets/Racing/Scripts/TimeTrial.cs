using UnityEngine;

namespace CircuitRacing
{
    public sealed class TimeTrial : MonoBehaviour
    {
        public int CompletedLaps { get; private set; }
        public bool Started { get; private set; }
        public bool Finished => CompletedLaps >= 3;
        public float LapTime => Started && !Finished ? Time.time - lapStart : lastLap;
        public float BestLap { get; private set; }

        private readonly Vector3[] gates = { new Vector3(85f, 0f, 8.5f), new Vector3(0f, 0f, 60f), new Vector3(-85f, 0f, 0f), new Vector3(0f, 0f, -60f) };
        private readonly Vector3[] directions = { Vector3.forward, Vector3.left, Vector3.back, Vector3.right };
        private int nextGate;
        private float lapStart;
        private float lastLap;
        private Vector3 previous;

        private void Start()
        {
            BestLap = PlayerPrefs.GetFloat("CircuitRacing.Circuit01.BestLap", 0f);
            ResetTrial();
        }

        private void FixedUpdate()
        {
            Vector3 current = transform.position;
            if (!Finished)
            {
                Vector3 normal = directions[nextGate];
                Vector3 gate = gates[nextGate];
                float before = Vector3.Dot(previous - gate, normal);
                float after = Vector3.Dot(current - gate, normal);
                if (before < 0f && after >= 0f)
                {
                    Vector3 crossing = Vector3.Lerp(previous, current, -before / (after - before));
                    Vector3 right = Vector3.Cross(Vector3.up, normal);
                    if (Mathf.Abs(Vector3.Dot(crossing - gate, right)) < 7.2f && Mathf.Abs(crossing.y) < 3f)
                    {
                        if (nextGate == 0)
                        {
                            if (Started)
                            {
                                lastLap = Time.time - lapStart;
                                CompletedLaps++;
                                if (BestLap <= 0f || lastLap < BestLap)
                                {
                                    BestLap = lastLap;
                                    PlayerPrefs.SetFloat("CircuitRacing.Circuit01.BestLap", BestLap);
                                    PlayerPrefs.Save();
                                }
                            }
                            Started = true;
                            lapStart = Time.time;
                        }
                        nextGate = (nextGate + 1) % gates.Length;
                    }
                }
            }
            previous = current;
        }

        public void ResetTrial()
        {
            CompletedLaps = 0;
            Started = false;
            nextGate = 0;
            lapStart = Time.time;
            lastLap = 0f;
            previous = transform.position;
        }

        public static string Format(float seconds) => string.Format("{0:00}:{1:00.00}", (int)seconds / 60, seconds % 60f);
    }
}
