using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SplitMask.Editor
{
    /// <summary>Rebuilds the split mask from the included geometry source.</summary>
    public static class SplitMaskBuilder
    {
        private const string Root = "Assets/SplitMask";
        private const string SourcePath = Root + "/Source/mesh.json";

        [Serializable]
        private sealed class MeshDocument
        {
            public MeshPart[] parts;
        }

        [Serializable]
        private sealed class MeshPart
        {
            public string name;
            public string material;
            public float[] vertices;
            public float[] normals;
            public float[] uvs;
            public int[] triangles;
        }

        private sealed class BuiltPart
        {
            public string name;
            public string material;
            public Mesh mesh;
        }

        [MenuItem("Tools/Split Mask/Rebuild Prefabs and Export Package")]
        public static void BuildAndExport()
        {
            Build();
            Export();
        }

        [MenuItem("Tools/Split Mask/Rebuild, Render Preview and Export")]
        public static void BuildAndRender()
        {
            Build();
            RenderPreview();
            Export();
        }

        private static void Build()
        {
            if (!File.Exists(SourcePath))
                throw new FileNotFoundException("Split mask source geometry is missing.", SourcePath);

            EnsureFolder(Root + "/Meshes");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Prefabs");

            var document = JsonUtility.FromJson<MeshDocument>(File.ReadAllText(SourcePath));
            if (document == null || document.parts == null || document.parts.Length == 0)
                throw new InvalidDataException("The source contains no mesh parts.");

            var builtParts = new List<BuiltPart>();
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in document.parts)
            {
                ValidatePart(part);
                var name = SafeName(part.name);
                if (!usedNames.Add(name))
                    throw new InvalidDataException("Duplicate mesh part name: " + name);

                string meshPath = Root + "/Meshes/" + name + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                bool isNew = mesh == null;
                if (isNew)
                    mesh = new Mesh();
                else
                    mesh.Clear();

                mesh.name = name;
                int count = part.vertices.Length / 3;
                mesh.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                var vertices = new Vector3[count];
                var normals = new Vector3[count];
                var uvs = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    vertices[i] = new Vector3(part.vertices[i * 3], part.vertices[i * 3 + 1], part.vertices[i * 3 + 2]);
                    normals[i] = new Vector3(part.normals[i * 3], part.normals[i * 3 + 1], part.normals[i * 3 + 2]);
                    uvs[i] = new Vector2(part.uvs[i * 2], part.uvs[i * 2 + 1]);
                }
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.triangles = part.triangles;
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                if (isNew)
                    AssetDatabase.CreateAsset(mesh, meshPath);
                else
                    EditorUtility.SetDirty(mesh);
                builtParts.Add(new BuiltPart { name = name, material = part.material, mesh = mesh });
            }

            Shader standard = Shader.Find("Standard");
            if (standard == null)
                throw new InvalidOperationException("Unity Standard shader was not found.");
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            var black = new Color(0.025f, 0.028f, 0.032f, 1f);
            var ivory = new Color(0.89f, 0.875f, 0.84f, 1f);
            Material blackBuiltIn = SaveMaterial("SatinBlack_BuiltIn", standard, black, 0.29f);
            Material ivoryBuiltIn = SaveMaterial("Ivory_BuiltIn", standard, ivory, 0.32f);
            SavePrefab(builtParts, "SplitMask_BuiltIn", blackBuiltIn, ivoryBuiltIn);
            if (urp != null)
            {
                Material blackUrp = SaveMaterial("SatinBlack_URP", urp, black, 0.29f);
                Material ivoryUrp = SaveMaterial("Ivory_URP", urp, ivory, 0.32f);
                SavePrefab(builtParts, "SplitMask", blackUrp, ivoryUrp);
            }
            else
            {
                Debug.LogWarning("URP Lit is unavailable in this project; SplitMask uses Standard materials.");
                SavePrefab(builtParts, "SplitMask", blackBuiltIn, ivoryBuiltIn);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("SplitMask: rebuilt " + builtParts.Count + " native mesh assets and two prefabs.");
        }

        private static Material SaveMaterial(string name, Shader shader, Color color, float smoothness)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;
            if (isNew)
                material = new Material(shader);
            else
                material.shader = shader;
            material.name = name;
            material.shaderKeywords = Array.Empty<string>();
            material.renderQueue = -1;
            material.enableInstancing = true;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
            if (isNew)
                AssetDatabase.CreateAsset(material, path);
            else
                EditorUtility.SetDirty(material);
            return material;
        }

        private static void SavePrefab(List<BuiltPart> parts, string name, Material black, Material ivory)
        {
            var root = new GameObject(name);
            try
            {
                foreach (var part in parts)
                {
                    var child = new GameObject(part.name);
                    child.transform.SetParent(root.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = part.material == "black" ? black : ivory;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                string path = Root + "/Prefabs/" + name + ".prefab";
                var result = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || result == null)
                    throw new IOException("Could not save prefab: " + path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Export()
        {
            string path = Environment.GetEnvironmentVariable("SPLITMASK_EXPORT_PATH");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "outputs", "SplitMask.unitypackage"));
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.ExportPackage(Root, path, ExportPackageOptions.Recurse);
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new IOException("Unity did not create the package: " + path);
            Debug.Log("SPLITMASK_EXPORT_OK " + path + " (" + new FileInfo(path).Length + " bytes)");
        }

        private static void RenderPreview()
        {
            string path = Environment.GetEnvironmentVariable("SPLITMASK_PREVIEW_PATH");
            if (string.IsNullOrWhiteSpace(path))
                path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "outputs", "SplitMask-preview.png"));
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/SplitMask_BuiltIn.prefab");
            if (prefab == null)
                throw new FileNotFoundException("The preview prefab was not built.");

            var oldPipeline = GraphicsSettings.defaultRenderPipeline;
            var oldQualityPipeline = QualitySettings.renderPipeline;
            var oldScene = SceneManager.GetActiveScene();
            // Unity refuses additive editor scenes while its startup scene is untitled.
            // Batch builds use a scratch project, so that empty startup scene can be
            // replaced. Interactive saved scenes retain the additive cleanup path.
            bool replaceUntitledScene = Application.isBatchMode && string.IsNullOrEmpty(oldScene.path);
            Scene previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceUntitledScene ? NewSceneMode.Single : NewSceneMode.Additive);
            RenderTexture target = null;
            Texture2D pixels = null;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                SceneManager.SetActiveScene(previewScene);
                GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.37f, 0.39f, 0.43f);
                RenderSettings.ambientIntensity = 1f;
                RenderSettings.reflectionIntensity = 0.5f;
                RenderSettings.skybox = null;
                const int layer = 30;
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, previewScene);
                foreach (Transform t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float height = Mathf.Max(bounds.size.y, 0.01f);
                var cameraObject = new GameObject("Preview Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.48f, 0.49f, 0.51f, 1f);
                camera.cullingMask = 1 << layer;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.28f;
                camera.nearClipPlane = height * 0.01f;
                camera.farClipPlane = height * 10f;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.transform.position = bounds.center + new Vector3(height * 1.2f, height * 0.15f, height * 2.8f);
                camera.transform.LookAt(bounds.center, Vector3.up);
                AddLight("Soft key", new Vector3(28f, 155f, 0f), 1.8f, new Color(1f, 0.96f, 0.91f), layer, LightShadows.Soft);
                AddLight("Fill", new Vector3(12f, 220f, 0f), 0.65f, new Color(0.83f, 0.9f, 1f), layer, LightShadows.None);
                AddLight("Edge light", new Vector3(0f, -30f, 0f), 0.8f, Color.white, layer, LightShadows.None);
                target = new RenderTexture(1400, 1400, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.antiAliasing = 4;
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                camera.targetTexture = null;
                Debug.Log("SPLITMASK_PREVIEW_OK " + path);
            }
            finally
            {
                RenderTexture.active = oldActive;
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                GraphicsSettings.defaultRenderPipeline = oldPipeline;
                QualitySettings.renderPipeline = oldQualityPipeline;
                if (replaceUntitledScene)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else
                {
                    EditorSceneManager.CloseScene(previewScene, true);
                    if (oldScene.IsValid() && oldScene.isLoaded) SceneManager.SetActiveScene(oldScene);
                }
            }
        }

        private static void AddLight(string name, Vector3 rotation, float intensity, Color color, int layer, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(rotation);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            light.shadowStrength = 0.65f;
            light.shadowBias = 0.005f;
            light.shadowNormalBias = 0.005f;
            light.cullingMask = 1 << layer;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException("Every mesh part needs a name.");
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private static void ValidatePart(MeshPart part)
        {
            if (part == null || part.vertices == null || part.vertices.Length == 0 || part.vertices.Length % 3 != 0)
                throw new InvalidDataException("Invalid vertex data.");
            int count = part.vertices.Length / 3;
            if (part.normals == null || part.normals.Length != part.vertices.Length || part.uvs == null || part.uvs.Length != count * 2)
                throw new InvalidDataException("Normals or UV count does not match vertices: " + part.name);
            if (part.material != "black" && part.material != "ivory")
                throw new InvalidDataException("Unknown mask material: " + part.material);
            if (part.triangles == null || part.triangles.Length == 0 || part.triangles.Length % 3 != 0)
                throw new InvalidDataException("Invalid triangle data: " + part.name);
            foreach (int index in part.triangles)
                if (index < 0 || index >= count)
                    throw new InvalidDataException("Triangle index out of range: " + part.name);
        }
    }
}
