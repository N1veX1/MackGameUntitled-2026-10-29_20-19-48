using UnityEditor;
using UnityEngine;

/// <summary>Restore the supplied mask in Scene View without changing its mesh or materials.</summary>
public static class OrnamentMaskVisibilityEditor
{
    [MenuItem("Tools/Mask Traversal/Show masks in Scene View")]
    public static void RepairOpenScenes()
    {
        if (Application.isPlaying) return;
        foreach (var controller in Object.FindObjectsByType<CornerMaskTraversalController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Repair(controller);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
    }
    public static void Repair(CornerMaskTraversalController controller)
    {
        var data = new SerializedObject(controller);
        var mask = data.FindProperty("ornamentMask").objectReferenceValue as GameObject;
        var camera = data.FindProperty("playerCamera").objectReferenceValue as Camera;
        if (!mask || !camera) return;
        var view = camera.GetComponent<OrnamentMaskView>();
        if (!view) return;
        var settings = new SerializedObject(view);
        settings.FindProperty("wornPosition").vector3Value = new Vector3(0, -.02f, .09f);
        settings.ApplyModifiedPropertiesWithoutUndo();
        view.Configure(camera, mask, controller);
        view.ApplyState(false);
        foreach (var component in mask.GetComponentsInChildren<Component>(true))
        {
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(component.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component.gameObject);
        }
        EditorUtility.SetDirty(view); EditorUtility.SetDirty(camera);
        PrefabUtility.RecordPrefabInstancePropertyModifications(view);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
    }
}
