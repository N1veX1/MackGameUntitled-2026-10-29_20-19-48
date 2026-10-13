using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sample = CornerTraversalSceneBuilder.Sample;
using Object = UnityEngine.Object;

/// <summary>Separate walking slabs, walls, ceilings and gravity fillets. No tall wall is swept through a gravity bend.</summary>
public static class GravityRoomModulesBuilder
{
    const string Geometry = "Assets/MaskTraversal/Geometry/Modules";
    const string Kit = "Assets/Логика/3 уровень/Модули";
    const float HalfWidth = 4.8f, Height = 4.4f, Body = .9f, Radius = 1.1f;
    // The outer walking fillet is 2 m. Roof planes meet 2.4 m before/after the fillet.
    const float Cut = Height - Radius - Body;
    static int meshIndex;

    public static void Build(Transform route, Transform rooms, List<Sample> path,
        List<Vector2Int> straights, List<Vector2Int> flats, List<Vector2Int> corners,
        Material concrete, Material frame, Material glass)
    {
        Folder(Geometry); Folder(Kit + "/Пол"); Folder(Kit + "/Стены"); Folder(Kit + "/Скругления");
        meshIndex = 0;
        var floors = Group("Пол — отдельные секции", route);
        var turns = Group("Скругления — отдельные переходы гравитации", route);
        var walls = Group("Стены — отдельные секции", rooms);
        var ceilings = Group("Потолки — отдельные секции", rooms);
        var fittings = Group("Окна и рамы", rooms);
        var ranges = straights.Concat(flats).Concat(corners).OrderBy(r => r.x).ToList();
        int number = 0;
        foreach (var range in ranges)
        {
            bool gravity = corners.Contains(range);
            var samples = path.GetRange(range.x, range.y - range.x + 1);
            string name = (++number).ToString("D2") + (gravity ? " — Скругление гравитации" : flats.Contains(range) ? " — Поворот пола" : " — Пол");
            var module = Frame(name, gravity ? turns : floors, samples[0]);
            WalkingSurface(module, samples, concrete, "WalkModule" + number.ToString("D2"));
            SaveWalkingPrefab(module, samples, gravity, Kit + (gravity ? "/Скругления/" : "/Пол/") + name + ".prefab");
        }
        int roomIndex = 0;
        foreach (var range in straights)
        {
            var s = path[range.x]; float length = Vector3.Distance(s.point, path[range.y].point);
            float from = corners.Any(c => c.y == range.x) ? Cut : 0;
            float to = length - (corners.Any(c => c.x == range.y) ? Cut : 0);
            var wallFrame = Frame((++roomIndex).ToString("D2") + " — Стены комнаты", walls, s);
            var roofFrame = Frame(roomIndex.ToString("D2") + " — Потолок комнаты", ceilings, s);
            Box(roofFrame, "Потолок", new Vector3(0, Height + .12f, (from + to) * .5f), new Vector3(HalfWidth * 2 + .48f, .24f, to - from), concrete);
            foreach (int side in new[] { -1, 1 })
            {
                var wall = Box(wallFrame, side < 0 ? "Левая стена" : "Правая стена", new Vector3(side * (HalfWidth + .12f), Height * .5f, (from + to) * .5f), new Vector3(.24f, Height, to - from), concrete);
                if (roomIndex == 1 && side < 0) SavePlainPrefab(wall, Kit + "/Стены/Стена.prefab");
            }
            if (roomIndex == 1) SavePlainPrefab(roofFrame.GetChild(0), Kit + "/Стены/Потолок.prefab");
            var decor = Frame(roomIndex.ToString("D2") + " — Окна", fittings, s);
            for (float z = from + 1.4f; z < to - 1.2f; z += 3.4f)
                foreach (int side in new[] { -1, 1 })
                {
                    Box(decor, "Окно", new Vector3(side * (HalfWidth - .025f), 2.6f, z), new Vector3(.025f, 1.9f, 2.35f), glass, false);
                    Box(decor, "Подоконник", new Vector3(side * (HalfWidth - .05f), 1.65f, z), new Vector3(.12f, .09f, 2.4f), frame, false);
                }
        }
        foreach (var range in corners) CornerWalls(walls, path.GetRange(range.x, range.y - range.x + 1), concrete);
        foreach (var range in flats) FlatRoom(walls, ceilings, path.GetRange(range.x, range.y - range.x + 1), concrete);
        foreach (int index in new[] { 0, path.Count - 1 })
        {
            var end = Frame(index == 0 ? "Стена за стартом" : "Стена выхода", walls, path[index]);
            Box(end, "Торцевая стена", new Vector3(0, Height / 2, index == 0 ? -.12f : .12f), new Vector3(HalfWidth * 2 + .48f, Height, .24f), concrete);
        }
        AssetDatabase.SaveAssets();
    }

