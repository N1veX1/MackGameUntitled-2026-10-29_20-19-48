using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

[InitializeOnLoad]
public static class HospitalWingExpansion {
    const string Base="Assets/HospitalAwakening/";
    static Transform wing;
    static Material wall,floor,ceiling,metal,teal,light,dark;
    static HospitalWingExpansion() { EditorApplication.delayCall+=Run; EditorApplication.delayCall+=PreviewExisting; }
    static void PreviewExisting() {
        if(!File.Exists(Base+"wing-preview.pending")) return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating) { EditorApplication.delayCall+=PreviewExisting; return; }
        var existing=GameObject.Find("MEDICAL WING / 20 ROOMS"); if(!existing) return; wing=existing.transform;
        Physics.SyncTransforms();
        if(Physics.Raycast(new Vector3(-14,1.6f,-11.2f),Vector3.right,99)) throw new System.Exception("Corridor obstructed");
        int rooms=0,doors=0; foreach(Transform t in wing) if(t.name.StartsWith("Patient room")) rooms++;
        foreach(var t in wing.GetComponentsInChildren<HospitalInteractable>()) if(t.action==HospitalInteractable.ActionType.Door) doors++;
        if(rooms!=17||doors!=19) throw new System.Exception("Wing count mismatch");
        Capture(); File.AppendAllText("C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs/hospital-wing-report.txt","Verified: 17 added rooms, 19 added doors; 99 m corridor path clear.\n"); AssetDatabase.DeleteAsset(Base+"wing-preview.pending");
    }
    static GameObject Box(string name,Vector3 pos,Vector3 size,Material mat,Transform parent) {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.SetParent(parent,false); o.transform.localPosition=pos; o.transform.localScale=size; o.GetComponent<Renderer>().sharedMaterial=mat; return o;
    }
    static Transform Group(string name,Transform parent) { var t=new GameObject(name).transform; t.SetParent(parent,false); return t; }
    static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/"+name+".mat");
    static void Label(string text,Vector3 position,Transform parent,Vector3 rotation,float size=.012f) {
        var t=Group("Sign / "+text,parent); t.localPosition=position; t.localRotation=Quaternion.Euler(rotation);
        var m=t.gameObject.AddComponent<TextMesh>(); m.font=AssetDatabase.LoadAssetAtPath<Font>(Base+"HospitalArial.ttf"); m.text=text; m.fontSize=64; m.characterSize=size; m.anchor=TextAnchor.MiddleCenter; m.alignment=TextAlignment.Center; m.color=Color.white; t.GetComponent<Renderer>().sharedMaterial=m.font.material;
    }
    static void Lamp(Vector3 pos,Transform parent,float power=8) {
        var t=Group("Ceiling illumination",parent); t.localPosition=pos; var l=t.gameObject.AddComponent<Light>(); l.type=LightType.Point; l.range=9; l.intensity=power; l.color=new Color(.88f,.94f,1); l.shadows=LightShadows.None;
    }
    static void Door(Vector3 position,Quaternion rotation,Transform parent,string title) {
        var t=Group("Door / "+title,parent); t.localPosition=position; t.localRotation=rotation;
        Box("Left jamb",new Vector3(-1.13f,1.35f,0),new Vector3(.12f,2.7f,.23f),metal,t); Box("Right jamb",new Vector3(1.13f,1.35f,0),new Vector3(.12f,2.7f,.23f),metal,t);
        Box("Header",new Vector3(0,2.72f,0),new Vector3(2.4f,.16f,.25f),metal,t);
        var leaf=Box("Sliding door leaf",new Vector3(0,1.32f,0),new Vector3(2.12f,2.6f,.1f),teal,t);
        var a=t.gameObject.AddComponent<HospitalInteractable>(); a.action=HospitalInteractable.ActionType.Door; a.title=title; a.doorLeaf=leaf.transform;
        Label(title,new Vector3(0,2.97f,-.14f),t,Vector3.zero,.008f);
    }
    static void Room(int number,float x,bool south,Transform template) {
        var t=Group("Patient room "+number.ToString("D2"),wing); t.localPosition=new Vector3(x,0,south?-22.4f:0); if(south) t.localRotation=Quaternion.Euler(0,180,0);
        Box("Floor",new Vector3(0,-.12f,0),new Vector3(10.2f,.2f,17.4f),floor,t);
        Box("Rear wall",new Vector3(0,1.65f,8.65f),new Vector3(10.2f,3.3f,.16f),wall,t);
        foreach(float side in new[]{-1f,1f}) {
            Box("Side wall",new Vector3(side*5.06f,1.65f,0),new Vector3(.14f,3.3f,17.4f),wall,t);
            Box("Entrance wall",new Vector3(side*3.15f,1.35f,-8.65f),new Vector3(3.9f,2.7f,.16f),wall,t);
        }
        Box("Entrance lintel",new Vector3(0,3,-8.65f),new Vector3(10.2f,.6f,.16f),wall,t);
        Door(new Vector3(0,0,-8.65f),Quaternion.identity,t,"ПАЛАТА "+number.ToString("D2"));
        for(int ix=0;ix<5;ix++) for(int iz=0;iz<9;iz++) {
            bool lit=ix==2&&(iz==1||iz==4||iz==7);
            Box(lit?"Recessed square light":"Ceiling panel",new Vector3(-4+ix*2,3.36f,-7.7f+iz*1.92f),new Vector3(1.985f,.1f,1.905f),lit?light:ceiling,t);
        }
        foreach(float z in new[]{-5f,0f,5f}) Lamp(new Vector3(0,2.95f,z),t);
        var furnishings=Object.Instantiate(template.gameObject,t).transform; furnishings.name="Furniture / bed / equipment / sanitary"; furnishings.localPosition=Vector3.zero; furnishings.localRotation=Quaternion.identity;
        var remove=new List<GameObject>(); foreach(Transform child in furnishings) if(child.name.StartsWith("Privacy control")) remove.Add(child.gameObject); foreach(var child in remove) Object.DestroyImmediate(child);
        Label("МЕДИЦИНСКИЙ ОТСЕК / "+number.ToString("D2"),new Vector3(0,2.5f,8.54f),t,Vector3.zero);
    }
    static void Run() {
        if(!File.Exists(Base+"wing.pending")) return;
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        if(EditorApplication.isCompiling||EditorApplication.isUpdating) { EditorApplication.delayCall+=Run; return; }
        try {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if(scene.name!="GOSPITOL CHAPTER 1 AWAKING") return;
            var template=GameObject.Find("03 • Central patient room"); if(!template) throw new System.Exception("Central room template missing");
            var old=GameObject.Find("MEDICAL WING / 20 ROOMS"); if(old) Object.DestroyImmediate(old);
            wing=new GameObject("MEDICAL WING / 20 ROOMS").transform;
            wall=Load("Architecture_Walls_TextureSlot"); floor=Load("Architecture_Floor_TextureSlot"); ceiling=Load("Architecture_Ceiling_TextureSlot"); metal=Load("02 Brushed alloy"); teal=Load("04 Medical teal"); light=Load("09 Ceiling diffuser"); dark=Load("03 Graphite");
            int n=4; for(int i=3;i<10;i++) Room(n++, -10.2f+i*10.2f,false,template.transform);
            for(int i=0;i<10;i++) Room(n++,-10.2f+i*10.2f,true,template.transform);
            var corridor=Group("Main corridor / 102 metres",wing); corridor.localPosition=new Vector3(35.7f,0,-11.2f);
            Box("Corridor floor",new Vector3(0,-.12f,0),new Vector3(102.2f,.2f,5.2f),floor,corridor);
            for(int i=0;i<51;i++) {
                float x=-50+i*2;
                Box("Ceiling panel",new Vector3(x,3.36f,0),new Vector3(1.985f,.1f,5.2f),ceiling,corridor);
                if(i%2==0) { Box("Corridor light",new Vector3(x,3.29f,0),new Vector3(1.15f,.04f,1.15f),light,corridor); Lamp(new Vector3(x,2.95f,0),corridor,6); }
            }
            foreach(float side in new[]{-1f,1f}) {
                Door(new Vector3(side*51,0,0),Quaternion.Euler(0,90,0),corridor,"МЕДБЛОК / ВЫХОД");
                foreach(float z in new[]{-1.9f,1.9f}) Box("End bulkhead",new Vector3(side*51,1.35f,z),new Vector3(.16f,2.7f,1.45f),wall,corridor);
                Box("End lintel",new Vector3(side*51,3,0),new Vector3(.16f,.6f,5.2f),wall,corridor);
            }
            Label("МЕДИЦИНСКИЙ БЛОК  /  ПАЛАТЫ 01–20",new Vector3(-35,2.5f,2.5f),corridor,Vector3.zero,.012f);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Capture();
            File.WriteAllText("C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs/hospital-wing-report.txt","Saved: "+scene.path+"\n20 rooms: 3 existing + 17 added\n102 m corridor, 5.2 m wide\nDoors at both corridor ends\n17 furnished rooms with entrance doors\n26 corridor ceiling lights\n");
            AssetDatabase.DeleteAsset(Base+"wing.pending"); Debug.Log("HOSPITAL_WING_20_ROOMS_SAVED");
        } catch(System.Exception e) { File.WriteAllText(Base+"wing-error.txt",e.ToString()); Debug.LogException(e); }
    }
    static void Capture() {
        var go=new GameObject("Temporary plan camera"); var c=go.AddComponent<Camera>(); c.CopyFrom(Object.FindAnyObjectByType<HospitalExplorer>().eyes);
        typeof(HospitalSceneBuilder).GetMethod("SetupRenderer",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{c});
        var roofs=new List<GameObject>(); foreach(var t in Object.FindObjectsByType<Transform>()) if(t.name.StartsWith("Ceiling")||t.name=="Recessed square light"||t.name=="Corridor light"||t.name=="Square ceiling panel"||t.name=="Square recessed light") { if(t.gameObject.activeSelf) { roofs.Add(t.gameObject); t.gameObject.SetActive(false); } }
        c.transform.position=new Vector3(36,90,-55); c.transform.LookAt(new Vector3(36,0,-7)); c.orthographic=true; c.orthographicSize=36; c.aspect=1.6f;
        c.farClipPlane=300;
        var rt=new RenderTexture(1920,1200,24); var before=RenderTexture.active; c.targetTexture=rt; c.Render(); RenderTexture.active=rt;
        var im=new Texture2D(1920,1200,TextureFormat.RGB24,false); im.ReadPixels(new Rect(0,0,1920,1200),0,0); im.Apply(); File.WriteAllBytes("C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs/hospital-wing.png",im.EncodeToPNG());
        foreach(var roof in roofs) roof.SetActive(true);
        c.orthographic=false; c.transform.position=new Vector3(-12,1.65f,-11.2f); c.transform.LookAt(new Vector3(45,1.65f,-11.2f)); c.Render(); im.ReadPixels(new Rect(0,0,1920,1200),0,0); im.Apply(); File.WriteAllBytes("C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs/hospital-corridor.png",im.EncodeToPNG());
        RenderTexture.active=before; Object.DestroyImmediate(im); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }
}
