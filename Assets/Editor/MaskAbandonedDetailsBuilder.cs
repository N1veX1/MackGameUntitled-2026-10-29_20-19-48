using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Editable scenery only. The existing level geometry and gate references are preserved.</summary>
public static class MaskAbandonedDetailsBuilder
{
    const string ScenePath = "Assets/Scenes/Level01_Tutorial.unity";
    const string Folder = "Assets/MaskEnvironment";
    const string RootName = "Abandoned complex details";
    static Material concrete, metal, rust, dark, ivory, purple, warm, dust;
    static Transform root;

    public static void BuildBatch()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Build();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("ABANDONED_DETAILS_SCENE_SAVED");
    }

    [MenuItem("Tools/Mask Demo/Refresh Abandoned Details")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) { Debug.LogWarning("Open Level01_Tutorial before refreshing its details."); return; }
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "MaskEnvironment");
        MakeMaterials();
        var old = GameObject.Find(RootName);
        if (old) Undo.DestroyObjectImmediate(old);
        root = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Add abandoned complex details");

        // One material for the barriers and their backing surfaces keeps the visual merge coherent.
        string[] surfaces = { "Single blocking wall", "Left wall", "Junction floor", "Floor / visible underside",
            "Floor / visible underside (1)", "Left branch floor", "Left branch floor (1)",
            "Branch backing wall Z", "Branch end wall", "Tutorial wall Y concrete", "Tutorial wall Z concrete" };
        foreach (var name in surfaces)
        {
            var go = GameObject.Find(name);
            if (go && go.TryGetComponent<Renderer>(out var renderer))
            { Undo.RecordObject(renderer, "Weather concrete"); renderer.sharedMaterial = concrete; }
        }
        var lightObject = GameObject.Find("Soft directional light");
        if (lightObject && lightObject.TryGetComponent<Light>(out var main))
        { Undo.RecordObject(main, "Dim complex lighting"); main.intensity = .68f; main.color = new Color(.80f,.85f,.91f); }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.26f,.29f,.33f);
        RenderSettings.ambientEquatorColor = new Color(.19f,.21f,.24f);
        RenderSettings.ambientGroundColor = new Color(.12f,.13f,.15f);
        RenderSettings.fog = false; // The distant orthographic cameras need the same readability as first person.
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        { Undo.RecordObject(camera, "Complex background"); camera.backgroundColor = new Color(.025f,.032f,.044f); }

        var signs = Group("Instructions / editable TextMesh");
        Sign(signs, "X / left wall", new Vector3(-2.978f,1.95f,-.1f), Vector3.right, Vector3.up, 2.65f, 1.58f,
            "01   /   X", "БОКОВОЙ ОБЗОР", "M — НАДЕТЬ МАСКУ\n1 — X / −X\nДОЖДИТЕСЬ ОСТАНОВКИ КАМЕРЫ.\nПОДОЙДИТЕ ВПЛОТНУЮ К СТЕНЕ.");
        Sign(signs, "Y / floor", new Vector3(-1.2f,.018f,4.78f), Vector3.up, Vector3.forward, 3.05f, 1.5f,
            "2 — Y / −Y", "СМОТРИТЕ СВЕРХУ", "К СТЕНЕ ВПЛОТНУЮ\nОНА СОЛЬЁТСЯ С ПОЛОМ");
        // At the junction, before turning left; mounted to the existing left wall.
        Sign(signs, "Z / before left turn", new Vector3(-2.978f,2.1f,7.13f), Vector3.right, Vector3.up, 1.44f, 1.65f,
            "03   /   Z", "ПОВОРОТ НАЛЕВО", "3 — Z / −Z\nПОДОЙДИТЕ К СТЕНЕ.\nЗА НЕЙ — ПОРТАЛ.");

        var services = Group("Old pipes and cable trays / no colliders");
        Pipe(services, "Supply pipe", new Vector3(-2.82f,3.38f,-3.5f), new Vector3(-2.82f,3.38f,1.2f), .062f, metal);
        Pipe(services, "Return pipe", new Vector3(-2.83f,3.65f,-3.5f), new Vector3(-2.83f,3.65f,1.2f), .041f, rust);
        foreach(float z in new[]{-3.2f,-1.1f,.9f})
        {
            Box(services,"Pipe bracket",new Vector3(-2.88f,3.51f,z),new Vector3(.22f,.5f,.09f),dark);
            Pipe(services,"Rusted collar",new Vector3(-2.82f,3.38f,z-.045f),new Vector3(-2.82f,3.38f,z+.045f),.076f,rust);
        }
        Pipe(services,"Open ceiling conduit",new Vector3(-2.86f,3.55f,2.9f),new Vector3(-2.86f,3.55f,5.1f),.035f,metal);
        Box(services,"Old junction cabinet",new Vector3(-2.82f,1.05f,-2.85f),new Vector3(.28f,.68f,.48f),metal);
        Box(services,"Cabinet latch",new Vector3(-2.664f,1.03f,-2.73f),new Vector3(.035f,.19f,.035f),rust);
        for(int i=0;i<5;i++) Box(services,"Cabinet vent",new Vector3(-2.672f,1.18f-i*.045f,-2.85f),new Vector3(.012f,.012f,.28f),dark);
        Pipe(services,"Branch overhead cable",new Vector3(-8.7f,3.78f,7.98f),new Vector3(-5.65f,3.78f,7.98f),.035f,metal);

        var marks = Group("Worn floor markings");
        foreach(float z in new[]{-2.8f,-2.1f,-1.4f,-.7f,0,.7f,2.9f,3.5f,7f,7.65f,8.3f,9f})
            Box(marks,"Faded edge dash",new Vector3(.88f,.008f,z),new Vector3(.055f,.009f,.29f),ivory);
        for(int i=0;i<7;i++) Box(marks,"Left branch edge dash",new Vector3(-3.8f-i*.64f,.008f,11.77f),new Vector3(.28f,.009f,.055f),ivory);
        Arrow(marks,new Vector3(-1.2f,.016f,8.72f),Vector3.left);
        Arrow(marks,new Vector3(-6.5f,.016f,10),Vector3.left);

        var lamps = Group("Dim service lights");
        Light[] lights = {
            Lamp(lamps,new Vector3(-1.2f,3.91f,-1.3f),false),
            Lamp(lamps,new Vector3(-2.8f,3.18f,4.05f),true),
            Lamp(lamps,new Vector3(-6.8f,3.82f,10),false)
        };
        var particles = Group("Sparse floating dust");
        ParticleSystem[] systems = {
            Dust(particles,new Vector3(-1.2f,2,-.65f),new Vector3(3.5f,2.6f,3.7f)),
            Dust(particles,new Vector3(-1.2f,2.05f,4.7f),new Vector3(3.5f,2.8f,2.8f)),
            Dust(particles,new Vector3(-6.5f,2,10),new Vector3(3.4f,2.5f,3.2f))
        };
        var atmosphere = root.gameObject.AddComponent<MaskAmbientDetails>();
        atmosphere.Configure(lights, systems);
        var tutorial = Object.FindAnyObjectByType<MaskTutorialLevel>();
        if(tutorial) { var so = new SerializedObject(tutorial); so.FindProperty("instructionsOnSurfaces").boolValue=true; so.ApplyModifiedProperties(); }
        foreach(var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            if(!text.font) text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var depth=text.GetComponent<MaskWorldText>();if(!depth) depth=text.gameObject.AddComponent<MaskWorldText>();
            depth.Configure(AssetDatabase.LoadAssetAtPath<Shader>(Folder+"/WorldStencilText.shader"));
        }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    static Transform Group(string name) { var go=new GameObject(name); go.transform.SetParent(root,false); return go.transform; }
    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
        go.transform.position=position;go.transform.localScale=scale;Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial=mat;return go;
    }
    static void Pipe(Transform parent,string name,Vector3 a,Vector3 b,float radius,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);
        go.transform.position=(a+b)*.5f;go.transform.rotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
        go.transform.localScale=new Vector3(radius*2,(b-a).magnitude*.5f,radius*2);
        Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;
    }
    static void Sign(Transform parent,string name,Vector3 position,Vector3 normal,Vector3 up,float width,float height,string title,string subtitle,string body)
    {
        var sign=new GameObject(name).transform;sign.SetParent(parent,false);sign.position=position;
        sign.rotation=Quaternion.LookRotation(-normal,up);
        var board=Box(sign,"Weathered backing",position, new Vector3(width,height,.012f),dark);
        board.transform.rotation=sign.rotation;
        Text(sign,"Axis heading",title,new Vector3(-width*.44f,height*.30f,-.012f),width*.88f,height*.23f,new Color(.72f,.56f,.94f),true);
        Text(sign,"Heading",subtitle,new Vector3(-width*.44f,height*.075f,-.014f),width*.88f,height*.13f,new Color(.87f,.84f,.73f),true);
        Text(sign,"Explanation",body,new Vector3(-width*.44f,-height*.10f,-.016f),width*.88f,height*.36f,new Color(.78f,.78f,.73f),false);
        for(int side=-1;side<=1;side+=2) for(int top=-1;top<=1;top+=2)
        {
            var bolt=Box(sign,"Rusted fastening",Vector3.zero,new Vector3(.026f,.026f,.009f),rust);
            bolt.transform.localPosition=new Vector3(side*width*.475f,top*height*.455f,-.014f);bolt.transform.localRotation=Quaternion.identity;
        }
    }
    static void Text(Transform parent,string name,string value,Vector3 local,float maxWidth,float maxHeight,Color color,bool bold)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=local;
        var text=go.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=value;
        text.fontSize=64;text.characterSize=.08f;text.anchor=TextAnchor.UpperLeft;text.alignment=TextAlignment.Left;
        text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;text.color=color;text.lineSpacing=1.2f;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=text.font.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        var size=renderer.localBounds.size;float scale=Mathf.Min(maxWidth/Mathf.Max(.001f,size.x),maxHeight/Mathf.Max(.001f,size.y));
        go.transform.localScale=Vector3.one*scale;
    }
    static void Arrow(Transform parent,Vector3 center,Vector3 direction)
    {
        var t=new GameObject("Painted route arrow").transform;t.SetParent(parent,false);t.position=center;t.rotation=Quaternion.LookRotation(direction,Vector3.up);
        var shaft=Box(t,"Arrow shaft",Vector3.zero,new Vector3(.055f,.008f,.6f),purple);shaft.transform.localPosition=Vector3.zero;shaft.transform.localRotation=Quaternion.identity;
        foreach(int sign in new[]{-1,1}) { var wing=Box(t,"Arrow tip",Vector3.zero,new Vector3(.05f,.008f,.27f),purple);wing.transform.localPosition=new Vector3(sign*.083f,0,.20f);wing.transform.localRotation=Quaternion.Euler(0,sign*-40,0); }
    }
    static Light Lamp(Transform parent,Vector3 position,bool wall)
    {
        Box(parent,"Old light housing",position,new Vector3(wall?.12f:1.1f,.10f,wall?.7f:.25f),metal);
        Box(parent,"Warm light diffuser",position+new Vector3(wall?.068f:0,-.058f,0),new Vector3(wall?.018f:.9f,.025f,wall?.56f:.15f),warm);
        var light=new GameObject("Soft service light").AddComponent<Light>();light.transform.SetParent(parent,false);
        light.transform.position=position+new Vector3(wall?.3f:0,-.23f,0);light.type=LightType.Point;light.color=new Color(1,.79f,.53f);
        light.intensity=1.5f;light.range=5;light.shadows=LightShadows.None;return light;
    }
    static ParticleSystem Dust(Transform parent,Vector3 position,Vector3 size)
    {
        var go=new GameObject("Dust motes / no collision");go.transform.SetParent(parent,false);go.transform.position=position;
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=ps.main;main.duration=12;main.loop=true;main.prewarm=true;main.startLifetime=new ParticleSystem.MinMaxCurve(8,15);
        main.startSpeed=.012f;main.startSize=new ParticleSystem.MinMaxCurve(.009f,.025f);main.maxParticles=110;
        main.startColor=new Color(.7f,.67f,.6f,.2f);main.simulationSpace=ParticleSystemSimulationSpace.World;
        var emission=ps.emission;emission.rateOverTime=6;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=size;
        var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
        velocity.x=.012f;velocity.y=.018f;velocity.z=.009f;
        var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.6f,.2f),new GradientAlphaKey(.6f,.7f),new GradientAlphaKey(0,1)});color.color=gradient;
        var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=dust;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        return ps;
    }
    static Material Mat(string name,Color color,float metallic=0,Color? emission=null)
    {
        string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path); }
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_Metallic",metallic);
        if(emission.HasValue){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",emission.Value);}
        EditorUtility.SetDirty(mat);return mat;
    }
    static void MakeMaterials()
    {
        concrete=Mat("AgedConcrete",new Color(.61f,.61f,.6f));
        metal=Mat("OldPaintedMetal",new Color(.16f,.19f,.2f),.3f);
        rust=Mat("OxidizedFastenings",new Color(.27f,.14f,.075f),.1f);
        dark=Mat("CharcoalInstructionPlates",new Color(.055f,.062f,.065f));
        ivory=Mat("FadedSafetyPaint",new Color(.47f,.44f,.32f));
        purple=Mat("FadedPurplePaint",new Color(.48f,.30f,.62f),0,new Color(.12f,.055f,.18f));
        warm=Mat("WarmDiffuser",new Color(.78f,.64f,.4f),0,new Color(1.4f,1.07f,.64f));
        string texPath=Folder+"/ConcreteWear.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if(!texture)
        {
            const int n=256;texture=new Texture2D(n,n,TextureFormat.RGBA32,true);texture.name="ConcreteWear";texture.wrapMode=TextureWrapMode.Repeat;
            var pixels=new Color[n*n];var random=new System.Random(14521);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float large=Mathf.PerlinNoise(x/42f,y/42f),fine=(float)random.NextDouble();
                float v=.78f+.16f*large+.065f*fine;
                if(fine<.018f)v-=.13f;pixels[y*n+x]=new Color(v,v*.993f,v*.97f,1);
            }
            texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,texPath);
        }
        concrete.SetTexture("_BaseMap",texture);concrete.SetTextureScale("_BaseMap",new Vector2(3,3));
        metal.SetTexture("_BaseMap",texture);dark.SetTexture("_BaseMap",texture);rust.SetTexture("_BaseMap",texture);
        string dustPath=Folder+"/DustMotes.mat";dust=AssetDatabase.LoadAssetAtPath<Material>(dustPath);
        if(!dust) { dust=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(dust,dustPath); }
        dust.SetFloat("_Surface",1);dust.SetFloat("_Blend",0);dust.SetFloat("_ZWrite",0);dust.SetFloat("_Cull",0);
        dust.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);dust.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
        dust.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");dust.renderQueue=3000;dust.SetColor("_BaseColor",Color.white);
        string dotPath=Folder+"/SoftDust.asset";var dot=AssetDatabase.LoadAssetAtPath<Texture2D>(dotPath);
        if(!dot) { dot=new Texture2D(32,32,TextureFormat.RGBA32,false);dot.name="SoftDust";var p=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){float r=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;p[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-r),2));}dot.SetPixels(p);dot.Apply();AssetDatabase.CreateAsset(dot,dotPath); }
        dust.SetTexture("_BaseMap",dot);EditorUtility.SetDirty(dust);EditorUtility.SetDirty(concrete);
    }
}


