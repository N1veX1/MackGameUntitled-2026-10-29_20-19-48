using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LogicAssetBuilder
{
    const string Root = "Assets/Логика";
    [MenuItem("Tools/Логика/Create reusable prefabs")]
    public static void Build()
    {
        // Work in additive temporary scenes; existing level scenes are never saved by this tool.
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(previous.path))
            throw new Exception("Save your current scene before building the reusable asset library.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        try
        {
            foreach (string folder in new[] { "1 уровень", "2 уровень", "3 уровень", "3 уровень/Geometry" }) Folder(Root + "/" + folder);
            var concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskEnvironment/AgedConcrete.mat");
            for (int i = 0; i < 6; i++) BuildAxisWall((MaskAxisCamera.ViewDirection)i, concrete);
            var office = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskInversion/Generated/AgedConcrete.mat");
            BuildInversionWall("Стена_видна_без_маски", true, concrete);
            BuildInversionWall("Стена_видна_в_маске", false, office ? office : concrete);
            CopyPrefab("Assets/MaskInversion/Bridges/RootBridge.prefab", Root + "/2 уровень/Мост_без_маски.prefab");
            CopyPrefab("Assets/MaskInversion/Bridges/OfficeBridge.prefab", Root + "/2 уровень/Мост_в_маске.prefab");
            foreach (string type in new[] { "Вверх", "Вниз", "Направо", "Налево", "Прямая" }) BuildCorner(type, concrete);
        }
        finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous); }
        SavePlayer("Assets/Scenes/Level01_Tutorial.unity", 1);
        SavePlayer("Assets/Scenes/Level02_Tutorial.unity", 2);
        SavePlayer("Assets/Scenes/Level03_CornerTraversal.unity", 3);
        BuildGravityTemplate();
        AssetDatabase.SaveAssets();
        Debug.Log("LOGIC_ASSETS_BUILT: six axis walls, two inversion walls, two bridges, four corners, straight floor, players and connected template.");
    }
    public static void BatchBuild() { try { EditorSceneManager.OpenScene("Assets/Scenes/Level03_CornerTraversal.unity"); Build(); EditorApplication.Exit(0); } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); } }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    static void BuildAxisWall(MaskAxisCamera.ViewDirection direction, Material material)
    {
        string[] names = { "X", "-X", "Y", "-Y", "Z", "-Z" };
        var root = new GameObject("Стена " + names[(int)direction]);
        var settings = root.AddComponent<AxisWallAsset>(); settings.direction = direction;
        bool z = (int)direction >= 4;
        settings.obstacle = Box(root.transform, "Исчезающая стена", new Vector3(0, 2, 0), z ? new Vector3(.3f, 4, 6) : new Vector3(6, 4, .3f), material);
        settings.obstacle.AddComponent<MaskProximityWall>();
        Vector3 pos, size;
        switch (direction)
        {
            case MaskAxisCamera.ViewDirection.X: pos = new Vector3(-3.2f, 2, 0); size = new Vector3(.3f, 4.2f, 4); break;
            case MaskAxisCamera.ViewDirection.MinusX: pos = new Vector3(3.2f, 2, 0); size = new Vector3(.3f, 4.2f, 4); break;
            case MaskAxisCamera.ViewDirection.Y: pos = new Vector3(0, -.2f, 0); size = new Vector3(6.6f, .3f, 4); break;
            case MaskAxisCamera.ViewDirection.MinusY: pos = new Vector3(0, 4.2f, 0); size = new Vector3(6.6f, .3f, 4); break;
            case MaskAxisCamera.ViewDirection.Z: pos = new Vector3(0, 2, -3.2f); size = new Vector3(4, 4.2f, .3f); break;
            default: pos = new Vector3(0, 2, 3.2f); size = new Vector3(4, 4.2f, .3f); break;
        }
        settings.mergeSurface = Box(root.transform, "Поверхность для слияния", pos, size, material).GetComponent<Renderer>();
        Save(root, Root + "/1 уровень/Стена_" + names[(int)direction] + ".prefab");
    }
    static void BuildInversionWall(string name, bool openWithMask, Material material)
    {
        var root = new GameObject(name);
        Box(root.transform, "Внешний вид — можно заменить", new Vector3(0, 2, 0), new Vector3(6, 4, .35f), material);
        root.AddComponent<MaskInversionGate>().Configure(MaskInversionGate.GateKind.Wall, openWithMask);
        Save(root, Root + "/2 уровень/" + name + ".prefab");
    }
    static void CopyPrefab(string from, string to)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(from);
        if (!source) throw new Exception("Missing prefab: " + from);
        var clone = Object.Instantiate(source); clone.name = System.IO.Path.GetFileNameWithoutExtension(to); Save(clone, to);
    }
    struct Sample { public Vector3 p; public Quaternion q; public Sample(Vector3 p, Quaternion q) { this.p = p; this.q = q; } }
    static void Straight(List<Sample> path, float length)
    {
        var start = path.Last(); int count = Mathf.CeilToInt(length / .15f);
        for (int i = 1; i <= count; i++) path.Add(new Sample(start.p + start.q * Vector3.forward * (length * i / count), start.q));
    }
    static float Distance(List<Sample> path) { float d = 0; for (int i = 1; i < path.Count; i++) d += Vector3.Distance(path[i - 1].p, path[i].p); return d; }
    static void Arc(List<Sample> path, Vector3 localCenter, Vector3 localAxis, float angle)
    {
        var start = path.Last(); Vector3 center = start.p + start.q * localCenter, axis = start.q * localAxis;
        for (int i = 1; i <= 40; i++) { var rotation = Quaternion.AngleAxis(angle * i / 40, axis); path.Add(new Sample(center + rotation * (start.p - center), rotation * start.q)); }
    }
    static void BuildCorner(string name, Material material)
    {
        var root = new GameObject("Платформа — " + name);
        var piece = root.AddComponent<GravityRoutePiece>();
        piece.samples = new GameObject("Route samples").transform; piece.samples.SetParent(root.transform, false);
        var path = new List<Sample> { new Sample(Vector3.up * .9f, Quaternion.identity) };
        Straight(path, 2);
        if (name == "Направо" || name == "Налево")
        {
            float sign = name == "Направо" ? 1 : -1;
            Arc(path, Vector3.right * (3 * sign), Vector3.up, 90 * sign); Straight(path, .5f);
        }
        float turnStart = Distance(path);
        if (name != "Прямая")
        {
            int sign = name == "Вниз" ? -1 : 1;
            Arc(path, Vector3.up * (1.1f * sign), Vector3.left, 90 * sign);
            piece.corner = root.AddComponent<RoundedCornerTransition>();
            piece.corner.startDistance = turnStart; piece.corner.endDistance = Distance(path); piece.corner.transitionName = name;
        }
        Straight(path, 2);
        for (int i = 0; i < path.Count; i++)
        {
            var node = new GameObject("Route " + i.ToString("D3")).transform; node.SetParent(piece.samples, false);
            node.localPosition = path[i].p; node.localRotation = path[i].q;
        }
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); float length = 0;
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0) length += Vector3.Distance(path[i - 1].p, path[i].p);
            var s = path[i]; Vector3 up = s.q * Vector3.up, right = s.q * Vector3.right;
            Vector3 top = s.p - up * .9f, bottom = top - up * .25f;
            vertices.AddRange(new[] { top - right * 2, top + right * 2, bottom - right * 2, bottom + right * 2 });
            for (int k = 0; k < 4; k++) uv.Add(new Vector2(k % 2, length * .25f));
            if (i == 0) continue;
            int a = (i - 1) * 4, b = i * 4;
            Quad(triangles, a, b, b + 1, a + 1); Quad(triangles, a + 2, a + 3, b + 3, b + 2);
            Quad(triangles, a, a + 2, b + 2, b); Quad(triangles, a + 1, b + 1, b + 3, a + 3);
        }
        int end = vertices.Count - 4; Quad(triangles, 0, 1, 3, 2); Quad(triangles, end, end + 2, end + 3, end + 1);
        string meshPath = Root + "/3 уровень/Geometry/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath); bool create = !mesh; if (create) mesh = new Mesh(); else mesh.Clear();
        mesh.name = "Gravity module " + name; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, meshPath); else EditorUtility.SetDirty(mesh);
        var surface = new GameObject("Бетонная платформа"); surface.transform.SetParent(root.transform, false);
        surface.AddComponent<MeshFilter>().sharedMesh = mesh; surface.AddComponent<MeshRenderer>().sharedMaterial = material; surface.AddComponent<MeshCollider>().sharedMesh = mesh;
        Save(root, Root + "/3 уровень/" + name + ".prefab");
    }
    static void Quad(List<int> list, int a, int b, int c, int d) { list.AddRange(new[] { a, b, c, a, c, d }); }
    static void SavePlayer(string scenePath, int level)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
        bool alreadyOpen = scene.IsValid() && scene.isLoaded;
        if (!alreadyOpen) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        GameObject clone = null;
        try
        {
            GameObject source = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                Component player = level == 1 ? root.GetComponentInChildren<MaskDemoPlayer>(true) : level == 2 ? (Component)root.GetComponentInChildren<MaskInversionPlayer>(true) : root.GetComponentInChildren<CornerMaskTraversalController>(true);
                if (player) { source = player.gameObject; break; }
            }
            if (!source) throw new Exception("Player missing in " + scenePath);
            clone = Object.Instantiate(source); source = clone;
            if (level == 1)
            {
                var mask = source.GetComponentInChildren<MaskAxisCamera>(true); var data = new SerializedObject(mask);
                foreach (var field in new[] { "wallRenderers", "wallColliders", "topCutawayRenderers", "oppositeSideCutawayRenderers", "directionIndicators" }) data.FindProperty(field).arraySize = 0;
                data.FindProperty("focusTarget").objectReferenceValue = source.transform; data.FindProperty("playerTarget").objectReferenceValue = source.transform; data.ApplyModifiedPropertiesWithoutUndo();
            }
            if (level == 3) source.GetComponent<CornerMaskTraversalController>().ConfigureRoute(null, Array.Empty<RoundedCornerTransition>(), 2);
            PrefabUtility.SaveAsPrefabAsset(source, Root + "/" + level + " уровень/Игрок.prefab");
        }
        finally { if (clone) Object.DestroyImmediate(clone); if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
    }
    static void BuildGravityTemplate()
    {
        var root = new GameObject("Своя локация — гравитация");
        var layout = root.AddComponent<GravityRouteAssembler>();
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/3 уровень/Игрок.prefab"), root.transform);
        layout.player = player.GetComponent<CornerMaskTraversalController>();
        string[] order = { "Прямая", "Вверх", "Вниз", "Направо", "Налево", "Прямая" };
        layout.pieces = new GravityRoutePiece[order.Length];
        for (int i = 0; i < order.Length; i++)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/3 уровень/" + order[i] + ".prefab"), root.transform);
            go.name = (i + 1).ToString("D2") + " — " + order[i]; layout.pieces[i] = go.GetComponent<GravityRoutePiece>();
        }
        layout.BuildRoute();
        Save(root, Root + "/3 уровень/Шаблон_локации.prefab");
    }
    static void Save(GameObject root, string path) { PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); }
}
