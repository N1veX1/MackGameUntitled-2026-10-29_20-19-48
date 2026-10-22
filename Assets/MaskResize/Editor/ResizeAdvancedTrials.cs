using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MaskResize.Editor
{
    // Four further combinations using the same reusable blocks and collision rules.
    public static class ResizeAdvancedTrials
    {
        const string GroupName = "06 — Продвинутые испытания масштаба";
        const string Kit = "Assets/Логика/4 уровень/";
        static Material concrete, dark, cyan, amber;
        static GameObject boardTemplate;

        [MenuItem("Tools/Mask Resize/Add Advanced Trials 7–10")]
        public static void AddTrials()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ResizeTutorialBuilder.ScenePath);
            if (GameObject.Find(GroupName)) { Debug.Log("RESIZE_ADVANCED_ALREADY_PRESENT"); return; }
            if (!GameObject.Find("05 — Комбинированные испытания масштаба"))
                throw new InvalidOperationException("Add trials 4–6 first: Tools > Mask Resize > Extend Tutorial 4.");
            var root = Group(GroupName, GameObject.Find("04 — Лаборатория масштаба").transform);
            var floors = Group("01 — Пол и площадки", root);
            var walls = Group("02 — Стены и проёмы", root);
            var puzzles = Group("03 — Изменяемые объекты", root);
            var guidance = Group("04 — Подсказки и свет", root);
            var checkpoints = Group("05 — Точки возрождения", root);
            concrete = Mat("Concrete"); dark = Mat("DarkSteel"); cyan = Mat("ResizeMark"); amber = Mat("WarmLight");
            boardTemplate = GameObject.Find("01 / ДЛИНА");
            if (!boardTemplate) throw new InvalidOperationException("Missing instruction board template.");
            var end = GameObject.Find("End wall");
            end.transform.position = new Vector3(0, 10.7f, 184.25f);
            end.transform.localScale = new Vector3(9, 21.4f, .5f);
            var previousFinish = GameObject.Find("Finish");
            previousFinish.name = "Checkpoint before advanced trials";
            previousFinish.GetComponent<ResizeCheckpoint>().Configure(new Vector3(0, 9.03f, 108));
            Object.DestroyImmediate(GameObject.Find("ОБУЧЕНИЕ ПРОЙДЕНО"));

            Box(walls, "Advanced left wall", new Vector3(-4.75f, 10.7f, 148), new Vector3(.5f, 21.4f, 72), concrete);
            Box(walls, "Advanced right wall", new Vector3(4.75f, 10.7f, 148), new Vector3(.5f, 21.4f, 72), concrete);
            Roof(walls, "07 Roof", 112, 132, 15.65f);
            Roof(walls, "08 Roof", 132, 142, 18.65f);
            // This low roof is functional: the ninth bridge must be lowered before crossing.
            Box(walls, "09 Low tunnel roof", new Vector3(0, 14.25f, 146), new Vector3(9, .3f, 8), concrete);
            Roof(walls, "09 Landing roof", 150, 158, 18.65f);
            Roof(walls, "10 Roof", 158, 184, 21.65f);

            // 07: the bridge halves face the same direction but have offset centre lines.
            Item(puzzles, "07 Left bridge", "Пол — удлиняемый мост", new Vector3(-1.8f, 9, 112),
                new Vector3(1.4f, .35f, 2), new Vector3(.8f, .35f, 1), new Vector3(5.2f, .35f, 6), new Vector3(0, .5f, -.5f));
            Item(puzzles, "07 Right bridge", "Пол — удлиняемый мост", new Vector3(1.8f, 9, 118),
                new Vector3(1.4f, .35f, 2), new Vector3(.8f, .35f, 1), new Vector3(5.2f, .35f, 6), new Vector3(0, .5f, -.5f));
            Floor(floors, "07 Landing", 124, 132, 9);
            Board(guidance, "07 / СОСТАВНОЙ МОСТ", "1 — удлини левую часть\nС неё настрой правую часть\n3 — расширь обе к середине\nСоедини их и перейди вправо", new Vector3(-4.42f, 11.3f, 108), Quaternion.Euler(0, -90, 0));
            Board(guidance, "СОЕДИНИ ОБЕ ЧАСТИ", "Каждая часть растёт отдельно\nМесто перехода — посередине", new Vector3(4.42f, 11.3f, 120), Quaternion.Euler(0, 90, 0));
            Checkpoint(checkpoints, "Checkpoint after split bridge", new Vector3(0, 10.4f, 128), new Vector3(0, 9.03f, 128));

            // 08: a broad lift cannot rise through the fixed slot until it is narrowed.
            Item(puzzles, "08 Slot lift", "Платформа — подъём", new Vector3(0, 9, 133.5f),
                new Vector3(4.4f, .18f, 3), new Vector3(1.4f, .18f, 3), new Vector3(4.4f, 3, 3), Vector3.down * .5f);
            foreach (int side in new[] { -1, 1 })
            {
                Box(walls, "08 Fixed slot ledge", new Vector3(side * 2.925f, 10.25f, 133.5f), new Vector3(3.15f, .3f, 3), concrete);
                Box(guidance, "08 Slot rim", new Vector3(side * 1.36f, 10.41f, 133.5f), new Vector3(.06f, .02f, 3), cyan, false);
            }
            Floor(floors, "08 Upper landing", 135, 142, 12);
            Board(guidance, "08 / ВПИСАТЬСЯ В ПРОЁМ", "Встань в центр платформы\n3 — сузь её под проём\n2 — поднимись через него\nШирокая платформа не пройдёт", new Vector3(-4.42f, 11.3f, 129), Quaternion.Euler(0, -90, 0));
            Checkpoint(checkpoints, "Checkpoint after slot lift", new Vector3(0, 13.4f, 139), new Vector3(0, 12.03f, 139));

            // 09: lower the bridge for headroom, narrow it for the posts, then extend.
            Item(puzzles, "09 Tunnel bridge", "Блок — все размеры", new Vector3(0, 12, 142),
                new Vector3(3, 1.2f, 2), new Vector3(.8f, .18f, 1), new Vector3(4, 1.2f, 8), new Vector3(0, -.5f, -.5f));
            foreach (int side in new[] { -1, 1 })
            {
                Box(walls, "09 Tunnel post", new Vector3(side * 2.85f, 13.05f, 146), new Vector3(3.3f, 2.1f, 1), concrete);
                Box(guidance, "09 Narrow opening rim", new Vector3(side * 1.21f, 13.05f, 145.48f), new Vector3(.06f, 2.05f, .03f), cyan, false);
            }
            Floor(floors, "09 Rest landing", 150, 158, 12);
            Board(guidance, "09 / НИЗКО И УЗКО", "2 — опусти верх моста\n3 — сузь его между опорами\n1 — протяни через тоннель\nОставь место над головой", new Vector3(4.42f, 14.3f, 139), Quaternion.Euler(0, 90, 0));
            Checkpoint(checkpoints, "Checkpoint after low tunnel", new Vector3(0, 13.4f, 154), new Vector3(0, 12.03f, 154));

            // 10: use two independently anchored platforms to climb two storeys.
            Item(puzzles, "10 First lift", "Платформа — подъём", new Vector3(0, 12, 159.5f),
                new Vector3(3.2f, .18f, 3), new Vector3(1.4f, .18f, 3), new Vector3(4, 3, 3), Vector3.down * .5f);
            Box(floors, "10 Intermediate island", new Vector3(0, 14.75f, 162.5f), new Vector3(4, .5f, 3), concrete);
            Item(puzzles, "10 Summit bridge", "Блок — все размеры", new Vector3(0, 12, 164),
                new Vector3(2.4f, 3, 2), new Vector3(1, 3, 1), new Vector3(8.8f, 6, 8), new Vector3(0, -.5f, -.5f));
            Box(floors, "10 Left summit landing", new Vector3(-3.3f, 17.75f, 174), new Vector3(2.4f, .5f, 4), concrete);
            Floor(floors, "10 Finish floor", 176, 184, 18);
            Board(guidance, "10 / ДВА ЭТАЖА", "2 — поднимись на первой платформе\nПерейди через островок на вторую\nПодними, удлини и расширь её\nВыход наверху слева", new Vector3(-4.42f, 14.3f, 155), Quaternion.Euler(0, -90, 0));
            Board(guidance, "ФИНАЛЬНЫЙ ПОДЪЁМ", "2 — высота до верхней площадки\n1 — длина до дальнего края\n3 — ширина к выходу слева", new Vector3(4.42f, 17.3f, 163), Quaternion.Euler(0, 90, 0));
            Board(guidance, "ОБУЧЕНИЕ ПРОЙДЕНО", "Все десять испытаний завершены\nДлина, ширина и высота работают вместе\nМожно вернуться к любой головоломке", new Vector3(0, 20.1f, 183.94f), Quaternion.identity);
            Checkpoint(checkpoints, "Finish", new Vector3(0, 19.4f, 180), new Vector3(0, 18.03f, 178), true);

            foreach (var edge in new[] { new Vector3(0, 9.012f, 111.7f), new Vector3(0, 9.012f, 131.7f), new Vector3(0, 12.012f, 141.7f), new Vector3(0, 12.012f, 157.7f) })
                Box(guidance, "Pit edge stripe", edge, new Vector3(8.7f, .018f, .1f), amber, false);
            Box(guidance, "10 Summit marker", new Vector3(-3.3f, 18.012f, 172.15f), new Vector3(2.3f, .018f, .1f), cyan, false);
            foreach (var pit in new[] { new Vector2(112, 124), new Vector2(132, 135), new Vector2(142, 150), new Vector2(158, 176) })
                Box(guidance, "Deep pit", new Vector3(0, -11, (pit.x + pit.y) * .5f), new Vector3(9, .5f, pit.y - pit.x), dark);
            Lamp(guidance, 116, 12.3f); Lamp(guidance, 126, 12.3f); Lamp(guidance, 137, 15.3f);
            Lamp(guidance, 146, 13.5f); Lamp(guidance, 154, 15.3f); Lamp(guidance, 162, 18.1f);
            Lamp(guidance, 171, 20.8f); Lamp(guidance, 180, 20.8f);
            Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("RESIZE_ADVANCED_BUILT: 10 trials, four further combinations, editable objects and checkpoints.");
        }
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskResize/Materials/" + name + ".mat");
        static Transform Group(string name, Transform parent) { var node = new GameObject(name).transform; node.SetParent(parent, false); return node; }
        static void Box(Transform parent, string name, Vector3 p, Vector3 size, Material material, bool solid = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.SetParent(parent, false); box.transform.position = p; box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(box.GetComponent<Collider>());
        }
        static void Floor(Transform parent, string name, float start, float end, float top)
            => Box(parent, name, new Vector3(0, top - .25f, (start + end) * .5f), new Vector3(9, .5f, end - start), concrete);
        static void Roof(Transform parent, string name, float start, float end, float y)
            => Box(parent, name, new Vector3(0, y, (start + end) * .5f), new Vector3(10, .5f, end - start), concrete);
        static void Item(Transform parent, string name, string prefab, Vector3 position, Vector3 size, Vector3 min, Vector3 max, Vector3 anchor)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + prefab + ".prefab");
            if (!source) throw new InvalidOperationException("Missing prefab: " + prefab);
            var result = Object.Instantiate(source, parent); result.name = name;
            result.transform.SetPositionAndRotation(position, Quaternion.identity); result.transform.localScale = Vector3.one;
            var component = result.GetComponent<ResizableMaskObject>(); component.Configure(component.Solid, size, min, max, anchor);
            result.GetComponent<ResizeObjectMarker>().SendMessage("LateUpdate");
        }
        static void Board(Transform parent, string title, string body, Vector3 position, Quaternion rotation)
        {
            var result = Object.Instantiate(boardTemplate, parent); result.name = title;
            result.transform.SetPositionAndRotation(position, rotation);
            result.GetComponentInChildren<TextMesh>().text = title + "\n\n" + body;
        }
        static void Checkpoint(Transform parent, string name, Vector3 position, Vector3 feet, bool finish = false)
        {
            var node = Group(name, parent); node.position = position;
            var box = node.gameObject.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(8.8f, 2.8f, 1);
            node.gameObject.AddComponent<ResizeCheckpoint>().Configure(feet, finish);
        }
        static void Lamp(Transform parent, float z, float y)
        {
            foreach (int side in new[] { -1, 1 })
                Box(parent, "Advanced wall strip", new Vector3(side * 4.46f, y, z), new Vector3(.06f, .13f, 1.3f), amber, false);
            var light = Group("Advanced warm light", parent).gameObject.AddComponent<Light>();
            light.transform.position = new Vector3(0, y, z); light.type = LightType.Point;
            light.color = new Color(1, .82f, .61f); light.intensity = 2.6f; light.range = 10; light.shadows = LightShadows.None;
        }
    }
}
