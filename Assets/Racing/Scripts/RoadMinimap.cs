using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CircuitRacing
{
    [RequireComponent(typeof(RawImage))]
    public sealed class RoadMinimap : MonoBehaviour
    {
        public RoadRaceDirector director;
        public Camera ViewCamera { get; private set; }
        private RawImage display;
        private RenderTexture texture;
        private VolumeProfile profile;
        private ColorAdjustments color;
        private float nextRender;

        private void Start()
        {
            display = GetComponent<RawImage>();
            display.raycastTarget = false;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            texture = new RenderTexture(536, 448, 24, RenderTextureFormat.ARGB32)
            {
                name = "Terrain Minimap Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1
            };
            texture.Create();
            display.texture = texture;

            var cameraObject = new GameObject("Terrain Minimap Camera") { layer = 5 };
            cameraObject.transform.SetParent(director.transform, false);
            ViewCamera = cameraObject.AddComponent<Camera>();
            ViewCamera.enabled = false;
            ViewCamera.orthographic = true;
            ViewCamera.orthographicSize = 140f;
            ViewCamera.aspect = (float)texture.width / texture.height;
            ViewCamera.nearClipPlane = 1f;
            ViewCamera.farClipPlane = 850f;
            ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            ViewCamera.backgroundColor = new Color(0.15f, 0.2f, 0.16f);
            ViewCamera.cullingMask = ~(1 << 5);
            ViewCamera.targetTexture = texture;
            ViewCamera.depth = -20f;
            ViewCamera.allowHDR = true;
            ViewCamera.allowMSAA = false;
            ViewCamera.useOcclusionCulling = false;
            var data = ViewCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.None;
            // A private volume keeps night-map exposure independent of the driving and mirror cameras.
            data.volumeLayerMask = 1 << 5;
            data.volumeTrigger = ViewCamera.transform;
            var volume = cameraObject.AddComponent<Volume>();
            volume.isGlobal = true;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(12f);
            color.saturation.Override(12f);
            volume.sharedProfile = profile;
            UpdatePose();
        }

        private void UpdatePose()
        {
            var car = director.Player.Car;
            Vector3 forward = Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized;
            Vector3 centre = car.transform.position + forward * 70f;
            // These point-to-point landscapes extend along world Z; keep their ends inside the map.
            var points = director.route.points;
            centre.z = Mathf.Clamp(centre.z, points[0].z + 142f, points[points.Length - 1].z - 142f);
            ViewCamera.transform.SetPositionAndRotation(centre + Vector3.up * 450f, Quaternion.Euler(90f, 0f, 0f));
        }

        private void LateUpdate()
        {
            if (ViewCamera == null) return;
            ViewCamera.enabled = (director.State == RoadRaceState.Racing || director.State == RoadRaceState.Countdown)
                && Time.timeScale > 0f && Time.unscaledTime >= nextRender;
            if (!ViewCamera.enabled) return;
            UpdatePose();
            var time = director.lighting.CurrentTime;
            color.postExposure.Override(time == RoadTime.Night ? 2.5f : time == RoadTime.Sunset ? 1f : 0.35f);
            nextRender = Time.unscaledTime + 1f / 15f;
        }

        private void OnEnable() => nextRender = 0f;

        private void OnDisable()
        {
            if (ViewCamera != null) ViewCamera.enabled = false;
        }

        private void OnDestroy()
        {
            if (ViewCamera != null)
            {
                ViewCamera.enabled = false;
                ViewCamera.targetTexture = null;
                ViewCamera.GetComponent<Volume>().sharedProfile = null;
                Destroy(ViewCamera.gameObject);
            }
            if (display != null) display.texture = null;
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (profile != null)
            {
                foreach (var component in profile.components) Destroy(component);
                Destroy(profile);
            }
        }
    }
}
