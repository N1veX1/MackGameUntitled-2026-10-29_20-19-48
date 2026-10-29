using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class CornerTraversalSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Level03_CornerTraversal.unity";
    const string Root = "Assets/MaskTraversal";
    const float Radius = 1.1f, HalfWidth = 4.8f, BodyOffset = .9f;
    public sealed class Sample
    {
        public Vector3 point, up, forward;
        public Sample(Vector3 point, Vector3 up, Vector3 forward) { this.point = point; this.up = up; this.forward = forward; }
    }
    static readonly List<Sample> path = new();
    static readonly List<int> cornerStarts = new(), cornerEnds = new();
    static readonly List<Vector2Int> straightSections = new();
    static readonly List<Vector2Int> flatSections = new();

    [MenuItem("Tools/Mask Traversal/Build Ornament Corner Level")]
    public static void Build()
    {
        var mask = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MaskOrnament/Prefabs/MaskOrnament.prefab");
        if (!mask) throw new System.Exception("The supplied MaskOrnament prefab is required.");
        EnsureFolder("Geometry"); EnsureFolder("Materials"); EnsureFolder("Prefabs");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.13f, .17f, .22f);
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.018f, .03f, .052f);
        RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = .007f;
        var concrete = MaterialAsset("NightConcrete", new Color(.18f, .215f, .25f), .18f);
        var steel = MaterialAsset("GraphiteFrame", new Color(.07f, .10f, .135f), .45f);
        var platform = MaterialAsset("GravityPlatform", new Color(.11f, .23f, .18f), .36f);
        var earlierConcrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskEnvironment/AgedConcrete.mat");
        if (earlierConcrete)
        {
            concrete.CopyPropertiesFromMaterial(earlierConcrete); platform.CopyPropertiesFromMaterial(earlierConcrete);
            EditorUtility.SetDirty(concrete); EditorUtility.SetDirty(platform);
        }
        var green = MaterialAsset("GravityGuide", new Color(.34f, .9f, .53f), .2f, new Color(.28f, 1.1f, .46f));
        var guideShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (guideShader) { green.shader = guideShader; green.SetColor("_BaseColor", new Color(.24f, .85f, .43f)); EditorUtility.SetDirty(green); }
        var amber = MaterialAsset("WarmLamps", new Color(.94f, .68f, .35f), .2f, new Color(1.5f, .83f, .3f));
        var glass = MaterialAsset("NightWindows", new Color(.10f, .2f, .34f), .8f);
        var root = new GameObject("03 — Night gravity complex").transform;
        var rooms = Group("01 — Connected rooms and shafts", root);
        var route = Group("02 — Editable gravity platform", root);
        var details = Group("03 — Lights and guidance", root);
        BuildPath();
        GravityRoomModulesBuilder.Build(route, rooms, path, straightSections, flatSections,
            cornerStarts.Zip(cornerEnds, (a, b) => new Vector2Int(a, b)).ToList(), concrete, steel, glass);
        var routeNodes = Group("Ordered editable route samples", route);
        for (int i = 0; i < path.Count; i++)
        {
            var node = Group("Route " + i.ToString("D3"), routeNodes);
            node.position = path[i].point;
            node.rotation = Quaternion.LookRotation(path[i].forward, path[i].up);
        }
        string[] names = { "01 — Floor to front wall", "02 — Front wall to right wall", "03 — Right wall to return wall", "04 — Descent to lower exit floor" };
        string[] before = { "Y", "-Z", "-X", "Z" }, after = { "-Z", "-X", "Z", "Y" };
        var corners = new List<RoundedCornerTransition>();
        for (int i = 0; i < 4; i++)
        {
            var marker = Group(names[i], route).gameObject.AddComponent<RoundedCornerTransition>();
            marker.transitionName = names[i]; marker.axisBefore = before[i]; marker.axisAfter = after[i];
            marker.startDistance = DistanceAt(cornerStarts[i]); marker.endDistance = DistanceAt(cornerEnds[i]);
            corners.Add(marker);
        }
        PrefabUtility.SaveAsPrefabAsset(route.gameObject, Root + "/Prefabs/LongGravityPlatform.prefab");
        BuildCornerPrefab(platform, green);
        BuildDetails(details, steel, amber, amber);

        var player = new GameObject("Player — Ornament gravity");
        var camera = Group("Player Camera", player.transform).gameObject.AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.nearClipPlane = .045f; camera.farClipPlane = 180; camera.fieldOfView = 76;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.007f, .013f, .03f);
        camera.gameObject.AddComponent<AudioListener>();
        var controller = player.AddComponent<CornerMaskTraversalController>();
        var settings = new SerializedObject(controller);
        settings.FindProperty("routeRoot").objectReferenceValue = routeNodes;
        settings.FindProperty("playerCamera").objectReferenceValue = camera;
        settings.FindProperty("startDistance").floatValue = 2;
        settings.FindProperty("platformHalfWidth").floatValue = HalfWidth;
        var array = settings.FindProperty("roundedCorners"); array.arraySize = corners.Count;
        for (int i = 0; i < corners.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = corners[i];
        var ornament = (GameObject)PrefabUtility.InstantiatePrefab(mask, scene);
        ornament.name = "Ornament mask — original supplied prefab";
        ornament.transform.SetParent(camera.transform, false);
        ornament.transform.localPosition = new Vector3(0, -.055f, .18f);
        settings.FindProperty("ornamentMask").objectReferenceValue = ornament;
        settings.ApplyModifiedPropertiesWithoutUndo();
        BuildMaskView(camera, ornament);
        controller.InitializeRoute();
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(s => s.path != ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
        AssetDatabase.SaveAssets();
        Debug.Log($"LONG_GRAVITY_BUILD_OK: {DistanceAt(path.Count - 1):F1} m, four gravity corners, two lateral bends, original Ornament mask.");
    }

    static void BuildPath()
    {
        path.Clear(); cornerStarts.Clear(); cornerEnds.Clear(); straightSections.Clear(); flatSections.Clear();
        path.Add(new Sample(new Vector3(0, .9f, 0), Vector3.up, Vector3.forward));
        Straight(14); Turn(1); Straight(7.5f);
        FlatTurn(1); Straight(8); Turn(1); Straight(10);
        Turn(1); Straight(24); FlatTurn(1); Straight(17); Turn(1); Straight(15);
    }
    static void Straight(float distance)
    {
        int start = path.Count - 1;
        var last = path[path.Count - 1]; int count = Mathf.CeilToInt(distance / .2f);
        for (int i = 1; i <= count; i++) path.Add(new Sample(last.point + last.forward * (distance * i / count), last.up, last.forward));
        straightSections.Add(new Vector2Int(start, path.Count - 1));
    }
    static void FlatTurn(int direction)
    {
        int start = path.Count - 1;
        var last = path[path.Count - 1];
        Vector3 center = last.point + Vector3.Cross(last.up, last.forward) * (6f * direction);
        for (int i = 1; i <= 64; i++)
        {
            var rotation = Quaternion.AngleAxis(90f * direction * i / 64, last.up);
            path.Add(new Sample(center + rotation * (last.point - center), last.up, rotation * last.forward));
        }
        flatSections.Add(new Vector2Int(start, path.Count - 1));
    }
    static void Turn(int direction)
    {
        var last = path[path.Count - 1]; cornerStarts.Add(path.Count - 1);
        Vector3 center = last.point + last.up * (Radius * direction);
        Vector3 axis = -Vector3.Cross(last.up, last.forward) * direction;
        for (int i = 1; i <= 40; i++)
        {
            var rotation = Quaternion.AngleAxis(90f * i / 40, axis);
            path.Add(new Sample(center + rotation * (last.point - center), rotation * last.up, rotation * last.forward));
        }
        cornerEnds.Add(path.Count - 1);
    }
    static float DistanceAt(int index)
    {
        float distance = 0;
        for (int i = 1; i <= index; i++) distance += Vector3.Distance(path[i - 1].point, path[i].point);
        return distance;
    }

    static void BuildTrack(Transform parent, List<Sample> samples, Material surface, Material guide, string assetName)
    {
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        float distance = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i]; var right = Vector3.Cross(s.up, s.forward).normalized;
            Vector3 top = s.point - s.up * BodyOffset, bottom = top - s.up * .32f;
            if (i > 0) distance += Vector3.Distance(samples[i - 1].point, s.point);
            vertices.AddRange(new[] { top - right * HalfWidth, top + right * HalfWidth, bottom - right * HalfWidth, bottom + right * HalfWidth,
                top - right * HalfWidth, bottom - right * HalfWidth, top + right * HalfWidth, bottom + right * HalfWidth });
            normals.AddRange(new[] { s.up, s.up, -s.up, -s.up, -right, -right, right, right });
            for (int k = 0; k < 8; k++) uv.Add(new Vector2(k % 2, distance * .25f));
            if (i == 0) continue;
            int a = (i - 1) * 8, b = i * 8;
            Quad(triangles, a, b, b + 1, a + 1);
            Quad(triangles, a + 2, a + 3, b + 3, b + 2);
            Quad(triangles, a + 4, a + 5, b + 5, b + 4);
            Quad(triangles, a + 6, b + 6, b + 7, a + 7);
        }
        // End caps only: no internal faces or collider seams across the walking surface.
        Quad(triangles, 0, 1, 3, 2);
        int end = (samples.Count - 1) * 8; Quad(triangles, end, end + 2, end + 3, end + 1);
        string meshPath = Root + "/Geometry/" + assetName + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        bool create = !mesh;
        if (create) mesh = new Mesh(); else mesh.Clear();
        mesh.name = assetName; mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, meshPath); else EditorUtility.SetDirty(mesh);
        var platform = Group("Continuous concrete floor and walls", parent).gameObject;
        platform.AddComponent<MeshFilter>().sharedMesh = mesh; platform.AddComponent<MeshRenderer>().sharedMaterial = surface;
        platform.AddComponent<MeshCollider>().sharedMesh = mesh;
    }
    static void BuildCornerPrefab(Material platform, Material guide)
    {
        var original = path[cornerStarts[0]];
        var samples = path.GetRange(cornerStarts[0], cornerEnds[0] - cornerStarts[0] + 1)
            .Select(s => new Sample(s.point - original.point + Vector3.up * BodyOffset, s.up, s.forward)).ToList();
        var corner = new GameObject("Rounded gravity corner — reusable");
        BuildTrack(corner.transform, samples, platform, guide, "RoundedCorner");
        var nodes = Group("Ordered editable route samples", corner.transform);
        for (int i = 0; i < samples.Count; i++)
        {
            var node = Group("Route " + i.ToString("D3"), nodes); node.localPosition = samples[i].point;
            node.localRotation = Quaternion.LookRotation(samples[i].forward, samples[i].up);
        }
        var marker = corner.AddComponent<RoundedCornerTransition>(); marker.axisBefore = "Y"; marker.axisAfter = "-Z";
        marker.startDistance = 0; marker.endDistance = DistanceAt(cornerEnds[0]) - DistanceAt(cornerStarts[0]);
        PrefabUtility.SaveAsPrefabAsset(corner, Root + "/Prefabs/RoundedGravityCorner.prefab");
        Object.DestroyImmediate(corner);
    }

    static void BuildDetails(Transform parent, Material steel, Material green, Material amber)
    {
        var moon = Group("Cold night light", parent); moon.rotation = Quaternion.Euler(35, -35, 0);
        var directional = moon.gameObject.AddComponent<Light>(); directional.type = LightType.Directional;
        directional.color = new Color(.42f, .58f, .82f); directional.intensity = .7f; directional.shadows = LightShadows.Soft;
        float nextLamp = 1;
        for (int i = 0; i < path.Count; i++)
        {
            float distance = DistanceAt(i); if (distance < nextLamp) continue; nextLamp = distance + 4;
            var s = path[i]; Vector3 right = Vector3.Cross(s.up, s.forward); int side = i % 2 == 0 ? -1 : 1;
            var mount = Group("Light aligned to local floor", parent); mount.position = s.point - s.up * BodyOffset; mount.rotation = Quaternion.LookRotation(s.forward, s.up);
            Box(mount, "Warm wall luminaire", new Vector3(side * (HalfWidth - .08f), 2.8f, 0), new Vector3(.12f, .14f, 1.4f), amber);
            var lamp = Group("Warm local pool of light", mount); lamp.localPosition = new Vector3(side * (HalfWidth - .8f), 3.1f, 0);
            var light = lamp.gameObject.AddComponent<Light>(); light.type = LightType.Point;
            light.color = new Color(1, .77f, .47f); light.intensity = 2.2f; light.range = 8; light.shadows = LightShadows.None;
        }
        Sign(parent, "03 / ГРАВИТАЦИЯ", "ОСТАНОВИСЬ И НАЖМИ M\nПОЛ ПРОДОЛЖАЕТСЯ НА СТЕНЕ", new Vector3(-3.5f, 2.1f, 4), Quaternion.identity, steel);
        string[] messages = { "НАДЕНЬ МАСКУ\nИ ПРОЙДИ ПО ИЗГИБУ", "ПУТЬ ПРОДОЛЖАЕТСЯ\nНА БОКОВОЙ СТЕНЕ", "ПРОЙДИ ПО СКРУГЛЕНИЮ\nИ ПРОДОЛЖАЙ СПУСК", "ВОЗВРАЩЕНИЕ НА ПОЛ\nПРОДОЛЖАЙ К ВЫХОДУ" };
        for (int i = 0; i < 4; i++)
        {
            var s = path[cornerStarts[i]]; Quaternion rotation = Quaternion.LookRotation(s.forward, s.up);
            Sign(parent, (i + 1).ToString("D2") + " / ПЕРЕХОД", messages[i], s.point - s.forward * 1.2f + Vector3.Cross(s.up, s.forward) * -3.2f + s.up * .7f, rotation, steel);
        }
        var last = path[path.Count - 1];
        var exit = Group("Exit aligned to final floor", parent); exit.position = last.point - last.up * BodyOffset;
        exit.rotation = Quaternion.LookRotation(last.forward, last.up);
        Sign(exit, "ВЫХОД", "МАРШРУТ ПРОЙДЕН\nТЕПЕРЬ МОЖНО ВЕРНУТЬСЯ ОБРАТНО", exit.TransformPoint(new Vector3(0, 2.3f, .1f)), exit.rotation, steel);
        foreach (int side in new[] { -1, 1 }) Box(exit, "Exit light jamb", new Vector3(side * 1.6f, 1.6f, .12f), new Vector3(.08f, 3.2f, .12f), amber);
        Box(exit, "Exit light lintel", new Vector3(0, 3.2f, .12f), new Vector3(3.25f, .08f, .12f), amber);
    }
    static void Sign(Transform parent, string title, string caption, Vector3 position, Quaternion rotation, Material backing)
    {
        var board = Group(title, parent); board.position = position; board.rotation = rotation;
        var plate = Box(board, "Sign backing", Vector3.zero, new Vector3(2.3f, 1.05f, .08f), backing);
        Object.DestroyImmediate(plate.GetComponent<Collider>());
        var label = Group("Instruction", board); label.localPosition = new Vector3(0, 0, -.051f);
        var text = label.gameObject.AddComponent<TextMesh>(); text.text = title + "\n\n" + caption;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.characterSize = .055f; text.fontSize = 64; text.color = new Color(.76f, 1, .8f);
        text.font.RequestCharactersInTexture(text.text, text.fontSize);
        var renderer = text.GetComponent<MeshRenderer>();
        string materialPath = Root + "/Materials/InstructionText.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material) { material = new Material(Shader.Find("MaskTraversal/DepthText")); AssetDatabase.CreateAsset(material, materialPath); }
        material.mainTexture = text.font.material.mainTexture;
        renderer.sharedMaterial = material;
        Vector3 textSize = renderer.localBounds.size;
        float scale = Mathf.Min(2.1f / Mathf.Max(.01f, textSize.x), .9f / Mathf.Max(.01f, textSize.y));
        label.localScale = Vector3.one * scale;
        EditorUtility.SetDirty(material);
    }
    static Material MaterialAsset(string name, Color color, float smoothness, Color? glow = null)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit"); if (!shader) shader = Shader.Find("Standard");
            material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path);
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", smoothness);
        if (glow.HasValue) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", glow.Value); }
        EditorUtility.SetDirty(material); return material;
    }
    static void BuildMaskView(Camera camera, GameObject ornament)
    {
        var view = camera.gameObject.AddComponent<OrnamentMaskView>();
        var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad); overlay.name = "Cosmic lens — visible only inside mask";
        Object.DestroyImmediate(overlay.GetComponent<Collider>()); overlay.transform.SetParent(camera.transform, false);
        string path = Root + "/Materials/CosmicLens.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(Shader.Find("MaskTraversal/CosmicMask")); AssetDatabase.CreateAsset(material, path); }
        var renderer = overlay.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        var lightObject = Group("Mask inner rim fill light", camera.transform); lightObject.localPosition = new Vector3(0, 0, -.035f);
        var light = lightObject.gameObject.AddComponent<Light>(); light.type = LightType.Point;
        light.range = .32f; light.intensity = .0015f; light.color = new Color(.65f, .69f, 1); light.shadows = LightShadows.None;
        var settings = new SerializedObject(view);
        settings.FindProperty("cosmicOverlay").objectReferenceValue = renderer;
        settings.FindProperty("interiorLight").objectReferenceValue = light;
        settings.ApplyModifiedPropertiesWithoutUndo();
    }
    static void EnsureFolder(string folder) { if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder); }
    static Transform Group(string name, Transform parent) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    static Transform Box(Transform parent, string name, Vector3 at, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent, false);
        box.transform.localPosition = at; box.transform.localScale = size; box.GetComponent<Renderer>().sharedMaterial = material; return box.transform;
    }
    static void Quad(List<int> triangles, int a, int b, int c, int d) { triangles.AddRange(new[] { a, b, c, a, c, d }); }
}

