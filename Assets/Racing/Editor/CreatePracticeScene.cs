using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CircuitRacing.Editor
{
    public static class CreatePracticeScene
    {
        private const string Root = "Assets/Racing";
        public const string ScenePath = Root + "/Scenes/DualViewPractice.unity";

        [MenuItem("Racing/Create Dual View Practice")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before creating the scene.");
            if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("The practice scene already exists. Open it from Assets/Racing/Scenes.");
            Folder("Materials");
            Folder("Meshes");
            Folder("Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var white = Material("Marking White", new Color(0.9f, 0.93f, 0.94f), 0f, 0.2f);
            var red = Material("Signal Red", new Color(0.72f, 0.035f, 0.025f), 0.15f, 0.55f);
            var dark = Material("Carbon", new Color(0.021f, 0.026f, 0.03f), 0.1f, 0.25f);
            var metal = Material("Alloy", new Color(0.45f, 0.5f, 0.54f), 0.8f, 0.65f);
            var rubber = Material("Rubber", new Color(0.028f, 0.03f, 0.032f), 0f, 0.1f);
            var grass = Material("Grass", new Color(0.18f, 0.28f, 0.1f), 0f, 0.1f);
            var asphalt = Material("Asphalt", new Color(0.17f, 0.18f, 0.18f), 0f, 0.18f);
            var gravel = Material("Runoff", new Color(0.38f, 0.41f, 0.36f), 0f, 0.08f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.68f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.49f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.23f, 0.28f, 0.19f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.7f, 0.82f, 0.9f);
            RenderSettings.fogDensity = 0.0015f;
            var sunlight = new GameObject("Sun").AddComponent<Light>();
            sunlight.type = LightType.Directional;
            sunlight.intensity = 2.2f;
            sunlight.color = new Color(1f, 0.93f, 0.81f);
            sunlight.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            sunlight.shadows = LightShadows.Soft;
            RenderSettings.sun = sunlight;
            QualitySettings.shadowDistance = 150f;

            var environment = new GameObject("Practice Circuit").transform;
            Box("Ground", environment, new Vector3(0f, -0.3f, 0f), new Vector3(800f, 0.5f, 800f), grass, true);
            Ring("Runoff", environment, -11f, 11f, 0f, gravel, gravel, false);
            Ring("Track", environment, -7f, 7f, 0.025f, asphalt, asphalt, true);
            Ring("Inner curb", environment, -8.1f, -7f, 0.03f, red, white, false);
            Ring("Outer curb", environment, 7f, 8.1f, 0.03f, red, white, false);
            Ring("Inner edge", environment, -6.85f, -6.7f, 0.035f, white, white, false);
            Ring("Outer edge", environment, 6.7f, 6.85f, 0.035f, white, white, false);

            for (int i = 0; i < 96; i++)
            {
                float a = i * Mathf.PI * 2f / 96f;
                var barrier = Box("Outer barrier", environment, Point(a, 12f) + Vector3.up * 0.5f,
                    new Vector3(0.35f, 1f, 4.8f), i % 4 < 2 ? white : red, true);
                barrier.rotation = Quaternion.LookRotation(Point(a + 0.01f, 12f) - Point(a, 12f));
                if (i % 2 == 0)
                {
                    var line = Box("Centre marking", environment, Point(a, 0f) + Vector3.up * 0.04f,
                        new Vector3(0.12f, 0.012f, 2.4f), white);
                    line.rotation = Quaternion.LookRotation(Point(a + 0.01f, 0f) - Point(a, 0f));
                }
            }
            for (int x = 0; x < 14; x++)
                for (int z = 0; z < 2; z++)
                    Box("Start finish marking", environment, new Vector3(78.5f + x, 0.05f, 8f + z),
                        new Vector3(1f, 0.02f, 1f), (x + z) % 2 == 0 ? white : dark);
            Box("Gantry left", environment, new Vector3(75.5f, 3f, 8.5f), new Vector3(0.55f, 6f, 0.55f), metal, true);
            Box("Gantry right", environment, new Vector3(94.5f, 3f, 8.5f), new Vector3(0.55f, 6f, 0.55f), metal, true);
            Box("Gantry banner", environment, new Vector3(85f, 5.7f, 8.5f), new Vector3(19.5f, 1.1f, 0.5f), dark, true);
            Label("Circuit Sign", environment, new Vector3(85f, 5.7f, 8.22f), "C I R C U I T   /   0 1", 0.12f, white.color);

            var foliage = Material("Pine foliage", new Color(0.075f, 0.2f, 0.13f), 0f, 0.15f);
            var trunk = Material("Tree trunk", new Color(0.2f, 0.16f, 0.13f), 0f, 0f);
            var stone = Material("Distant ridge", new Color(0.26f, 0.34f, 0.34f), 0f, 0.1f);
            var random = new System.Random(31);
            for (int i = 0; i < 90; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2f;
                float extra = 20f + (float)random.NextDouble() * 50f;
                Vector3 p = Point(a, extra);
                float height = 4f + (float)random.NextDouble() * 6f;
                Box("Pine trunk", environment, p + Vector3.up * height * 0.25f,
                    new Vector3(0.45f, height * 0.5f, 0.45f), trunk);
                Cone("Pine", environment, p + Vector3.up * 1.6f, height * 0.3f, height, foliage);
            }
            for (int i = 0; i < 22; i++)
            {
                float a = i * Mathf.PI * 2f / 22f;
                Cone("Ridge", environment, Point(a, 140f), 38f + (float)random.NextDouble() * 30f,
                    25f + (float)random.NextDouble() * 65f, stone);
            }

            RaceCarController car = Car(red, dark, metal, rubber, white, out Transform cockpit,
                out Transform chase, out Transform target, out TextMesh speedDisplay);
            var cameraObject = new GameObject("Race Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.65f, 0.8f, 0.91f);
            camera.farClipPlane = 700f;
            var pipelineCamera = camera.GetUniversalAdditionalCameraData();
            pipelineCamera.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            pipelineCamera.renderPostProcessing = true;
            var rig = cameraObject.AddComponent<RaceCameraRig>();
            rig.car = car;
            rig.cockpitAnchor = cockpit;
            rig.chaseAnchor = chase;
            rig.lookTarget = target;
            rig.SnapToTarget();
            var hud = new GameObject("Race HUD").AddComponent<RaceHud>();
            hud.car = car;
            hud.cameraRig = rig;
            hud.dashboardSpeed = speedDisplay;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = car.gameObject;
            Debug.Log("Dual-view practice scene created: " + ScenePath);
        }

        private static RaceCarController Car(Material paint, Material carbon, Material metal, Material rubber,
            Material white, out Transform cockpit, out Transform chase, out Transform target, out TextMesh speedDisplay)
        {
            var root = new GameObject("Player Car - Temporary Model");
            root.transform.position = new Vector3(85f, 0.85f, -6f);
            var body = root.AddComponent<Rigidbody>();
            body.mass = 1350f;
            body.linearDamping = 0.04f;
            body.angularDamping = 0.65f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var hull = root.AddComponent<BoxCollider>();
            hull.center = new Vector3(0f, -0.02f, 0f);
            hull.size = new Vector3(1.75f, 0.5f, 4.1f);
            var car = root.AddComponent<RaceCarController>();
            var visual = new GameObject("Temporary Body and Cockpit").transform;
            visual.SetParent(root.transform, false);
            Box("Floor", visual, new Vector3(0f, -0.15f, 0f), new Vector3(1.83f, 0.25f, 4.1f), carbon);
            Box("Bonnet", visual, new Vector3(0f, 0.08f, 1.35f), new Vector3(1.82f, 0.28f, 1.42f), paint);
            Box("Rear deck", visual, new Vector3(0f, 0.11f, -1.4f), new Vector3(1.86f, 0.4f, 1.3f), paint);
            Box("Left sill", visual, new Vector3(-0.9f, 0.05f, -0.02f), new Vector3(0.15f, 0.5f, 1.9f), paint);
            Box("Right sill", visual, new Vector3(0.9f, 0.05f, -0.02f), new Vector3(0.15f, 0.5f, 1.9f), paint);
            Box("Front splitter", visual, new Vector3(0f, -0.13f, 2.05f), new Vector3(1.98f, 0.07f, 0.23f), carbon);
            Box("Bonnet stripe", visual, new Vector3(0f, 0.225f, 1.35f), new Vector3(0.4f, 0.01f, 1.42f), white);
            Box("Rear stripe", visual, new Vector3(0f, 0.315f, -1.4f), new Vector3(0.4f, 0.01f, 1.3f), white);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Headlight", visual, new Vector3(side * 0.64f, 0.07f, 2.07f), new Vector3(0.43f, 0.09f, 0.04f), white);
                Box("Wing support", visual, new Vector3(side * 0.62f, 0.44f, -1.83f), new Vector3(0.07f, 0.7f, 0.12f), carbon);
                Box("Seat cushion", visual, new Vector3(side * 0.44f, 0.03f, -0.3f), new Vector3(0.54f, 0.13f, 0.68f), carbon);
                var seat = Box("Bucket seat", visual, new Vector3(side * 0.44f, 0.38f, -0.63f), new Vector3(0.54f, 0.83f, 0.16f), carbon);
                seat.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                Box("Mirror", visual, new Vector3(side * 1.01f, 0.5f, 0.45f), new Vector3(0.25f, 0.13f, 0.22f), paint);
                Box("Mirror glass", visual, new Vector3(side * 1.01f, 0.5f, 0.333f), new Vector3(0.21f, 0.09f, 0.014f), metal);
            }
            Box("Rear wing", visual, new Vector3(0f, 0.83f, -1.83f), new Vector3(2.12f, 0.07f, 0.44f), carbon);
            Box("Dashboard", visual, new Vector3(0f, 0.36f, 0.58f), new Vector3(1.7f, 0.23f, 0.28f), carbon);
            Box("Instrument display", visual, new Vector3(-0.43f, 0.41f, 0.42f), new Vector3(0.44f, 0.19f, 0.035f), rubber);
            speedDisplay = Label("Digital speed", visual, new Vector3(-0.43f, 0.425f, 0.393f), "000", 0.027f, new Color(0.6f, 1f, 0.84f));
            var wheel = new GameObject("Steering Wheel").transform;
            wheel.SetParent(visual, false);
            wheel.localPosition = new Vector3(-0.43f, 0.34f, 0.1f);
            wheel.localRotation = Quaternion.Euler(13f, 0f, 0f);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                var grip = Box("Grip", wheel, new Vector3(Mathf.Cos(a) * 0.195f, Mathf.Sin(a) * 0.195f, 0f),
                    new Vector3(0.06f, 0.055f, 0.042f), carbon);
                grip.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            }
            Box("Steering spokes", wheel, Vector3.zero, new Vector3(0.34f, 0.045f, 0.028f), metal);
            Box("Steering hub", wheel, Vector3.zero, new Vector3(0.085f, 0.085f, 0.048f), paint);
            car.steeringWheel = wheel;
            car.wheels = new WheelCollider[4];
            car.wheelVisuals = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(i % 2 == 0 ? -0.88f : 0.88f, -0.29f, i < 2 ? 1.29f : -1.28f);
                var colliderObject = new GameObject("Wheel Collider " + i);
                colliderObject.transform.SetParent(root.transform, false);
                colliderObject.transform.localPosition = p;
                var collider = colliderObject.AddComponent<WheelCollider>();
                collider.radius = 0.34f;
                collider.mass = 25f;
                collider.suspensionDistance = 0.2f;
                collider.suspensionSpring = new JointSpring { spring = 38000f, damper = 4800f, targetPosition = 0.5f };
                collider.forwardFriction = new WheelFrictionCurve { extremumSlip = 0.35f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.65f, stiffness = 1.35f };
                collider.sidewaysFriction = new WheelFrictionCurve { extremumSlip = 0.2f, extremumValue = 1f, asymptoteSlip = 0.5f, asymptoteValue = 0.7f, stiffness = 1.5f };
                collider.ConfigureVehicleSubsteps(5f, 12, 15);
                car.wheels[i] = collider;
                var tireRoot = new GameObject("Wheel Visual " + i).transform;
                tireRoot.SetParent(root.transform, false);
                tireRoot.localPosition = p - Vector3.up * 0.1f;
                Cylinder("Tire", tireRoot, Vector3.zero, new Vector3(0.68f, 0.14f, 0.68f), rubber);
                Cylinder("Rim", tireRoot, new Vector3(i % 2 == 0 ? -0.145f : 0.145f, 0f, 0f), new Vector3(0.43f, 0.012f, 0.43f), metal);
                car.wheelVisuals[i] = tireRoot;
            }
            cockpit = Anchor("Cockpit Camera Anchor", root.transform, new Vector3(-0.43f, 0.77f, -0.4f));
            cockpit.localRotation = Quaternion.Euler(5f, 0f, 0f);
            chase = Anchor("Chase Camera Anchor", root.transform, new Vector3(0f, 2.65f, -6.2f));
            target = Anchor("Camera Look Target", root.transform, new Vector3(0f, 0.48f, 1.1f));
            return car;
        }

        private static void Folder(string name)
        {
            if (!AssetDatabase.IsValidFolder(Root + "/" + name)) AssetDatabase.CreateFolder(Root, name);
        }

        private static Material Material(string name, Color color, float metallic, float smoothness)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform Anchor(string name, Transform parent, Vector3 position)
        {
            var item = new GameObject(name).transform;
            item.SetParent(parent, false);
            item.localPosition = position;
            return item;
        }

        private static Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = false)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            return item.transform;
        }

        private static void Cylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
        }

        private static TextMesh Label(string name, Transform parent, Vector3 position, string text, float size, Color color)
        {
            var item = Anchor(name, parent, position);
            var label = item.gameObject.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 64;
            label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            return label;
        }

        private static Vector3 Point(float angle, float offset)
        {
            var center = new Vector3(85f * Mathf.Cos(angle), 0f, 60f * Mathf.Sin(angle));
            return center + new Vector3(Mathf.Cos(angle) / 85f, 0f, Mathf.Sin(angle) / 60f).normalized * offset;
        }

        private static void Ring(string name, Transform parent, float inner, float outer, float height,
            Material materialA, Material materialB, bool collision)
        {
            const int segments = 240;
            var vertices = new Vector3[(segments + 1) * 2];
            var triangles = new[] { new List<int>(), new List<int>() };
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i * 2] = Point(a, inner) + Vector3.up * height;
                vertices[i * 2 + 1] = Point(a, outer) + Vector3.up * height;
                if (i == segments) continue;
                var indices = triangles[(i / 3) % 2];
                int v = i * 2;
                indices.AddRange(new[] { v, v + 2, v + 1, v + 1, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = name, vertices = vertices, subMeshCount = 2 };
            mesh.SetTriangles(triangles[0], 0);
            mesh.SetTriangles(triangles[1], 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, Root + "/Meshes/" + name + ".asset");
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterials = new[] { materialA, materialB };
            if (collision) item.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private static void Cone(string name, Transform parent, Vector3 position, float radius, float height, Material material)
        {
            const int sides = 9;
            var vertices = new Vector3[sides * 3];
            var triangles = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float b = (i + 1) * Mathf.PI * 2f / sides;
                vertices[i * 3] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                vertices[i * 3 + 1] = Vector3.up * height;
                vertices[i * 3 + 2] = new Vector3(Mathf.Cos(b) * radius, 0f, Mathf.Sin(b) * radius);
                triangles[i * 3] = i * 3;
                triangles[i * 3 + 1] = i * 3 + 1;
                triangles[i * 3 + 2] = i * 3 + 2;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Root + "/Meshes/" + name + ".asset"));
            var item = Anchor(name, parent, position);
            item.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            item.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
