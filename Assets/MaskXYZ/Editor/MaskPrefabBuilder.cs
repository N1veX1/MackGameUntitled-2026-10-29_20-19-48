using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaskXYZ.Editor
{
    public static class MaskPrefabBuilder
    {
        const string Root = "Assets/MaskXYZ";
        const string Generated = Root + "/Generated";
        const float Height = 0.26f;

        [MenuItem("Tools/Mask XYZ/Rebuild Mask Prefab")]
        public static void Build()
        {
            EnsureFolder(Generated);
            LoadConcept(out var body, out var face);
            var bounds = FindBounds(body);
            var blackMesh = SaveAsset(BuildMesh(body, bounds, false), Generated + "/MaskBlackMesh.asset");
            var whiteMesh = SaveAsset(BuildMesh(face, bounds, true), Generated + "/MaskWhiteMesh.asset");

            var black = SaveAsset(CreateMaterial("MaskBlack", new Color(0.035f, 0.036f, 0.04f)),
                Generated + "/MaskBlack.mat");
            var ivory = SaveAsset(CreateMaterial("MaskIvory", new Color(0.94f, 0.925f, 0.89f)),
                Generated + "/MaskIvory.mat");

            var root = new GameObject("MaskXYZ");
            try
            {
                AddPart(root.transform, "Black shell and X-Y-Z relief", blackMesh, black);
                AddPart(root.transform, "Ivory face plate", whiteMesh, ivory);
                PrefabUtility.SaveAsPrefabAsset(root, Generated + "/MaskXYZ.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Mask XYZ prefab generated. Height 0.26 m; front faces +Z; eye holes are open through the shell.");
        }

        public static void BuildAndExport()
        {
            Build();
            var destination = Environment.GetEnvironmentVariable("MASKXYZ_EXPORT_PATH");
            if (string.IsNullOrEmpty(destination))
                destination = Path.GetFullPath("MaskXYZ.unitypackage");
            AssetDatabase.ExportPackage(Root, destination, ExportPackageOptions.Recurse);
            Debug.Log("Exported " + destination);
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(Root, "Generated");
        }

        static void AddPart(Transform parent, string name, Mesh mesh, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No compatible Lit shader found.");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.05f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.32f);
            return material;
        }

        static T SaveAsset<T>(T asset, string path) where T : UnityEngine.Object
        {
            var old = AssetDatabase.LoadMainAssetAtPath(path);
            if (old is T current)
            {
                EditorUtility.CopySerialized(asset, current);
                EditorUtility.SetDirty(current);
                UnityEngine.Object.DestroyImmediate(asset);
                return current;
            }
            if (old != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        struct BinaryMap
        {
            public int width;
            public int height;
            public bool[] data;
            public bool At(int x, int y)
            {
                return x >= 0 && y >= 0 && x < width && y < height && data[y * width + x];
            }
        }

        struct Bounds
        {
            public int left, right, bottom, top;
        }

        static BinaryMap LoadBinary(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException(path);
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var pixels = texture.GetPixels32();
            var data = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; ++i) data[i] = pixels[i].r > 127;
            return new BinaryMap { width = texture.width, height = texture.height, data = data };
        }

        // Symmetric silhouette and eye openings; letters cut through both layers.
        static void RefineMask(ref BinaryMap body, ref BinaryMap face)
        {
            var b = FindBounds(body);
            float cx = (b.left + b.right) * .5f;
            float cy = (b.bottom + b.top) * .5f;
            float hw = (b.right - b.left) * .5f;
            float hh = (b.top - b.bottom) * .5f;
            var solid = new bool[body.data.Length];
            for (int y = b.bottom; y <= b.top; y++)
            {
                int left = body.width, right = -1;
                for (int x = b.left; x <= b.right; x++)
                    if (body.At(x, y)) { left = Mathf.Min(left, x); right = Mathf.Max(right, x); }
                float radius = (right - left) * .5f;
                for (int x = b.left; x <= b.right; x++)
                {
                    // Matched angular eye slots, centered on the same symmetry axis.
                    float v = (y - cy) / hh;
                    float u = Mathf.Abs((x - cx) / hw);
                    float upper = .07f + .32f * u;
                    float lower = u < .27f ? -.03f + .65f * (u - .12f) : .07f + .08f * (u - .27f);
                    bool eye = u > .12f && u < .70f && v < upper && v > lower;
                    solid[y * body.width + x] = Mathf.Abs(x - cx) <= radius && !eye;
                }
            }
            body.data = solid;
            face.data = new bool[solid.Length];
            for (int y = 0; y < body.height; y++)
            for (int x = 0; x < body.width; x++)
            {
                bool inset = true;
                for (int dy = -5; dy <= 5 && inset; dy++)
                for (int dx = -5; dx <= 5; dx++)
                    if (!body.At(x + dx, y + dy)) { inset = false; break; }
                face.data[y * body.width + x] = inset;
            }
            for (int y = 0; y < body.height; y++)
            for (int x = 0; x < body.width; x++)
            {
                var p = new Vector2((x - cx) / hw, (y - cy) / hh);
                bool cut = Stroke(p, -.16f, .78f, .16f, .42f) || Stroke(p, .16f, .78f, -.16f, .42f)
                    || Stroke(p, -.57f, -.16f, -.43f, -.34f) || Stroke(p, -.29f, -.16f, -.43f, -.34f)
                    || Stroke(p, -.43f, -.34f, -.43f, -.55f)
                    || Stroke(p, .29f, -.16f, .57f, -.16f) || Stroke(p, .57f, -.16f, .29f, -.55f)
                    || Stroke(p, .29f, -.55f, .57f, -.55f);
                if (cut) { body.data[y * body.width + x] = false; face.data[y * body.width + x] = false; }
            }
        }

        static bool Stroke(Vector2 p, float ax, float ay, float bx, float by)
        {
            var a = new Vector2(ax, ay);
            var d = new Vector2(bx - ax, by - ay);
            return Vector2.Distance(p, a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude)) < .028f;
        }

        static void LoadConcept(out BinaryMap body, out BinaryMap face)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(Root + "/Source/Concept.png"));
            const int w = 512;
            int h = Mathf.RoundToInt(w * (float)texture.height / texture.width);
            body = new BinaryMap { width = w, height = h, data = new bool[w*h] };
            face = new BinaryMap { width = w, height = h, data = new bool[w*h] };
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x+.5f)/w, v = (y+.5f)/h;
                Color c = texture.GetPixelBilinear(u, v);
                float grey = (c.r+c.g+c.b)/3;
                bool eyeArea = (u > .375f && u < .495f || u > .575f && u < .70f) && v > .49f && v < .61f;
                bool eye = eyeArea && grey < .3f;
                body.data[y*w+x] = grey < .975f && !eye;
                face.data[y*w+x] = grey > .3f && grey < .975f && !eye;
            }
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static Bounds FindBounds(BinaryMap map)
        {
            var b = new Bounds { left = map.width, right = -1, bottom = map.height, top = -1 };
            for (int y = 0; y < map.height; ++y)
            for (int x = 0; x < map.width; ++x)
            {
                if (!map.At(x, y)) continue;
                b.left = Math.Min(b.left, x);
                b.right = Math.Max(b.right, x);
                b.bottom = Math.Min(b.bottom, y);
                b.top = Math.Max(b.top, y);
            }
            if (b.right < b.left) throw new InvalidOperationException("Empty mask map.");
            return b;
        }

        static Mesh BuildMesh(BinaryMap map, Bounds bounds, bool ivory)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var front = new Dictionary<int, int>();
            var rear = new Dictionary<int, int>();
            float pixel = Height / (bounds.top - bounds.bottom + 1);
            float cx = (bounds.left + bounds.right + 1) * 0.5f;
            float cy = (bounds.bottom + bounds.top + 1) * 0.5f;

            Vector3 Point(int x, int y, bool isFront)
            {
                float px = (x - cx) * pixel;
                float py = (y - cy) * pixel;
                float nx = (x - cx) / (bounds.right - bounds.left + 1f) * 2f;
                float ny = (y - cy) / (bounds.top - bounds.bottom + 1f) * 2f;
                // The center projects forward by about 22 mm relative to the
                // sides, giving the mask a gentle face-hugging horizontal arc.
                float horizontalBow = 0.045f * Mathf.Max(0f, 1f - nx * nx);
                float verticalBow = 0.009f * Mathf.Max(0f, 1f - ny * ny);
                float shellFront = 0.006f + horizontalBow + verticalBow;
                float z = ivory
                    ? shellFront + (isFront ? 0.0025f : 0.0003f)
                    : (isFront ? shellFront : shellFront - 0.014f);
                return new Vector3(px, py, z);
            }

            int GridVertex(int x, int y, bool isFront)
            {
                int key = y * (map.width + 1) + x;
                var lookup = isFront ? front : rear;
                if (lookup.TryGetValue(key, out int index)) return index;
                index = vertices.Count;
                lookup[key] = index;
                vertices.Add(Point(x, y, isFront));
                uvs.Add(new Vector2((float)x / map.width, (float)y / map.height));
                return index;
            }

            void Triangle(int a, int b, int c)
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }

            void Side(int x0, int y0, int x1, int y1, bool flip)
            {
                int a = vertices.Count;
                vertices.Add(Point(x0, y0, true));
                vertices.Add(Point(x1, y1, true));
                vertices.Add(Point(x1, y1, false));
                vertices.Add(Point(x0, y0, false));
                for (int i = 0; i < 4; ++i) uvs.Add(new Vector2(i == 1 || i == 2 ? 1 : 0, i >= 2 ? 1 : 0));
                if (flip) { Triangle(a, a + 2, a + 1); Triangle(a, a + 3, a + 2); }
                else { Triangle(a, a + 1, a + 2); Triangle(a, a + 2, a + 3); }
            }

            for (int y = 0; y < map.height; ++y)
            for (int x = 0; x < map.width; ++x)
            {
                if (!map.At(x, y)) continue;
                int bl = GridVertex(x, y, true);
                int br = GridVertex(x + 1, y, true);
                int tr = GridVertex(x + 1, y + 1, true);
                int tl = GridVertex(x, y + 1, true);
                Triangle(bl, br, tr); Triangle(bl, tr, tl);
                bl = GridVertex(x, y, false);
                br = GridVertex(x + 1, y, false);
                tr = GridVertex(x + 1, y + 1, false);
                tl = GridVertex(x, y + 1, false);
                Triangle(bl, tr, br); Triangle(bl, tl, tr);
                if (!map.At(x - 1, y)) Side(x, y, x, y + 1, false);
                if (!map.At(x + 1, y)) Side(x + 1, y, x + 1, y + 1, true);
                if (!map.At(x, y - 1)) Side(x, y, x + 1, y, true);
                if (!map.At(x, y + 1)) Side(x, y + 1, x + 1, y + 1, false);
            }

            var mesh = new Mesh { name = ivory ? "Mask ivory face" : "Mask black shell" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
