using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MaskXYZ.Editor
{
    public static class MaskDemoBuilder
    {
        const string ScenePath = "Assets/Scenes/MaskDemo.unity";
        const string Generated = "Assets/MaskXYZ/Generated";

        [MenuItem("Tools/Mask XYZ/Build Complete Demo")]
        public static void Build()
        {
            MaskPrefabBuilder.Build();
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.56f, 0.59f);

            var concrete = Material("Concrete", new Color(0.48f, 0.49f, 0.51f));
            var darkConcrete = Material("DarkConcrete", new Color(0.29f, 0.30f, 0.33f));
            var characterMaterial = Material("BlockoutCharacter", new Color(0.74f, 0.76f, 0.78f));
            var purple = Material("PassagePurple", new Color(0.54f, 0.22f, 0.94f), true);
            var red = Material("BlockedRed", new Color(0.92f, 0.12f, 0.16f), true);

            var level = new GameObject("Concrete level — same geometry in every view");
            Cube("Continuous floor", level.transform, new Vector3(0, -0.1f, 0),
                new Vector3(4, 0.2f, 10), concrete);
            Cube("Left wall at entrance", level.transform, new Vector3(-2.1f, 1.5f, -3.75f),
                new Vector3(0.2f, 3, 2.5f), concrete);
            Cube("Entrance ceiling", level.transform, new Vector3(0, 3.05f, -3.75f),
                new Vector3(4.2f, 0.2f, 2.5f), concrete);
            var blockerGroup = new GameObject("Blocking obstacle and red X — solid geometry");
            blockerGroup.transform.SetParent(level.transform, false);
            Cube("Blocking wall", blockerGroup.transform,
                new Vector3(-.8f, 1.5f, 0), new Vector3(2.4f, 3, 0.25f), darkConcrete);
            var slashA = Cube("Red X left stroke", blockerGroup.transform,
                new Vector3(-0.2f, 1.5f, -0.15f), new Vector3(0.12f, 1.05f, 0.025f), red);
            slashA.transform.localRotation = Quaternion.Euler(0, 0, 38);
            var slashB = Cube("Red X right stroke", blockerGroup.transform,
                new Vector3(0.2f, 1.5f, -0.15f), new Vector3(0.12f, 1.05f, 0.025f), red);
            slashB.transform.localRotation = Quaternion.Euler(0, 0, -38);

            var character = new GameObject("Blockout character");
            character.transform.position = new Vector3(0, 0, -1.55f);
            Primitive("Body", PrimitiveType.Capsule, character.transform,
                new Vector3(0, 0.8f, 0), new Vector3(0.58f, 0.78f, 0.42f), characterMaterial);
            var head = Primitive("Head", PrimitiveType.Sphere, character.transform,
                new Vector3(0, 1.67f, 0), new Vector3(0.37f, 0.39f, 0.36f), characterMaterial);
            var maskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/MaskXYZ.prefab");
            if (!maskPrefab) throw new InvalidOperationException("Mask prefab was not generated.");
            var mask = (GameObject)PrefabUtility.InstantiatePrefab(maskPrefab);
            mask.name = "Mask XYZ — equipped visual";
            mask.transform.SetParent(character.transform, false);
            mask.transform.localPosition = new Vector3(0, 1.67f, 0.16f);
            mask.transform.localScale = Vector3.one * 1.15f;

            var arrow = new GameObject("Purple passage arrow");
            arrow.transform.position = new Vector3(1.2f, 0, 0);
            Cube("Arrow shaft", arrow.transform, new Vector3(0, 0.015f, 1.35f),
                new Vector3(0.12f, 0.012f, 2.2f), purple);
            var tipLeft = Cube("Arrow tip left", arrow.transform,
                new Vector3(-0.23f, 0.015f, 2.48f), new Vector3(0.11f, 0.012f, 0.75f), purple);
            tipLeft.transform.localRotation = Quaternion.Euler(0, -43, 0);
            var tipRight = Cube("Arrow tip right", arrow.transform,
                new Vector3(0.23f, 0.015f, 2.48f), new Vector3(0.11f, 0.012f, 0.75f), purple);
            tipRight.transform.localRotation = Quaternion.Euler(0, 43, 0);

            var light = new GameObject("Soft directional light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -25, 0);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.59f, 0.61f, 0.65f);
            camera.fieldOfView = 65;
            camera.nearClipPlane = 0.03f;
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, -4.7f), Quaternion.identity);
            cameraGo.AddComponent<AudioListener>();

            var focus = new GameObject("View focus");
            focus.transform.position = new Vector3(0, 1.1f, -0.1f);
            var controller = new GameObject("Mask axis controller").AddComponent<MaskAxisCamera>();
            controller.Configure(camera, focus.transform, new[] { blockerGroup }, mask, new[] { arrow });

            CreateHud(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Mask demo scene saved: " + ScenePath);
        }

        public static void BuildForIntegration() { Build(); }

        static Material Material(string name, Color color, bool emission = false)
        {
            string path = Generated + "/" + name + ".mat";
            var old = AssetDatabase.LoadMainAssetAtPath(path);
            if (old) AssetDatabase.DeleteAsset(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
            if (emission)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject Primitive(string name, PrimitiveType type, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static GameObject Cube(string name, Transform parent, Vector3 position,
            Vector3 scale, Material material)
        {
            return Primitive(name, PrimitiveType.Cube, parent, position, scale, material);
        }

        static void CreateHud(MaskAxisCamera controller)
        {
            var canvasGo = new GameObject("Mask UI", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var panel = UiObject("Controls panel", canvasGo.transform, new Vector2(25, -25),
                new Vector2(770, 208));
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.045f, 0.047f, 0.06f, 0.89f);
            var state = Label("State", panel.transform, "БЕЗ МАСКИ  •  ПУТЬ ЗАКРЫТ",
                new Vector2(20, -15), new Vector2(730, 36), 26, TextAnchor.MiddleLeft);
            var view = Label("View caption", panel.transform, "M — НАДЕТЬ МАСКУ",
                new Vector2(20, -53), new Vector2(730, 28), 19, TextAnchor.MiddleLeft);
            Label("Tagline", panel.transform, "ОДНА МАСКА — ШЕСТЬ ОБЗОРОВ",
                new Vector2(20, -161), new Vector2(730, 26), 18, TextAnchor.MiddleLeft);

            var maskButton = UiButton("Equip mask", panel.transform, "МАСКА",
                new Vector2(20, -93), new Vector2(124, 55));
            string[] names = { "1  X / -X", "2  Y / -Y", "3  Z / -Z" };
            var buttons = new Button[3];
            for (int i = 0; i < 3; i++)
                buttons[i] = UiButton("Mode " + names[i], panel.transform, names[i],
                    new Vector2(157 + 187 * i, -93), new Vector2(171, 55));

            var hud = canvasGo.AddComponent<MaskDemoHud>();
            hud.Configure(controller, maskButton, buttons, state, view);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        static GameObject UiObject(string name, Transform parent, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return go;
        }

        static Text Label(string name, Transform parent, string value, Vector2 offset,
            Vector2 size, int fontSize, TextAnchor align)
        {
            var go = UiObject(name, parent, offset, size);
            var text = go.AddComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        static Button UiButton(string name, Transform parent, string caption,
            Vector2 offset, Vector2 size)
        {
            var go = UiObject(name, parent, offset, size);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.17f, 0.18f, 0.22f);
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            Label("Label", go.transform, caption, Vector2.zero, size, 22, TextAnchor.MiddleCenter);
            return button;
        }
    }
}

