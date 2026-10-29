using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Build the editable six-wall tutorial in the original level-one scene.</summary>
public static class MaskSixTrialBuilder
{
    const string ScenePath = "Assets/Scenes/Level01_Tutorial.unity";
    const string WallRoot = "Assets/MaskWalls";
    static Material concrete, metal, purple, dark, red, safety;
    static Transform scenery, wallGroup, guideGroup;

    [MenuItem("Tools/Mask Demo/Build Six-Trial Level")]
    public static void Build()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var scene = SceneManager.GetActiveScene();
        var playerObject = GameObject.Find("Player");
        var lightObject = GameObject.Find("Soft directional light");
        if (!playerObject || !lightObject) throw new Exception("Base player or light is missing from Level01_Tutorial.");
        foreach (var root in scene.GetRootGameObjects())
            if (root != playerObject && root != lightObject) UnityEngine.Object.DestroyImmediate(root);

        MakeFolders();
        concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskEnvironment/AgedConcrete.mat");
        metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskEnvironment/OldPaintedMetal.mat");
        purple = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskEnvironment/FadedPurplePaint.mat");
        dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskEnvironment/CharcoalInstructionPlates.mat");
        red = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskDemoMaterials/BlockedRed.mat");
        safety = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskEnvironment/FadedSafetyPaint.mat");
        if (!concrete || !metal || !purple || !dark) throw new Exception("Complex materials are missing.");

        scenery = new GameObject("Concrete complex / editable geometry").transform;
        wallGroup = Group(scenery, "WALLS / customize prefabs in Assets/MaskWalls");
        guideGroup = Group(scenery, "Signs and route markings");
        Transform floorGroup = Group(scenery, "Floors and ceilings");
        Transform propGroup = Group(scenery, "Worn details and lights");

        var player = playerObject.GetComponent<MaskDemoPlayer>();
        var axis = playerObject.GetComponentInChildren<MaskAxisCamera>(true);
        if (!player || !axis) throw new Exception("Player control components are missing.");
        playerObject.transform.SetPositionAndRotation(new Vector3(0, 1, -3.5f), Quaternion.identity);
        var maskConfig = new SerializedObject(axis);
        maskConfig.FindProperty("wallRenderers").arraySize = 0;
        maskConfig.FindProperty("wallColliders").arraySize = 0;
        maskConfig.FindProperty("orthographicSize").floatValue = 7.5f;
        maskConfig.FindProperty("cameraDistance").floatValue = 18f;
        maskConfig.FindProperty("playerTarget").objectReferenceValue = playerObject.transform;
        maskConfig.ApplyModifiedPropertiesWithoutUndo();

        // Wider main corridor and a long left branch. The open views for each
        // direction are local to that wall; unrelated concrete stays present.
        Renderer yBacking = Box(floorGroup, "Main floor A", new Vector3(0, -.25f, 6.5f), new Vector3(6.4f, .5f, 24f), concrete).GetComponent<Renderer>();
        Box(floorGroup, "Main floor B", new Vector3(0, -.25f, 16.5f), new Vector3(6.4f, .5f, 3f), concrete);
        Box(floorGroup, "-Y left footbridge", new Vector3(-1.8f, -.25f, 20), new Vector3(2.8f, .5f, 4f), concrete);
        Box(floorGroup, "-Y right footbridge", new Vector3(1.8f, -.25f, 20), new Vector3(2.8f, .5f, 4f), concrete);
        Box(floorGroup, "Main floor C", new Vector3(0, -.25f, 23.5f), new Vector3(6.4f, .5f, 3f), concrete);
        Box(floorGroup, "Long left branch floor", new Vector3(-12.75f, -.25f, 25), new Vector3(26f, .5f, 5.8f), concrete);
        Box(floorGroup, "Outside code alcove floor", new Vector3(-6.1f, -.25f, -2), new Vector3(5.1f, .5f, 6.5f), concrete);

