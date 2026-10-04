using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Attaches the imported mask while preserving tutorial scene references.</summary>
public static class InversionMaskModelBuilder
{
    public const string ImportedMaskPath = "Assets/SplitMask/Prefabs/SplitMask.prefab";
    const string PrefabPath = "Assets/MaskInversion/Generated/SplitInversionMask.prefab";

    [MenuItem("Tools/Mask Inversion/Use Imported Inversion Mask")]
    public static void Rebuild()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(ImportedMaskPath);
        if (!source) throw new FileNotFoundException("Import Инверсия.unitypackage first.", ImportedMaskPath);
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("SplitInversionMask");
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; --i)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            var mask = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            mask.transform.localPosition = Vector3.zero;
            mask.transform.localRotation = Quaternion.identity;
            mask.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            if (exists) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Tutorial 2 uses the imported SplitMask prefab from Инверсия.unitypackage.");
    }
}
