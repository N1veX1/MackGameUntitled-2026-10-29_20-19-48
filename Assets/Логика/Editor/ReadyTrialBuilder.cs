using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Standalone rooms using existing mask behaviours; never saves tutorial scenes.</summary>
public static class ReadyTrialBuilder
{
    public const string Kit = "Assets/Логика/";
    public static readonly string[] AxisNames = { "Испытание 01 — Стена X", "Испытание 02 — Стена -X", "Испытание 03 — Стена Y", "Испытание 04 — Осмотр -Y", "Испытание 05 — Стена Z", "Испытание 06 — Стена -Z" };
    public static readonly string[] InversionNames = { "Испытание 01 — Надеть маску", "Испытание 02 — Снять маску", "Испытание 03 — Мост из корней", "Испытание 04 — Мост офиса", "Испытание 05 — Чередование стен и мостов" };
    public static readonly string[] GravityNames = { "Испытание 01 — Подъём на стену", "Испытание 02 — Спуск со стены", "Испытание 03 — Правый поворот и подъём", "Испытание 04 — Левый поворот и подъём" };
    static Material concrete, dark, accent, textMaterial;
    static Font font;
    static Transform root, floors, obstacles, decor;

    [MenuItem("Tools/Логика/Create ready trials for levels 1–3")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) return;
        var previous = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(previous.path)) throw new InvalidOperationException("Сначала сохраните текущую сцену. Готовые префабы уже можно брать из папки Логика.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskEnvironment/AgedConcrete.mat");
            dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskResize/Materials/DarkSteel.mat");
            accent = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskResize/Materials/ResizeMark.mat");
            textMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskResize/Materials/InstructionText.mat");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < 6; i++) if (!Exists(1, AxisNames[i])) Axis(i);
            for (int i = 0; i < 5; i++) if (!Exists(2, InversionNames[i])) Inversion(i);
            for (int i = 0; i < 4; i++) if (!Exists(3, GravityNames[i])) Gravity(i);
            for (int i = 1; i <= 3; i++) if (!Exists(i, "Старт — готовый пример")) Starter(i);
            AssetDatabase.SaveAssets();
            Debug.Log("READY_TRIALS_BUILT: 6 XYZ rooms, 5 inversion rooms, 4 gravity courses and 3 playable starters; source scenes unchanged.");
        }
        finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
    }
    public static string PathFor(int level, string name) => Kit + level + " уровень/" + name + ".prefab";
    static bool Exists(int level, string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PathFor(level, name));
    static void Begin(string name)
    {
        root = new GameObject(name).transform;
        floors = Group("01 — Пол и площадки", root); obstacles = Group("02 — Препятствия и стены", root); decor = Group("03 — Подсказки и свет", root);
    }
    static void Save(int level, string name)
    {
        if (!PrefabUtility.SaveAsPrefabAsset(root.gameObject, PathFor(level, name))) throw new Exception("Failed to save " + name);
        Object.DestroyImmediate(root.gameObject);
    }
    static void Axis(int index)
    {
        Begin(AxisNames[index]);
        if (index == 3)
        {
            Floor(0, 12, 6.6f);
            Label("Надпись под площадкой", "КЛЮЧ: 314", new Vector3(-1.7f, -.51f, 5), Quaternion.Euler(-90, 0, 0), .08f);
            Board("ОСМОТР СНИЗУ · -Y", "M — надень маску\n2 — выбери Y, ещё раз 2 — -Y\nНайди надпись под площадкой", new Vector3(-3.45f, 2.3f, 2));
            Markers(new Vector3(0, 0, 12)); Save(1, AxisNames[index]); return;
        }
        string[] suffix = { "X", "-X", "Y", "-Y", "Z", "-Z" };
        var wall = Copy(Kit + "1 уровень/Стена_" + suffix[index] + ".prefab", obstacles);
        var asset = wall.GetComponent<AxisWallAsset>();
        asset.mask = null; asset.player = null;
        bool z = index >= 4;
        wall.transform.localPosition = z ? new Vector3(6, 0, 0) : new Vector3(0, 0, 6);
        if (index == 2)
        {
            Floor(0, 4, 6.6f); Floor(8, 12, 6.6f);
            asset.mergeSurface.transform.localPosition = new Vector3(0, -.25f, 0);
            asset.mergeSurface.transform.localScale = new Vector3(6.6f, .5f, 4);
        }
        else if (z) Box(floors, "Пол вдоль X", new Vector3(6, -.25f, 0), new Vector3(12, .5f, 6.6f), concrete);
        else Floor(0, 12, 6.6f);
        // Keep the camera-facing side open. An opaque shell here would hide the projection.
        Board("СТЕНА " + suffix[index], "M — надень маску\n" + (index < 2 ? "1" : index < 4 ? "2" : "3") + " — выбери " + suffix[index] + "\nПодойди вплотную после остановки камеры\nВ другой оси стена возвращается", z ? new Vector3(2, 2.3f, -3.45f) : new Vector3(-3.45f, 2.3f, 2), z);
        Markers(z ? new Vector3(12, 0, 0) : new Vector3(0, 0, 12), z ? Quaternion.Euler(0, 90, 0) : Quaternion.identity);
        Save(1, AxisNames[index]);
    }
    static void Inversion(int index)
    {
        Begin(InversionNames[index]); float length = index == 4 ? 28 : 12;
        if (index < 2) { Floor(0, 12); Gate(index == 0, 6); }
        else if (index < 4) { Floor(0, 4); Floor(6.8f, 12); Bridge(index == 3, 5.4f); }
        else
        {
            Floor(0, 8); Floor(10.8f, 18); Floor(20.8f, 28);
            Gate(true, 5); Bridge(false, 9.4f); Gate(false, 15); Bridge(true, 19.4f);
            Checkpoint(new Vector3(0, 0, 12), false, "Промежуточная площадка");
        }
        foreach (int side in new[] { -1, 1 }) Box(obstacles, "Боковая стена", new Vector3(side * 3.45f, 2.2f, length * .5f), new Vector3(.3f, 4.4f, length), concrete);
        Box(obstacles, "Потолок", new Vector3(0, 4.65f, length * .5f), new Vector3(7.2f, .5f, length), concrete);
        string message = index == 0 ? "Перед тобой стена\nM — надень маску и пройди\nСнимешь маску — стена вернётся" : index == 1 ?
            "Стена существует только в маске\nСними её, чтобы пройти\nНе возвращай стену внутри себя" : index == 2 ?
            "Корни держат тебя без маски\nСними маску перед пропастью\nСмена состояния убирает мост" : index == 3 ?
            "Целый пол существует в маске\nНадень её перед пропастью\nБез маски опора исчезнет" :
            "Стена: надень · мост: сними\nСледующая стена: сними\nПоследний мост: надень\nМеняй состояние на площадках";
        Board(InversionNames[index], message, new Vector3(-3.27f, 2.3f, 2));
        Markers(new Vector3(0, 0, length));
        var zone = Group("Зона падения", root); zone.localPosition = new Vector3(0, -3, length * .5f);
        var trigger = zone.gameObject.AddComponent<BoxCollider>(); trigger.size = new Vector3(12, 1, length + 6); trigger.isTrigger = true;
        zone.gameObject.AddComponent<LogicTrialFallZone>();
        Lamp(new Vector3(0, 3.8f, 2)); Lamp(new Vector3(0, 3.8f, length - 2)); Save(2, InversionNames[index]);
    }
    static void Gate(bool openOn, float z)
    {
        var gate = Copy(Kit + "2 уровень/" + (openOn ? "Стена_видна_без_маски" : "Стена_видна_в_маске") + ".prefab", obstacles);
        gate.transform.localPosition = new Vector3(0, 0, z);
        gate.GetComponent<MaskInversionGate>().Configure(MaskInversionGate.GateKind.Wall, openOn);
    }
    static void Bridge(bool on, float z)
    {
        var bridge = Copy(Kit + "2 уровень/" + (on ? "Мост_в_маске" : "Мост_без_маски") + ".prefab", obstacles);
        bridge.transform.localPosition = new Vector3(0, -.25f, z);
        bridge.GetComponent<MaskInversionGate>().Configure(MaskInversionGate.GateKind.Bridge, on);
    }
    struct Sample { public Vector3 p; public Quaternion q; public Sample(Transform t) { p = t.position; q = t.rotation; } }
    static void Gravity(int index)
    {
        Begin(GravityNames[index]);
        string[] turns = { "Вверх", "Вниз", "Направо", "Налево" };
        var temp = Group("Temporary source pieces", root);
        var parts = new List<GravityRoutePiece>();
        foreach (string name in new[] { "Прямая", turns[index], "Прямая" }) parts.Add(Copy(Kit + "3 уровень/" + name + ".prefab", temp).GetComponent<GravityRoutePiece>());
        for (int i = 1; i < parts.Count; i++)
        {
            Quaternion delta = parts[i - 1].Exit.rotation * Quaternion.Inverse(parts[i].Entry.rotation);
            parts[i].transform.rotation = delta * parts[i].transform.rotation;
            parts[i].transform.position += parts[i - 1].Exit.position - parts[i].Entry.position;
        }
        var samples = new List<Sample>(); float distance = 0, cornerStart = 0, cornerEnd = 0;
        foreach (var part in parts)
        {
            float offset = distance;
            for (int j = 0; j < part.samples.childCount; j++)
            {
                if (j == 0 && samples.Count > 0) continue;
                var s = new Sample(part.samples.GetChild(j));
                if (samples.Count > 0) distance += Vector3.Distance(samples[samples.Count - 1].p, s.p);
                samples.Add(s);
            }
            if (part.corner) { cornerStart = offset + part.corner.startDistance; cornerEnd = offset + part.corner.endDistance; }
        }
        Object.DestroyImmediate(temp.gameObject);
        var piece = root.gameObject.AddComponent<GravityRoutePiece>(); piece.samples = Group("Route samples", root);
        for (int i = 0; i < samples.Count; i++)
        { var node = Group("Route " + i.ToString("D4"), piece.samples); node.localPosition = samples[i].p; node.localRotation = samples[i].q; }
        piece.corner = root.gameObject.AddComponent<RoundedCornerTransition>(); piece.corner.startDistance = cornerStart; piece.corner.endDistance = cornerEnd;
        piece.corner.transitionName = turns[index];
        int start = 0, end = samples.Count - 1; float cumulative = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            cumulative += Vector3.Distance(samples[i - 1].p, samples[i].p);
            if (Mathf.Abs(cumulative - cornerStart) < .01f) start = i;
            if (Mathf.Abs(cumulative - cornerEnd) < .01f) end = i;
        }
        MeshPart(index, "Пол до перехода", samples.GetRange(0, start + 1), floors);
        MeshPart(index, "Скруглённый переход", samples.GetRange(start, end - start + 1), obstacles);
        MeshPart(index, "Пол после перехода", samples.GetRange(end, samples.Count - end), floors);
        // Wall sections follow the local floor; never rotate the global XYZ axes.
        for (int i = 0; i < samples.Count - 1; i += 5)
        {
            int next = Mathf.Min(i + 5, samples.Count - 1); var a = samples[i]; var b = samples[next];
            var frame = Quaternion.Slerp(a.q, b.q, .5f); Vector3 center = (a.p + b.p) * .5f;
            float length = Vector3.Distance(a.p, b.p) + .025f;
            foreach (int side in new[] { -1, 1 })
            {
                var wall = Box(obstacles, "Боковая секция", center + frame * new Vector3(side * 2.2f, .4f, 0), new Vector3(.25f, 2.6f, length), concrete);
                wall.transform.localRotation = frame;
            }
        }
        Board(GravityNames[index], "M — надень маску перед скруглением\nИди по непрерывному пути\nБез маски смена гравитации закрыта\nКамерой управляешь самостоятельно", new Vector3(-1.98f, 2.1f, 1.8f));
        var entry = Group("Вход", root); entry.localPosition = samples[0].p - samples[0].q * Vector3.up * .9f; entry.localRotation = samples[0].q;
        var exit = Group("Выход", root); var last = samples[samples.Count - 1]; exit.localPosition = last.p - last.q * Vector3.up * .9f; exit.localRotation = last.q;
        Lamp(new Vector3(0, 3.3f, 2)); Save(3, GravityNames[index]);
    }
    static void MeshPart(int trial, string label, List<Sample> samples, Transform parent)
    {
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); float distance = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i]; if (i > 0) distance += Vector3.Distance(samples[i - 1].p, s.p);
            Vector3 top = s.p - s.q * Vector3.up * .9f, bottom = top - s.q * Vector3.up * .25f, right = s.q * Vector3.right * 2;
            vertices.AddRange(new[] { top - right, top + right, bottom - right, bottom + right });
            for (int k = 0; k < 4; k++) uv.Add(new Vector2(k % 2, distance * .25f));
            if (i == 0) continue; int a = (i - 1) * 4, b = i * 4;
            Quad(triangles,a,b,b+1,a+1); Quad(triangles,a+2,a+3,b+3,b+2); Quad(triangles,a,a+2,b+2,b); Quad(triangles,a+1,b+1,b+3,a+3);
        }
        Quad(triangles,0,1,3,2); int last = vertices.Count-4; Quad(triangles,last,last+2,last+3,last+1);
        string path = Kit + "3 уровень/Geometry/Испытание " + (trial+1) + " — " + label + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh) { mesh = new Mesh { name = label }; mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,path); }
        var node = Group(label,parent); node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = concrete; node.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
    }
    static void Quad(List<int> list,int a,int b,int c,int d) => list.AddRange(new[] {a,b,c,a,c,d});
    static void Starter(int level)
    {
        Begin("Старт — готовый пример");
        var room = Copy(PathFor(level, level == 1 ? AxisNames[0] : level == 2 ? InversionNames[4] : GravityNames[0]), root);
        var player = Copy(Kit + level + " уровень/Игрок.prefab", root); player.transform.localPosition = new Vector3(0,.93f,1);
        if (level == 3)
        {
            var assembler = root.gameObject.AddComponent<GravityRouteAssembler>(); assembler.player = player.GetComponent<CornerMaskTraversalController>();
            assembler.pieces = new[] { room.GetComponent<GravityRoutePiece>() }; assembler.halfWidth = 1.9f; assembler.BuildRoute();
        }
        else if (level == 1)
        {
            var wall = room.GetComponentInChildren<AxisWallAsset>(); wall.player = player.GetComponent<MaskDemoPlayer>(); wall.mask = player.GetComponentInChildren<MaskAxisCamera>();
        }
        Save(level,"Старт — готовый пример");
    }
    static GameObject Copy(string path, Transform parent)
    { var source = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!source) throw new Exception("Missing " + path); var result = Object.Instantiate(source,parent); result.name = source.name; return result; }
    static Transform Group(string name, Transform parent) { var node = new GameObject(name).transform; node.SetParent(parent,false); return node; }
    static GameObject Box(Transform parent,string name,Vector3 p,Vector3 size,Material m)
    { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=p; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=m; return go; }
    static void Floor(float a,float b,float width=6.6f) => Box(floors,"Пол",new Vector3(0,-.25f,(a+b)*.5f),new Vector3(width,.5f,b-a),concrete);
    static void Markers(Vector3 exit, Quaternion? rotation=null)
    {
        Quaternion q = rotation ?? Quaternion.identity;
        var a=Group("Вход",root); a.localRotation=q; var b=Group("Выход",root); b.localPosition=exit; b.localRotation=q;
        Checkpoint(q*Vector3.forward,false,"Вход",q); Checkpoint(exit-q*Vector3.forward,false,"Выход",q);
    }
    static void Checkpoint(Vector3 feet,bool mask,string name,Quaternion? rotation=null)
    {
        var point=Group(name+" — место появления",root); point.localPosition=feet+Vector3.up*.93f; point.localRotation=rotation??Quaternion.identity;
        var node=Group(name+" — контрольная точка",root); node.localPosition=feet+Vector3.up*1.4f; node.localRotation=point.localRotation;
        var box=node.gameObject.AddComponent<BoxCollider>(); box.size=new Vector3(6.4f,2.8f,.6f); box.isTrigger=true;
        var check=node.gameObject.AddComponent<LogicTrialCheckpoint>(); check.spawn=point; check.inversionMaskOn=mask;
    }
    static void Board(string title,string body,Vector3 p,bool frontal=false)
    {
        var node=Group("Подсказка — "+title,decor); node.localPosition=p; node.localRotation=frontal?Quaternion.identity:Quaternion.Euler(0,-90,0);
        var panel=Box(node,"Табличка",Vector3.zero,new Vector3(3.8f,2.1f,.05f),dark); Object.DestroyImmediate(panel.GetComponent<Collider>());
        var label=Group("Текст",node); label.localPosition=new Vector3(-1.6f,.84f,-.04f); Text(label,title+"\n\n"+body,.033f);
    }
    static void Label(string name,string text,Vector3 p,Quaternion rotation,float size)
    { var node=Group(name,decor); node.localPosition=p; node.localRotation=rotation; Text(node,text,size); }
    static void Text(Transform node,string text,float size)
    { var t=node.gameObject.AddComponent<TextMesh>(); t.font=font; t.fontSize=64; t.characterSize=size; t.anchor=TextAnchor.UpperLeft; t.text=text; t.color=new Color(.83f,.94f,.92f); node.GetComponent<Renderer>().sharedMaterial=textMaterial; }
    static void Lamp(Vector3 p)
    { var lamp=Group("Свет",decor).gameObject.AddComponent<Light>(); lamp.transform.localPosition=p; lamp.type=LightType.Point; lamp.color=new Color(1,.82f,.65f); lamp.intensity=2; lamp.range=9; lamp.shadows=UnityEngine.LightShadows.None; }
}