        // +X uses the left wall as its backing; -X uses the right wall.
        Renderer xBacking = Box(wallGroup, "X / matching LEFT wall", new Vector3(-3.3f, 2, 2), new Vector3(.5f, 4.5f, 4.5f), concrete).GetComponent<Renderer>();
        Renderer minusXBacking = Box(wallGroup, "-X / matching RIGHT wall", new Vector3(3.3f, 2, 8), new Vector3(.5f, 4.5f, 4.5f), concrete).GetComponent<Renderer>();
        Box(wallGroup, "Left side / start", new Vector3(-3.3f, 2, -3.25f), new Vector3(.5f, 4.5f, 6f), concrete);
        Box(wallGroup, "Left side / middle", new Vector3(-3.3f, 2, 16f), new Vector3(.5f, 4.5f, 11.5f), concrete);
        Box(wallGroup, "Right side / approach", new Vector3(3.3f, 2, -3.25f), new Vector3(.5f, 4.5f, 6f), concrete);
        Box(wallGroup, "Right side / middle A", new Vector3(3.3f, 2, 5), new Vector3(.5f, 4.5f, 1.5f), concrete);
        Box(wallGroup, "Right side / middle B", new Vector3(3.3f, 2, 16.8f), new Vector3(.5f, 4.5f, 13.5f), concrete);
        Box(wallGroup, "Right side / turn", new Vector3(3.3f, 2, 25), new Vector3(.5f, 4.5f, 5.8f), concrete);
        Box(wallGroup, "Starting end wall", new Vector3(0, 2, -6.5f), new Vector3(6.6f, 4.5f, .5f), concrete);

        // Roof remains over the complex except at +Y. At -Y, the ceiling is
        // the visible backing while the central floor opening exposes the wall.
        Roof(floorGroup, -3, 9);
        Roof(floorGroup, 10, 12);
        Renderer minusYBacking = Roof(floorGroup, 16, 27.7f);
        Box(floorGroup, "Branch roof", new Vector3(-12.75f, 4.28f, 25), new Vector3(26f, .45f, 5.8f), concrete);
        Box(floorGroup, "Alcove roof", new Vector3(-6.1f, 4.28f, -2), new Vector3(5.1f, .45f, 6.5f), concrete);

        // Two side walls double as true projected backings for the Z views.
        Renderer zBacking = Box(wallGroup, "Z / near-side merge wall", new Vector3(-5.6f, 2, 21.95f), new Vector3(5.8f, 4.5f, .5f), concrete).GetComponent<Renderer>();
        Renderer minusZBacking = Box(wallGroup, "-Z / far-side merge wall", new Vector3(-11.6f, 2, 28.05f), new Vector3(5.8f, 4.5f, .5f), concrete).GetComponent<Renderer>();
        Box(wallGroup, "Branch near wall / between views", new Vector3(-9, 2, 21.95f), new Vector3(1.1f, 4.5f, .5f), concrete);
        Box(wallGroup, "Branch near wall / door section", new Vector3(-19, 2, 21.95f), new Vector3(8.3f, 4.5f, .5f), concrete);
        Box(wallGroup, "Branch far wall / turn", new Vector3(-.7f, 2, 28.05f), new Vector3(7.6f, 4.5f, .5f), concrete);
        Box(wallGroup, "Branch far wall / between views", new Vector3(-9, 2, 28.05f), new Vector3(1.1f, 4.5f, .5f), concrete);
        Box(wallGroup, "Branch far wall / door section", new Vector3(-19, 2, 28.05f), new Vector3(8.3f, 4.5f, .5f), concrete);
        Box(wallGroup, "Branch end wall", new Vector3(-25.95f, 2, 25), new Vector3(.5f, 4.5f, 6.6f), concrete);
        Box(wallGroup, "Alcove outer side", new Vector3(-8.9f, 2, -2), new Vector3(.5f, 4.5f, 6.5f), concrete);
        Box(wallGroup, "Alcove rear wall / code", new Vector3(-6.1f, 2, -5.45f), new Vector3(5.6f, 4.5f, .5f), concrete);
        Box(wallGroup, "Alcove front wall", new Vector3(-6.1f, 2, 1.45f), new Vector3(5.6f, 4.5f, .5f), concrete);

