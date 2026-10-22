using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MaskResize.Editor
{
    public static class ResizeTrialPrefabBuilder
    {
        public const string Kit = "Assets/Логика/4 уровень/";
        public static readonly string[] Names = {
            "07 — Составной мост", "08 — Подъём через проём", "09 — Низкий тоннель",
            "10 — Два этажа", "11 — Мост за поворотом", "12 — Подъём и спуск" };
        static Material concrete, dark, cyan, amber;
        static GameObject board;
        static Transform root, floors, geometry, puzzles, signs, checkpoints;

        [MenuItem("Tools/Mask Resize/Open Updated Tutorial 4")]
        public static void OpenTutorial()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode before opening the saved tutorial."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ResizeTutorialBuilder.ScenePath);
        }
        [MenuItem("Tools/Mask Resize/Create Trial Prefabs 7–12")]
        public static void CreatePrefabs()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Export();
        }
        public static void Export()
        {
            EditorSceneManager.OpenScene(ResizeTutorialBuilder.ScenePath);
            concrete = Mat("Concrete"); dark = Mat("DarkSteel"); cyan = Mat("ResizeMark"); amber = Mat("WarmLight");
            board = GameObject.Find("01 / ДЛИНА");
            if (!board || !GameObject.Find("07 Left bridge")) throw new InvalidOperationException("Open the tutorial containing trials 7–10 first.");
            for (int trial = 7; trial <= 12; trial++)
            {
                string path = Kit + Names[trial - 7] + ".prefab";
                // Preserve the user's edited assets if this menu is invoked again.
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
                root = new GameObject(Names[trial - 7]).transform;
                floors = Group("01 — Пол и площадки"); geometry = Group("02 — Стены и проёмы");
                puzzles = Group("03 — Изменяемые объекты"); signs = Group("04 — Подсказки и свет"); checkpoints = Group("05 — Возрождение");
                try
                {
                    if (trial <= 10) CopyAuthored(trial); else if (trial == 11) TurnBridge(); else UpAndDown();
                    root.position = Vector3.zero;
                    foreach (var marker in root.GetComponentsInChildren<ResizeObjectMarker>()) marker.SendMessage("LateUpdate");
                    if (!PrefabUtility.SaveAsPrefabAsset(root.gameObject, path)) throw new InvalidOperationException("Could not save " + path);
                }
                finally { Object.DestroyImmediate(root.gameObject); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("RESIZE_TRIAL_PREFABS_EXPORTED: 6 portable trials, 7–12, with local spawn markers and editable geometry.");
        }
        public static void ExportAndExtend()
        {
            Export();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (GameObject.Find("07 — Дополнительные головоломки 11–12")) return;
            var parent = new GameObject("07 — Дополнительные головоломки 11–12").transform;
            parent.SetParent(GameObject.Find("04 — Лаборатория масштаба").transform, false);
            var a = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit + Names[4] + ".prefab"), parent);
            a.transform.position = new Vector3(0, 18, 180);
            var b = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Kit + Names[5] + ".prefab"), parent);
            b.transform.position = a.transform.Find("Выход").position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(a.transform); PrefabUtility.RecordPrefabInstancePropertyModifications(b.transform);
            var last = b.transform.Find("05 — Возрождение/Выход — контрольная точка").GetComponent<ResizeCheckpoint>();
            last.Configure(b.transform.Find("05 — Возрождение/Выход — место появления"), true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(last);
            var old = GameObject.Find("Finish"); old.name = "Checkpoint before trial 11";
            old.GetComponent<ResizeCheckpoint>().Configure(new Vector3(0, 18.03f, 178));
            Object.DestroyImmediate(GameObject.Find("ОБУЧЕНИЕ ПРОЙДЕНО"));
            // Trim the previous landing at the new module's entrance; no stacked floors.
            var oldFloor = GameObject.Find("10 Finish floor").transform;
            oldFloor.position = new Vector3(0, 17.75f, 178); oldFloor.localScale = new Vector3(9, .5f, 4);
            var end = GameObject.Find("End wall").transform;
            end.position = new Vector3(10.5f, 12.2f, 223.25f); end.localScale = new Vector3(9, 24.4f, .5f);
            root = parent; signs = parent;
            Board("ОБУЧЕНИЕ ПРОЙДЕНО", "Все 12 испытаний пройдены\nТы умеешь сочетать три размера\nМожно вернуться к предыдущим задачам", new Vector3(10.5f, 20.1f, 222.94f), Quaternion.identity);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("RESIZE_TUTORIAL_12_READY: two new prefab instances appended to the same scene.");
        }
        static void CopyAuthored(int trial)
        {
            float start = trial == 7 ? 108 : trial == 8 ? 128 : trial == 9 ? 139 : 154;
            float y = trial <= 8 ? 9 : 12;
            float length = trial == 7 ? 20 : trial == 8 ? 11 : trial == 9 ? 15 : 26;
            float rise = trial == 8 ? 3 : trial == 10 ? 6 : 0;
            root.position = new Vector3(0, y, start);
            string prefix = trial.ToString("00") + " ";
            var source = GameObject.Find("06 — Продвинутые испытания масштаба").transform;
            foreach (Transform category in source)
            foreach (Transform item in category)
            {
                if (!item.name.StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (item.name.EndsWith("Roof", StringComparison.Ordinal) || item.name.EndsWith("roof", StringComparison.Ordinal) ||
                    item.name == "07 Landing" || item.name == "08 Upper landing" || item.name == "09 Rest landing" || item.name == "10 Finish floor")
                {
                    if (item.name != "09 Low tunnel roof") continue;
                }
                var destination = item.GetComponent<ResizableMaskObject>() ? puzzles :
                    item.name.Contains("island") || item.name.Contains("landing") ? floors :
                    item.name.Contains("rim") || item.name.Contains("marker") ? signs : geometry;
                var copy = Object.Instantiate(item.gameObject, destination);
                copy.name = item.name; copy.transform.SetPositionAndRotation(item.position, item.rotation);
            }
            Floor("Входная площадка", 0, trial == 9 ? 3 : 4, 0);
            Floor("Выходная площадка", trial == 7 ? 16 : trial == 8 ? 7 : trial == 9 ? 11 : 22, length, rise);
            Shell(length, rise + 6.4f);
            string body = trial == 7 ? "1 — удлини обе части\n3 — расширь обе к центру\nПерейди с левой части на правую" :
                trial == 8 ? "3 — сузь платформу под проём\nВстань в центр платформы\n2 — поднимись через проём" :
                trial == 9 ? "2 — опусти верх моста\n3 — сузь между опорами\n1 — протяни через тоннель" :
                "2 — поднимись на первой платформе\nПерейди на второй блок\nПодними, удлини и расширь его\nВыход наверху слева";
            Board(Names[trial - 7], body, new Vector3(-4.42f, 2.3f, 2), Quaternion.Euler(0, -90, 0));
            Anchors(new Vector3(0, rise, length));
            Lamp(2, 3); Lamp(length - 2, rise + 3);
        }
        static void TurnBridge()
        {
            Floor("Входная площадка", 0, 4, 0);
            Item("11 Approach bridge", new Vector3(0, 0, 4), new Vector3(2, .35f, 2), new Vector3(1, .35f, 1), new Vector3(3, .35f, 6), new Vector3(0, .5f, -.5f));
            Box(floors, "11 Turning island", new Vector3(0, -.25f, 11.5f), new Vector3(3, .5f, 3), concrete);
            var cross = Item("11 Cross bridge", new Vector3(1.5f, 0, 11.5f), new Vector3(1, .35f, 2), new Vector3(1, .35f, 1), new Vector3(3, .35f, 7), new Vector3(0, .5f, -.5f));
            cross.localRotation = Quaternion.Euler(0, 90, 0);
            Box(floors, "11 Right landing", new Vector3(10.5f, -.25f, 13.5f), new Vector3(4, .5f, 7), concrete);
            Box(geometry, "Левая стена", new Vector3(-4.75f, 3.2f, 8.5f), new Vector3(.5f, 6.4f, 17), concrete);
            Box(geometry, "Правая стена", new Vector3(12.75f, 3.2f, 8.5f), new Vector3(.5f, 6.4f, 17), concrete);
            Box(geometry, "Потолок", new Vector3(4, 6.65f, 8.5f), new Vector3(18, .5f, 17), concrete);
            Box(geometry, "Торец у поворота", new Vector3(2, 3.2f, 17.25f), new Vector3(13, 6.4f, .5f), concrete);
            Board(Names[4], "1 — удлини мост до островка\nНа островке поверни направо\nУ второго моста своя длина\n1 и 3 — построй путь вправо", new Vector3(-4.42f, 2.3f, 2), Quaternion.Euler(0, -90, 0));
            Board("ДЛИНА ПОВЕРНУЛАСЬ", "Размеры следуют самому объекту\n1 растягивает этот мост вправо\nКамерой поворачивай сам", new Vector3(3, 2.3f, 16.94f), Quaternion.identity);
            Anchors(new Vector3(10.5f, 0, 17)); Lamp(3, 3); Lamp(11.5f, 3, 6);
        }
        static void UpAndDown()
        {
            Floor("Входная площадка", 0, 4, 0);
            Item("12 High lift", new Vector3(0, 0, 5.5f), new Vector3(3.2f, 3, 3), new Vector3(1.4f, .18f, 3), new Vector3(4, 3, 3), Vector3.down * .5f);
            Box(floors, "12 Upper island", new Vector3(0, 2.75f, 8.5f), new Vector3(4, .5f, 3), concrete);
            Item("12 Descending bridge", new Vector3(0, 0, 10), new Vector3(1.4f, 3, 2), new Vector3(.8f, .18f, 1), new Vector3(8.8f, 3, 8), new Vector3(0, -.5f, -.5f));
            Box(floors, "12 Left landing", new Vector3(-3.3f, -.25f, 20), new Vector3(2.4f, .5f, 4), concrete);
            Floor("Выходная площадка", 22, 26, 0); Shell(26, 6.4f);
            Board(Names[5], "2 — сначала опусти платформу\nВстань на неё и поднимись\nПерейди на следующий блок\nОпусти его вместе с собой", new Vector3(-4.42f, 2.3f, 2), Quaternion.Euler(0, -90, 0));
            Board("ВНИЗ И ВПЕРЁД", "Снизь второй блок к нижнему полу\n1 — удлини мост\n3 — расширь к выходу слева\nРазмеры можно уменьшать под собой", new Vector3(4.42f, 5.3f, 9), Quaternion.Euler(0, 90, 0));
            Anchors(new Vector3(0, 0, 26)); Lamp(3, 3); Lamp(10, 5.8f); Lamp(23, 3);
        }
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskResize/Materials/" + name + ".mat");
        static Transform Group(string name) { var t = new GameObject(name).transform; t.SetParent(root, false); return t; }
        static void Box(Transform parent, string name, Vector3 p, Vector3 size, Material m, bool solid = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(parent, false); box.transform.localPosition = p; box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = m; if (!solid) Object.DestroyImmediate(box.GetComponent<Collider>());
        }
        static void Floor(string name, float start, float end, float y) => Box(floors, name, new Vector3(0, y - .25f, (start + end) * .5f), new Vector3(9, .5f, end - start), concrete);
        static void Shell(float length, float height)
        {
            foreach (int side in new[] { -1, 1 }) Box(geometry, "Боковая стена", new Vector3(side * 4.75f, height * .5f, length * .5f), new Vector3(.5f, height, length), concrete);
            Box(geometry, "Потолок", new Vector3(0, height + .25f, length * .5f), new Vector3(10, .5f, length), concrete);
        }
        static Transform Item(string name, Vector3 p, Vector3 size, Vector3 min, Vector3 max, Vector3 anchor)
        {
            var copy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Блок — все размеры.prefab"), puzzles);
            copy.name = name; copy.transform.localPosition = p; copy.transform.localRotation = Quaternion.identity;
            var component = copy.GetComponent<ResizableMaskObject>(); component.Configure(component.Solid, size, min, max, anchor); return copy.transform;
        }
        static void Board(string title, string body, Vector3 p, Quaternion q)
        {
            var copy = Object.Instantiate(board, signs); copy.name = title;
            copy.transform.localPosition = p; copy.transform.localRotation = q; copy.GetComponentInChildren<TextMesh>().text = title + "\n\n" + body;
        }
        static void Anchors(Vector3 exit)
        {
            var entry = Group("Вход"); entry.localPosition = Vector3.zero;
            var end = Group("Выход"); end.localPosition = exit;
            Checkpoint("Вход", new Vector3(0, 0, 1)); Checkpoint("Выход", exit - Vector3.forward * 1.5f);
        }
        static void Checkpoint(string label, Vector3 feet)
        {
            var spawn = new GameObject(label + " — место появления").transform; spawn.SetParent(checkpoints, false); spawn.localPosition = feet + Vector3.up * .03f;
            var node = new GameObject(label + " — контрольная точка").transform; node.SetParent(checkpoints, false); node.localPosition = feet + Vector3.up * 1.4f;
            var collider = node.gameObject.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(3.8f, 2.8f, .8f);
            node.gameObject.AddComponent<ResizeCheckpoint>().Configure(spawn);
        }
        static void Lamp(float z, float y, float x = 0)
        {
            Box(signs, "Светильник", new Vector3(x, y, z), new Vector3(.6f, .1f, .3f), amber, false);
            var node = new GameObject("Мягкий свет").transform; node.SetParent(signs, false); node.localPosition = new Vector3(x, y - .2f, z);
            var light = node.gameObject.AddComponent<Light>(); light.type = LightType.Point; light.range = 9; light.intensity = 2.5f;
            light.color = new Color(1, .82f, .61f); light.shadows = LightShadows.None;
        }
    }
}
