using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Builds Level 1 as an editable copy of the six-view demo scene.</summary>
[InitializeOnLoad]
public static class MaskTutorialSceneBuilder
{
    private const string SourceScene = "Assets/Scenes/MaskSixViews.unity";
    private const string TutorialScene = "Assets/Scenes/Level01_Tutorial.unity";
    private const string PurpleMaterial = "Assets/MaskDemoMaterials/TutorialPurple.mat";

    static MaskTutorialSceneBuilder()
    {
        EditorApplication.delayCall += CreateIfMissing;
    }

    [MenuItem("Tools/Mask Demo/Open Level 1 Tutorial")]
    public static void OpenLevel()
    {
        CreateIfMissing();
        if (File.Exists(TutorialScene))
            EditorSceneManager.OpenScene(TutorialScene, OpenSceneMode.Single);
        else
            Debug.LogWarning("Level 1 could not be created. Save the current untitled scene and try again.");
    }

    private static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            !File.Exists(SourceScene) || File.Exists(TutorialScene)) return;

        Scene previous = SceneManager.GetActiveScene();
        if (!Application.isBatchMode && previous.IsValid() &&
            string.IsNullOrEmpty(previous.path) && previous.isDirty) return;
        bool preservePrevious = previous.IsValid() && !string.IsNullOrEmpty(previous.path);

        if (!AssetDatabase.CopyAsset(SourceScene, TutorialScene))
        {
            Debug.LogError("Could not copy demo scene to tutorial scene.");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(TutorialScene,
            preservePrevious ? OpenSceneMode.Additive : OpenSceneMode.Single);
        SceneManager.SetActiveScene(scene);
        try
        {
            BuildTutorial(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Level 1 tutorial created: " + TutorialScene);
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

    private static void BuildTutorial(Scene scene)
    {
        GameObject player = FindRoot(scene, "Player");
        if (player == null) throw new System.InvalidOperationException("Demo scene has no Player.");
        Transform cameraTransform = player.transform.Find("Player Camera");
        if (cameraTransform == null)
            throw new System.InvalidOperationException("Demo scene has no Player Camera.");
        GameObject cameraObject = cameraTransform.gameObject;
        GameObject demoUi = FindRoot(scene, "Mask buttons and mode labels");
        MaskAxisCamera mask = cameraObject.GetComponent<MaskAxisCamera>();
        if (mask == null)
            throw new System.InvalidOperationException("Demo scene has no MaskAxisCamera.");

        if (demoUi != null) Object.DestroyImmediate(demoUi);

        GameObject tutorial = new GameObject("Level 1 Tutorial Controller");
        MaskTutorialLevel tutorialComponent = tutorial.AddComponent<MaskTutorialLevel>();
        SerializedObject data = new SerializedObject(tutorialComponent);
        data.FindProperty("mask").objectReferenceValue = mask;
        data.FindProperty("player").objectReferenceValue = player.transform;
        data.ApplyModifiedPropertiesWithoutUndo();

        Material purple = AssetDatabase.LoadAssetAtPath<Material>(PurpleMaterial);
        if (purple == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            purple = new Material(shader) { name = "TutorialPurple" };
            Color color = new Color(.56f, .28f, .9f);
            if (purple.HasProperty("_BaseColor")) purple.SetColor("_BaseColor", color);
            else purple.color = color;
            if (purple.HasProperty("_EmissionColor"))
            {
                purple.EnableKeyword("_EMISSION");
                purple.SetColor("_EmissionColor", color * 1.5f);
            }
            AssetDatabase.CreateAsset(purple, PurpleMaterial);
        }

        GameObject landmarks = new GameObject("Tutorial landmarks / no passage blockers");
        Marker(PrimitiveType.Cube, "Start beacon", landmarks.transform,
            new Vector3(2.75f, 1.35f, -.8f), new Vector3(.14f, 2.7f, .14f), purple);
        Marker(PrimitiveType.Cube, "Exit left beacon", landmarks.transform,
            new Vector3(-2.55f, 1.4f, 12.2f), new Vector3(.14f, 2.8f, .14f), purple);
        Marker(PrimitiveType.Cube, "Exit right beacon", landmarks.transform,
            new Vector3(2.55f, 1.4f, 12.2f), new Vector3(.14f, 2.8f, .14f), purple);
        Marker(PrimitiveType.Sphere, "Exit light orb", landmarks.transform,
            new Vector3(0, 3.1f, 12.2f), Vector3.one * .48f, purple);

        GameObject lightObject = new GameObject("Exit purple light");
        lightObject.transform.position = new Vector3(0, 2.8f, 12.2f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(.57f, .34f, 1f);
        light.range = 6f;
        light.intensity = 2.5f;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static void Marker(PrimitiveType type, string name, Transform parent,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject marker = GameObject.CreatePrimitive(type);
        marker.name = name;
        marker.transform.SetParent(parent, false);
        marker.transform.position = position;
        marker.transform.localScale = scale;
        marker.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(marker.GetComponent<Collider>());
    }

    private static void AddToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene item in scenes)
            if (item.path == TutorialScene) return;
        scenes.Insert(0, new EditorBuildSettingsScene(TutorialScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
