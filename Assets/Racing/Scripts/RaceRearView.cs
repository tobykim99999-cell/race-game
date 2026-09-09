using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CircuitRacing
{
    [RequireComponent(typeof(RawImage))]
    public sealed class RaceRearView : MonoBehaviour
    {
        public RaceCarController car;
        private Camera rearCamera;
        private RenderTexture texture;
        private RawImage display;

        private void Start()
        {
            display = GetComponent<RawImage>();
            display.raycastTarget = false;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            display.uvRect = new Rect(1f, 0f, -1f, 1f);
            texture = new RenderTexture(864, 224, 24, RenderTextureFormat.ARGB32)
            {
                name = "Rear View Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1
            };
            texture.Create();
            display.texture = texture;

            rearCamera = new GameObject("Rear View Camera").AddComponent<Camera>();
            rearCamera.transform.SetParent(car.transform, false);
            // Mount behind the body so the Porsche's own cabin and rear deck cannot obscure traffic.
            rearCamera.transform.localPosition = new Vector3(0f, 0.95f, -2.65f);
            rearCamera.transform.localRotation = Quaternion.Euler(5f, 180f, 0f);
            rearCamera.fieldOfView = 27f;
            rearCamera.aspect = (float)texture.width / texture.height;
            rearCamera.nearClipPlane = 0.08f;
            rearCamera.farClipPlane = 400f;
            rearCamera.clearFlags = CameraClearFlags.Skybox;
            rearCamera.cullingMask = ~(1 << 5);
            rearCamera.targetTexture = texture;
            rearCamera.depth = -10f;
            rearCamera.allowHDR = true;
            rearCamera.allowMSAA = false;
            rearCamera.useOcclusionCulling = false;
            var data = rearCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = false;
            data.requiresDepthOption = CameraOverrideOption.Off;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.None;
        }

        private void LateUpdate()
        {
            if (rearCamera != null) rearCamera.enabled = Time.timeScale > 0f;
        }

        private void OnDisable()
        {
            if (rearCamera != null) rearCamera.enabled = false;
        }

        private void OnDestroy()
        {
            if (rearCamera != null)
            {
                rearCamera.enabled = false;
                rearCamera.targetTexture = null;
                Destroy(rearCamera.gameObject);
            }
            if (display != null) display.texture = null;
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
