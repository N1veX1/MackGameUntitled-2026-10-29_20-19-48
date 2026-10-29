using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Reflection;

[InitializeOnLoad]
public static class HospitalObservationRevision {
    const string Base="Assets/HospitalAwakening/";
    static Material glass,metal,wall;
    static HospitalObservationRevision() { EditorApplication.delayCall+=Apply; }
    static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material mat) {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetParent(parent,false); o.transform.localPosition=p; o.transform.localScale=size; o.GetComponent<Renderer>().sharedMaterial=mat; return o;
    }
    static void Glass(Transform room,float x,List<Renderer> all) {
        for(int i=0;i<9;i++) {
            float z=-7.68f+i*1.92f;
            var pane=Box("Observation glass",room,new Vector3(x,1.7f,z),new Vector3(.035f,3.18f,1.88f),glass); pane.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; all.Add(pane.GetComponent<Renderer>());
            Box("Observation mullion",room,new Vector3(x,1.65f,z-.96f),new Vector3(.075f,3.3f,.045f),metal);
            Box("Observation lower frame",room,new Vector3(x,.07f,z),new Vector3(.08f,.14f,1.92f),metal);
            Box("Observation upper frame",room,new Vector3(x,3.22f,z),new Vector3(.08f,.15f,1.92f),metal);
        }
    }
    static void Apply() {
        if(!File.Exists(Base+"observation.pending")) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(EditorApplication.isUpdating||EditorApplication.isCompiling) { EditorApplication.delayCall+=Apply; return; }
        try {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if(scene.name!="GOSPITOL CHAPTER 1 AWAKING") return;
            var wing=GameObject.Find("MEDICAL WING / 20 ROOMS"); if(!wing) return;
            glass=AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/07 Smart glass clear.mat"); metal=AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/02 Brushed alloy.mat"); wall=AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/Architecture_Walls_TextureSlot.mat");
            var panes=new List<Renderer>(); int rooms=0;
            foreach(Transform room in wing.transform) {
                if(!room.name.StartsWith("Patient room")) continue;
                var delete=new List<GameObject>(); foreach(Transform t in room) if(t.name=="Side wall") delete.Add(t.gameObject); foreach(var o in delete) Object.DestroyImmediate(o);
                foreach(Transform t in room) if(t.name=="Entrance wall") { float side=Mathf.Sign(t.localPosition.x); t.localPosition=new Vector3(side*2.69f,1.35f,-8.65f); t.localScale=new Vector3(3.02f,2.7f,.16f); }
                foreach(var t in room.GetComponentsInChildren<Transform>()) if(t.name=="Desk / electronic news / patient record") t.localPosition=new Vector3(3.48f,0,1.5f);
                Glass(room,-4.2f,panes); Glass(room,4.2f,panes); rooms++;
                foreach(float side in new[]{-1f,1f}) {
                    Box("Staff lane marking",room,new Vector3(side*4.7f,.003f,0),new Vector3(.055f,.008f,17.1f),metal);
                    for(int z=0;z<3;z++) {
                        var o=new GameObject("Staff corridor light"); o.transform.SetParent(room,false); o.transform.localPosition=new Vector3(side*4.65f,2.95f,-5+z*5);
                        var l=o.AddComponent<Light>(); l.type=LightType.Point; l.intensity=3; l.range=5; l.color=new Color(.8f,.92f,1);
                    }
                }
            }
            // Add observation strips along the outer walls of the original block as well.
            var original=GameObject.Find("HOSPITAL • AWAKENING").transform;
            Glass(original,-14.15f,panes); Glass(original,14.15f,panes);
            foreach(float x in new[]{-15.3f,86.7f}) Box("Staff corridor outer perimeter",wing.transform,new Vector3(x,1.65f,-22.4f),new Vector3(.16f,3.3f,17.4f),wall);
            Box("North staff outer perimeter",wing.transform,new Vector3(86.7f,1.65f,0),new Vector3(.16f,3.3f,17.4f),wall);
            // Close the accidental gaps next to the original outside room doors.
            foreach(float side in new[]{-1f,1f}) Box("Aligned original door wall infill",original,new Vector3(side*13.08f,1.3f,-8.72f),new Vector3(3.1f,2.6f,.23f),wall);
            int fixedDoors=0;
            foreach(var a in Object.FindObjectsByType<HospitalInteractable>()) {
                if(a.action!=HospitalInteractable.ActionType.Door||!a.doorLeaf) continue;
                var leaf=a.doorLeaf; leaf.localRotation=Quaternion.identity;
                bool modern=leaf.name=="Sliding door leaf";
                leaf.localPosition=new Vector3(0,modern?1.32f:1.28f,0);
                leaf.localScale=new Vector3(modern?2.19f:1.67f,modern?2.66f:2.57f,.14f);
                // Broad frame trim overlaps wall edges; the moving leaf overlaps the inner jamb.
                var parent=leaf.parent;
                float half=modern?1.14f:.87f;
                foreach(float side in new[]{-1f,1f}) Box("Aligned jamb cover",parent,new Vector3(side*half,1.34f,0),new Vector3(.18f,2.72f,.29f),metal);
                Box("Aligned header cover",parent,new Vector3(0,2.69f,0),new Vector3(half*2+.18f,.18f,.29f),metal);
                fixedDoors++;
            }
            var control=Object.FindAnyObjectByType<HospitalInteractable>();
            foreach(var a in Object.FindObjectsByType<HospitalInteractable>()) if(a.action==HospitalInteractable.ActionType.Glass) {
                var list=new List<Renderer>(a.glassPanels); list.AddRange(panes); a.glassPanels=list.ToArray();
            }
            Physics.SyncTransforms();
            if(Physics.Raycast(new Vector3(-14,1.6f,-11.2f),Vector3.right,99)) throw new System.Exception("Main corridor blocked");
            if(rooms!=17) throw new System.Exception("Unexpected added room count");
            // A 1.8 m staff lane spans the shared boundary between adjacent rooms.
            foreach(Transform room in wing.transform) if(room.name.StartsWith("Patient room")) {
                var origin=room.TransformPoint(new Vector3(4.65f,1.5f,-8.3f)); var forward=room.TransformDirection(Vector3.forward);
                if(Physics.Raycast(origin,forward,16.5f)) throw new System.Exception("Staff lane obstructed in "+room.name);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            typeof(HospitalWingExpansion).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            File.WriteAllText("C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs/hospital-observation-report.txt","Saved. 20 patient rooms retained.\n17 added rooms now have glass walls and staff lanes.\n"+panes.Count+" new switchable glass panels.\n"+fixedDoors+" doors aligned with overlapping frame trims.\nMain corridor and all 17 checked staff lanes are clear.\n");
            AssetDatabase.DeleteAsset(Base+"observation.pending"); Debug.Log("HOSPITAL_OBSERVATION_CORRIDORS_SAVED");
        } catch(System.Exception e) { File.WriteAllText(Base+"observation-error.txt",e.ToString()); Debug.LogException(e); }
    }
}
