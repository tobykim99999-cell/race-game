using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CircuitRacing.Editor
{
    public static class CreateLongRoadScenes
    {
        public const string Root = "Assets/Racing/LongRoad";
        private const string Art = "Assets/Racing/Art/LongRoad/";
        public static readonly string[] Names = { "Green Plains", "Pine Forest", "Alpine Pass", "Red Desert" };
        public static string ScenePath(int index) => "Assets/Racing/Scenes/LongRoad" + (RoadRegion)index + ".unity";
        private static Material asphalt, white, yellow, metal, dark, gravel, grass, soil, sand, snow, rock, bark, leaves, jet;
        private static Material[] pickupMaterials;
        private static Mesh treeTrunk, treeLeaves, distantLeaves, rockMesh;
        private static GameObject scannedFir;
        private static GameObject vehicle;
        private static RoadRegionDefinition[] definitions;
        private static readonly float[] TerrainOffsets = { -1200, -900, -650, -450, -320, -230, -165, -120, -85, -60, -42, -30, -22, -16, -10, 0, 10, 16, 22, 30, 42, 60, 85, 120, 165, 230, 320, 450, 650, 900, 1200 };

        public static void RunBatch()
        {
            try { Generate(); EditorApplication.Exit(0); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        [MenuItem("Racing/Generate Long Road Regions")]
        public static void Generate()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play mode and save the active scene before generating regions.");
            foreach (string folder in new[] { Root, Root + "/Materials", Root + "/Meshes", Root + "/Regions", Root + "/Navigation", Root + "/Previews" })
                if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
            Materials();
            Trees();
            VehiclePrefab();
            definitions = new RoadRegionDefinition[4];
            for (int i = 0; i < 4; i++)
            {
                string path = Root + "/Regions/" + (RoadRegion)i + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<RoadRegionDefinition>(path);
                if (definition == null) { definition = ScriptableObject.CreateInstance<RoadRegionDefinition>(); AssetDatabase.CreateAsset(definition, path); }
                definition.region = (RoadRegion)i;
                definition.displayName = Names[i];
                definition.sceneName = Path.GetFileNameWithoutExtension(ScenePath(i));
                definitions[i] = definition;
            }
            for (int i = 0; i < 4; i++) GenerateRegion(i);
            var scenes = Enumerable.Range(0, 4).Select(i => new EditorBuildSettingsScene(ScenePath(i), true)).ToList();
            scenes.Add(new EditorBuildSettingsScene(CreatePracticeScene.ScenePath, true));
            foreach (var existing in EditorBuildSettings.scenes)
                if (!scenes.Any(scene => scene.path == existing.path)) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.OpenScene(ScenePath(0));
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Captures");
            File.WriteAllText("Captures/long-road-generation.json", Newtonsoft.Json.JsonConvert.SerializeObject(definitions.Select(d => new { d.displayName, d.sceneName, d.distanceMetres }), Newtonsoft.Json.Formatting.Indented));
            Debug.Log("LONG_ROAD_GENERATED: four regions, three opponents per region. No player build was run.");
        }

        private static void Materials()
        {
            foreach (string file in Directory.GetFiles(Art).Where(p => p.EndsWith(".jpg") || p.EndsWith(".png")))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                importer.maxTextureSize = 2048;
                importer.anisoLevel = 8;
                if (file.Contains("normal")) importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            asphalt = Surface("Road asphalt", Color.white, "Assets/Racing/Art/PolyHaven/asphalt_diff.jpg", 0.22f);
            asphalt.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Racing/Art/PolyHaven/asphalt_normal.jpg"));
            asphalt.EnableKeyword("_NORMALMAP"); asphalt.SetFloat("_BumpScale", 0.3f);
            grass = Surface("Meadow", new Color(0.76f, 0.9f, 0.65f), "Assets/Racing/Art/PolyHaven/grass_diff.jpg");
            soil = Surface("Forest floor", Color.white, Art + "forest_ground.jpg");
            sand = Surface("Sand", new Color(1f, 0.83f, 0.61f), Art + "sand.jpg");
            snow = Surface("Snow", Color.white, Art + "snow.jpg", 0.2f);
            rock = Surface("Rock", Color.white, Art + "boulder_diff.jpg");
            rock.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "boulder_normal.jpg"));
            rock.EnableKeyword("_NORMALMAP"); rock.SetFloat("_BumpScale", 0.7f);
            bark = Surface("Bark", Color.white, Art + "fir_bark.jpg");
            white = Surface("Road white", new Color(0.93f, 0.94f, 0.85f));
            yellow = Surface("Road amber", new Color(1f, 0.64f, 0.12f));
            metal = Surface("Guardrail", new Color(0.42f, 0.47f, 0.5f), null, 0.55f);
            metal.SetFloat("_Metallic", 0.7f);
            dark = Surface("Signs", new Color(0.025f, 0.045f, 0.055f));
            gravel = Surface("Shoulder gravel", new Color(0.6f, 0.6f, 0.56f), Art + "forest_ground.jpg");
            white.SetColor("_EmissionColor", new Color(0.12f, 0.14f, 0.12f)); white.EnableKeyword("_EMISSION");
            yellow.SetColor("_EmissionColor", new Color(1.3f, 0.65f, 0.07f)); yellow.EnableKeyword("_EMISSION");
            Texture2D diffuse = ReadTexture(Art + "fir_twig.jpg");
            Texture2D alpha = ReadTexture(Art + "fir_alpha.png");
            Color[] pixels = diffuse.GetPixels();
            for (int y = 0; y < diffuse.height; y++)
                for (int x = 0; x < diffuse.width; x++) pixels[y * diffuse.width + x].a = alpha.GetPixelBilinear((x + 0.5f) / diffuse.width, (y + 0.5f) / diffuse.height).r;
            // JPEG loading changes the texture to RGB24; write the mask into an explicit RGBA texture.
            var combined = new Texture2D(diffuse.width, diffuse.height, TextureFormat.RGBA32, false);
            combined.SetPixels(pixels); combined.Apply();
            File.WriteAllBytes(Root + "/FirBranches.png", combined.EncodeToPNG());
            Object.DestroyImmediate(diffuse); Object.DestroyImmediate(alpha); Object.DestroyImmediate(combined);
            AssetDatabase.ImportAsset(Root + "/FirBranches.png");
            var foliageImporter = (TextureImporter)AssetImporter.GetAtPath(Root + "/FirBranches.png");
            foliageImporter.alphaIsTransparency = true; foliageImporter.mipMapsPreserveCoverage = true; foliageImporter.SaveAndReimport();
            leaves = Surface("Fir branches", new Color(0.88f, 1f, 0.87f), Root + "/FirBranches.png");
            leaves.SetFloat("_AlphaClip", 1f); leaves.SetFloat("_Cutoff", 0.25f); leaves.SetFloat("_Cull", 0f);
            leaves.EnableKeyword("_ALPHATEST_ON"); leaves.renderQueue = 2450;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "boulder.fbx");
            var mesh = source.GetComponentsInChildren<MeshFilter>().First(filter => filter.sharedMesh != null);
            rockMesh = mesh.sharedMesh;
            jet = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            jet.name = "Boost jet";
            jet.SetFloat("_Surface", 1f); jet.SetFloat("_Blend", 2f); jet.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); jet.SetFloat("_DstBlend", (float)BlendMode.One);
            jet.SetFloat("_ZWrite", 0); jet.SetFloat("_Cull", 0); jet.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); jet.renderQueue = 3000;
            jet.SetColor("_BaseColor", new Color(0.3f, 1.7f, 3.5f, 1f));
            var glow = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                glow.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - radius), 2f)));
            }
            glow.Apply();
            jet.SetTexture("_BaseMap", Save(glow, Root + "/JetGlow.asset"));
            jet = Save(jet, Root + "/Materials/Boost jet.mat");
            pickupMaterials = new[] { Emissive("Energy", new Color(0.1f, 1f, 0.65f)), Emissive("Turbo", new Color(0.13f, 0.55f, 1f)), Emissive("Grip", new Color(1f, 0.47f, 0.06f)) };
            RealMaterialSetup.ApplyMaterials();
        }

        private static Texture2D ReadTexture(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(path));
            return texture;
        }

        private static Material Surface(string name, Color color, string texture = null, float smoothness = 0.08f)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (texture != null) material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
            return Save(material, Root + "/Materials/" + name + ".mat");
        }

        private static Material Emissive(string name, Color color)
        {
            var material = Surface(name, color, null, 0.65f);
            material.SetColor("_EmissionColor", color * 3f);
            material.EnableKeyword("_EMISSION");
            return material;
        }

        private static T Save<T>(T asset, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void VehiclePrefab()
        {
            EditorSceneManager.OpenScene(CreatePracticeScene.ScenePath);
            var original = Object.FindAnyObjectByType<RaceCarController>();
            var clone = Object.Instantiate(original.gameObject);
            clone.name = "Long Road Porsche";
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var child in clone.GetComponentsInChildren<Transform>(true).Reverse())
                if (child != clone.transform && !child.gameObject.activeSelf) Object.DestroyImmediate(child.gameObject);
            foreach (var trial in clone.GetComponentsInChildren<TimeTrial>()) Object.DestroyImmediate(trial);
            foreach (var hud in clone.GetComponentsInChildren<RaceHud>()) Object.DestroyImmediate(hud);
            foreach (var text in clone.GetComponentsInChildren<TextMesh>()) Object.DestroyImmediate(text.gameObject);
            var abilities = clone.AddComponent<VehicleAbilities>();
            var jets = new List<ParticleSystem>();
            var lights = new List<Light>();
            for (int side = -1; side <= 1; side += 2)
            {
                var exhaust = new GameObject("Boost Exhaust " + side, typeof(ParticleSystem));
                exhaust.transform.SetParent(clone.transform, false);
                exhaust.transform.localPosition = new Vector3(side * 0.52f, -0.38f, -2.26f);
                exhaust.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var particles = exhaust.GetComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.23f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 10f); main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.34f);
                main.startColor = new Color(0.35f, 0.8f, 1f, 0.9f); main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 100;
                var emission = particles.emission; emission.rateOverTime = 110;
                var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 4; shape.radius = 0.055f;
                var size = particles.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
                var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = jet;
                renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 1.5f; renderer.velocityScale = 0.06f;
                var lamp = new GameObject("Jet Light " + side).AddComponent<Light>();
                lamp.transform.SetParent(exhaust.transform, false); lamp.color = new Color(0.1f, 0.55f, 1f); lamp.range = 4f; lamp.intensity = 3f; lamp.enabled = false;
                jets.Add(particles); lights.Add(lamp);
                var headlight = new GameObject("Headlamp " + side).AddComponent<Light>();
                headlight.transform.SetParent(clone.transform, false);
                headlight.transform.localPosition = new Vector3(side * 0.67f, -0.05f, 2.15f);
                headlight.transform.localRotation = Quaternion.Euler(3f, side * 4f, 0f);
                headlight.type = LightType.Spot; headlight.range = 135; headlight.spotAngle = 50; headlight.innerSpotAngle = 23;
                headlight.intensity = 160; headlight.color = new Color(0.85f, 0.93f, 1f); headlight.shadows = LightShadows.None;
            }
            abilities.jets = jets.ToArray(); abilities.jetLights = lights.ToArray();
            clone.AddComponent<RoadRaceProgress>();
            vehicle = PrefabUtility.SaveAsPrefabAsset(clone, Root + "/RoadPorsche.prefab");
            Object.DestroyImmediate(clone);
        }

        private static Vector3 RoutePoint(float z, int region)
        {
            float fade = Mathf.SmoothStep(0, 1, Mathf.Clamp01((z - 120f) / 500f));
            float x, y;
            switch (region)
            {
                case 1: x = 160f * Mathf.Sin(z / 360f) + 65f * Mathf.Sin(z / 170f); y = 28f + 15f * Mathf.Sin(z / 620f); break;
                case 2: x = 250f * Mathf.Sin(z / 370f) + 90f * Mathf.Sin(z / 195f); y = 30f + 110f * Mathf.Pow(Mathf.Sin(z / 5900f * Mathf.PI), 2f) + 12f * Mathf.Sin(z / 410f); break;
                case 3: x = 225f * Mathf.Sin(z / 630f) + 80f * Mathf.Sin(z / 260f); y = 25f + 13f * Mathf.Sin(z / 510f); break;
                default: x = 160f * Mathf.Sin(z / 620f) + 58f * Mathf.Sin(z / 280f); y = 25f + 12f * Mathf.Sin(z / 710f); break;
            }
            return new Vector3(x * fade, Mathf.Lerp(25f, y, fade), z);
        }

        private static void GenerateRegion(int index)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var route = new GameObject("Open Road Route").AddComponent<RoadRoute>();
            float raceRouteLength = new[] { 6200f, 5700f, 5900f, 7000f }[index];
            // Retain every original sample (including a partial final segment).
            // Extend the landscape beyond the camera's 1800 m far plane.
            const float postFinishContinuation = 2400f;
            int racePointCount = Mathf.CeilToInt(raceRouteLength / 8f) + 1;
            int count = racePointCount + Mathf.CeilToInt(postFinishContinuation / 8f);
            route.points = new Vector3[count]; route.distances = new float[count];
            for (int i = 0; i < count; i++)
            {
                float z = i < racePointCount ? Mathf.Min(raceRouteLength, i * 8f)
                    : raceRouteLength + (i - racePointCount + 1) * 8f;
                route.points[i] = RoutePoint(z, index);
                if (i > 0) route.distances[i] = route.distances[i - 1] + Vector3.Distance(route.points[i], route.points[i - 1]);
            }
            route.finishDistance = route.distances[racePointCount - 1] - 100f;
            definitions[index].distanceMetres = route.FinishDistance - 24f;
            EditorUtility.SetDirty(definitions[index]);
            var environment = new GameObject(Names[index] + " Landscape").transform;
            var sources = new List<NavMeshBuildSource>();
            var random = new System.Random(1701 + index);
            for (int first = 0, chunk = 0; first < count - 1; first += 40, chunk++)
            {
                int last = Mathf.Min(count - 1, first + 40);
                var parent = new GameObject("Road Sector " + chunk.ToString("00")).transform;
                parent.SetParent(environment, false);
                Mesh road = Strip(route, first, last, -8, 8, 0, index + "_Road_" + chunk, 4);
                MeshObject("Asphalt", parent, road, new[] { asphalt }, true);
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Mesh, sourceObject = road, transform = Matrix4x4.identity, area = 0 });
                MeshObject("Shoulder", parent, Strip(route, first, last, -12, 12, -0.06f, index + "_Shoulder_" + chunk, 5), new[] { index == 3 ? sand : gravel }, true);
                MeshObject("Left edge", parent, Strip(route, first, last, -7.8f, -7.62f, 0.018f, index + "_EdgeL_" + chunk, 4), new[] { white });
                MeshObject("Right edge", parent, Strip(route, first, last, 7.62f, 7.8f, 0.018f, index + "_EdgeR_" + chunk, 4), new[] { white });
                Terrain(route, first, last, index, chunk, parent);
                Scenery(route, first, last, index, parent, random);
            }
            RoadFurniture(route, environment, index);
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = 1f; settings.agentHeight = 1.6f; settings.agentClimb = 0.3f; settings.agentSlope = 35;
            settings.overrideVoxelSize = true; settings.voxelSize = 0.5f;
            var bounds = new Bounds(route.points[0], Vector3.zero);
            foreach (var point in route.points) bounds.Encapsulate(point);
            bounds.Expand(new Vector3(40, 40, 40));
            var navigation = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (navigation == null) throw new InvalidOperationException("Road navigation baking failed.");
            route.navigation = Save(navigation, Root + "/Navigation/" + (RoadRegion)index + ".asset");
            var director = new GameObject("Road Race").AddComponent<RoadRaceDirector>();
            director.route = route; director.region = definitions[index]; director.regions = definitions;
            director.racers = new RoadRaceProgress[4];
            string[] racerNames = { "YOU", "NOVA", "APEX", "BLAZE" };
            Color[] paint = { new Color(0.38f, 0.51f, 0.58f), new Color(0.8f, 0.055f, 0.035f), new Color(0.04f, 0.31f, 0.6f), new Color(0.9f, 0.64f, 0.04f) };
            for (int i = 0; i < 4; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(vehicle);
                instance.name = "Porsche 911 - " + racerNames[i];
                float lane = (i % 2 == 0 ? -1 : 1) * 2.8f;
                float distance = i < 2 ? 24f : 14f;
                instance.transform.SetPositionAndRotation(route.Sample(distance, lane) + Vector3.up * 0.85f, Quaternion.LookRotation(route.Direction(distance), Vector3.up));
                var car = instance.GetComponent<RaceCarController>(); car.playerControlled = i == 0;
                var progress = instance.GetComponent<RoadRaceProgress>();
                progress.director = director; progress.racerName = racerNames[i]; progress.racerIndex = i; progress.lane = lane;
                director.racers[i] = progress;
                var bodyPaint = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Racing/Art/Porsche911/Materials/body_main.mat"));
                bodyPaint.SetColor("_BaseColor", paint[i]);
                bodyPaint = Save(bodyPaint, Root + "/Materials/Paint " + i + ".mat");
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int m = 0; m < materials.Length; m++) if (materials[m] != null && materials[m].name == "body_main") materials[m] = bodyPaint;
                    renderer.sharedMaterials = materials;
                    if (i > 0 && new[] { "rug", "leather", "belt", "seam", "dynamics", "upholstery", "pedal", "monitor", "steering_wheel_signs" }.Any(part => renderer.name.Contains(part))) renderer.enabled = false;
                }
                if (i > 0)
                {
                    var agent = instance.AddComponent<NavMeshAgent>();
                    agent.enabled = false; agent.radius = 1.15f; agent.height = 1.4f; agent.baseOffset = 0.65f;
                    agent.acceleration = 30f; agent.angularSpeed = 180f; agent.stoppingDistance = 0f;
                    agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                    agent.avoidancePriority = 25 + i * 15;
                    instance.AddComponent<RoadRaceAi>().cruiseSpeed = 164f + i * 10f;
                    var lod = instance.AddComponent<LODGroup>();
                    lod.SetLODs(new[] { new LOD(0.005f, instance.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray()) });
                    lod.RecalculateBounds();
                }
            }
            CameraRig(director);
            director.lighting = Lighting(director, index);
            new GameObject("Road HUD").AddComponent<RoadRaceHud>().director = director;
            EditorSceneManager.SaveScene(scene, ScenePath(index));
            Debug.Log("Generated " + Names[index] + " " + definitions[index].distanceMetres.ToString("0") + " m");
        }

        private static void CameraRig(RoadRaceDirector director)
        {
            var camera = new GameObject("Race Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.clearFlags = CameraClearFlags.Skybox; camera.farClipPlane = 1800;
            camera.allowHDR = true;
            var additional = camera.GetUniversalAdditionalCameraData();
            additional.renderPostProcessing = true; additional.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rig = camera.gameObject.AddComponent<RaceCameraRig>();
            rig.car = director.Player.GetComponent<RaceCarController>();
            rig.cockpitAnchor = rig.car.transform.Find("Cockpit Camera Anchor");
            rig.chaseAnchor = rig.car.transform.Find("Chase Camera Anchor");
            rig.lookTarget = rig.car.transform.Find("Camera Look Target");
            if (rig.cockpitAnchor == null || rig.chaseAnchor == null || rig.lookTarget == null) throw new InvalidOperationException("Missing Porsche camera anchors.");
            director.cameraRig = rig;
            rig.SnapToTarget();
        }

        private static RoadLighting Lighting(RoadRaceDirector director, int index)
        {
            var lighting = new GameObject("Region Lighting").AddComponent<RoadLighting>();
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
            lighting.sun = sun; RenderSettings.sun = sun;
            QualitySettings.shadowDistance = 140f;
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_AtmosphereThickness", 0.9f);
            lighting.sky = Save(sky, Root + "/Materials/Sky " + index + ".mat");
            var probe = new GameObject("Road Reflection").AddComponent<ReflectionProbe>();
            probe.transform.position = director.route.Sample(40) + Vector3.up * 4f;
            probe.mode = ReflectionProbeMode.Realtime; probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.size = new Vector3(3000, 1000, 16000); probe.center = new Vector3(0, 0, 3500);
            probe.resolution = 128; probe.clearFlags = ReflectionProbeClearFlags.Skybox; probe.farClipPlane = 1500;
            lighting.reflection = probe;
            var volume = new GameObject("Road Color").AddComponent<Volume>(); volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tonemapping = profile.Add<Tonemapping>(true); tonemapping.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.65f); bloom.threshold.Override(1f); bloom.scatter.Override(0.65f);
            var grading = profile.Add<ColorAdjustments>(true); grading.postExposure.Override(0.25f); grading.saturation.Override(7f); grading.contrast.Override(8f);
            volume.sharedProfile = Save(profile, Root + "/Regions/Color " + index + ".asset");
            foreach (var component in volume.sharedProfile.components)
                if (!AssetDatabase.Contains(component)) AssetDatabase.AddObjectToAsset(component, volume.sharedProfile);
            lighting.Apply(RoadTime.Day);
            // Keep the asset sky in the serialized scene; the runtime clone belongs to Play mode.
            RenderSettings.skybox = lighting.sky;
            return lighting;
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            private readonly Dictionary<(Vector3, Vector2), int> indices = new Dictionary<(Vector3, Vector2), int>();
            private int Vertex(Vector3 point, Vector2 texture)
            {
                if (indices.TryGetValue((point, texture), out int index)) return index;
                index = vertices.Count;
                vertices.Add(point); uv.Add(texture); indices.Add((point, texture), index);
                return index;
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int ia = Vertex(a, ua), ib = Vertex(b, ub), ic = Vertex(c, uc), id = Vertex(d, ud);
                triangles.AddRange(new[] { ia, ic, ib, ia, id, ic });
            }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                return Save(mesh, Root + "/Meshes/" + name + ".asset");
            }
        }

        private static Mesh Strip(RoadRoute route, int first, int last, float left, float right, float height, string name, float repeat)
        {
            var geometry = new Geometry();
            for (int i = first; i < last; i++)
            {
                float a = route.distances[i], b = route.distances[i + 1];
                geometry.Quad(route.Sample(a, left) + Vector3.up * height, route.Sample(a, right) + Vector3.up * height,
                    route.Sample(b, right) + Vector3.up * height, route.Sample(b, left) + Vector3.up * height,
                    new Vector2(left / repeat, a / repeat), new Vector2(right / repeat, a / repeat), new Vector2(right / repeat, b / repeat), new Vector2(left / repeat, b / repeat));
            }
            return geometry.Mesh(name);
        }

        private static MeshRenderer MeshObject(string name, Transform parent, Mesh mesh, Material[] materials, bool collision = false)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false); item.isStatic = true;
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            if (collision) item.AddComponent<MeshCollider>().sharedMesh = mesh;
            return renderer;
        }

        private static float GroundHeight(Vector3 point, float lane, float roadY, int region)
        {
            float shoulder = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(15f, region == 2 ? 350f : region == 3 ? 160f : 90f, Mathf.Abs(lane)));
            float broad = Mathf.PerlinNoise(point.x * 0.0016f + 33f, point.z * 0.0016f + region * 17f);
            float detail = Mathf.PerlinNoise(point.x * 0.009f + 8f, point.z * 0.009f) - 0.5f;
            float height = region == 2 ? broad * broad * 240f + detail * 18f : region == 3 ? broad * 80f + detail * 7f : broad * 42f + detail * 5f;
            return roadY - 0.17f + shoulder * (height - (region == 2 ? 12f : 10f));
        }

        private static Vector3 Ground(RoadRoute route, float distance, float lane, int region)
        {
            Vector3 point = route.Sample(distance, lane);
            point.y = GroundHeight(point, lane, point.y, region);
            return point;
        }

        private static void Terrain(RoadRoute route, int first, int last, int region, int chunk, Transform parent)
        {
            var low = new Geometry(); var high = new Geometry(); var rocky = new Geometry();
            for (int i = first; i < last; i += 2)
                for (int j = 0; j < TerrainOffsets.Length - 1; j++)
                {
                    float a = route.distances[i], b = route.distances[Mathf.Min(i + 2, last)];
                    Vector3 p = Ground(route, a, TerrainOffsets[j], region), q = Ground(route, a, TerrainOffsets[j + 1], region);
                    Vector3 r = Ground(route, b, TerrainOffsets[j + 1], region), s = Ground(route, b, TerrainOffsets[j], region);
                    float altitude = (p.y + q.y + r.y + s.y) * 0.25f;
                    var geometry = region == 2 && altitude > 220f ? high
                        : region == 2 && altitude > route.Sample(a).y + 24f ? rocky : low;
                    geometry.Quad(p, q, r, s, new Vector2(p.x, p.z) / 12f, new Vector2(q.x, q.z) / 12f, new Vector2(r.x, r.z) / 12f, new Vector2(s.x, s.z) / 12f);
                }
            Material ground = region == 3 ? sand : region == 1 ? soil : grass;
            MeshObject("Terrain", parent, low.Mesh(region + "_Terrain_" + chunk), new[] { ground }, true);
            if (region == 2)
            {
                MeshObject("High snow", parent, high.Mesh(region + "_Snow_" + chunk), new[] { snow }, true);
                MeshObject("Rocky slopes", parent, rocky.Mesh(region + "_RockSlope_" + chunk), new[] { rock }, true);
            }
        }

        private static void Trees()
        {
            var trunk = new Geometry();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4, b = (i + 1) * Mathf.PI / 4;
                trunk.Quad(new Vector3(Mathf.Cos(a) * 0.26f, 0, Mathf.Sin(a) * 0.26f), new Vector3(Mathf.Cos(b) * 0.26f, 0, Mathf.Sin(b) * 0.26f),
                    new Vector3(Mathf.Cos(b) * 0.035f, 12, Mathf.Sin(b) * 0.035f), new Vector3(Mathf.Cos(a) * 0.035f, 12, Mathf.Sin(a) * 0.035f),
                    new Vector2(0, 0), new Vector2(0.4f, 0), new Vector2(0.4f, 5), new Vector2(0, 5));
            }
            treeTrunk = trunk.Mesh("Fir trunk");
            treeLeaves = BranchMesh("Fir foliage", 15, 7);
            distantLeaves = BranchMesh("Fir foliage distant", 8, 5);
            scannedFir = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "fir_tree_01_2k.fbx");
        }

        private static Mesh BranchMesh(string name, int levels, int spokes)
        {
            var geometry = new Geometry();
            for (int level = 0; level < levels; level++)
            {
                float t = level / (float)levels;
                float y = 1.6f + 10f * t;
                float length = 3.8f * (1f - t) + 0.15f;
                for (int spoke = 0; spoke < spokes; spoke++)
                {
                    float angle = spoke * Mathf.PI * 2 / spokes + level * 2.4f;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    float branchLength = length * (0.78f + 0.26f * Mathf.Sin(level * 19.3f + spoke * 13.7f));
                    Vector3 side = Vector3.Cross(radial, Vector3.up) * branchLength * 0.5f;
                    Vector3 root = Vector3.up * y + radial * 0.1f;
                    Vector3 tip = root + radial * branchLength + Vector3.up * (-0.45f + t * 0.9f);
                    geometry.Quad(root - side, root + side, tip + side, tip - side, new Vector2(0.27f, 0.20f), new Vector2(0.67f, 0.20f), new Vector2(0.67f, 0.60f), new Vector2(0.27f, 0.60f));
                    side = (side.normalized + Vector3.up).normalized * branchLength * 0.45f;
                    geometry.Quad(root - side, root + side, tip + side, tip - side, new Vector2(0.27f, 0.20f), new Vector2(0.67f, 0.20f), new Vector2(0.67f, 0.60f), new Vector2(0.27f, 0.60f));
                }
            }
            return geometry.Mesh(name);
        }

        private static void Scenery(RoadRoute route, int first, int last, int region, Transform parent, System.Random random)
        {
            float start = route.distances[first], end = route.distances[last];
            int treeCount = region == 1 ? Mathf.CeilToInt((end - start) * 0.9f) : region == 0 ? 16 : region == 2 ? 26 : 0;
            float Rand(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
            for (int i = 0; i < treeCount; i++)
            {
                float d = region == 1 ? Mathf.Lerp(start, end, (i / 2 + Rand(0.1f, 0.9f)) / Mathf.Ceil(treeCount / 2f)) : Rand(start, end);
                int band = i / 2 % 4;
                float lane = (region == 1 ? band < 2 ? Rand(20f, 38f) : band == 2 ? Rand(40f, 72f) : Rand(75f, 150f)
                    : 19f + Rand(0f, 126f)) * (i % 2 == 0 ? -1 : 1);
                Vector3 point = Ground(route, d, lane, region);
                if (region == 2 && point.y > 160f) continue;
                if (scannedFir != null)
                {
                    var scanned = (GameObject)PrefabUtility.InstantiatePrefab(scannedFir, parent);
                    scanned.name = "Fir (scanned CC0)";
                    var scannedTree = scanned.transform;
                    scannedTree.position = point;
                    scannedTree.rotation = Quaternion.Euler(0, Rand(0, 360), 0);
                    scannedTree.localScale = Vector3.one * Rand(0.78f, 1.18f);
                    var variants = scanned.GetComponentsInChildren<MeshRenderer>(true);
                    var selected = variants[i % variants.Length];
                    foreach (var renderer in variants)
                    {
                        renderer.gameObject.SetActive(renderer == selected);
                        // Each scanned mesh contains both wood and foliage submeshes.
                        // Preserve its slots; mesh names identify variants, not surface types.
                        renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                            material != null && (material.name.IndexOf("twig", StringComparison.OrdinalIgnoreCase) >= 0
                                || material.name.IndexOf("branches", StringComparison.OrdinalIgnoreCase) >= 0)
                                ? leaves : bark).ToArray();
                    }
                    // The FBX lays out three alternative trees side by side. Centre
                    // just the selected tree at its roadside placement.
                    Bounds treeBounds = selected.bounds;
                    scannedTree.position += point - new Vector3(treeBounds.center.x, treeBounds.min.y, treeBounds.center.z);
                    // Keep the entire tree crown outside asphalt and shoulders,
                    // including nearby bends whose centre line may be closer.
                    float radius = new Vector2(treeBounds.extents.x, treeBounds.extents.z).magnitude;
                    if (!TreeClearsRoad(route, point, radius)) { Object.DestroyImmediate(scanned); continue; }
                    var distant = new GameObject("Distant fir").transform;
                    distant.SetParent(scannedTree, false);
                    distant.position = point;
                    distant.localScale = Vector3.one * treeBounds.size.y / (12f * scannedTree.localScale.y);
                    var distantTrunk = MeshObject("Distant trunk", distant, treeTrunk, new[] { bark });
                    var distantNeedles = MeshObject("Distant needles", distant, distantLeaves, new[] { leaves });
                    var scannedLod = scanned.AddComponent<LODGroup>();
                    scannedLod.SetLODs(new[] { new LOD(0.12f, new Renderer[] { selected }),
                        new LOD(0.018f, new Renderer[] { distantTrunk, distantNeedles }) });
                    scannedLod.RecalculateBounds();
                    if (Mathf.Abs(lane) < 45f)
                    {
                        var collider = scanned.AddComponent<CapsuleCollider>(); collider.radius = 0.35f; collider.height = 10;
                        collider.center = scannedTree.InverseTransformPoint(point) + Vector3.up * 5;
                    }
                    continue;
                }
                var tree = new GameObject("Fir").transform; tree.SetParent(parent, false);
                var trunk = MeshObject("Trunk", tree, treeTrunk, new[] { bark });
                var near = MeshObject("Needles", tree, treeLeaves, new[] { leaves });
                var far = MeshObject("Distant needles", tree, distantLeaves, new[] { leaves });
                var lod = tree.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.065f, new Renderer[] { trunk, near }), new LOD(0.018f, new Renderer[] { trunk, far }) });
                lod.RecalculateBounds();
                tree.position = point; tree.rotation = Quaternion.Euler(0, Rand(0, 360), 0); tree.localScale = Vector3.one * Rand(0.72f, 1.8f);
                if (Mathf.Abs(lane) < 45f)
                {
                    var collider = tree.gameObject.AddComponent<CapsuleCollider>(); collider.radius = 0.25f; collider.height = 10; collider.center = Vector3.up * 5;
                }
            }
            int rocks = region == 2 || region == 3 ? 18 : 5;
            for (int i = 0; i < rocks; i++)
            {
                float d = Rand(start, end), lane = Rand(22f, 220f) * (i % 2 == 0 ? -1 : 1);
                var renderer = MeshObject("Boulder", parent, rockMesh, new[] { rock });
                float size = Rand(region >= 2 ? 4f : 1f, region >= 2 ? 22f : 4f);
                renderer.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, rockMesh.bounds.size.y);
                renderer.transform.rotation = Quaternion.Euler(Rand(-15, 15), Rand(0, 360), 0);
                renderer.transform.position = Ground(route, d, lane, region) - Vector3.up * size * 0.15f - renderer.transform.TransformVector(rockMesh.bounds.center - Vector3.up * rockMesh.bounds.extents.y);
                var lod = renderer.gameObject.AddComponent<LODGroup>(); lod.SetLODs(new[] { new LOD(0.012f, new[] { renderer }) }); lod.RecalculateBounds();
                if (Mathf.Abs(lane) < 55) { var collider = renderer.gameObject.AddComponent<BoxCollider>(); collider.center = rockMesh.bounds.center; collider.size = rockMesh.bounds.size * 0.8f; }
            }
        }

        private static bool TreeClearsRoad(RoadRoute route, Vector3 point, float radius)
        {
            float clearance = route.width * 0.5f + 5f + radius;
            Vector2 centre = new Vector2(point.x, point.z);
            for (int i = 0; i < route.points.Length - 1; i++)
            {
                if (point.z < route.points[i].z - clearance || point.z > route.points[i + 1].z + clearance) continue;
                Vector2 a = new Vector2(route.points[i].x, route.points[i].z);
                Vector2 b = new Vector2(route.points[i + 1].x, route.points[i + 1].z);
                Vector2 segment = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(centre - a, segment) / segment.sqrMagnitude);
                if ((centre - a - segment * t).sqrMagnitude < clearance * clearance) return false;
            }
            return true;
        }

        private static Transform Box(string name, Transform parent, Vector3 position, Vector3 size, Material material, Quaternion rotation, bool collision = false)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name; item.isStatic = true;
            item.transform.SetParent(parent, false); item.transform.SetPositionAndRotation(position, rotation); item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(item.GetComponent<Collider>());
            return item.transform;
        }

        private static void RoadFurniture(RoadRoute route, Transform parent, int region)
        {
            for (float d = 6; d < route.Length - 5; d += 16f)
            {
                Quaternion rotation = Quaternion.LookRotation(route.Direction(d), Vector3.up);
                Box("Centre dash", parent, route.Sample(d) + Vector3.up * 0.023f, new Vector3(0.16f, 0.015f, 5), white, rotation);
            }
            for (float d = 25; d < route.Length - 8; d += 32)
            {
                Quaternion rotation = Quaternion.LookRotation(route.Direction(d), Vector3.up);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = route.Sample(d, side * 10.5f);
                    Box("Road delineator", parent, p + Vector3.up * 0.65f, new Vector3(0.17f, 1.3f, 0.18f), white, rotation);
                    Box("Reflector", parent, p + Vector3.up * 1.05f - rotation * Vector3.forward * 0.1f, new Vector3(0.19f, 0.22f, 0.04f), yellow, rotation);
                    if (region == 2 || route.Curvature(d) > 0.004f)
                    {
                        Vector3 a = route.Sample(d - 16, side * 12.3f) + Vector3.up * 0.75f, b = route.Sample(d + 16, side * 12.3f) + Vector3.up * 0.75f;
                        Box("Guardrail", parent, (a + b) * 0.5f, new Vector3(0.22f, 0.42f, Vector3.Distance(a, b) + 0.15f), metal, Quaternion.LookRotation(b - a, Vector3.up), true);
                        Box("Rail post", parent, p + rotation * Vector3.right * side * 1.8f + Vector3.up * 0.4f, new Vector3(0.16f, 0.8f, 0.16f), metal, rotation);
                    }
                }
            }
            for (float d = 450; d < route.FinishDistance - 200; d += 550)
            {
                Quaternion rotation = Quaternion.LookRotation(route.Direction(d), Vector3.up);
                Vector3 p = route.Sample(d, 11.5f);
                Box("Distance sign pole", parent, p + Vector3.up * 1.5f, new Vector3(0.12f, 3, 0.12f), metal, rotation);
                Box("Distance sign", parent, p + Vector3.up * 3, new Vector3(2.8f, 1.1f, 0.15f), dark, rotation);
                WorldText(parent, p + Vector3.up * 3 - rotation * Vector3.forward * 0.09f, rotation, Mathf.CeilToInt((route.FinishDistance - d) / 1000f) + " KM", 0.065f);
            }
            for (float d = 250; d < route.FinishDistance - 100; d += 300)
            {
                int group = Mathf.RoundToInt((d - 250) / 300);
                for (int lane = -1; lane <= 1; lane++)
                {
                    var item = new GameObject("Pickup " + group + " " + lane);
                    item.transform.SetParent(parent, false); item.transform.position = route.Sample(d, lane * 4.5f) + Vector3.up * 0.9f;
                    var collider = item.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(2.7f, 2.2f, 2.7f);
                    var pickup = item.AddComponent<RoadPickup>(); pickup.kind = (RoadPickupKind)((group + lane + 3) % 3);
                    var visual = new GameObject("Pickup Visual").transform; visual.SetParent(item.transform, false); pickup.visual = visual;
                    var core = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(core.GetComponent<Collider>());
                    core.name = pickup.kind.ToString(); core.transform.SetParent(visual, false); core.transform.localScale = Vector3.one * 0.7f; core.transform.localRotation = Quaternion.Euler(45, 0, 45);
                    core.GetComponent<Renderer>().sharedMaterial = pickupMaterials[(int)pickup.kind];
                    var ring = visual.gameObject.AddComponent<LineRenderer>(); ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 32; ring.widthMultiplier = 0.055f; ring.sharedMaterial = pickupMaterials[(int)pickup.kind];
                    for (int i = 0; i < 32; i++) ring.SetPosition(i, new Vector3(Mathf.Cos(i * Mathf.PI / 16) * 0.85f, 0, Mathf.Sin(i * Mathf.PI / 16) * 0.85f));
                }
            }
            Gantry(route, 40f, parent, "START", false);
            Gantry(route, route.FinishDistance, parent, "FINISH", true);
        }

        private static void Gantry(RoadRoute route, float distance, Transform parent, string title, bool checker)
        {
            Quaternion rotation = Quaternion.LookRotation(route.Direction(distance), Vector3.up);
            for (int side = -1; side <= 1; side += 2)
                Box(title + " pylon", parent, route.Sample(distance, side * 9.8f) + Vector3.up * 3f, new Vector3(0.6f, 6f, 0.6f), metal, rotation, true);
            Vector3 banner = route.Sample(distance) + Vector3.up * 5.6f;
            Box(title + " banner", parent, banner, new Vector3(20, 1.25f, 0.5f), dark, rotation);
            WorldText(parent, banner - rotation * Vector3.forward * 0.27f, rotation, title, 0.1f);
            for (int lane = 0; lane < 16; lane++)
                for (int row = 0; row < 2; row++)
                    Box(title + " line", parent, route.Sample(distance + row, lane - 7.5f) + Vector3.up * 0.025f, new Vector3(1, 0.02f, 1), checker && (lane + row) % 2 == 0 ? dark : white, rotation);
        }

        private static void WorldText(Transform parent, Vector3 position, Quaternion rotation, string text, float size)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.transform.SetParent(parent, false); label.transform.SetPositionAndRotation(position, rotation);
            label.text = text; label.fontSize = 64; label.characterSize = size; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = Color.white;
        }
    }
}
