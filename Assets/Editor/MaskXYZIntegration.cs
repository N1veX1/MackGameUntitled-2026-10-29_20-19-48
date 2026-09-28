using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Replaces the temporary visor with the imported MaskXYZ prefab.</summary>
public static class MaskXYZIntegration
{
    private const string PrefabPath = "Assets/MaskXYZ/Generated/MaskXYZ.prefab";
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MaskSixViews.unity",
        "Assets/Scenes/Level01_Tutorial.unity"
    };

    [MenuItem("Tools/Mask Demo/Use Imported MaskXYZ Prefab")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) throw new InvalidOperationException("MaskXYZ prefab was not found: " + PrefabPath);

        foreach (string path in ScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            GameObject player = FindRoot(scene, "Player");
            if (player == null) throw new InvalidOperationException(path + ": Player was not found.");

            Transform oldVisor = FindDirectChild(player.transform, "Purple visor / front is +Z");
            if (oldVisor != null) UnityEngine.Object.DestroyImmediate(oldVisor.gameObject);

            Transform existing = FindDirectChild(player.transform, "MaskXYZ — equipped visual");
            GameObject visual;
            if (existing != null) visual = existing.gameObject;
            else
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                visual.name = "MaskXYZ — equipped visual";
                visual.transform.SetParent(player.transform, false);
                visual.transform.localPosition = new Vector3(0f, .52f, .235f);
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
            }

            MaskAxisCamera controller = player.GetComponentInChildren<MaskAxisCamera>(true);
            if (controller == null)
                throw new InvalidOperationException(path + ": MaskAxisCamera was not found.");
            SerializedObject data = new SerializedObject(controller);
            SerializedProperty visualProperty = data.FindProperty("maskVisual");
            if (visualProperty == null)
                throw new InvalidOperationException("MaskAxisCamera has no maskVisual field.");
            visualProperty.objectReferenceValue = visual;
            data.ApplyModifiedPropertiesWithoutUndo();
            visual.SetActive(false);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Imported MaskXYZ prefab attached: " + path);
        }
        AssetDatabase.SaveAssets();
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == name) return root;
        return null;
    }

    private static Transform FindDirectChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
            if (child.name == name) return child;
        return null;
    }
}