        var prefabs = new GameObject[6];
        string[] shortNames = { "X", "MinusX", "Y", "MinusY", "Z", "MinusZ" };
        for (int i = 0; i < 6; i++) prefabs[i] = MakeWallPrefab(shortNames[i]);
        Vector3[] positions = {
            new Vector3(0, 2, 2), new Vector3(0, 2, 8),
            new Vector3(0, 2, 14), new Vector3(0, 2, 20),
            new Vector3(-5.6f, 2, 25), new Vector3(-11.6f, 2, 25)
        };
        Vector3[] sizes = {
            new Vector3(6.4f, 4, .35f), new Vector3(6.4f, 4, .35f),
            new Vector3(6.4f, 4, .35f), new Vector3(6.4f, 4, .35f),
            new Vector3(.35f, 4, 5.8f), new Vector3(.35f, 4, 5.8f)
        };
        GameObject[] gates = new GameObject[6];
        Transform[] approaches = new Transform[6];
        Vector3[] approachPositions = {
            new Vector3(0, 1, -.5f), new Vector3(0, 1, 5.5f),
            new Vector3(0, 1, 11.5f), new Vector3(-1.8f, 1, 17.6f),
            new Vector3(-3.3f, 1, 25), new Vector3(-9.2f, 1, 25)
        };
        for (int i = 0; i < 6; i++)
        {
            gates[i] = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
            gates[i].name = "TRIAL " + (i + 1) + " / " + shortNames[i] + " wall";
            gates[i].transform.SetParent(wallGroup, false);
            gates[i].transform.position = positions[i];
            gates[i].transform.localScale = sizes[i];
            approaches[i] = Point(guideGroup, "Room " + (i + 1) + " / respawn before " + shortNames[i], approachPositions[i]);
        }

        // Low wear and a few dim fixtures; light is kept away from merge faces.
        for (float z = -4; z <= 23; z += 3)
            Box(guideGroup, "Faded floor edge", new Vector3(2.7f, .012f, z), new Vector3(.05f, .015f, .5f), safety, false);
        for (float x = -22; x <= -2; x += 3)
            Box(guideGroup, "Faded branch edge", new Vector3(x, .012f, 27.45f), new Vector3(.5f, .015f, .05f), safety, false);
        for (float z = -4; z <= 22; z += 6)
        {
            Box(propGroup, "Old service conduit", new Vector3(-2.98f, 3.75f, z), new Vector3(.08f, .08f, 1.1f), metal, false);
            Box(propGroup, "Worn light housing", new Vector3(2.96f, 3.3f, z), new Vector3(.17f, .32f, .68f), metal, false);
        }
        Sign(guideGroup, "X / left wall", new Vector3(-2.99f, 2.3f, -.6f), new Vector3(0, 1.8f, -.6f),
            "01  X\n1 + маска\nК стене вплотную");
        Sign(guideGroup, "-X / right wall", new Vector3(2.99f, 2.3f, 6.2f), new Vector3(0, 1.8f, 6.2f),
            "02  −X\nНажмите 1 ещё раз");
        Sign(guideGroup, "Y / floor", new Vector3(0, .035f, 11.4f), new Vector3(0, 2, 11.4f),
            "03  Y\n2 · вид сверху", true);
        Sign(guideGroup, "-Y / before footbridge", new Vector3(-2.97f, 2.25f, 17.3f), new Vector3(0, 1.8f, 17.3f),
            "04  −Y\n2 ещё раз · снизу\nИдите по боковому мосту");
        Sign(guideGroup, "Z / before left turn", new Vector3(2.95f, 2.25f, 23.1f), new Vector3(0, 1.8f, 23.1f),
            "05  Z\nПоворот налево\n3 · вид спереди");
        Sign(guideGroup, "-Z / opposite view", new Vector3(-8.9f, 2.25f, 27.7f), new Vector3(-8.9f, 1.8f, 25),
            "06  −Z\n3 ещё раз · вид сзади");

        Transform start = Point(guideGroup, "START", new Vector3(0, 1, -3.5f));
        Transform codeArrival = Point(guideGroup, "Arrival / hidden code outside main route", new Vector3(-6.1f, 1, -.2f));
        Transform finalArrival = Point(guideGroup, "Arrival / back to final room", new Vector3(-15f, 1, 25));
        Transform toCode = Portal(propGroup, "Return portal / final to hidden code", new Vector3(-13f, 1, 25));
        Transform fromCode = Portal(propGroup, "Return portal / code to final", new Vector3(-6.1f, 1, .3f));
        Sign(guideGroup, "Outside code direction", new Vector3(-6.1f, 3.1f, -5.06f), new Vector3(-6.1f, 1.8f, -2),
            "СЕКРЕТ ЗА СТЕНОЙ");
        Box(propGroup, "Code plaque", new Vector3(-6.1f, 1.72f, -5.08f), new Vector3(3.3f, 1.1f, .08f), dark, false);
        TextMesh codeText = Sign(guideGroup, "RANDOM CODE / revealed after all six walls", new Vector3(-6.1f, 1.72f, -4.99f),
            new Vector3(-6.1f, 1.7f, -2), "КОД: 0000", false, .2f);
        GameObject cover = Box(propGroup, "Paint covering code until completion", new Vector3(-6.1f, 1.72f, -4.86f),
            new Vector3(3f, .75f, .06f), dark, false);
        for (int i = 0; i < 4; i++)
            Box(cover.transform, "Smudged black paint", new Vector3(-.37f + i * .24f, (i % 2 == 0 ? .2f : -.2f), -.55f),
                new Vector3(.22f, .17f, .4f), metal, false, true);

