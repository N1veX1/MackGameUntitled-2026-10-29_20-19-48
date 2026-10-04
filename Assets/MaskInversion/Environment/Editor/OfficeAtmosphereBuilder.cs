using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Object=UnityEngine.Object;

/// <summary>Upgrades only the scenery prefab. Original level collision and gameplay remain intact.</summary>
public static class OfficeAtmosphereBuilder
{
    const string Folder="Assets/MaskInversion/Environment";
    static readonly float[] Bays={-.8f,3.3f,14.3f,18.8f,26.5f,30.8f};
    [MenuItem("Tools/Mask Inversion/Environment/Update Windows and Night")]
    public static void Upgrade()
    {
        var root=PrefabUtility.LoadPrefabContents(InversionOfficeBuilder.PrefabPath);
        try {ApplyTo(root);PrefabUtility.SaveAsPrefabAsset(root,InversionOfficeBuilder.PrefabPath);AssetDatabase.SaveAssets();}
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    public static void ApplyTo(GameObject root)
    {
        var world=root.GetComponent<InversionOfficeWorld>();
        world.abandonedAmbient=new Color(.14f,.185f,.215f);
        world.restoredAmbient=new Color(.48f,.5f,.52f);
        world.nightFogColor=new Color(.018f,.046f,.035f);world.nightFogDensity=.014f;
        var glass=Material("Transparent daylight glazing","Universal Render Pipeline/Lit",new Color(.72f,.9f,.95f,.13f));
        glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);
        glass.SetFloat("_Smoothness",.86f);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.SetOverrideTag("RenderType","Transparent");glass.renderQueue=3000;
        glass.SetFloat("_BlendModePreserveSpecular",0);glass.SetFloat("_SpecularHighlights",0);glass.SetFloat("_EnvironmentReflections",0);
        glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");glass.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");glass.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        glass.SetShaderPassEnabled("ShadowCaster",false);glass.SetShaderPassEnabled("DepthOnly",false);EditorUtility.SetDirty(glass);
        for(int state=0;state<2;state++)
        {
            bool night=state==0;var p=(night?world.overgrownWorld:world.restoredWorld).transform;
            var previous=p.Find("Atmosphere - windows and exterior");if(previous)Object.DestroyImmediate(previous.gameObject);
            var g=new GameObject("Atmosphere - windows and exterior").transform;g.SetParent(p,false);
            foreach(var r in p.GetComponentsInChildren<Renderer>(true))
            {
                if(r.name=="Glazing panel") {r.enabled=!night;if(!night){r.sharedMaterial=glass;r.shadowCastingMode=ShadowCastingMode.Off;}}
                if(r.name=="Daylight above atrium")r.enabled=false;
            }
            foreach(var light in p.GetComponentsInChildren<Light>(true))
            {
                if(light.type==LightType.Directional)
                {
                    light.color=night?new Color(.43f,.59f,1):new Color(1,.94f,.8f);
                    light.intensity=night?.28f:1.2f;light.transform.localRotation=Quaternion.Euler(38,-65,0);
                    light.shadowStrength=.75f;
                }
                else {light.enabled=!night;if(!night)light.intensity=1.65f;}
            }
            Walls(g,night?world.abandonedWall:world.restoredWall);
            Exterior(g,night);
            if(night)Night(g);
            else foreach(float z in Bays)
            {
                var l=Light(g,"Sunlight through window",new Vector3(4.5f,3.35f,z+.25f),new Color(1,.91f,.71f),4.5f,11);
                l.type=LightType.Spot;l.spotAngle=85;l.innerSpotAngle=55;l.transform.LookAt(new Vector3(-2,.3f,z+1.5f));l.shadows=LightShadows.Soft;
            }
        }
    }
    static Material Material(string name,string shader,Color color)
    {
        string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
        m.shader=Shader.Find(shader);m.SetColor(m.HasProperty("_BaseColor")?"_BaseColor":m.HasProperty("_Tint")?"_Tint":"_Color",color);EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(Transform p,string name,Vector3 at,Vector3 scale,Material mat,bool shadows=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=scale;
        Object.DestroyImmediate(g.GetComponent<Collider>());var r=g.GetComponent<Renderer>();r.sharedMaterial=mat;if(!shadows)r.shadowCastingMode=ShadowCastingMode.Off;return g;
    }
    static void Walls(Transform p,Material mat)
    {
        foreach(int side in new[]{-1,1})
        {
            Box(p,"Window wall - lower",new Vector3(side*3.8f,.4375f,14.5f),new Vector3(.55f,.975f,39),mat);
            Box(p,"Window wall - upper",new Vector3(side*3.8f,3.8625f,14.5f),new Vector3(.55f,1.175f,39),mat);
            float start=-5;
            foreach(float bay in Bays)
            {
                float edge=bay+.25f-.85f;
                Box(p,"Window pier",new Vector3(side*3.8f,2.1f,(start+edge)/2),new Vector3(.55f,2.35f,edge-start),mat);
                start=bay+.25f+.85f;
            }
            Box(p,"Window pier end",new Vector3(side*3.8f,2.1f,(start+34)/2),new Vector3(.55f,2.35f,34-start),mat);
        }
    }
    static void Exterior(Transform p,bool night)
    {
        var sky=Material(night?"Night exterior":"Day exterior","Universal Render Pipeline/Unlit",night?new Color(.012f,.026f,.045f):new Color(.65f,.82f,.94f));
        var building=Material(night?"Night distant buildings":"Day distant buildings","Universal Render Pipeline/Unlit",night?new Color(.009f,.019f,.029f):new Color(.50f,.59f,.62f));
        var stripe=Material(night?"Night distant windows":"Day distant windows","Universal Render Pipeline/Unlit",night?new Color(.04f,.065f,.075f):new Color(.31f,.46f,.54f));
        foreach(int side in new[]{-1,1})
        {
            Box(p,"Exterior sky",new Vector3(side*23,9,14),new Vector3(.1f,40,110),sky,false);
            for(int i=0;i<7;i++)
            {
                float z=-13+i*9;float height=5+(i%3)*2;
                Box(p,"Distant office silhouette",new Vector3(side*(16+i%2*2),height/2-1,z),new Vector3(3,height,6),building,false);
                for(float y=1;y<height-1;y+=1.5f)Box(p,"Distant glazing",new Vector3(side*(14.45f+i%2*2),y,z),new Vector3(.03f,.65f,5.6f),stripe,false);
            }
        }
        Box(p,"Sky above roof",new Vector3(0,20,14),new Vector3(46,.1f,110),sky,false);
        // Exterior only: never place a floor underneath the playable pits.
    }
    static Light Light(Transform p,string name,Vector3 at,Color color,float strength,float range)
    {
        var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.localPosition=at;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=strength;l.range=range;l.shadows=LightShadows.None;return l;
    }
    static void Night(Transform p)
    {
        var firefly=Material("Firefly soft green glow","MaskInversion/SoftFirefly",new Color(.55f,1,.18f,.95f));firefly.SetFloat("_Core",7);
        var mist=Material("Faint green drifting mist","MaskInversion/SoftFirefly",new Color(.14f,.42f,.19f,.026f));mist.SetFloat("_Core",0);
        Particles(p,"Fireflies",firefly,80,new Vector3(5.6f,2.9f,37),.14f,.045f,.095f,1551);
        Particles(p,"Green night wisps",mist,24,new Vector3(5.7f,1.2f,37),.035f,1.5f,3.5f,1881);
        for(int i=0;i<12;i++)
            Light(p,"Soft firefly bounce",new Vector3(i%2==0?-2.6f:2.6f,1.1f,-2+i*3.1f),new Color(.48f,1,.24f),.48f,3.4f);
    }
    static void Particles(Transform p,string name,Material mat,int count,Vector3 size,float speed,float minSize,float maxSize,uint seed)
    {
        var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.localPosition=new Vector3(0,name=="Fireflies"?1.6f:.8f,14.5f);
        var ps=g.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=seed;
        var m=ps.main;m.duration=16;m.loop=true;m.prewarm=true;m.startLifetime=16;m.startSpeed=speed;m.startSize=new ParticleSystem.MinMaxCurve(minSize,maxSize);m.maxParticles=count;m.simulationSpace=ParticleSystemSimulationSpace.Local;
        var e=ps.emission;e.rateOverTime=count/16f;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=size;
        var noise=ps.noise;noise.enabled=true;noise.strength=.18f;noise.frequency=.45f;noise.scrollSpeed=.18f;
        var col=ps.colorOverLifetime;col.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.9f,.18f),new GradientAlphaKey(.55f,.5f),new GradientAlphaKey(1,.72f),new GradientAlphaKey(0,1)});col.color=gradient;
        var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=mat;r.renderMode=ParticleSystemRenderMode.Billboard;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        EditorUtility.SetDirty(mat);
    }
}
