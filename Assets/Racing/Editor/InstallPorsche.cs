using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CircuitRacing.Editor
{
    public static class InstallPorsche
    {
        private const string Root = "Assets/Racing/Art/Porsche911";
        private const string ModelPath = Root + "/source/porsche_911_turbo.fbx";

        [MenuItem("Racing/Install Porsche In Practice Scene")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var car = UnityEngine.Object.FindAnyObjectByType<RaceCarController>();
            if (car == null) throw new InvalidOperationException("Open the dual-view practice scene first.");
            if (car.transform.Find("Porsche911") != null) throw new InvalidOperationException("Porsche is already installed.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException("Import the Porsche FBX first.");
            if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
            if (!AssetDatabase.IsValidFolder(Root + "/Derived")) AssetDatabase.CreateFolder(Root, "Derived");

            ConfigureTextures();
            var model = UnityEngine.Object.Instantiate(source, car.transform, false);
            model.name = "Porsche911";
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            model.transform.localPosition = new Vector3(0f, -0.58f, 2.27f);
            model.transform.Find("door_2").localRotation = Quaternion.Euler(270f, 0f, 0f);
            var materials = new Dictionary<string, Material>();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string name = slots[i].name;
                    if (!materials.TryGetValue(name, out Material material))
                    {
                        material = ConvertMaterial(name);
                        materials.Add(name, material);
                    }
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
            }

            var tire = model.transform.Find("AO_tire_main").GetComponent<MeshFilter>();
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                Predicate<Vector3> inWheel = p => (p.x < 2.27f ? 0 : 2) + (p.z < 0f ? 0 : 1) == index;
                Bounds bounds = BoundsFor(tire, model.transform, inWheel);
                Vector3 center = bounds.center;
                float radius = bounds.extents.y;
                Transform wheel = new GameObject("Porsche Wheel " + i).transform;
                wheel.SetParent(car.transform, false);
                wheel.position = model.transform.TransformPoint(center);
                wheel.rotation = car.transform.rotation;
                foreach (string part in new[] { "AO_tire_main", "wheel_rim", "discs" })
                {
                    var filter = model.transform.Find(part).GetComponent<MeshFilter>();
                    Mesh mesh = Extract(filter, model.transform, wheel, inWheel, part + "_" + i);
                    MeshObject(part, wheel, mesh, filter.GetComponent<Renderer>().sharedMaterials);
                }
                car.wheels[i].transform.localPosition = wheel.localPosition + Vector3.up * 0.1f;
                car.wheels[i].radius = radius;
                car.wheelVisuals[i].gameObject.SetActive(false);
                car.wheelVisuals[i] = wheel;
            }
            foreach (string part in new[] { "AO_tire_main", "wheel_rim", "discs" })
                model.transform.Find(part).GetComponent<Renderer>().enabled = false;

            Transform steering = new GameObject("Porsche Steering Wheel").transform;
            steering.SetParent(car.transform, false);
            steering.position = model.transform.TransformPoint(new Vector3(2.055f, 0.632f, -0.342f));
            steering.localRotation = Quaternion.Euler(15f, 0f, 0f);
            var steeringBounds = new Bounds(new Vector3(2.055f, 0.632f, -0.342f), new Vector3(0.2f, 0.39f, 0.43f));
            foreach (string part in new[] { "leather_all", "plast_leather_all", "plastic_all", "logo_all", "chrome_int", "steering_wheel_signs" })
            {
                var filter = model.transform.Find(part).GetComponent<MeshFilter>();
                Mesh moving = Extract(filter, model.transform, steering, steeringBounds.Contains, part + "_steering");
                if (moving.vertexCount == 0) continue;
                MeshObject(part, steering, moving, filter.GetComponent<Renderer>().sharedMaterials);
                filter.sharedMesh = Extract(filter, model.transform, filter.transform, p => !steeringBounds.Contains(p), part + "_fixed");
            }
            car.steeringWheel = steering;
            car.transform.Find("Temporary Body and Cockpit").gameObject.SetActive(false);
            car.name = "Porsche 911 - Player";
            car.GetComponent<BoxCollider>().center = new Vector3(0f, -0.06f, 0f);
            car.GetComponent<BoxCollider>().size = new Vector3(1.83f, 0.76f, 4.43f);

            var camera = UnityEngine.Object.FindAnyObjectByType<RaceCameraRig>();
            camera.cockpitAnchor.position = model.transform.TransformPoint(new Vector3(2.43f, 0.91f, -0.35f));
            camera.cockpitAnchor.localRotation = Quaternion.identity;
            camera.chaseAnchor.localPosition = new Vector3(0f, 2.25f, -6.5f);
            camera.lookTarget.localPosition = new Vector3(0f, 0.28f, 0.8f);
            camera.SnapToTarget();
            var hud = UnityEngine.Object.FindAnyObjectByType<RaceHud>();
            hud.dashboardSpeed = null;

            AddLighting();
            EditorSceneManager.MarkSceneDirty(car.gameObject.scene);
            EditorSceneManager.SaveScene(car.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Porsche installed with separate wheels, steering and URP materials.");
        }

        private static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/textures" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = name.Contains("normal") || name.Contains("_nm");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal && !name.StartsWith("ao_") && !name.Contains("rough") && !name.Contains("opacity");
                importer.maxTextureSize = 2048;
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
        }

        private static Material ConvertMaterial(string name)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", new Color(0.05f, 0.055f, 0.06f));
            material.SetFloat("_Smoothness", 0.3f);
            string color = null;
            string normal = null;
            string rough = null;
            string ao = "AO_" + name + "_1024";
            switch (name)
            {
                case "body_main":
                    material.SetColor("_BaseColor", new Color(0.44f, 0.53f, 0.59f));
                    material.SetFloat("_Metallic", 0.6f); material.SetFloat("_Smoothness", 0.88f); break;
                case "leather_int": color = "leather_Color"; normal = "leather_normal"; rough = "leather_roughness"; break;
                case "leather_seam": color = "leather_Color"; normal = "leather_seam_combo_NM"; ao = "AO_leather_int_1024"; rough = "leather_roughness"; break;
                case "leather_perforated": color = "leather_Color"; normal = "Leather_perfo_normal"; rough = "Leather_perfo_roughness"; break;
                case "carbon_int": color = "Carbon_color"; rough = "Carbon_roughness"; ao = null; break;
                case "rug_interior": color = "rug_color"; normal = "rug_NM"; break;
                case "upholstery": color = "upholstery_color"; normal = "upholstery_NM"; break;
                case "dynamics": color = "dynamics_color"; normal = "dynamic_NM"; rough = "dynamic_roughness"; ao = null; break;
                case "Discs": color = "Discs_color"; rough = "Discs_rough"; ao = null; material.SetFloat("_Metallic", 0.85f); break;
                case "belts": normal = "belts_normal"; rough = "belts_roughness"; break;
                case "tires": normal = "tire_NM_all"; material.SetFloat("_Smoothness", 0.08f); break;
                case "LOGO1": color = "logo_colors_bright"; normal = "logo_NM"; material.SetFloat("_Metallic", 0.55f); break;
                case "number_plate1": color = "number_plate_logo"; ao = null; break;
                case "reflectors": color = "reflectors_color"; normal = "reflectors_NM"; ao = null; break;
                case "rim_chrome": case "chrom_int": case "pipes_chrom": case "Mirror":
                    material.SetColor("_BaseColor", new Color(0.56f, 0.61f, 0.64f));
                    material.SetFloat("_Metallic", 1f); material.SetFloat("_Smoothness", 0.88f); break;
                case "rim_black": case "black_metal":
                    material.SetColor("_BaseColor", new Color(0.07f, 0.075f, 0.08f));
                    material.SetFloat("_Metallic", 0.8f); material.SetFloat("_Smoothness", 0.65f); break;
                case "brakes": material.SetColor("_BaseColor", new Color(0.7f, 0.022f, 0.012f)); material.SetFloat("_Metallic", 0.35f); break;
                case "bl_pl__GL_int_ext": material.SetFloat("_Smoothness", 0.85f); ao = null; break;
                case "headlights_pattern": normal = "headlights_pattern_NM"; material.SetColor("_BaseColor", Color.white); ao = null; material.SetFloat("_Metallic", 0.6f); break;
                case "headlights_plastic_ring": normal = "headlight_plastic_ring_NM"; ao = null; break;
                case "hedlights_grid": normal = "headlight_grid_NM"; ao = null; break;
                case "monitor": material.SetColor("_BaseColor", new Color(0.009f, 0.013f, 0.018f)); material.SetFloat("_Smoothness", 0.4f); ao = null; break;
                case "red_light_main":
                    material.SetColor("_BaseColor", new Color(0.55f, 0.006f, 0.004f));
                    material.SetColor("_EmissionColor", new Color(0.4f, 0.003f, 0.002f)); material.EnableKeyword("_EMISSION"); ao = null; break;
                case "lights": Transparent(material, new Color(0.85f, 0.9f, 0.95f, 0.07f)); ao = null; break;
                case "windows": Transparent(material, new Color(0.38f, 0.52f, 0.6f, 0.08f)); ao = null; break;
                case "windows_edge": Transparent(material, new Color(0.016f, 0.02f, 0.026f, 0.75f)); ao = null; break;
                case "windows_dots":
                    material.SetTexture("_BaseMap", WindowDots());
                    material.SetFloat("_AlphaClip", 1f); material.SetFloat("_Cutoff", 0.5f);
                    material.EnableKeyword("_ALPHATEST_ON"); material.renderQueue = 2450; ao = null; break;
            }
            if (color != null)
            {
                material.SetTexture("_BaseMap", Texture(color));
                material.SetColor("_BaseColor", Color.white);
            }
            if (normal != null)
            {
                material.SetTexture("_BumpMap", Texture(normal));
                material.SetFloat("_BumpScale", 0.45f);
                material.EnableKeyword("_NORMALMAP");
            }
            if (ao != null && Texture(ao) != null)
            {
                material.SetTexture("_OcclusionMap", Texture(ao));
                material.SetFloat("_OcclusionStrength", 0.65f);
                material.EnableKeyword("_OCCLUSIONMAP");
            }
            if (rough != null)
            {
                material.SetTexture("_MetallicGlossMap", RoughnessMap(rough, material.GetFloat("_Metallic")));
                material.SetFloat("_Smoothness", 0.8f);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            AssetDatabase.CreateAsset(material, Root + "/Materials/" + name + ".mat");
            return material;
        }

        private static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/textures/" + name + ".jpeg");

        private static Texture2D ReadTexture(string name)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            texture.LoadImage(System.IO.File.ReadAllBytes(Root + "/textures/" + name + ".jpeg"));
            return texture;
        }

        private static Texture2D RoughnessMap(string name, float metal)
        {
            string path = Root + "/Derived/" + name + "_URP.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            var texture = ReadTexture(name);
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(metal, 0f, 0f, 1f - pixels[i].r);
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            texture.name = name + "_URP";
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static Texture2D WindowDots()
        {
            var texture = ReadTexture("windows_dots_opacity");
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1f, 1f, 1f, 1f - pixels[i].r);
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            AssetDatabase.CreateAsset(texture, Root + "/Derived/WindowDots.asset");
            return texture;
        }

        private static void Transparent(Material material, Color color)
        {
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Smoothness", 0.92f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = 3000;
        }

        private static Bounds BoundsFor(MeshFilter filter, Transform model, Predicate<Vector3> condition)
        {
            bool first = true;
            Bounds bounds = default;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 p = model.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                if (!condition(p)) continue;
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                else bounds.Encapsulate(p);
            }
            return bounds;
        }

        // Split the source's merged parts in the editor, preserving their UVs, submeshes and authored normals.
        private static Mesh Extract(MeshFilter filter, Transform model, Transform destination, Predicate<Vector3> condition, string name)
        {
            Mesh source = filter.sharedMesh;
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uv = source.uv;
            Vector4[] tangents = source.tangents;
            Matrix4x4 matrix = destination.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            var remap = new Dictionary<int, int>();
            var output = new List<Vector3>();
            var outputNormals = new List<Vector3>();
            var outputUv = new List<Vector2>();
            var outputTangents = new List<Vector4>();
            var submeshes = new List<int>[source.subMeshCount];
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                submeshes[sub] = new List<int>();
                int[] indices = source.GetTriangles(sub);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 center = (vertices[indices[i]] + vertices[indices[i + 1]] + vertices[indices[i + 2]]) / 3f;
                    center = model.InverseTransformPoint(filter.transform.TransformPoint(center));
                    if (!condition(center)) continue;
                    for (int j = 0; j < 3; j++)
                    {
                        int original = indices[i + j];
                        if (!remap.TryGetValue(original, out int mapped))
                        {
                            mapped = output.Count;
                            remap.Add(original, mapped);
                            output.Add(matrix.MultiplyPoint3x4(vertices[original]));
                            if (normals.Length == vertices.Length) outputNormals.Add(normalMatrix.MultiplyVector(normals[original]).normalized);
                            if (uv.Length == vertices.Length) outputUv.Add(uv[original]);
                            if (tangents.Length == vertices.Length)
                            {
                                Vector3 tangent = matrix.MultiplyVector(tangents[original]).normalized;
                                outputTangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, tangents[original].w));
                            }
                        }
                        submeshes[sub].Add(mapped);
                    }
                }
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, subMeshCount = source.subMeshCount };
            mesh.SetVertices(output);
            mesh.SetNormals(outputNormals);
            mesh.SetUVs(0, outputUv);
            mesh.SetTangents(outputTangents);
            for (int sub = 0; sub < submeshes.Length; sub++) mesh.SetTriangles(submeshes[sub], sub);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Root + "/Derived/" + name + ".asset");
            return mesh;
        }

        private static void MeshObject(string name, Transform parent, Mesh mesh, Material[] materials)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterials = materials;
        }

        private static void AddLighting()
        {
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_AtmosphereThickness", 0.8f);
            sky.SetColor("_SkyTint", new Color(0.48f, 0.53f, 0.57f));
            sky.SetColor("_GroundColor", new Color(0.2f, 0.24f, 0.18f));
            sky.SetFloat("_Exposure", 1.2f);
            AssetDatabase.CreateAsset(sky, Root + "/Materials/CircuitSky.mat");
            RenderSettings.skybox = sky;
            Camera.main.clearFlags = CameraClearFlags.Skybox;
            var probe = new GameObject("Vehicle Reflection").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(85f, 2f, 0f);
            probe.size = new Vector3(400f, 100f, 400f);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.resolution = 128;
            probe.cullingMask = ~0;
            var volume = new GameObject("Circuit Color Grade").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            profile.Add<ColorAdjustments>().contrast.Override(8f);
            AssetDatabase.CreateAsset(profile, Root + "/Materials/CircuitGrade.asset");
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            volume.sharedProfile = profile;
            DynamicGI.UpdateEnvironment();
        }

        [MenuItem("Racing/Finish Circuit Scene")]
        public static void FinishScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var car = UnityEngine.Object.FindAnyObjectByType<RaceCarController>();
            if (car == null || car.transform.Find("Porsche911") == null) throw new InvalidOperationException("Install the Porsche first.");
            if (car.GetComponent<TimeTrial>() == null) car.gameObject.AddComponent<TimeTrial>();
            var rig = UnityEngine.Object.FindAnyObjectByType<RaceCameraRig>();
            rig.GetComponent<Camera>().nearClipPlane = 0.035f;
            rig.SnapToTarget();

            if (car.transform.Find("Cabin Fill") == null)
            {
                var fill = new GameObject("Cabin Fill").AddComponent<Light>();
                fill.transform.SetParent(car.transform, false);
                fill.transform.localPosition = new Vector3(-0.1f, 0.36f, -0.2f);
                fill.type = LightType.Point;
                fill.range = 2.5f;
                fill.intensity = 1.7f;
                fill.color = new Color(0.85f, 0.91f, 1f);
                fill.shadows = LightShadows.None;
            }
            if (car.transform.Find("Dashboard Speed") == null)
            {
                var display = new GameObject("Dashboard Speed").AddComponent<TextMesh>();
                display.transform.SetParent(car.transform, false);
                display.transform.localPosition = new Vector3(-0.342f, 0.158f, 0.295f);
                display.text = "000"; display.fontSize = 64; display.characterSize = 0.0055f;
                display.anchor = TextAnchor.MiddleCenter;
                display.color = new Color(0.8f, 1f, 0.93f);
                UnityEngine.Object.FindAnyObjectByType<RaceHud>().dashboardSpeed = display;
            }
            foreach (var text in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                var renderer = text.GetComponent<MeshRenderer>();
                if (renderer.sharedMaterial.shader.name == "Circuit/WorldText") continue;
                var material = new Material(Shader.Find("Circuit/WorldText"));
                material.mainTexture = renderer.sharedMaterial.mainTexture;
                AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath("Assets/Racing/Materials/WorldText.mat"));
                renderer.sharedMaterial = material;
            }

            const string art = "Assets/Racing/Art/PolyHaven";
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(art + "/asphalt_normal.jpg");
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.anisoLevel = 8;
            normalImporter.SaveAndReimport();
            var asphalt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Racing/Materials/Asphalt.mat");
            asphalt.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(art + "/asphalt_diff.jpg"));
            asphalt.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(art + "/asphalt_normal.jpg"));
            asphalt.SetColor("_BaseColor", new Color(0.7f, 0.7f, 0.7f));
            asphalt.SetFloat("_BumpScale", 0.45f);
            asphalt.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(asphalt);
            var road = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Racing/Meshes/Track.asset");
            var uv = new Vector2[road.vertexCount];
            var vertices = road.vertices;
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(vertices[i].x / 5f, vertices[i].z / 5f);
            road.uv = uv;
            road.RecalculateTangents();
            EditorUtility.SetDirty(road);

            if (GameObject.Find("Circuit Landscape") == null)
            {
                var data = new TerrainData { heightmapResolution = 257, size = new Vector3(512f, 90f, 512f) };
                var heights = new float[257, 257];
                for (int z = 0; z < 257; z++)
                    for (int x = 0; x < 257; x++)
                    {
                        float wx = x * 2f - 256f;
                        float wz = z * 2f - 256f;
                        float radius = Mathf.Sqrt(wx * wx / (85f * 85f) + wz * wz / (60f * 60f));
                        float slope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radius - 1.32f) / 1.5f));
                        heights[z, x] = slope * (4f + 20f * Mathf.PerlinNoise(wx * 0.019f + 30f, wz * 0.019f + 30f)
                            + 50f * Mathf.PerlinNoise(wx * 0.006f + 50f, wz * 0.006f + 50f)) / 90f;
                    }
                data.SetHeights(0, 0, heights);
                var layer = new TerrainLayer
                {
                    diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(art + "/grass_diff.jpg"),
                    tileSize = new Vector2(12f, 12f)
                };
                AssetDatabase.CreateAsset(layer, art + "/CircuitGrass.terrainlayer");
                data.terrainLayers = new[] { layer };
                AssetDatabase.CreateAsset(data, art + "/CircuitTerrain.asset");
                var terrain = Terrain.CreateTerrainGameObject(data);
                terrain.name = "Circuit Landscape";
                terrain.transform.position = new Vector3(-256f, -0.075f, -256f);
                terrain.GetComponent<Terrain>().heightmapPixelError = 4f;
                terrain.GetComponent<Terrain>().basemapDistance = 300f;
                var environment = GameObject.Find("Practice Circuit").transform;
                foreach (Transform child in environment)
                {
                    if (child.name == "Ground" || child.name == "Pine" || child.name == "Pine trunk" || child.name == "Ridge")
                        child.gameObject.SetActive(false);
                }
            }
            RenderSettings.fogColor = new Color(0.61f, 0.73f, 0.83f);
            RenderSettings.fogDensity = 0.002f;
            EditorSceneManager.MarkSceneDirty(car.gameObject.scene);
            EditorSceneManager.SaveScene(car.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
    }
}
