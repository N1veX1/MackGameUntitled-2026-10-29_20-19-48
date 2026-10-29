using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class HospitalSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Main game scene/GOSPITOL CHAPTER 1 AWAKING.unity";
    const string Base = "Assets/HospitalAwakening/";
    static Transform root;
    static Material wall, metal, dark, teal, linen, blue, glass, frost, whiteLight, cyan, red, green, paper;
    static Font font;
    static readonly List<Renderer> panes = new List<Renderer>();
    static HospitalSceneBuilder() { EditorApplication.delayCall += AutoBuild; }
    static void AutoBuild() {
        if (!File.Exists(Base + "build.pending") || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += AutoBuild; return; }
        try { Build(); AssetDatabase.DeleteAsset(Base + "build.pending"); if(File.Exists(Base+"build-error.txt")) AssetDatabase.DeleteAsset(Base+"build-error.txt"); if(File.Exists(Base+"verify.pending")) EditorApplication.delayCall += () => EditorApplication.isPlaying=true; }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText(Base + "build-error.txt", e.ToString()); }
    }
    static Material Mat(string name, Color color, float smooth=.35f, float metallic=0, bool emission=false, bool transparent=false) {
        string path=Base+"Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",smooth); m.SetFloat("_Metallic",metallic);
        if(emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*2); m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        if(transparent) {
            m.SetFloat("_Surface",1); m.SetFloat("_Blend",0); m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha",1); m.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite",0); m.SetFloat("_Cull",0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType","Transparent"); m.renderQueue=3000;
        }
        EditorUtility.SetDirty(m); return m;
    }
    static GameObject Group(string name, Transform parent=null) { var o=new GameObject(name); o.transform.SetParent(parent ? parent:root,false); return o; }
    static GameObject Shape(string name, Vector3 p, Vector3 s, Material m, Transform parent=null, PrimitiveType type=PrimitiveType.Cube, bool collider=true) {
        var o=GameObject.CreatePrimitive(type); o.name=name; o.transform.SetParent(parent?parent:root,false); o.transform.localPosition=p; o.transform.localScale=s; o.GetComponent<Renderer>().sharedMaterial=m;
        if(!collider) UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());
        return o;
    }
    static GameObject Box(string n,float x,float y,float z,float sx,float sy,float sz,Material m,Transform parent=null,bool collider=true) => Shape(n,new Vector3(x,y,z),new Vector3(sx,sy,sz),m,parent,PrimitiveType.Cube,collider);
    static void Text(string text,Vector3 p,float size,Color color,Transform parent=null,Vector3? rotation=null) {
        var o=Group("Label • "+text.Replace('\n',' '),parent); o.transform.localPosition=p; o.transform.localRotation=Quaternion.Euler(rotation??Vector3.zero);
        var t=o.AddComponent<TextMesh>(); t.text=text; t.font=font; t.fontSize=64; t.characterSize=size*.32f; t.anchor=TextAnchor.MiddleCenter; t.alignment=TextAlignment.Center; t.color=color;
        o.GetComponent<MeshRenderer>().sharedMaterial=font.material;
    }
    static void Line(string n,Vector3 a,Vector3 b,float radius,Material mat,Transform parent) {
        var o=Shape(n,(a+b)*.5f,new Vector3(radius,Vector3.Distance(a,b)*.5f,radius),mat,parent,PrimitiveType.Cylinder,false); o.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
    }
    static void Lamp(Vector3 pos,float intensity,float range) {
        var o=Group("Ceiling light"); o.transform.localPosition=pos; var l=o.AddComponent<Light>(); l.type=LightType.Point; l.color=new Color(.92f,.95f,1); l.intensity=intensity; l.range=range; l.shadows=LightShadows.None;
    }
    [MenuItem("Tools/Hospital/Build awakening hospital")]
    public static void Build() {
        var active=SceneManager.GetActiveScene();
        if(active.isDirty) throw new Exception("Сохраните текущую сцену перед сборкой госпиталя.");
        var scene=active.path==ScenePath?active:EditorSceneManager.OpenScene(ScenePath);
        if(!Directory.Exists(Base+"Materials")) Directory.CreateDirectory(Base+"Materials");
        AssetDatabase.Refresh(); panes.Clear();
        var old=GameObject.Find("HOSPITAL • AWAKENING"); if(old) UnityEngine.Object.DestroyImmediate(old);
        root=new GameObject("HOSPITAL • AWAKENING").transform;
        wall=Mat("Architecture_Walls_TextureSlot",new Color(.78f,.78f,.76f),.12f); metal=Mat("02 Brushed alloy",new Color(.45f,.55f,.59f),.7f,.65f);
        dark=Mat("03 Graphite",new Color(.035f,.065f,.08f)); teal=Mat("04 Medical teal",new Color(.06f,.31f,.34f),.5f);
        linen=Mat("05 Linen",new Color(.84f,.89f,.88f),.1f); blue=Mat("06 Cobalt ceramic",new Color(.025f,.15f,.58f),.85f);
        glass=Mat("07 Smart glass clear",new Color(.28f,.72f,.8f,.13f),.94f,.1f,false,true); frost=Mat("08 Smart glass privacy",new Color(.5f,.65f,.67f),.22f);
        glass.SetFloat("_SpecularHighlights",0); glass.SetFloat("_BlendModePreserveSpecular",0); glass.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        whiteLight=Mat("09 Ceiling diffuser",new Color(.75f,.89f,1),.5f,0,true); cyan=Mat("10 Screen cyan",new Color(.12f,.8f,.83f),.5f,0,true);
        red=Mat("11 Rose petals",new Color(.62f,.025f,.08f),.2f); green=Mat("12 Leaves",new Color(.08f,.25f,.12f),.2f); paper=Mat("13 Paper",new Color(.9f,.87f,.75f));
        var floorA=Mat("Architecture_Floor_TextureSlot",new Color(.57f,.58f,.58f),.2f); var floorB=floorA;
        var ceilingMat=Mat("Architecture_Ceiling_TextureSlot",new Color(.86f,.86f,.83f),.08f); var grout=Mat("17 Grout",new Color(.19f,.25f,.27f));
        font=Font.CreateDynamicFontFromOSFont("Arial",64);
        // Save a stable font asset so Cyrillic labels survive editor reloads.
        string fontPath=Base+"HospitalArial.ttf";
        if(!File.Exists(fontPath)) File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf"),fontPath);
        AssetDatabase.ImportAsset(fontPath); font=AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        var architecture=Group("01 • Architecture / replaceable surfaces").transform;
        Box("Floor substrate",0,-.13f,0,18.3f,.2f,12.3f,grout,architecture);
        var floors=Group("Floor tiles • 750 mm",architecture).transform;
        for(int x=0;x<24;x++) for(int z=0;z<16;z++) Box("Floor tile",-8.625f+x*.75f,-.018f,-5.625f+z*.75f,.74f,.036f,.74f,(x+z)%3==0?floorB:floorA,floors,false);
        Box("Rear wall",0,1.65f,6.1f,18.3f,3.3f,.2f,wall,architecture); Box("West wall",-9.1f,1.65f,0,.2f,3.3f,12.3f,wall,architecture); Box("East wall",9.1f,1.65f,0,.2f,3.3f,12.3f,wall,architecture);
        // Three front doors follow the sketch, with a continuous lintel.
        Box("Front lintel",0,2.95f,-6.1f,18.3f,.7f,.2f,wall,architecture);
        foreach(float x in new[]{-7.45f,-3.0f,3.0f,7.45f}) Box("Front wall segment",x,1.3f,-6.1f,x== -3f||x==3f?4.4f:3.1f,2.6f,.2f,wall,architecture);
        // Adjust outer front sections to leave the side-door openings clear.
        foreach(Transform t in architecture) if(t.name=="Front wall segment" && Mathf.Abs(t.localPosition.x)>7) { t.localPosition=new Vector3(Mathf.Sign(t.localPosition.x)*8.05f,1.3f,-6.1f); t.localScale=new Vector3(1.9f,2.6f,.2f); }
        var ceiling=Group("Ceiling tiles • 1 m / 3.3 m clear height",architecture).transform;
        for(int x=0;x<18;x++) for(int z=0;z<12;z++) {
            bool lit=(x==2||x==8||x==9||x==15)&&(z==2||z==6||z==10);
            Box(lit?"Square recessed light":"Square ceiling panel",-8.5f+x,3.36f,-5.5f+z,.985f,.1f,.985f,lit?whiteLight:ceilingMat,ceiling,false);
        }
        foreach(float x in new[]{-6f,0f,6f}) foreach(float z in new[]{-3f,1f,4f}) Lamp(new Vector3(x,2.95f,z),x==0?7f:5f,6.5f);
        foreach(float x in new[]{-9f,9f}) Box("Wall base rail",x,.15f,0,.06f,.3f,12,dark,architecture);
        foreach(float x in new[]{-6f,0f,6f}) {
            Box("Rear equipment band",x,1.2f,5.96f,5.6f,.15f,.04f,teal,architecture);
            Door(new Vector3(x,0,-6f),x==0?"01 / ПРОБУЖДЕНИЕ":"НАБЛЮДЕНИЕ",architecture);
        }
        foreach(float side in new[]{-1f,1f}) Box("Front side-room closure",side*4.45f,1.3f,-6.1f,1.5f,2.6f,.2f,wall,architecture);
        architecture.localScale=new Vector3(1.7f,1,1.43f);
        GlassPartition(-4.485f); GlassPartition(4.485f); GlassPartition(-6.285f); GlassPartition(6.285f);
        foreach(float x in new[]{-5.385f,5.385f}) {
            Box("Staff observation workstation",x,.9f,7.7f,1.35f,.1f,.65f,linen);
            Box("Observation terminal",x,1.18f,7.85f,.65f,.42f,.08f,dark);
            Text("МЕДПЕРСОНАЛ / НАБЛЮДЕНИЕ",new Vector3(x,2.4f,8.45f),.035f,Color.white);
            Lamp(new Vector3(x,2.95f,2),6,8);
        }
        var central=Group("03 • Central patient room").transform;
        Bed(new Vector3(-.7f,0,7.03f),central,"01"); Bed(new Vector3(-10.4f,0,7.03f),Group("04 • West observation bay").transform,"02"); Bed(new Vector3(10.4f,0,7.03f),Group("05 • East observation bay").transform,"03");
        Bedside(new Vector3(.78f,0,8.06f),central);
        Equipment(new Vector3(-3.5f,0,7.63f),central); Equipment(new Vector3(12.5f,0,7.63f),root);
        Desk(new Vector3(3.8f,0,1.5f),central);
        PrivacyConsole(new Vector3(4.05f,0,-6.24f),central);
        HospitalFurniture.Sanitary(new Vector3(-14.95f,0,2.08f),-90,root);
        HospitalFurniture.Sanitary(new Vector3(2.75f,0,8.1f),0,central);
        HospitalFurniture.Sanitary(new Vector3(14.95f,0,2.08f),90,root);
        Text("МЕДИЦИНСКИЙ БЛОК\nВОССТАНОВЛЕНИЕ / 01",new Vector3(0,2.65f,8.55f),.065f,new Color(.08f,.27f,.3f));
        Text("БЛОК НАЦИЙ  /  ПРОГРАММА МАСКА",new Vector3(0,2.94f,8.54f),.032f,new Color(.08f,.27f,.3f));
        var camera=Camera.main; if(!camera) camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        foreach(var l in UnityEngine.Object.FindObjectsByType<Light>()) if(l.type==LightType.Directional) l.gameObject.SetActive(false);
        var player=Group("06 • Player / WASD + E"); player.transform.position=new Vector3(0,.04f,-4.5f);
        var cc=player.AddComponent<CharacterController>(); cc.height=1.75f; cc.radius=.26f; cc.center=new Vector3(0,.875f,0); cc.stepOffset=.2f;
        camera.transform.SetParent(player.transform,false); camera.transform.localPosition=new Vector3(0,1.65f,0); camera.transform.localRotation=Quaternion.identity; camera.fieldOfView=68; camera.nearClipPlane=.04f; camera.farClipPlane=80;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.08f,.12f,.15f); player.AddComponent<HospitalExplorer>().eyes=camera;
        SetupRenderer(camera);
        RenderSettings.skybox=null; RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.32f,.39f,.43f); RenderSettings.ambientIntensity=1; RenderSettings.fog=false;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,1.2f,1),Quaternion.Euler(22,0,0),12);
        foreach(var t in architecture.GetComponentsInChildren<Transform>()) if(t.name=="Front wall segment" && Mathf.Abs(t.localPosition.x)==3) { t.localScale=new Vector3(1.84f,2.6f,.2f); t.localPosition=new Vector3(Mathf.Sign(t.localPosition.x)*1.72f,1.3f,-6.1f); }
        HospitalFurniture.TextureReady(architecture);
        EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Preview(camera);
        File.WriteAllText(Base+"build-report.txt","SUCCESS\nScene: "+ScenePath+"\nGlass panels: "+panes.Count+"\nObjects: "+root.GetComponentsInChildren<Transform>(true).Length+"\nBuilt: "+DateTime.Now);
        Debug.Log("HOSPITAL_BUILD_SUCCESS: "+ScenePath);
    }
    static void SetupRenderer(Camera camera) {
        // Other levels use a stencil override for the Mask. Give this camera its own renderer.
        string path=Base+"HospitalRenderer.asset";
        var data=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if(!data) {
            data=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));
            data.name="HospitalRenderer"; data.rendererFeatures.Clear();
            var stencil=data.defaultStencilState; stencil.overrideStencilState=false; data.defaultStencilState=stencil;
            AssetDatabase.CreateAsset(data,path);
        }
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(!pipeline) throw new Exception("URP asset missing");
        var so=new SerializedObject(pipeline); var list=so.FindProperty("m_RendererDataList"); int index=-1;
        for(int i=0;i<list.arraySize;i++) if(list.GetArrayElementAtIndex(i).objectReferenceValue==data) index=i;
        if(index<0) { index=list.arraySize; list.arraySize++; list.GetArrayElementAtIndex(index).objectReferenceValue=data; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline); }
        camera.GetUniversalAdditionalCameraData().SetRenderer(index);
    }
    static void Door(Vector3 p,string label,Transform parent) {
        var g=Group("Sliding door • "+label,parent).transform; g.localPosition=p;
        Box("Door left jamb",-.86f,1.3f,0,.12f,2.6f,.26f,metal,g); Box("Door right jamb",.86f,1.3f,0,.12f,2.6f,.26f,metal,g); Box("Door header",0,2.62f,0,1.84f,.15f,.3f,metal,g);
        var leaf=Box("Sliding leaf",0,1.28f,0,1.6f,2.5f,.12f,teal,g);
        Box("Door observation window",0,.168f,-.58f,.56f,.192f,.2f,glass,leaf.transform,false);
        // Door leaf children use local coordinates adjusted for its primitive scale.
        var a=g.gameObject.AddComponent<HospitalInteractable>(); a.action=HospitalInteractable.ActionType.Door; a.title="Открыть / закрыть дверь"; a.doorLeaf=leaf.transform;
        Text(label,new Vector3(0,2.88f,.14f),.045f,Color.white,g,new Vector3(0,180,0));
    }
    static void GlassPartition(float x) {
        var g=Group(x<0?"02 • West smart-glass partition":"02 • East smart-glass partition").transform;
        for(int z=0;z<6;z++) {
            float cz=-5+z*2;

            Box("Glass lower frame",x,.24f,cz,.12f,.48f,2,wall,g);
            var pane=Box("Switchable smart glass",x,1.8f,cz,.035f,2.6f,1.93f,glass,g); pane.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off; panes.Add(pane.GetComponent<Renderer>());
            Box("Vertical mullion",x,1.8f,cz-1,.09f,2.95f,.055f,metal,g);
            Box("Upper frame",x,3.18f,cz,.13f,.14f,2,metal,g);
            Box("Privacy stripe",x,1.27f,cz,.044f,.045f,1.9f,linen,g,false);
        }
        g.localScale=new Vector3(1,1,1.43f);
        g.localPosition=new Vector3(0,0,0);
        // Open passages: no door leaf, track or jamb in either partition.

    }
    static void Bed(Vector3 p,Transform parent,string id) { HospitalFurniture.Bed(p,parent,id); }
    static void Bedside(Vector3 p,Transform parent) {
        var g=Group("Bedside cabinet / rose / blue water mug",parent).transform; g.localPosition=p;
        Box("Cabinet",0,.43f,0,.65f,.8f,.6f,wall,g); Box("Counter",0,.86f,0,.7f,.06f,.65f,metal,g);
        foreach(float y in new[]{.3f,.6f}) { Box("Drawer front",0,y,-.31f,.59f,.23f,.025f,linen,g); Box("Pull",0,y,-.34f,.2f,.025f,.035f,metal,g); }
        Shape("Ceramic rose pot",new Vector3(-.16f,1.005f,.08f),new Vector3(.21f,.13f,.21f),linen,g,PrimitiveType.Cylinder);
        Shape("Soil",new Vector3(-.16f,1.14f,.08f),new Vector3(.18f,.006f,.18f),dark,g,PrimitiveType.Cylinder,false);
        Line("Rose stem",new Vector3(-.16f,1.13f,.08f),new Vector3(-.13f,1.59f,.08f),.012f,green,g);
        for(int i=0;i<7;i++) { float a=i*Mathf.PI*2/7; var petal=Shape("Rose petal",new Vector3(-.13f+Mathf.Cos(a)*.045f,1.59f+(i%2)*.018f,.08f+Mathf.Sin(a)*.045f),new Vector3(.075f,.09f,.04f),red,g,PrimitiveType.Sphere,false); petal.transform.localRotation=Quaternion.Euler(20,i*51,25); }
        Shape("Rose heart",new Vector3(-.13f,1.63f,.08f),new Vector3(.055f,.07f,.055f),red,g,PrimitiveType.Sphere,false);
        var leaf=Shape("Rose leaf",new Vector3(-.08f,1.36f,.08f),new Vector3(.14f,.022f,.055f),green,g,PrimitiveType.Sphere,false); leaf.transform.localRotation=Quaternion.Euler(0,0,25);
        // Hollow cup mesh with visible water and a C-shaped handle.
        var cup=Group("Blue ceramic mug",g).transform; cup.localPosition=new Vector3(.17f,.9f,-.12f);
        var mesh=new Mesh(); var verts=new List<Vector3>(); var tris=new List<int>();
        for(int ring=0;ring<4;ring++) for(int i=0;i<40;i++) { float a=i*Mathf.PI*2/40; float r=ring<2?.075f:.061f; float y=(ring==1||ring==2)?.16f:.013f; verts.Add(new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r)); }
        for(int ring=0;ring<3;ring++) for(int i=0;i<40;i++) { int a=ring*40+i,b=ring*40+(i+1)%40,c=b+40,d=a+40; tris.AddRange(new[]{a,b,c,a,c,d}); }
        mesh.SetVertices(verts); mesh.SetTriangles(tris,0); mesh.RecalculateNormals(); string mugPath=Base+"MugCurrent.asset"; var savedMug=AssetDatabase.LoadAssetAtPath<Mesh>(mugPath); if(savedMug) { EditorUtility.CopySerialized(mesh,savedMug); UnityEngine.Object.DestroyImmediate(mesh); mesh=savedMug; } else AssetDatabase.CreateAsset(mesh,mugPath); cup.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh; cup.gameObject.AddComponent<MeshRenderer>().sharedMaterial=blue;
        Shape("Water surface",new Vector3(0,.13f,0),new Vector3(.119f,.004f,.119f),glass,cup,PrimitiveType.Cylinder,false);
        for(int i=0;i<12;i++) { float a=(-120+i*240f/12)*Mathf.Deg2Rad,b=(-120+(i+1)*240f/12)*Mathf.Deg2Rad; Line("Mug handle",new Vector3(.09f+Mathf.Cos(a)*.06f,.08f+Mathf.Sin(a)*.06f,0),new Vector3(.09f+Mathf.Cos(b)*.06f,.08f+Mathf.Sin(b)*.06f,0),.012f,blue,cup); }
    }
    static void Equipment(Vector3 p,Transform parent) { HospitalFurniture.Equipment(p,parent); }
    static void Desk(Vector3 p,Transform parent) {
        var g=Group("Desk / electronic news / patient record",parent).transform; g.localPosition=p; g.localRotation=Quaternion.Euler(0,90,0);
        HospitalFurniture.Desktop(g);
        var tablet=Group("Electronic bulletin • interact to read",g).transform; tablet.localPosition=new Vector3(-.3f,.85f,.06f); tablet.localRotation=Quaternion.Euler(18,0,0);
        Box("Tablet housing",0,.028f,0,.6f,.045f,.42f,dark,tablet); Box("Electronic screen",0,.054f,0,.54f,.012f,.36f,teal,tablet);
        Text("ВНУТРИ ПЕРИМЕТРА\nГОРОДСКОЙ ЖУРНАЛ\n\nСОЛНЕЧНЫЙ КРУГ\nНовый парк • В субботу",new Vector3(0,.063f,0),.017f,Color.white,tablet,new Vector3(90,0,0));
        var a=tablet.gameObject.AddComponent<HospitalInteractable>(); a.title="Журнал «Внутри периметра»";
        a.body="НОВЫЙ ПАРК «СОЛНЕЧНЫЙ КРУГ» ОТКРЫВАЕТСЯ В СУББОТУ\n\nВпервые после восстановления третьего жилого кольца горожанам станет доступна большая прогулочная зона с живыми деревьями.\n\nДля детей подготовлены игровые площадки, для взрослых — тихие дорожки и крытая оранжерея. В день открытия городской оркестр выступит у центрального фонтана.\n\nВход свободный. Просим заранее бронировать время посещения оранжереи.\n\nПервые саженцы для парка жители сохраняли дома больше двенадцати лет.";
        var record=Box("Patient admission record",.45f,.846f,.03f,.38f,.008f,.5f,paper,g);
        Text("МЕДИЦИНСКАЯ ЗАПИСЬ\n\nТравма: испытательный сектор\nСостояние: стабилизировано\nНарушение памяти\nПовторный вводный курс\n\nАРХИВ: ДОСТУП ЗАКРЫТ",new Vector3(.45f,.854f,.03f),.012f,new Color(.08f,.15f,.17f),g,new Vector3(90,0,0));
        var d=record.AddComponent<HospitalInteractable>(); d.title="Медицинская запись / причина поступления";
        d.body="Причина поступления: травма, полученная в испытательном секторе.\n\nСостояние: стабилизировано.\n\nНаблюдение: выраженные нарушения автобиографической памяти. Речь и двигательные навыки сохранены.\n\nНазначение: первичная оценка, повторный вводный курс.\n\nВидеозапись инцидента перемещена в закрытый архив по распоряжению управляющей системы.";
        HospitalFurniture.Chair(new Vector3(0,0,-.98f),g);

    }
    static void PrivacyConsole(Vector3 p,Transform parent) {
        var g=Group("Privacy control / switch all glass",parent).transform; g.localPosition=p;
        Box("Console stand",0,.62f,0,.12f,1.24f,.12f,metal,g); Box("Console foot",0,.05f,0,.5f,.1f,.4f,dark,g);
        Box("Control panel",0,1.28f,0,.48f,.27f,.14f,dark,g); Box("Status light",0,1.29f,-.076f,.41f,.18f,.016f,teal,g,false);
        Text("ПРИВАТНОСТЬ\nПРОЗРАЧНО / МАТОВО",new Vector3(0,1.29f,-.09f),.017f,Color.white,g);
        var a=g.gameObject.AddComponent<HospitalInteractable>(); a.action=HospitalInteractable.ActionType.Glass; a.title="Переключить прозрачность стекла"; a.glassPanels=panes.ToArray(); a.clearGlass=glass; a.privacyGlass=frost;
    }
    static void Preview(Camera camera) {
        string output="C:/Users/serge/Documents/Codex/2026-10-29/vj/outputs"; Directory.CreateDirectory(output);
        var rt=new RenderTexture(1600,1000,24); var prior=RenderTexture.active; var target=camera.targetTexture;
        camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; var image=new Texture2D(1600,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes(output+"/hospital-interior.png",image.EncodeToPNG());
        var pos=camera.transform.position; var rot=camera.transform.rotation;
        Transform ceilings=null; foreach(var t in root.GetComponentsInChildren<Transform>()) if(t.name.StartsWith("Ceiling tiles")) ceilings=t;
        if(ceilings) ceilings.gameObject.SetActive(false);
        camera.transform.position=new Vector3(0,27,-15); camera.transform.LookAt(new Vector3(0,0,0)); camera.Render(); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes(output+"/hospital-layout.png",image.EncodeToPNG());
        if(ceilings) ceilings.gameObject.SetActive(true);
        camera.transform.position=new Vector3(.8f,1.65f,4.35f); camera.transform.LookAt(new Vector3(-1.35f,1.12f,7.45f)); camera.Render(); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes(output+"/hospital-bed-equipment.png",image.EncodeToPNG());
        camera.transform.position=new Vector3(1.3f,1.8f,-.9f); camera.transform.LookAt(new Vector3(3.2f,.85f,1.5f)); camera.Render(); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes(output+"/hospital-desk-chair.png",image.EncodeToPNG());
        if(ceilings) ceilings.gameObject.SetActive(true); camera.transform.SetPositionAndRotation(pos,rot); camera.targetTexture=target; RenderTexture.active=prior; UnityEngine.Object.DestroyImmediate(image); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
    }
}
// Revision 3: observation corridors and sanitary stations.
