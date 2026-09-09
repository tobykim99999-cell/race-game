using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CircuitRacing.Editor
{
    public static class RealMaterialSetup
    {
        private const string Art = "Assets/Racing/Art/PolyHaven/";
        private const string Materials = "Assets/Racing/LongRoad/Materials/";

        [MenuItem("Racing/Apply Scanned PBR Materials")]
        public static void RunBatch()
        {
            try
            {
                ApplyMaterials();
                Debug.Log("SCANNED_PBR_MATERIALS_COMPLETE");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        public static void ApplyMaterials()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Apply("Road asphalt", "asphalt_02_rough_2k.jpg", "asphalt_02_ao_2k.jpg", "asphalt_02_disp_2k.jpg", "asphalt_02_roughness_smoothness.png", "asphalt_02_nor_gl_2k.jpg");
            Apply("Meadow", "aerial_grass_rock_rough_2k.jpg", "aerial_grass_rock_ao_2k.jpg", "aerial_grass_rock_disp_2k.jpg", "aerial_grass_rock_roughness_smoothness.png", "aerial_grass_rock_nor_gl_2k.jpg");
            Apply("Shoulder gravel", "forest_ground_04_rough_2k.jpg", "forest_ground_04_ao_2k.jpg", "forest_ground_04_disp_2k.jpg", "forest_ground_04_roughness_smoothness.png", "forest_ground_04_nor_gl_2k.jpg");
            Apply("Forest floor", "forest_ground_04_rough_2k.jpg", "forest_ground_04_ao_2k.jpg", "forest_ground_04_disp_2k.jpg", "forest_ground_04_roughness_smoothness.png", "forest_ground_04_nor_gl_2k.jpg");
            Apply("Sand", "sand_01_rough_2k.jpg", "sand_01_ao_2k.jpg", "sand_01_disp_2k.jpg", "sand_01_roughness_smoothness.png", "sand_01_nor_gl_2k.jpg");
            Apply("Snow", "snow_01_rough_2k.jpg", "snow_01_ao_2k.jpg", "snow_01_disp_2k.jpg", "snow_01_roughness_smoothness.png", "snow_01_nor_gl_2k.jpg");
            AssetDatabase.SaveAssets();
        }

        private static void Apply(string materialName, string roughness, string occlusion, string displacement, string smoothnessName, string normal)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + materialName + ".mat");
            if (material == null) throw new InvalidOperationException("Missing material: " + materialName);
            var smoothness = BuildSmoothness(Art + roughness, Art + smoothnessName);
            material.SetTexture("_MetallicGlossMap", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + occlusion));
            material.SetFloat("_OcclusionStrength", 0.75f);
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetTexture("_ParallaxMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + displacement));
            material.SetFloat("_Parallax", materialName == "Road asphalt" ? 0.018f : 0.035f);
            material.EnableKeyword("_PARALLAXMAP");
            if (!string.IsNullOrEmpty(normal))
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + normal));
                material.SetFloat("_BumpScale", materialName == "Road asphalt" ? 0.32f : 0.5f);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material);
        }

        private static Texture2D BuildSmoothness(string sourcePath, string outputPath)
        {
            if (!File.Exists(sourcePath)) throw new InvalidOperationException("Missing roughness texture: " + sourcePath);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(sourcePath))) throw new InvalidOperationException("Could not decode roughness texture: " + sourcePath);
            var pixels = source.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                byte smooth = (byte)(255 - pixels[i].r);
                pixels[i] = new Color32(255, 255, 255, smooth);
            }
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels); image.Apply(false, false);
            File.WriteAllBytes(outputPath, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
        }
    }
}
