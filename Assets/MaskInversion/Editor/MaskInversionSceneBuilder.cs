using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MaskInversionSceneBuilder
{
    const string Root = "Assets/MaskInversion";
    const string Generated = Root + "/Generated";
    const string WallPrefabs = Root + "/Walls";
    const string Scenes = "Assets/Scenes";
    const string ScenePath = Scenes + "/Level02_Tutorial.unity";
    const string MaskPath = Generated + "/SplitInversionMask.prefab";
    static Material concrete, trim, purple, black, ivory, eyeWhite;

    [MenuItem("Tools/Mask Inversion/Build Tutorial 2")]
    public static void BuildAll()
    {
        EnsureFolder(Root);
        EnsureFolder(Generated);
        EnsureFolder(WallPrefabs);
        EnsureFolder(Scenes);
        concrete = MakeMaterial("AgedConcrete", new Color(.26f,.28f,.29f), .04f);
        trim = MakeMaterial("DarkConcreteTrim", new Color(.13f,.15f,.16f), .05f);
        purple = MakeMaterial("TutorialPurple", new Color(.44f,.21f,.78f), .45f);
        black = MakeMaterial("MaskBlackHalf", new Color(.018f,.021f,.025f), .28f);
        ivory = MakeMaterial("MaskIvoryHalf", new Color(.88f,.86f,.81f), .28f);
        eyeWhite = MakeMaterial("MaskLightEyeMark", new Color(.94f,.91f,.84f), .35f);
        BuildPhasePrefabs();
        GameObject maskPrefab = BuildMaskPrefab();
        if (!maskPrefab) maskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MaskPath);
        BuildScene(maskPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created the editable Level 2 inverted-mask tutorial at " + ScenePath);
    }

    static GameObject BuildMaskPrefab()
    {
        InversionMaskModelBuilder.Rebuild();
        return AssetDatabase.LoadAssetAtPath<GameObject>(MaskPath);
    }
    static Material MakeMaterial(string name,Color color,float smoothness)
    {
        string path=Generated+"/"+name+".mat";
        Material existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader=Shader.Find("Universal Render Pipeline/Lit"); if(!shader)shader=Shader.Find("Standard");
        Material m=existing?existing:new Material(shader){name=name};
        if(shader&&m.shader!=shader)m.shader=shader;
        SetColor(m,color);
        if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smoothness);
        if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",smoothness);
        if((name=="MaskBlackHalf"||name=="MaskIvoryHalf"||name=="MaskLightEyeMark")&&m.HasProperty("_Cull"))m.SetFloat("_Cull",0f);
        if(existing){EditorUtility.SetDirty(m);return m;}
        AssetDatabase.CreateAsset(m,path); return m;
    }
    static void SetColor(Material m,Color color){if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);if(m.HasProperty("_Color"))m.SetColor("_Color",color);}

    static void BuildScene(GameObject maskPrefab)
    {
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
        {
            Debug.Log("Level02_Tutorial already exists; keeping its edits."); AddToBuildSettings(); return;
        }
        Scene previous=SceneManager.GetActiveScene(); bool preserve=previous.IsValid() && !string.IsNullOrEmpty(previous.path); Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,preserve?NewSceneMode.Additive:NewSceneMode.Single); if(preserve)SceneManager.SetActiveScene(scene);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.12f,.13f,.15f);
        RenderSettings.fog=true; RenderSettings.fogColor=new Color(.075f,.085f,.095f); RenderSettings.fogDensity=.018f;
        GameObject lightObject=new GameObject("Dusty overhead light"); lightObject.transform.rotation=Quaternion.Euler(48f,-25f,0f);
        Light directional=lightObject.AddComponent<Light>(); directional.type=LightType.Directional; directional.intensity=.62f; directional.color=new Color(.74f,.78f,.82f);
        CreateRoom();
        var player=new GameObject("Player"); player.transform.position=new Vector3(0,.9f,-1.8f);
        var capsule=player.AddComponent<CharacterController>(); capsule.height=1.8f; capsule.radius=.28f; capsule.center=Vector3.zero; capsule.stepOffset=.2f;
        var cameraObject=new GameObject("First Person Camera"); cameraObject.transform.SetParent(player.transform,false); cameraObject.transform.localPosition=new Vector3(0,.8f,0);
        var camera=cameraObject.AddComponent<Camera>(); camera.nearClipPlane=.04f; camera.fieldOfView=76f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.035f,.043f,.053f);
        cameraObject.AddComponent<AudioListener>();
        var mask=PrefabUtility.InstantiatePrefab(maskPrefab,cameraObject.transform) as GameObject;
        if(mask){mask.name="Equipped split mask";mask.transform.localPosition=new Vector3(0f,-.016f,.055f);mask.transform.localRotation=Quaternion.identity;mask.transform.localScale=Vector3.one;mask.SetActive(false);}
        var controller=player.AddComponent<MaskInversionPlayer>();
        var serialized=new SerializedObject(controller); serialized.FindProperty("playerCamera").objectReferenceValue=camera; serialized.FindProperty("wornMaskVisual").objectReferenceValue=mask; serialized.ApplyModifiedPropertiesWithoutUndo();
        var level=new GameObject("Mask inversion tutorial"); var tutorial=level.AddComponent<MaskInversionTutorial>();
        var gates=new List<MaskInversionGate>();
        gates.Add(CreatePhaseBlock("Wall 1 · opens while mask is worn",new Vector3(0,2.15f,6),new Vector3(7.1f,4.3f,.34f),MaskInversionGate.GateKind.Wall,true));
        gates.Add(CreatePhaseBlock("Bridge 1 - roots - mask OFF",new Vector3(0,-.25f,10.9f),new Vector3(6.6f,.5f,2.8f),MaskInversionGate.GateKind.Bridge,false));
        gates.Add(CreatePhaseBlock("Wall 2 · opens while mask is removed",new Vector3(0,2.15f,17),new Vector3(7.1f,4.3f,.34f),MaskInversionGate.GateKind.Wall,false));
        gates.Add(CreatePhaseBlock("Bridge 2 - office floor - mask ON",new Vector3(0,-.25f,22),new Vector3(6.6f,.5f,3f),MaskInversionGate.GateKind.Bridge,true));
        serialized=new SerializedObject(tutorial); serialized.FindProperty("player").objectReferenceValue=controller;
        var list=serialized.FindProperty("sequence"); list.arraySize=gates.Count;
        for(int i=0;i<gates.Count;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=gates[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AddWorldLabel("1 · НАДЕНЬ МАСКУ",new Vector3(-2.95f,3.55f,3.8f),Quaternion.Euler(0,180,0));
        AddWorldLabel("МОСТ 1 · МАСКА СНЯТА",new Vector3(-2.95f,3.55f,9.15f),Quaternion.Euler(0,180,0));
        AddWorldLabel("2 · СНИМИ МАСКУ",new Vector3(-2.95f,3.55f,14.8f),Quaternion.Euler(0,180,0));
        AddWorldLabel("МОСТ 2 · МАСКА НАДЕТА",new Vector3(-2.95f,3.55f,20.0f),Quaternion.Euler(0,180,0));
        AddWorldLabel("ВЫХОД · TUTORIAL 2",new Vector3(0,2.5f,31.3f),Quaternion.identity);
        AddPurpleMarker("Exit beacon left",new Vector3(-1.4f,1.4f,31.8f),new Vector3(.12f,2.8f,.12f));
        AddPurpleMarker("Exit beacon right",new Vector3(1.4f,1.4f,31.8f),new Vector3(.12f,2.8f,.12f));
        var glow=new GameObject("Soft purple exit light");glow.transform.position=new Vector3(0,2.6f,31.8f);var point=glow.AddComponent<Light>();point.type=LightType.Point;point.color=new Color(.55f,.31f,1f);point.range=7f;point.intensity=2f;
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene,ScenePath); AddToBuildSettings();
        if(preserve) { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true); }
    }

    static void CreateRoom()
    {
        Block("Level 2 main floor · start",new Vector3(0,-.25f,2.25f),new Vector3(7.1f,.5f,14.5f),concrete);
        Block("Level 2 main floor · middle",new Vector3(0,-.25f,16.4f),new Vector3(7.1f,.5f,8.2f),concrete);
        Block("Level 2 main floor · exit",new Vector3(0,-.25f,28.75f),new Vector3(7.1f,.5f,10.5f),concrete);
        Block("Left concrete wall",new Vector3(-3.8f,2.2f,14.5f),new Vector3(.55f,4.5f,39f),concrete);
        Block("Right concrete wall",new Vector3(3.8f,2.2f,14.5f),new Vector3(.55f,4.5f,39f),concrete);
        Block("Segmented ceiling",new Vector3(0,4.55f,14.5f),new Vector3(7.6f,.3f,39f),trim);
        Block("Start back wall",new Vector3(0,2.2f,-5.4f),new Vector3(7.6f,4.5f,.4f),concrete);
        Block("End wall left of exit",new Vector3(-2.25f,2.2f,34.1f),new Vector3(3.1f,4.5f,.4f),concrete);
        Block("End wall right of exit",new Vector3(2.25f,2.2f,34.1f),new Vector3(3.1f,4.5f,.4f),concrete);
        CreatePitTrigger();
        for(int z=2;z<32;z+=5)
        {
            var lamp=new GameObject("Flickering maintenance lamp");lamp.transform.position=new Vector3(0,4.23f,z);
            var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.68f,.72f,.76f);light.intensity=.42f;light.range=5.5f;
        }
        Block("Door opening frame left",new Vector3(-1.75f,2.2f,34.1f),new Vector3(.35f,4.5f,.55f),trim);
        Block("Door opening frame right",new Vector3(1.75f,2.2f,34.1f),new Vector3(.35f,4.5f,.55f),trim);
        Block("Door opening lintel",new Vector3(0,4.15f,34.1f),new Vector3(3.8f,.45f,.55f),trim);
    }

    static void BuildPhasePrefabs()
    {
        SavePhasePrefab("Wall_OpenWithMask",MaskInversionGate.GateKind.Wall,true,concrete);
        SavePhasePrefab("Wall_OpenWithoutMask",MaskInversionGate.GateKind.Wall,false,concrete);
        SavePhasePrefab("Bridge_ExistsWithMask",MaskInversionGate.GateKind.Bridge,true,purple);
        SavePhasePrefab("Bridge_ExistsWithoutMask",MaskInversionGate.GateKind.Bridge,false,purple);
    }
    static void SavePhasePrefab(string file,MaskInversionGate.GateKind kind,bool maskOn,Material material)
    {
        string path=WallPrefabs+"/"+file+".prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path))return;
        var template=GameObject.CreatePrimitive(PrimitiveType.Cube);template.name=file;template.GetComponent<Renderer>().sharedMaterial=material;
        var gate=template.AddComponent<MaskInversionGate>();
        var data=new SerializedObject(gate);data.FindProperty("kind").enumValueIndex=(int)kind;data.FindProperty("existsWithMaskOn").boolValue=maskOn;data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(template,path);Object.DestroyImmediate(template);
    }
    static MaskInversionGate CreatePhaseBlock(string name,Vector3 position,Vector3 size,MaskInversionGate.GateKind kind,bool maskOn)
    {
        string file=kind==MaskInversionGate.GateKind.Wall?(maskOn?"Wall_OpenWithMask":"Wall_OpenWithoutMask"):(maskOn?"Bridge_ExistsWithMask":"Bridge_ExistsWithoutMask");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(WallPrefabs+"/"+file+".prefab");
        if (kind == MaskInversionGate.GateKind.Bridge)
            prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MaskInversion/Bridges/"+(maskOn?"OfficeBridge":"RootBridge")+".prefab");
        GameObject block=PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        block.name=name;block.transform.position=position;block.transform.localScale=size;
        if(kind==MaskInversionGate.GateKind.Wall) block.GetComponent<Renderer>().sharedMaterial=concrete;
        var gate=block.GetComponent<MaskInversionGate>();gate.Configure(kind,maskOn);return gate;
    }

    static GameObject Block(string name,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static void AddWorldLabel(string text,Vector3 pos,Quaternion rotation)
    {
        var go=new GameObject("Tutorial sign · "+text);go.transform.position=pos;go.transform.rotation=rotation;
        var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.fontSize=56;mesh.characterSize=.06f;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=new Color(.77f,.68f,1f);mesh.fontStyle=FontStyle.Bold;
    }
    static void AddPurpleMarker(string name,Vector3 pos,Vector3 size){var go=Block(name,pos,size,purple);Object.DestroyImmediate(go.GetComponent<Collider>());}
    static void CreatePitTrigger()
    {
        var go=new GameObject("Fall respawn trigger");go.transform.position=new Vector3(0,-3.5f,14.5f);var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.size=new Vector3(12,1,45);
        go.AddComponent<MaskInversionFallTrigger>();
    }
    static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/');string name=System.IO.Path.GetFileName(path);
        if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,name);
    }
    static void AddToBuildSettings()
    {
        var scenes=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach(var item in scenes)if(item.path==ScenePath)return;
        scenes.Add(new EditorBuildSettingsScene(ScenePath,true));
        EditorBuildSettings.scenes=scenes.ToArray();
    }
}











