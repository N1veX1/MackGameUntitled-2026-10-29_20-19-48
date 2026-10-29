using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MaskResize.Editor
{
    public static class ResizeTutorialBuilder
    {
        public const string ScenePath = "Assets/Scenes/Level04_ResizeTutorial.unity";
        const string Root = "Assets/MaskResize";
        const string Kit = "Assets/Логика/4 уровень";
        static Material concrete, dark, cyan, amber, resizeSurface, lettering;
        static Mesh signMesh;
        static Font font;

        [MenuItem("Tools/Mask Resize/Create Tutorial 4")]
        public static void CreateTutorial()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }
        public static void Build()
        {
            Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Geometry");
            Directory.CreateDirectory(Kit); Directory.CreateDirectory("Assets/Scenes"); AssetDatabase.Refresh();
            var mask = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShieldMask/Prefabs/ShieldMask.prefab");
            if (!mask) throw new System.Exception("Import the supplied ShieldMask.unitypackage first.");
            concrete = Mat("Concrete", new Color(.33f, .37f, .38f));
            dark = Mat("DarkSteel", new Color(.045f, .07f, .085f), .35f);
            resizeSurface = Mat("ResizeSurface", new Color(.20f, .31f, .33f));
            resizeSurface.EnableKeyword("_EMISSION"); EditorUtility.SetDirty(resizeSurface);
            cyan = Mat("ResizeMark", new Color(.16f, .93f, .82f), .25f, true);
            amber = Mat("WarmLight", new Color(1, .73f, .38f), .2f, true);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            string fontPath = Root + "/Materials/InstructionText.mat";
            lettering = AssetDatabase.LoadAssetAtPath<Material>(fontPath);
            if (!lettering)
            {
                lettering = new Material(Shader.Find("MaskResize/DepthText"));
                lettering.mainTexture = font.material.mainTexture; AssetDatabase.CreateAsset(lettering, fontPath);
            }
            signMesh = MakeSignMesh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.34f, .39f, .42f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(.045f, .068f, .09f); RenderSettings.fogDensity = .006f;
            var root = Group("04 — Лаборатория масштаба", null);
            var floors = Group("01 — Пол и площадки", root);
            var walls = Group("02 — Стены", root);
            var props = Group("03 — Изменяемые объекты", root);
            var guidance = Group("04 — Свет и подсказки", root);

            Box(floors, "Start floor", new Vector3(0, -.25f, 2.5f), new Vector3(9, .5f, 11), concrete);
            Box(floors, "Middle floor", new Vector3(0, -.25f, 28), new Vector3(9, .5f, 20), concrete);
            Box(floors, "Upper floor", new Vector3(0, 2.75f, 48.5f), new Vector3(9, .5f, 15), concrete);
            Box(walls, "Left wall", new Vector3(-4.75f, 3.7f, 26.5f), new Vector3(.5f, 7.4f, 59), concrete);
            Box(walls, "Right wall", new Vector3(4.75f, 3.7f, 26.5f), new Vector3(.5f, 7.4f, 59), concrete);
            Box(walls, "Entrance wall", new Vector3(0, 3.7f, -3.25f), new Vector3(9, 7.4f, .5f), concrete);
            Box(walls, "End wall", new Vector3(0, 3.7f, 56.25f), new Vector3(9, 7.4f, .5f), concrete);
            Box(walls, "Raised deck support", new Vector3(0, 1.25f, 48.5f), new Vector3(9, 2.5f, 15), dark);
            // A roof separate from the floors, with clear visual rhythm and ample lift headroom.
            Box(walls, "Roof", new Vector3(0, 7.65f, 26.5f), new Vector3(10, .5f, 59), concrete);
            for (int z = -2; z < 56; z += 6)
            {
                Box(guidance, "Ceiling beam", new Vector3(0, 7.25f, z), new Vector3(9, .24f, .22f), dark);
                foreach (int side in new[] { -1, 1 })
                {
                    Box(guidance, "Wall lamp", new Vector3(side * 4.46f, 4.9f, z), new Vector3(.06f, .13f, 1.3f), amber, false);
                    var light = Group("Warm pool of light", guidance).gameObject.AddComponent<Light>();
                    light.transform.position = new Vector3(side * 3.7f, 4.8f, z);
                    light.type = LightType.Point; light.color = new Color(1, .82f, .61f); light.intensity = 2.2f;
                    light.range = 8; light.shadows = LightShadows.None;
                }
            }
            var sun = Group("Soft fill", guidance).gameObject.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = .65f; sun.color = new Color(.66f, .79f, .91f);
            sun.transform.rotation = Quaternion.Euler(45, -25, 0); sun.shadows = LightShadows.Soft;

            var bridge = Resizable("Resize Bridge", props, new Vector3(0, 0, 8), new Vector3(3, .35f, 2),
                new Vector3(3, .35f, 1), new Vector3(3, .35f, 10), new Vector3(0, .5f, -.5f));
            var wall = Resizable("Resize Wall", props, new Vector3(0, 0, 28), new Vector3(8.8f, 3.2f, .55f),
                new Vector3(1.5f, 3.2f, .55f), new Vector3(8.8f, 3.2f, .55f), new Vector3(0, -.5f, 0));
            var lift = Resizable("Resize Lift", props, new Vector3(0, 0, 39.5f), new Vector3(3.2f, .18f, 3),
                new Vector3(3.2f, .18f, 3), new Vector3(3.2f, 3, 3), new Vector3(0, -.5f, 0));
            SaveReusable(bridge.gameObject, "Пол — удлиняемый мост");
            SaveReusable(wall.gameObject, "Стена — изменение размеров");
            SaveReusable(lift.gameObject, "Платформа — подъём");
            bridge.ConfigureIntroductoryHints(true);
            wall.ConfigureIntroductoryHints(true);
            lift.ConfigureIntroductoryHints(true);
            var block = Resizable("Настраиваемый блок", null, Vector3.zero, new Vector3(2, 2, 2),
                Vector3.one * .2f, Vector3.one * 8, new Vector3(0, -.5f, 0));
            SaveReusable(block.gameObject, "Блок — все размеры"); Object.DestroyImmediate(block.gameObject);

            Board(guidance, "01 / ДЛИНА", "M — надень маску\nНаведи прицел на мост\n1 — длина · колесо вверх\nДотяни мост до площадки", new Vector3(-4.42f, 2.4f, 5), Quaternion.Euler(0, -90, 0));
            Board(guidance, "02 / ШИРИНА", "3 — ширина\nКолесо вниз — уменьшить\nСузь стену и обойди сбоку", new Vector3(-4.42f, 2.4f, 23), Quaternion.Euler(0, -90, 0));
            Board(guidance, "03 / ВЫСОТА", "Встань на низкую платформу\nПосмотри вниз · 2 — высота\nКолесо вверх — поднимись\nПол под ногами поднимет тебя", new Vector3(-4.42f, 2.5f, 35), Quaternion.Euler(0, -90, 0));
            Board(guidance, "МАСШТАБ / 04", "Стрелки отмечают изменяемые объекты\n1 — длина · 2 — высота · 3 — ширина\nShift + колесо — точное изменение\nБез взгляда 5 сек. — возврат формы", new Vector3(4.42f, 2.5f, 2.8f), Quaternion.Euler(0, 90, 0));
            Board(guidance, "ОБУЧЕНИЕ ПРОЙДЕНО", "Теперь ты управляешь размерами\nМожно вернуться к любому объекту", new Vector3(0, 5.1f, 55.94f), Quaternion.identity);
            foreach (float z in new[] { 7.6f, 18.4f, 37.6f })
                Box(guidance, "Gap edge marking", new Vector3(0, .012f, z), new Vector3(8.7f, .018f, .1f), amber, false);
            Box(guidance, "Upper landing marking", new Vector3(0, 3.01f, 41.4f), new Vector3(8.7f, .018f, .1f), cyan, false);
            // Decorative recess emphasizes the pit without introducing a false walkable floor.
            Box(guidance, "Pit bottom", new Vector3(0, -11, 13), new Vector3(9, .5f, 10), dark);
            Box(guidance, "Lift pit bottom", new Vector3(0, -11, 39.5f), new Vector3(9, .5f, 3), dark);

            var playerObject = new GameObject("Player — Shield resize mask"); playerObject.transform.position = new Vector3(0, .03f, 0);
            var character = playerObject.AddComponent<CharacterController>(); character.height = 1.8f; character.center = Vector3.up * .9f;
            character.radius = .28f; character.stepOffset = .24f; character.skinWidth = .02f;
            var camera = Group("Player Camera", playerObject.transform).gameObject.AddComponent<Camera>();
            camera.transform.localPosition = Vector3.up * 1.7f; camera.tag = "MainCamera"; camera.fieldOfView = 76;
            camera.nearClipPlane = .04f; camera.farClipPlane = 150; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor; camera.gameObject.AddComponent<AudioListener>();
            var maskInstance = (GameObject)PrefabUtility.InstantiatePrefab(mask, scene);
            maskInstance.name = "ShieldMask — supplied original";
            var player = playerObject.AddComponent<ResizeMaskPlayer>(); player.Configure(camera, maskInstance);
            SaveReusable(playerObject, "Игрок — маска масштаба");
            Checkpoint(guidance, "Checkpoint after bridge", new Vector3(0, 1.4f, 20), new Vector3(0, .03f, 20), false);
            Checkpoint(guidance, "Checkpoint after wall", new Vector3(0, 1.4f, 33), new Vector3(0, .03f, 34), false);
            Checkpoint(guidance, "Finish", new Vector3(0, 4.4f, 50), new Vector3(0, 3.03f, 48), true);
            string path = AssetDatabase.GenerateUniqueAssetPath(ScenePath);
            EditorSceneManager.SaveScene(scene, path);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != path)) { scenes.Add(new EditorBuildSettingsScene(path, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            AssetDatabase.SaveAssets();
            Debug.Log("RESIZE_TUTORIAL_BUILT: " + path + "; original ShieldMask, three trials, reusable assets in " + Kit);
        }
        static void SaveReusable(GameObject source, string name)
        {
            string path = Kit + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;
            var copy = Object.Instantiate(source); copy.name = name; copy.transform.position = Vector3.zero;
            copy.transform.SetParent(null); PrefabUtility.SaveAsPrefabAsset(copy, path); Object.DestroyImmediate(copy);
        }
        static ResizableMaskObject Resizable(string name, Transform parent, Vector3 anchor, Vector3 initial, Vector3 min, Vector3 max, Vector3 fixedFace)
        {
            var root = Group(name, parent); root.position = anchor;
            var solid = Box(root, "Solid", Vector3.zero, Vector3.one, resizeSurface).transform;
            var item = root.gameObject.AddComponent<ResizableMaskObject>(); item.Configure(solid, initial, min, max, fixedFace);
            if(name=="Resize Bridge"||name=="Resize Lift") item.ConfigureThinPlatform(true);
            var signs = new Transform[6];
            for (int i = 0; i < signs.Length; i++)
            {
                signs[i] = Group("Resize symbol " + (i + 1), root);
                signs[i].gameObject.AddComponent<MeshFilter>().sharedMesh = signMesh;
                var renderer = signs[i].gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = cyan;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            root.gameObject.AddComponent<ResizeObjectMarker>().Configure(item, signs, null);
            return item;
        }
        static void Checkpoint(Transform parent, string name, Vector3 position, Vector3 feet, bool finish)
        {
            var node = Group(name, parent); node.position = position;
            var collider = node.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(8.8f, 2.8f, 1); collider.isTrigger = true;
            node.gameObject.AddComponent<ResizeCheckpoint>().Configure(feet, finish);
        }
        static Transform Group(string name, Transform parent)
        {
            var item = new GameObject(name).transform; item.SetParent(parent, false); return item;
        }
        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collides = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(parent, false); box.transform.localPosition = position; box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!collides) Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }
        static Material Mat(string name, Color color, float smoothness = .15f, bool unlit = false)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var value = AssetDatabase.LoadAssetAtPath<Material>(path); if (value) return value;
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!shader) throw new System.Exception("URP shader is required.");
            value = new Material(shader); value.name = name; value.SetColor("_BaseColor", color);
            value.SetFloat("_Smoothness", smoothness); if (unlit) value.SetFloat("_Cull", 0);
            AssetDatabase.CreateAsset(value, path); return value;
        }
        static void Board(Transform parent, string heading, string body, Vector3 position, Quaternion rotation)
        {
            var node = Group(heading, parent); node.SetPositionAndRotation(position, rotation);
            Box(node, "Board", Vector3.zero, new Vector3(3.9f, 2.1f, .05f), dark, false);
            Box(node, "Accent", new Vector3(-1.82f, 0, -.034f), new Vector3(.035f, 1.85f, .012f), cyan, false);
            var label = Group("Instructions", node); label.localPosition = new Vector3(-1.6f, .82f, -.04f);
            var text = label.gameObject.AddComponent<TextMesh>(); text.font = font; text.fontSize = 64;
            text.characterSize = .034f; text.anchor = TextAnchor.UpperLeft; text.alignment = TextAlignment.Left;
            text.text = heading + "\n\n" + body; text.color = new Color(.83f, .94f, .92f);
            label.GetComponent<Renderer>().sharedMaterial = lettering;
        }
        static Mesh MakeSignMesh()
        {
            const string path = Root + "/Geometry/ResizeArrows.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh) return mesh;
            var v = new List<Vector3>(); var t = new List<int>();
            void Triangle(Vector2 a, Vector2 b, Vector2 c) { int n = v.Count; v.Add(a); v.Add(b); v.Add(c); t.AddRange(new[] { n, n + 1, n + 2 }); }
            void Bar(Vector2 a, Vector2 b, float width)
            {
                var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
                Triangle(a - normal, a + normal, b + normal); Triangle(a - normal, b + normal, b - normal);
            }
            Bar(new Vector2(-.29f, 0), new Vector2(.29f, 0), .055f);
            Triangle(new Vector2(-.44f, 0), new Vector2(-.25f, .14f), new Vector2(-.25f, -.14f));
            Triangle(new Vector2(.44f, 0), new Vector2(.25f, -.14f), new Vector2(.25f, .14f));
            Vector2[] diamond = { new Vector2(0, .47f), new Vector2(.47f, 0), new Vector2(0, -.47f), new Vector2(-.47f, 0) };
            for (int i = 0; i < 4; i++) Bar(diamond[i], diamond[(i + 1) % 4], .025f);
            mesh = new Mesh { name = "Resize — diamond with opposing arrows" }; mesh.SetVertices(v); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
    }
}