    static void WalkingSurface(Transform module, List<Sample> samples, Material material, string key)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float distance = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i]; var up = module.InverseTransformDirection(s.up);
            var right = module.InverseTransformDirection(Vector3.Cross(s.up, s.forward));
            var top = module.InverseTransformPoint(s.point - s.up * Body); var bottom = top - up * .32f;
            if (i > 0) distance += Vector3.Distance(samples[i - 1].point - samples[i - 1].up * Body, s.point - s.up * Body);
            v.AddRange(new[] { top - right * HalfWidth, top + right * HalfWidth, bottom - right * HalfWidth, bottom + right * HalfWidth,
                top - right * HalfWidth, bottom - right * HalfWidth, top + right * HalfWidth, bottom + right * HalfWidth });
            n.AddRange(new[] { up, up, -up, -up, -right, -right, right, right });
            for (int k = 0; k < 8; k++) uv.Add(new Vector2((k % 2) * (k < 4 ? HalfWidth * 2 : .32f) * .25f, distance * .25f));
            if (i == 0) continue;
            int a = (i - 1) * 8, b = i * 8;
            Quad(t, a, b, b + 1, a + 1); Quad(t, a + 2, a + 3, b + 3, b + 2);
            Quad(t, a + 4, a + 5, b + 5, b + 4); Quad(t, a + 6, b + 6, b + 7, a + 7);
        }
        // Separate cap vertices keep the walking normals smooth at module joins.
        Cap(v, n, uv, t, new[] { v[0], v[1], v[3], v[2] }, -module.InverseTransformDirection(samples[0].forward));
        int end = (samples.Count - 1) * 8;
        Cap(v, n, uv, t, new[] { v[end], v[end + 2], v[end + 3], v[end + 1] }, module.InverseTransformDirection(samples.Last().forward));
        Surface(module, "Ходовая поверхность", key, v, uv, t, material, n);
    }

    static void CornerWalls(Transform parent, List<Sample> samples, Material material)
    {
        var frame = Frame("Стены у скругления — без пересечения поверхностей", parent, samples[0]);
        // A planar side panel encloses the junction of the two rooms. Sweeping a 4.4 m wall
        // around the 2 m floor radius would fold its upper edge through the ceiling.
        var boundary = new List<Vector3> { new Vector3(0, 0, -Cut) };
        foreach (var s in samples) boundary.Add(frame.InverseTransformPoint(s.point - s.up * Body));
        boundary.Add(new Vector3(0, Height, Radius + Body));
        boundary.Add(new Vector3(0, Height, -Cut));
        foreach (int side in new[] { -1, 1 })
        {
            var v = boundary.Select(p => p + Vector3.right * (side * HalfWidth)).ToList();
            var uv = v.Select(p => new Vector2(p.z * .25f, p.y * .25f)).ToList();
            var t = new List<int>(); Vector3 normal = Vector3.left * side;
            Vector3 center = v.Aggregate(Vector3.zero, (sum, p) => sum + p) / v.Count;
            v.Add(center); uv.Add(new Vector2(center.z * .25f, center.y * .25f));
            for (int i = 0; i < boundary.Count; i++)
            {
                int next = (i + 1) % boundary.Count;
                if (Vector3.Dot(Vector3.Cross(v[i] - center, v[next] - center), normal) > 0) t.AddRange(new[] { v.Count - 1, i, next });
                else t.AddRange(new[] { v.Count - 1, next, i });
            }
            var wall = Surface(frame, side < 0 ? "Левая боковая панель" : "Правая боковая панель", Key("CornerWall"), v, uv, t, material);
            // Individual side panels are editable; they are never merged with the walking corner.
            SavePlainPrefab(wall, Kit + "/Стены/" + wall.GetComponent<MeshFilter>().sharedMesh.name + ".prefab");
        }
    }

    static void FlatRoom(Transform walls, Transform roofs, List<Sample> samples, Material material)
    {
        foreach (int side in new[] { -1, 1 })
        {
            var frame = Frame("Стена бокового поворота", walls, samples[0]);
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>(); float d = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i]; var bottom = frame.InverseTransformPoint(s.point - s.up * Body + Vector3.Cross(s.up, s.forward) * (HalfWidth * side));
                if (i > 0) d += Vector3.Distance(bottom, v[v.Count - 2]);
                v.Add(bottom); v.Add(bottom + Vector3.up * Height); uv.Add(new Vector2(d * .25f, 0)); uv.Add(new Vector2(d * .25f, Height * .25f));
                if (i > 0) { int a = (i - 1) * 2, b = i * 2; if (side < 0) Quad(t, a, a + 1, b + 1, b); else Quad(t, a, b, b + 1, a + 1); }
            }
            Surface(frame, side < 0 ? "Левая стена поворота" : "Правая стена поворота", Key("BendWall"), v, uv, t, material);
        }
        var roof = Frame("Потолок бокового поворота", roofs, samples[0]);
        var vertices = new List<Vector3>(); var tex = new List<Vector2>(); var triangles = new List<int>();
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i]; var right = Vector3.Cross(s.up, s.forward);
            foreach (int side in new[] { -1, 1 })
            {
                var p = roof.InverseTransformPoint(s.point + s.up * (Height - Body) + right * (HalfWidth * side));
                vertices.Add(p); tex.Add(new Vector2(p.x, p.z) * .25f);
            }
            if (i > 0) { int a = (i - 1) * 2, b = i * 2; Quad(triangles, a, a + 1, b + 1, b); }
        }
        Surface(roof, "Потолок поворота", Key("BendRoof"), vertices, tex, triangles, material);
    }

    static Transform Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider = true)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var n = new List<Vector3>(); var t = new List<int>();
        Vector3 h = size * .5f;
        foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
        {
            Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.Cross(Vector3.up, normal);
            Vector3 w = Vector3.Cross(normal, u);
            Vector3 c = Vector3.Scale(normal, h), a = Vector3.Scale(u, h), b = Vector3.Scale(w, h);
            int start = v.Count;
            v.AddRange(new[] { c - a - b, c + a - b, c + a + b, c - a + b });
            n.AddRange(new[] { normal, normal, normal, normal });
            uv.AddRange(new[] { Vector2.zero, new Vector2(a.magnitude * .5f, 0), new Vector2(a.magnitude * .5f, b.magnitude * .5f), new Vector2(0, b.magnitude * .5f) });
            Quad(t, start, start + 1, start + 2, start + 3);
        }
        var result = Surface(parent, name, Key("Panel"), v, uv, t, material, n, collider); result.localPosition = center; return result;
    }
    static Transform Surface(Transform parent, string name, string key, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Material material, List<Vector3> normals = null, bool collider = true)
    {
        string assetPath = Geometry + "/" + key + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath); bool create = !mesh;
        if (create) mesh = new Mesh(); else mesh.Clear();
        mesh.name = key; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
        if (normals != null) mesh.SetNormals(normals); else mesh.RecalculateNormals();
        mesh.RecalculateTangents(); mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, assetPath); else EditorUtility.SetDirty(mesh);
        var go = Group(name, parent); go.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        if (collider) go.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }
    static void SaveWalkingPrefab(Transform source, List<Sample> samples, bool corner, string file)
    {
        var clone = Object.Instantiate(source.gameObject); clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var piece = clone.AddComponent<GravityRoutePiece>(); piece.samples = Group("Route samples", clone.transform);
        float d = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            var node = Group("Route " + i.ToString("D3"), piece.samples); var s = samples[i];
            node.localPosition = source.InverseTransformPoint(s.point);
            node.localRotation = Quaternion.Inverse(source.rotation) * Quaternion.LookRotation(s.forward, s.up);
            if (i > 0) d += Vector3.Distance(samples[i - 1].point, s.point);
        }
        if (corner) { piece.corner = clone.AddComponent<RoundedCornerTransition>(); piece.corner.startDistance = 0; piece.corner.endDistance = d; piece.corner.transitionName = source.name; }
        PrefabUtility.SaveAsPrefabAsset(clone, file); Object.DestroyImmediate(clone);
    }
    static void SavePlainPrefab(Transform source, string file)
    {
        var clone = Object.Instantiate(source.gameObject); clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        PrefabUtility.SaveAsPrefabAsset(clone, file); Object.DestroyImmediate(clone);
    }
    static Transform Frame(string name, Transform parent, Sample s)
    {
        var frame = Group(name, parent); frame.SetPositionAndRotation(s.point - s.up * Body, Quaternion.LookRotation(s.forward, s.up)); return frame;
    }
    static Transform Group(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    static string Key(string prefix) => prefix + (++meshIndex).ToString("D3");
    static void Folder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path)); }
    static void Quad(List<int> t, int a, int b, int c, int d) => t.AddRange(new[] { a, b, c, a, c, d });
    static void Cap(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3[] points, Vector3 normal)
    {
        int a = v.Count; v.AddRange(points); foreach (var p in points) { n.Add(normal); uv.Add(new Vector2(p.x, p.y) * .25f); } Quad(t, a, a + 1, a + 2, a + 3);
    }
}
