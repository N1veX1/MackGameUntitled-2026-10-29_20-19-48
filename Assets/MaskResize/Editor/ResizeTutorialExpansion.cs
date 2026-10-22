using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MaskResize.Editor
{
    // Adds an editable continuation to the existing tutorial, retaining its first three trials.
    public static class ResizeTutorialExpansion
    {
        const string ExtensionName = "05 — Комбинированные испытания масштаба";
        const string Kit = "Assets/Логика/4 уровень/";
        static Material concrete, dark, cyan, amber;
        static GameObject boardTemplate;

        [MenuItem("Tools/Mask Resize/Extend Tutorial 4")]
        public static void Extend()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildExtension();
        }

        public static void BuildExtension()
        {
            var scene = EditorSceneManager.OpenScene(ResizeTutorialBuilder.ScenePath);
            if (GameObject.Find(ExtensionName)) { Debug.Log("RESIZE_EXTENSION_ALREADY_PRESENT"); return; }
            var originalRoot = GameObject.Find("04 — Лаборатория масштаба");
            if (!originalRoot) throw new InvalidOperationException("Original Tutorial 4 hierarchy is missing.");
            concrete = Material("Concrete"); dark = Material("DarkSteel");
            cyan = Material("ResizeMark"); amber = Material("WarmLight");
            boardTemplate = GameObject.Find("01 / ДЛИНА");
            if (!boardTemplate) throw new InvalidOperationException("Instruction board template is missing.");
            var root = Group(ExtensionName, originalRoot.transform);
            var floors = Group("01 — Пол и площадки", root);
            var walls = Group("02 — Стены и потолок", root);
            var objects = Group("03 — Изменяемые объекты", root);
            var guidance = Group("04 — Подсказки и свет", root);
            var checkpoints = Group("05 — Точки возрождения", root);

            // Open the old finish; all original trial geometry keeps its transforms.
            var endWall = GameObject.Find("End wall");
            if (!endWall) throw new InvalidOperationException("Original end wall is missing.");
            endWall.transform.position = new Vector3(0, 6.2f, 112.25f);
            endWall.transform.localScale = new Vector3(9, 12.4f, .5f);
            var oldFinish = GameObject.Find("Finish");
            if (!oldFinish) throw new InvalidOperationException("Original finish trigger is missing.");
            oldFinish.name = "Checkpoint before combined trials";
            oldFinish.GetComponent<ResizeCheckpoint>().Configure(new Vector3(0, 3.03f, 49));
            var oldBoard = GameObject.Find("ОБУЧЕНИЕ ПРОЙДЕНО");
            if (oldBoard) Object.DestroyImmediate(oldBoard);

            Box(walls, "Extension left wall", new Vector3(-4.75f, 6.2f, 84), new Vector3(.5f, 12.4f, 56), concrete);
            Box(walls, "Extension right wall", new Vector3(4.75f, 6.2f, 84), new Vector3(.5f, 12.4f, 56), concrete);
            Box(walls, "Extension roof", new Vector3(0, 12.65f, 84), new Vector3(10, .5f, 56), concrete);

            // 04: the opposite landing is offset to the right. Length alone is insufficient.
            Box(floors, "04 Right landing", new Vector3(3.3f, 2.75f, 68), new Vector3(2.4f, .5f, 4), concrete);
            Box(floors, "04 Rest platform", new Vector3(0, 2.75f, 74), new Vector3(9, .5f, 8), concrete);
            Resizable(objects, "04 Offset bridge", "Пол — удлиняемый мост", new Vector3(0, 3, 56),
                new Vector3(.8f, .35f, 2), new Vector3(.8f, .35f, 1), new Vector3(8.8f, .35f, 10), new Vector3(0, .5f, -.5f));
            Board(guidance, "04 / ДЛИНА + ШИРИНА", "Площадка впереди справа\n1 — дотяни мост до края\n3 — расширь его к площадке\nРазмеры можно менять с моста", new Vector3(-4.42f, 5.3f, 52), Quaternion.Euler(0, -90, 0));
            Board(guidance, "ВЫХОД СПРАВА →", "Расширь мост, чтобы перейти\nна боковую площадку", new Vector3(4.42f, 5.3f, 68), Quaternion.Euler(0, 90, 0));
            Checkpoint(checkpoints, "Checkpoint after offset bridge", new Vector3(0, 4.4f, 72), new Vector3(0, 3.03f, 73));

            // 05: lift to the next storey, then reduce the wall width to go around it.
            Resizable(objects, "05 Lift", "Платформа — подъём", new Vector3(0, 3, 79.5f),
                new Vector3(3.2f, .18f, 3), new Vector3(1.2f, .18f, 3), new Vector3(5, 3, 3), Vector3.down * .5f);
            Box(floors, "05 Upper platform", new Vector3(0, 5.75f, 85.5f), new Vector3(9, .5f, 9), concrete);
            Resizable(objects, "05 Upper wall", "Стена — изменение размеров", new Vector3(0, 6, 85),
                new Vector3(8.8f, 2.8f, .55f), new Vector3(1.5f, .12f, .3f), new Vector3(8.8f, 3.4f, 2), Vector3.down * .5f);
            Board(guidance, "05 / ПОДЪЁМ И ПРОХОД", "Встань на платформу\n2 — подними себя на этаж\nНа этаже наведи на стену\n3 — сузь её и пройди сбоку", new Vector3(-4.42f, 5.3f, 75), Quaternion.Euler(0, -90, 0));
            Checkpoint(checkpoints, "Checkpoint after combined lift", new Vector3(0, 7.4f, 88), new Vector3(0, 6.03f, 88.5f));

            // 06: one bottom-anchored block becomes a tall, wide bridge to a left landing.
            Resizable(objects, "06 Final transform bridge", "Блок — все размеры", new Vector3(0, 3, 90),
                new Vector3(2, 3, 2), new Vector3(.8f, 3, 1), new Vector3(8.8f, 6, 10), new Vector3(0, -.5f, -.5f));
            Box(floors, "06 Left landing", new Vector3(-3.3f, 8.75f, 102), new Vector3(2.4f, .5f, 4), concrete);
            Box(floors, "06 Finish floor", new Vector3(0, 8.75f, 108), new Vector3(9, .5f, 8), concrete);
            Board(guidance, "06 / ТРИ РАЗМЕРА", "Встань на блок и смотри вниз\n2 — поднимись до верхнего края\n1 — удлини до дальней площадки\n3 — расширь к выходу слева", new Vector3(-4.42f, 8.3f, 88), Quaternion.Euler(0, -90, 0));
            Board(guidance, "← ВЫХОД СЛЕВА", "Высота, длина и ширина\nпомогают построить один путь", new Vector3(-4.42f, 11.2f, 102), Quaternion.Euler(0, -90, 0));
            Board(guidance, "ОБУЧЕНИЕ ПРОЙДЕНО", "Все шесть испытаний завершены\nТы умеешь сочетать три размера\nМожно вернуться и попробовать иначе", new Vector3(0, 11.1f, 111.94f), Quaternion.identity);
            Checkpoint(checkpoints, "Finish", new Vector3(0, 10.4f, 108), new Vector3(0, 9.03f, 106), true);

            foreach (var edge in new[] { new Vector3(0, 3.012f, 55.7f), new Vector3(0, 3.012f, 77.7f), new Vector3(0, 6.012f, 89.7f) })
                Box(guidance, "Pit edge stripe", edge, new Vector3(8.7f, .018f, .1f), amber, false);
            foreach (var edge in new[] { new Vector3(3.3f, 3.012f, 66.15f), new Vector3(-3.3f, 9.012f, 100.15f) })
                Box(guidance, "Offset landing stripe", edge, new Vector3(2.3f, .018f, .12f), cyan, false);
            Box(guidance, "04 Pit bottom", new Vector3(0, -11, 63), new Vector3(9, .5f, 14), dark);
            Box(guidance, "05 Pit bottom", new Vector3(0, -11, 79.5f), new Vector3(9, .5f, 3), dark);
            Box(guidance, "06 Pit bottom", new Vector3(0, -11, 97), new Vector3(9, .5f, 14), dark);
            for (int z = 58; z < 112; z += 8)
            {
                float height = z < 78 ? 5.6f : z < 100 ? 8.6f : 11.6f;
                Box(guidance, "Ceiling rib", new Vector3(0, 12.25f, z), new Vector3(9, .22f, .22f), dark);
                foreach (int side in new[] { -1, 1 })
                    Box(guidance, "Warm wall strip", new Vector3(side * 4.46f, height, z), new Vector3(.06f, .13f, 1.3f), amber, false);
                var lamp = Group("Extension light", guidance).gameObject.AddComponent<Light>();
                lamp.transform.position = new Vector3(0, height + .4f, z); lamp.type = LightType.Point;
                lamp.color = new Color(1, .82f, .61f); lamp.intensity = 3; lamp.range = 10; lamp.shadows = LightShadows.None;
            }
            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("RESIZE_EXTENSION_BUILT: six trials, 115 metre route, three combined challenges; original first three trials retained.");
        }

        static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskResize/Materials/" + name + ".mat");
        static Transform Group(string name, Transform parent)
        {
            var result = new GameObject(name).transform; result.SetParent(parent, false); return result;
        }
        static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name; result.transform.SetParent(parent, false);
            result.transform.position = position; result.transform.localScale = size;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(result.GetComponent<Collider>());
        }
        static void Resizable(Transform parent, string name, string prefab, Vector3 position, Vector3 size, Vector3 min, Vector3 max, Vector3 anchor)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + prefab + ".prefab");
            if (!source) throw new InvalidOperationException("Missing resize prefab: " + prefab);
            var result = Object.Instantiate(source, parent); result.name = name;
            result.transform.SetPositionAndRotation(position, Quaternion.identity); result.transform.localScale = Vector3.one;
            var component = result.GetComponent<ResizableMaskObject>();
            component.Configure(component.Solid, size, min, max, anchor);
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
    }
}
