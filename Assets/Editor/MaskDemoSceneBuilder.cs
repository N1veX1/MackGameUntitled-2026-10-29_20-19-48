using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates an editable demo scene once after import. The existing SampleScene
/// is kept untouched. Run Tools/Mask Demo/Open Scene to inspect the result.
/// </summary>
[InitializeOnLoad]
public static class MaskDemoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MaskSixViews.unity";
    private const string MaterialFolder = "Assets/MaskDemoMaterials";

    static MaskDemoSceneBuilder()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    [MenuItem("Tools/Mask Demo/Open Scene")]
    public static void OpenScene()
    {
        CreateIfMissing();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath)) return;
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets", "MaskDemoMaterials");

        Scene previous = SceneManager.GetActiveScene();
        // Unity batch mode starts with an untitled scene, which cannot host an
        // additive scene. Keep any saved user scene open in the interactive editor.
        if (!Application.isBatchMode && previous.IsValid() &&
            string.IsNullOrEmpty(previous.path) && previous.isDirty) return;
        bool preservePrevious = previous.IsValid() && !string.IsNullOrEmpty(previous.path);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            preservePrevious ? NewSceneMode.Additive : NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        try
        {
            BuildScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Mask demo scene created: " + ScenePath);
        }
        finally
        {
            if (preservePrevious)
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void BuildScene()
    {
        Material concrete = MakeMaterial("Concrete", new Color(.61f, .61f, .63f));
        Material darker = MakeMaterial("ConcreteDark", new Color(.38f, .39f, .42f));
        Material character = MakeMaterial("Character", new Color(.88f, .87f, .85f));
        Material visor = MakeMaterial("Visor", new Color(.28f, .16f, .43f));
        Material red = MakeMaterial("BlockedRed", new Color(.77f, .08f, .17f));

        GameObject level = new GameObject("Concrete Level — one continuous corridor");
        Renderer floor = Cube("Floor / visible underside", level.transform,
            new Vector3(0, -.25f, 5), new Vector3(6, .5f, 18), concrete);
        Renderer leftWall = Cube("Left wall", level.transform,
            new Vector3(-3.25f, 2, 5), new Vector3(.5f, 4.5f, 18), darker);
        Renderer ceiling = Cube("Ceiling", level.transform,
            new Vector3(0, 4.25f, 5), new Vector3(6, .5f, 18), concrete);
        Renderer blocker = Cube("Single blocking wall", level.transform,
            new Vector3(-.8f, 2, 2), new Vector3(4.4f, 4, .35f), concrete);
        Collider blockerCollider = blocker.GetComponent<Collider>();

        Renderer slashA = Cube("Red X A", level.transform,
            new Vector3(0, 2, 1.79f), new Vector3(.15f, 2.2f, .07f), red, false);
        slashA.transform.rotation = Quaternion.Euler(0, 0, 45);
        Renderer slashB = Cube("Red X B", level.transform,
            new Vector3(0, 2, 1.78f), new Vector3(.15f, 2.2f, .07f), red, false);
        slashB.transform.rotation = Quaternion.Euler(0, 0, -45);

        GameObject player = new GameObject("Player");
        player.transform.position = new Vector3(0, 1, -1.8f);
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.radius = .35f;
        controller.height = 1.9f;
        controller.center = Vector3.zero;
        player.AddComponent<MaskDemoPlayer>();
        Cube("Torso", player.transform, new Vector3(0, -.1f, 0),
            new Vector3(.58f, .8f, .35f), darker, false, true);
        Sphere("Head", player.transform, new Vector3(0, .52f, 0), .48f, character);
        Cube("Purple visor / front is +Z", player.transform,
            new Vector3(0, .55f, .235f), new Vector3(.35f, .13f, .05f), visor, false, true);
        Cube("Left arm", player.transform, new Vector3(-.37f, -.1f, 0),
            new Vector3(.16f, .69f, .2f), character, false, true);
        Cube("Right arm", player.transform, new Vector3(.37f, -.1f, 0),
            new Vector3(.16f, .69f, .2f), character, false, true);
        Cube("Left leg", player.transform, new Vector3(-.18f, -.68f, 0),
            new Vector3(.18f, .52f, .23f), character, false, true);
        Cube("Right leg", player.transform, new Vector3(.18f, -.68f, 0),
            new Vector3(.18f, .52f, .23f), character, false, true);

        GameObject cameraObject = new GameObject("Player Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0, .78f, .12f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 100f;
        camera.fieldOfView = 65f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.10f, .10f, .14f);
        cameraObject.AddComponent<AudioListener>();

        GameObject focus = new GameObject("Mask Camera Focus / move to reframe all six views");
        focus.transform.position = new Vector3(0, 1.5f, 5);
        MaskAxisCamera mask = cameraObject.AddComponent<MaskAxisCamera>();
        SerializedObject maskData = new SerializedObject(mask);
        maskData.FindProperty("viewCamera").objectReferenceValue = camera;
        maskData.FindProperty("focusTarget").objectReferenceValue = focus.transform;
        maskData.FindProperty("cameraDistance").floatValue = 13f;
        maskData.FindProperty("orthographicSize").floatValue = 6.5f;
        SetArray(maskData, "wallRenderers", blocker, slashA, slashB);
        SetArray(maskData, "wallColliders", blockerCollider);
        SetArray(maskData, "topCutawayRenderers", ceiling);
        SetArray(maskData, "oppositeSideCutawayRenderers", leftWall);
        maskData.ApplyModifiedPropertiesWithoutUndo();

        GameObject uiObject = new GameObject("Mask buttons and mode labels");
        MaskDemoUI ui = uiObject.AddComponent<MaskDemoUI>();
        SerializedObject uiData = new SerializedObject(ui);
        uiData.FindProperty("mask").objectReferenceValue = mask;
        uiData.ApplyModifiedPropertiesWithoutUndo();

        GameObject lightObject = new GameObject("Soft directional light");
        lightObject.transform.rotation = Quaternion.Euler(50, -35, 0);
        Light sun = lightObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(.96f, .95f, 1f);
        sun.intensity = 1.4f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .5f;

        // Keep floor solid: it occludes the character in the -Y underside view.
        floor.GetComponent<Collider>().enabled = true;
    }

    private static void SetArray(SerializedObject data, string name, params Object[] values)
    {
        SerializedProperty array = data.FindProperty(name);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static Material MakeMaterial(string name, Color color)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material found = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (found != null) return found;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        else material.color = color;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Renderer Cube(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, bool collider = true, bool local = false)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        if (local) cube.transform.localPosition = position;
        else cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(cube.GetComponent<Collider>());
        return cube.GetComponent<Renderer>();
    }

    private static void Sphere(string name, Transform parent, Vector3 localPosition,
        float diameter, Material material)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localPosition = localPosition;
        sphere.transform.localScale = Vector3.one * diameter;
        sphere.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(sphere.GetComponent<Collider>());
    }

    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene scene in scenes)
            if (scene.path == ScenePath) return;
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
