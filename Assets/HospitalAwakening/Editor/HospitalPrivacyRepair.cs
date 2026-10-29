using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

[InitializeOnLoad]
public static class HospitalPrivacyRepair {
    const string Base="Assets/HospitalAwakening/";
    static HospitalPrivacyRepair() { EditorApplication.delayCall+=Apply; }
    static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material) {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetParent(parent,false); o.transform.localPosition=position; o.transform.localScale=size; o.GetComponent<Renderer>().sharedMaterial=material; return o;
    }
    static void Apply() {
        if(!File.Exists(Base+"privacy-repair.pending")) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating) { EditorApplication.delayCall+=Apply; return; }
        try {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if(scene.name!="GOSPITOL CHAPTER 1 AWAKING") return;
            var material=AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/Architecture_Walls_TextureSlot.mat");
            var root=GameObject.Find("OPAQUE MEDICAL PERIMETER"); if(root) Object.DestroyImmediate(root); root=new GameObject("OPAQUE MEDICAL PERIMETER");
            // The two wings are enclosed, with only the main corridor's end doorways left open.
            foreach(float x in new[]{-15.55f,86.94f}) {
                Box("Opaque north exterior wall",root.transform,new Vector3(x,1.65f,-.03f),new Vector3(.24f,3.3f,17.66f),material);
                Box("Opaque south exterior wall",root.transform,new Vector3(x,1.65f,-22.44f),new Vector3(.24f,3.3f,17.58f),material);
            }
            foreach(float z in new[]{8.8f,-31.3f}) Box("Opaque rear exterior wall",root.transform,new Vector3(35.7f,1.65f,z),new Vector3(102.7f,3.3f,.24f),material);
            // Seal the short joins beside the end doors without crossing their openings.
            foreach(float x in new[]{-15.3f,86.7f}) foreach(float z in new[]{-13.1f,-9.3f}) Box("End door wall return",root.transform,new Vector3(x,1.35f,z),new Vector3(.8f,2.7f,1.55f),material);
            int sanitary=0;
            foreach(var t in Object.FindObjectsByType<Transform>()) if(t.name=="Sanitary station / toilet and washbasin") {
                var old=new List<GameObject>(); foreach(Transform child in t) if(child.name.StartsWith("Privacy enclosure")) old.Add(child.gameObject); foreach(var child in old) Object.DestroyImmediate(child);
                Box("Privacy enclosure left",t,new Vector3(-1.15f,1.2f,-.32f),new Vector3(.1f,2.4f,1.48f),material);
                Box("Privacy enclosure right",t,new Vector3(1.39f,1.2f,-.32f),new Vector3(.1f,2.4f,1.48f),material);
                Box("Privacy enclosure front",t,new Vector3(-.39f,1.2f,-1.01f),new Vector3(1.62f,2.4f,.1f),material);
                Box("Privacy enclosure back",t,new Vector3(.12f,1.2f,.41f),new Vector3(2.64f,2.4f,.1f),material);
                sanitary++;
            }
            Physics.SyncTransforms();
            foreach(float z in new[]{-27f,-20f,-4f,4f}) {
                if(!Physics.Raycast(new Vector3(-18,1.5f,z),Vector3.right,out var hit,3.5f)||!hit.collider.name.StartsWith("Opaque")) throw new System.Exception("Exterior not sealed at west z="+z);
                if(!Physics.Raycast(new Vector3(90,1.5f,z),Vector3.left,out hit,3.5f)||!hit.collider.name.StartsWith("Opaque")) throw new System.Exception("Exterior not sealed at east z="+z);
            }
            if(Physics.Raycast(new Vector3(-14,1.5f,-11.2f),Vector3.right,99)) throw new System.Exception("Main corridor blocked");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            typeof(HospitalWingExpansion).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            ExteriorPreview();
            File.WriteAllText("C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs/hospital-privacy-report.txt","Saved opaque exterior perimeter.\n"+sanitary+" sanitary enclosures added, each with a 0.9 m entry.\nExterior ray checks passed on both wings. Main corridor clear.\n");
            AssetDatabase.DeleteAsset(Base+"privacy-repair.pending"); Debug.Log("HOSPITAL_PRIVACY_REPAIR_SAVED");
        } catch(System.Exception e) { File.WriteAllText(Base+"privacy-repair-error.txt",e.ToString()); Debug.LogException(e); }
    }
    static void ExteriorPreview() {
        var o=new GameObject("Temporary exterior preview"); var c=o.AddComponent<Camera>(); c.CopyFrom(Object.FindAnyObjectByType<HospitalExplorer>().eyes); c.farClipPlane=300;
        typeof(HospitalSceneBuilder).GetMethod("SetupRenderer",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{c});
        c.transform.position=new Vector3(-25,10,-38); c.transform.LookAt(new Vector3(22,1,-7));
        var r=new RenderTexture(1600,1000,24); var before=RenderTexture.active; c.targetTexture=r; c.Render(); RenderTexture.active=r;
        var t=new Texture2D(1600,1000,TextureFormat.RGB24,false); t.ReadPixels(new Rect(0,0,1600,1000),0,0); t.Apply(); File.WriteAllBytes("C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs/hospital-exterior-fixed.png",t.EncodeToPNG());
        RenderTexture.active=before; Object.DestroyImmediate(t); r.Release(); Object.DestroyImmediate(r); Object.DestroyImmediate(o);
    }
}
