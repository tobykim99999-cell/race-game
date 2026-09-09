using UnityEngine;
using UnityEngine.Rendering;

namespace CircuitRacing
{
    public sealed class RoadLighting : MonoBehaviour
    {
        public Light sun;
        public Material sky;
        public ReflectionProbe reflection;
        public RoadTime CurrentTime { get; private set; }
        private Material runtimeSky;

        public void Apply(RoadTime selected)
        {
            CurrentTime = selected == RoadTime.Random ? (RoadTime)Random.Range(1, 4) : selected;
            if (runtimeSky == null) runtimeSky = new Material(sky);
            RenderSettings.skybox = runtimeSky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            if (CurrentTime == RoadTime.Night)
            {
                sun.transform.rotation = Quaternion.Euler(28f, 145f, 0f);
                sun.color = new Color(0.58f, 0.72f, 1f); sun.intensity = 0.38f;
                RenderSettings.ambientSkyColor = new Color(0.27f, 0.34f, 0.45f);
                RenderSettings.ambientEquatorColor = new Color(0.16f, 0.19f, 0.24f);
                RenderSettings.ambientGroundColor = new Color(0.08f, 0.1f, 0.13f);
                RenderSettings.fogColor = new Color(0.055f, 0.08f, 0.13f);
                RenderSettings.fogDensity = 0.0008f;
                runtimeSky.SetFloat("_Exposure", 0.1f);
                runtimeSky.SetColor("_SkyTint", new Color(0.2f, 0.28f, 0.46f));
            }
            else if (CurrentTime == RoadTime.Sunset)
            {
                sun.transform.rotation = Quaternion.Euler(11f, -65f, 0f);
                sun.color = new Color(1f, 0.59f, 0.32f); sun.intensity = 1.5f;
                RenderSettings.ambientSkyColor = new Color(0.47f, 0.38f, 0.45f);
                RenderSettings.ambientEquatorColor = new Color(0.4f, 0.31f, 0.29f);
                RenderSettings.ambientGroundColor = new Color(0.16f, 0.17f, 0.19f);
                RenderSettings.fogColor = new Color(0.6f, 0.43f, 0.38f);
                RenderSettings.fogDensity = 0.00055f;
                runtimeSky.SetFloat("_Exposure", 0.8f);
                runtimeSky.SetColor("_SkyTint", new Color(0.65f, 0.47f, 0.43f));
            }
            else
            {
                sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                sun.color = new Color(1f, 0.95f, 0.85f); sun.intensity = 2.2f;
                RenderSettings.ambientSkyColor = new Color(0.55f, 0.67f, 0.81f);
                RenderSettings.ambientEquatorColor = new Color(0.43f, 0.47f, 0.49f);
                RenderSettings.ambientGroundColor = new Color(0.24f, 0.27f, 0.22f);
                RenderSettings.fogColor = new Color(0.66f, 0.77f, 0.87f);
                RenderSettings.fogDensity = 0.00045f;
                runtimeSky.SetFloat("_Exposure", 1.25f);
                runtimeSky.SetColor("_SkyTint", new Color(0.5f, 0.55f, 0.6f));
            }
            foreach (var lamp in FindObjectsByType<Light>())
                if (lamp.name.StartsWith("Headlamp")) lamp.enabled = CurrentTime != RoadTime.Day;
            DynamicGI.UpdateEnvironment();
            if (reflection != null) reflection.RenderProbe();
        }

        private void OnDestroy() { if (runtimeSky != null) Destroy(runtimeSky); }
    }
}
