using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
[InitializeOnLoad]
public static class HospitalSanitaryPlacement {
    static HospitalSanitaryPlacement() { EditorApplication.delayCall+=Apply; }
    static void Apply() {
        if(!File.Exists("Assets/HospitalAwakening/sanitary.pending")) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating) { EditorApplication.delayCall+=Apply; return; }
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.name!="GOSPITOL CHAPTER 1 AWAKING") return;
        var parent=GameObject.Find("03 • Central patient room"); if(!parent) return;
        var old=parent.transform.Find("Sanitary station / toilet and washbasin");
        // Names contain slashes, so find the direct child by equality.
        foreach(Transform child in parent.transform) if(child.name=="Sanitary station / toilet and washbasin") old=child;
        if(!old) HospitalFurniture.Sanitary(new Vector3(2.75f,0,8.1f),0,parent.transform);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        var camera=Object.FindAnyObjectByType<Camera>(); var p=camera.transform.position; var q=camera.transform.rotation;
        var target=camera.targetTexture; var prior=RenderTexture.active; var rt=new RenderTexture(1600,1000,24);
        camera.transform.position=new Vector3(.4f,1.7f,5.2f); camera.transform.LookAt(new Vector3(2.45f,.9f,8.0f));
        camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply();
        File.WriteAllBytes("C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs/hospital-sanitary.png",image.EncodeToPNG());
        camera.transform.SetPositionAndRotation(p,q); camera.targetTexture=target; RenderTexture.active=prior; Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt);
        AssetDatabase.DeleteAsset("Assets/HospitalAwakening/sanitary.pending");
        Debug.Log("SANITARY_PLACEMENT_SAVED: central patient room, rear wall next to bed");
    }
}
// Apply after leaving Play Mode.

