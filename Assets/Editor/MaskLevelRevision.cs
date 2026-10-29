using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Update the existing layout without rebuilding the player or remaining walls.</summary>
public static class MaskLevelRevision
{
    const string ScenePath = "Assets/Scenes/Level01_Tutorial.unity";
    const string FloorPath = "Assets/MaskEnvironment/Level01Floor.asset";

    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("FIVE_WALL_REVISION_SAVED");
    }

    public static void ApplyToOpenScene()
    {
        var level = UnityEngine.Object.FindAnyObjectByType<MaskSixTrialLevel>();
        if (!level) throw new Exception("Tutorial controller missing.");
        var config = new SerializedObject(level);
        var trials = config.FindProperty("trials");
        for (int i = trials.arraySize - 1; i >= 0; i--)
        {
            var trial = trials.GetArrayElementAtIndex(i);
            if (trial.FindPropertyRelative("direction").enumValueIndex != (int)MaskAxisCamera.ViewDirection.MinusY) continue;
            var obstacle = trial.FindPropertyRelative("obstacle").objectReferenceValue as GameObject;
            var approach = trial.FindPropertyRelative("approachPoint").objectReferenceValue as Transform;
            if (obstacle) UnityEngine.Object.DestroyImmediate(obstacle);
            if (approach) UnityEngine.Object.DestroyImmediate(approach.gameObject);
            trials.DeleteArrayElementAtIndex(i);
        }
        config.ApplyModifiedPropertiesWithoutUndo();

        string[] obsolete = {
            "Main floor A", "Main floor B", "Main floor C", "-Y left footbridge", "-Y right footbridge",
            "Long left branch floor", "Outside code alcove floor", "Alcove roof", "Alcove outer side",
            "Alcove rear wall / code", "Alcove front wall", "Outside code direction", "-Y / before footbridge"
        };
        foreach (string name in obsolete) Remove(name);
        var floorGroup = Find("Floors and ceilings").transform;
        var floor = CreateFloor(floorGroup);
        config.Update();
        trials = config.FindProperty("trials");
        for (int i = 0; i < trials.arraySize; i++)
        {
            var trial = trials.GetArrayElementAtIndex(i);
            var direction = (MaskAxisCamera.ViewDirection)trial.FindPropertyRelative("direction").enumValueIndex;
            var obstacle = (GameObject)trial.FindPropertyRelative("obstacle").objectReferenceValue;
            obstacle.name = "TRIAL " + (i + 1) + " / " + direction + " wall";
            if (direction == MaskAxisCamera.ViewDirection.Y)
            {
                trial.FindPropertyRelative("mergeSurface").objectReferenceValue = floor;
                trial.FindPropertyRelative("appearanceReference").objectReferenceValue = floor;
            }
        }
        level.name = "FIVE WALLS / progression, respawn, underside code, door";
        var start = (Transform)config.FindProperty("startingPoint").objectReferenceValue;
        var arrival = (Transform)config.FindProperty("codeRoomArrival").objectReferenceValue;
        arrival.name = "Arrival / spawn above underside code";
        arrival.SetPositionAndRotation(start.position, start.rotation);
        var portal = (Transform)config.FindProperty("fromCodePortal").objectReferenceValue;
        portal.name = "Return portal / spawn to final door";
        portal.position = new Vector3(1.75f, 1, -4.6f);

        var text = (TextMesh)config.FindProperty("codeText").objectReferenceValue;
        text.name = "RANDOM CODE / outside below spawn / MinusY only";
        text.transform.SetPositionAndRotation(new Vector3(0, -.555f, -3.5f),
            Quaternion.LookRotation(Vector3.up, Vector3.back));
        text.characterSize = .105f;
        text.fontSize = 64;
        text.text = "КОД: ????";
        text.color = new Color(.87f, .79f, 1);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.GetComponent<Renderer>().sharedMaterial = text.font.material;
        var worldText = text.GetComponent<MaskWorldText>();
        if (!worldText) worldText = text.gameObject.AddComponent<MaskWorldText>();
        worldText.Configure(AssetDatabase.LoadAssetAtPath<Shader>("Assets/MaskEnvironment/WorldStencilText.shader"));
        text.gameObject.SetActive(false);

        var plaque = Find("Code plaque");
        plaque.transform.position = new Vector3(0, -.52f, -3.5f);
        plaque.transform.rotation = Quaternion.identity;
        plaque.transform.localScale = new Vector3(3.6f, .03f, 1.3f);
        var cover = (GameObject)config.FindProperty("paintCover").objectReferenceValue;
        for (int i = cover.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(cover.transform.GetChild(i).gameObject);
        cover.transform.SetPositionAndRotation(new Vector3(0, -.57f, -3.5f), Quaternion.identity);
        cover.transform.localScale = new Vector3(3.5f, .025f, 1.25f);
        cover.SetActive(true);
        config.ApplyModifiedPropertiesWithoutUndo();

        SetSign("Z / before left turn", "04  Z\nПоворот налево\n3 · вид спереди");
        SetSign("-Z / opposite view", "05  −Z\n3 ещё раз · вид сзади");
        FixSign("X / left wall", Quaternion.LookRotation(Vector3.left));
        FixSign("-X / right wall", Quaternion.LookRotation(Vector3.right));
        FixSign("Y / floor", Quaternion.LookRotation(Vector3.down, Vector3.forward));
        FixSign("Z / before left turn", Quaternion.LookRotation(Vector3.right));
        FixSign("-Z / opposite view", Quaternion.LookRotation(Vector3.forward));
        FixSign("Password prompt", Quaternion.LookRotation(Vector3.left));
        var guide = Find("Signs and route markings").transform;
        AddSign(guide, "Underside clue hint", new Vector3(-2.98f, 2.2f, -3.3f),
            Quaternion.LookRotation(Vector3.left), "СЕКРЕТ ПОД СПАВНОМ\nПосле испытаний: 2 → −Y\nСмотри снизу на платформу");
        AddSign(guide, "Return journey hint", new Vector3(-15.3f, 2.6f, 27.76f),
            Quaternion.LookRotation(Vector3.forward), "КОД ПОД СТАРТОМ\nE у портала — к спавну\nМаска: 2 → −Y");

        Physics.SyncTransforms();
        if (trials.arraySize != 5) throw new Exception("Expected five remaining walls.");
        Vector3[] points = { new Vector3(0, 1, -3.5f), new Vector3(0, 1, 17), new Vector3(0, 1, 20),
            new Vector3(0, 1, 22.1f), new Vector3(-3.2f, 1, 25), new Vector3(-14, 1, 25) };
        foreach (var point in points)
            if (!Physics.Raycast(point, Vector3.down, out var hit, 2f) ||
                Mathf.Abs(hit.point.y) > .025f || hit.collider != floor.GetComponent<Collider>())
                throw new Exception("Continuous floor verification failed at " + point);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("FIVE_WALL_GEOMETRY_VERIFIED");
    }

    static Renderer CreateFloor(Transform parent)
    {
        Remove("Continuous concrete floor / flat top Y=0");
        // Shared coplanar vertices across the turn: no overlapping slabs or raised joints.
        Vector2[] outline = {
            new Vector2(-3.2f,-6.25f), new Vector2(3.2f,-6.25f), new Vector2(3.2f,22.1f),
            new Vector2(3.2f,27.9f), new Vector2(-25.75f,27.9f), new Vector2(-25.75f,22.1f),
            new Vector2(-3.2f,22.1f)
        };
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
        foreach (float y in new[] { 0f, -.5f })
            foreach (var p in outline) { vertices.Add(new Vector3(p.x,y,p.y)); uv.Add(p * .35f); }
        int[] top = { 0,6,2, 0,2,1, 6,5,4, 6,4,3, 6,3,2 };
        triangles.AddRange(top);
        for (int i = 0; i < top.Length; i += 3)
        { triangles.Add(top[i]+7); triangles.Add(top[i+2]+7); triangles.Add(top[i+1]+7); }
        for (int i = 0; i < outline.Length; i++)
        {
            int next = (i+1) % outline.Length, n = vertices.Count;
            Vector2 a = outline[i], b = outline[next];
            vertices.AddRange(new[] { new Vector3(a.x,0,a.y),new Vector3(b.x,0,b.y),
                new Vector3(b.x,-.5f,b.y),new Vector3(a.x,-.5f,a.y) });
            float length = Vector2.Distance(a,b)*.35f;
            uv.AddRange(new[] { Vector2.zero, new Vector2(length,0),new Vector2(length,.175f),new Vector2(0,.175f) });
            triangles.AddRange(new[] { n,n+1,n+2,n,n+2,n+3 });
        }
        var mesh = new Mesh { name = "Continuous L-shaped concrete slab" };
        mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(FloorPath);
        if (existing) { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; }
        else AssetDatabase.CreateAsset(mesh,FloorPath);
        var go = new GameObject("Continuous concrete floor / flat top Y=0");
        go.transform.SetParent(parent,false); go.isStatic=true;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskEnvironment/AgedConcrete.mat");
        go.AddComponent<MeshCollider>().sharedMesh=mesh;
        return renderer;
    }

    static GameObject Find(string name)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform.gameObject;
        return null;
    }
    static void Remove(string name) { var go=Find(name); if(go) UnityEngine.Object.DestroyImmediate(go); }
    static void SetSign(string name, string message) { var go=Find(name); if(go) go.GetComponent<TextMesh>().text=message; }
    static void FixSign(string name, Quaternion rotation)
    {
        var go=Find(name);if(!go)return;
        go.transform.rotation=rotation;
        var text=go.GetComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var world=go.GetComponent<MaskWorldText>();
        if(!world) { go.GetComponent<Renderer>().sharedMaterial=text.font.material; world=go.AddComponent<MaskWorldText>(); }
        world.Configure(AssetDatabase.LoadAssetAtPath<Shader>("Assets/MaskEnvironment/WorldStencilText.shader"));
    }
    static void AddSign(Transform parent,string name,Vector3 position,Quaternion rotation,string message)
    {
        Remove(name);
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(position,rotation);
        var text=go.AddComponent<TextMesh>();text.text=message;text.fontSize=48;text.characterSize=.12f;
        text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
        text.color=new Color(.85f,.79f,.94f);text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<Renderer>().sharedMaterial=text.font.material;
        go.AddComponent<MaskWorldText>().Configure(AssetDatabase.LoadAssetAtPath<Shader>("Assets/MaskEnvironment/WorldStencilText.shader"));
    }
}