        GameObject door = Box(propGroup, "FINAL PASSWORD DOOR", new Vector3(-17.2f, 2, 25), new Vector3(.38f, 4, 5.4f), metal);
        Box(propGroup, "Door lintel", new Vector3(-17.2f, 4.15f, 25), new Vector3(.6f, .25f, 5.7f), dark);
        Box(propGroup, "Door warning stripe", new Vector3(-16.97f, 2.3f, 25), new Vector3(.03f, .2f, 4.6f), purple, false);
        Sign(guideGroup, "Password prompt", new Vector3(-16.85f, 3.1f, 25), new Vector3(-14.8f, 2, 25),
            "КОДОВАЯ ДВЕРЬ\nE — ПАРОЛЬ");
        Transform goal = Point(guideGroup, "VICTORY TRIGGER", new Vector3(-22.4f, 1, 25));
        Box(propGroup, "Victory beacon", new Vector3(-22.4f, .07f, 25), new Vector3(1.6f, .1f, 1.6f), purple, false);

        var controller = new GameObject("SIX TRIALS / progression, respawn, code, door").AddComponent<MaskSixTrialLevel>();
        var so = new SerializedObject(controller);
        so.FindProperty("mask").objectReferenceValue = axis;
        so.FindProperty("player").objectReferenceValue = player;
        var serializedTrials = so.FindProperty("trials");
        serializedTrials.arraySize = 6;
        Renderer[] backs = { xBacking, minusXBacking, yBacking, minusYBacking, zBacking, minusZBacking };
        for (int i = 0; i < 6; i++)
        {
            var element = serializedTrials.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("direction").enumValueIndex = i;
            element.FindPropertyRelative("obstacle").objectReferenceValue = gates[i];
            element.FindPropertyRelative("mergeSurface").objectReferenceValue = backs[i];
            element.FindPropertyRelative("appearanceReference").objectReferenceValue = backs[i];
            element.FindPropertyRelative("travelDirection").vector3Value = i < 4 ? Vector3.forward : Vector3.left;
            element.FindPropertyRelative("approachPoint").objectReferenceValue = approaches[i];
        }
        so.FindProperty("startingPoint").objectReferenceValue = start;
        so.FindProperty("codeRoomArrival").objectReferenceValue = codeArrival;
        so.FindProperty("finalRoomArrival").objectReferenceValue = finalArrival;
        so.FindProperty("toCodePortal").objectReferenceValue = toCode;
        so.FindProperty("fromCodePortal").objectReferenceValue = fromCode;
        so.FindProperty("codeText").objectReferenceValue = codeText;
        so.FindProperty("paintCover").objectReferenceValue = cover;
        so.FindProperty("door").objectReferenceValue = door.transform;
        so.FindProperty("doorCollider").objectReferenceValue = door.GetComponent<Collider>();
        so.FindProperty("victoryArea").objectReferenceValue = goal;
        so.ApplyModifiedPropertiesWithoutUndo();

        lightObject.GetComponent<Light>().intensity = .8f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.26f, .29f, .34f);
        RenderSettings.ambientEquatorColor = new Color(.18f, .21f, .25f);
        RenderSettings.ambientGroundColor = new Color(.11f, .12f, .14f);
        var camera = playerObject.GetComponentInChildren<Camera>(true);
        if (camera) { camera.backgroundColor = new Color(.025f, .03f, .04f); camera.farClipPlane = 150f; }
        VerifyGeometry(gates, backs, approaches);
        MaskLevelRevision.ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("SIX_TRIAL_LEVEL_BUILT");
    }

    static void VerifyGeometry(GameObject[] gates, Renderer[] backings, Transform[] approaches)
    {
        Physics.SyncTransforms();
        Vector3[] viewRays = {
            Vector3.left, Vector3.right, Vector3.down, Vector3.up,
            Vector3.back, Vector3.forward
        };
        for (int i = 0; i < gates.Length; i++)
        {
            var collider = gates[i].GetComponent<Collider>();
            if (!collider || !backings[i] || !approaches[i])
                throw new Exception("Trial " + i + " has a missing scene reference.");
            Bounds b = collider.bounds;
            Vector3 direction = viewRays[i];
            var ray = new Ray(b.center - direction * 12f, direction);
            RaycastHit[] hits = Physics.RaycastAll(ray, 15f, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, c) => a.distance.CompareTo(c.distance));
            if (hits.Length == 0 || hits[0].collider != collider)
                throw new Exception("Trial " + i + " is visually blocked before its wall by " +
                    (hits.Length == 0 ? "empty space" : hits[0].collider.name));
            float depth = Mathf.Abs(direction.x) * b.extents.x + Mathf.Abs(direction.y) * b.extents.y +
                Mathf.Abs(direction.z) * b.extents.z;
            if (!backings[i].bounds.IntersectRay(new Ray(b.center + direction * (depth + .04f), direction), out _))
                throw new Exception("Trial " + i + " has no matching projected backing.");
            if (!Physics.Raycast(approaches[i].position + Vector3.up * 2f, Vector3.down,
                out var floor, 4f, ~0, QueryTriggerInteraction.Ignore) || floor.collider.name.Contains("wall"))
                throw new Exception("Trial " + i + " has no safe approach floor.");
        }
        if (Physics.Raycast(new Vector3(0, -.7f, 20), Vector3.up,
            out var underside, 1f, ~0, QueryTriggerInteraction.Ignore) &&
            underside.collider.name.Contains("floor"))
            throw new Exception("-Y center opening is covered; the lower camera cannot see its wall.");
        Debug.Log("SIX_TRIAL_GEOMETRY_VERIFIED");
    }

    static void MakeFolders()
    {
        if (!AssetDatabase.IsValidFolder(WallRoot)) AssetDatabase.CreateFolder("Assets", "MaskWalls");
        if (!AssetDatabase.IsValidFolder(WallRoot + "/Prefabs")) AssetDatabase.CreateFolder(WallRoot, "Prefabs");
        if (!AssetDatabase.IsValidFolder(WallRoot + "/Materials")) AssetDatabase.CreateFolder(WallRoot, "Materials");
    }

    static GameObject MakeWallPrefab(string id)
    {
        string materialPath = WallRoot + "/Materials/Wall_" + id + ".mat";
        if (!AssetDatabase.LoadAssetAtPath<Material>(materialPath))
            AssetDatabase.CopyAsset("Assets/MaskEnvironment/AgedConcrete.mat", materialPath);
        Material unique = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        template.name = "Wall_" + id;
        template.GetComponent<Renderer>().sharedMaterial = unique;
        template.AddComponent<MaskProximityWall>();
        string path = WallRoot + "/Prefabs/Wall_" + id + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(template, path);
        UnityEngine.Object.DestroyImmediate(template);
        return prefab;
    }

    static Transform Group(Transform parent, string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    static Transform Point(Transform parent, string name, Vector3 position)
    {
        var point = new GameObject(name).transform;
        point.SetParent(parent, false);
        point.position = position;
        return point;
    }

    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material,
        bool collider = true, bool local = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        if (local) go.transform.localPosition = position; else go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static Renderer Roof(Transform parent, float fromZ, float toZ)
    {
        return Box(parent, "Main roof " + fromZ.ToString("F1") + "-" + toZ.ToString("F1"),
            new Vector3(0, 4.28f, (fromZ + toZ) / 2), new Vector3(6.4f, .45f, toZ - fromZ), concrete).GetComponent<Renderer>();
    }

    static TextMesh Sign(Transform parent, string name, Vector3 position, Vector3 viewer, string message,
        bool horizontal = false, float size = .13f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = horizontal ? Quaternion.LookRotation(Vector3.up, Vector3.forward) :
            Quaternion.LookRotation(viewer - position, Vector3.up);
        var text = go.AddComponent<TextMesh>();
        text.text = message;
        text.characterSize = size;
        text.fontSize = 46;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = new Color(.86f, .82f, .96f);
        return text;
    }

    static Transform Portal(Transform parent, string name, Vector3 position)
    {
        Transform root = Point(parent, name, position);
        Box(root, "Purple interaction pad", new Vector3(0, -1f, 0), new Vector3(1.65f, .08f, 1.65f), purple, false, true);
        Box(root, "Upright", new Vector3(-.78f, .45f, 0), new Vector3(.14f, 2.5f, .14f), purple, false, true);
        Box(root, "Upright", new Vector3(.78f, .45f, 0), new Vector3(.14f, 2.5f, .14f), purple, false, true);
        Box(root, "Lintel", new Vector3(0, 1.7f, 0), new Vector3(1.7f, .14f, .14f), purple, false, true);
        return root;
    }
}
