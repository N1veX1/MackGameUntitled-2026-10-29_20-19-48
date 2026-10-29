using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

public static class InversionOfficeBuilder
{
    const string Folder="Assets/MaskInversion/Environment";
    public const string PrefabPath=Folder+"/OfficeTwoWorlds.prefab";
    static Material white,stone,dirty,dirtyFloor,oak,oldWood,metal,rust,glass,oldGlass,black,screen,glow,moss,bark,crack;
    static Material[] greens;
    static int meshIndex;

    [MenuItem("Tools/Mask Inversion/Environment/Build Two Worlds Prefab")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder+"/Materials"); Directory.CreateDirectory(Folder+"/Meshes");
        AssetDatabase.Refresh();
        var randomState=Random.state;Random.InitState(4802);meshIndex=0;
        white=Mat("Clean warm plaster",new Color(.83f,.82f,.77f),.27f);
        stone=Mat("Clean limestone floor",new Color(.55f,.59f,.59f),.30f);
        dirty=Mat("Damp cracked plaster",new Color(.46f,.44f,.38f),.04f);
        dirtyFloor=Mat("Soiled floor",new Color(.36f,.34f,.28f),.08f);
        oak=Mat("Honey oak",new Color(.45f,.27f,.13f),.25f);
        oldWood=Mat("Weathered timber",new Color(.20f,.19f,.13f),.02f);
        metal=Mat("Champagne metal",new Color(.43f,.42f,.35f),.55f);
        rust=Mat("Oxidized frames",new Color(.19f,.20f,.15f),.04f);
        glass=Mat("Office glazing",new Color(.30f,.48f,.52f),.85f);
        oldGlass=Mat("Clouded glazing",new Color(.16f,.25f,.23f),.08f);
        black=Mat("Graphite",new Color(.045f,.06f,.065f),.3f);
        screen=Mat("Soft blue display",new Color(.08f,.27f,.32f),.3f,new Color(.07f,.34f,.43f));
        glow=Mat("Warm luminous diffuser",new Color(.96f,.87f,.69f),.1f,new Color(1.6f,1.23f,.74f));
        moss=Mat("Velvet moss",new Color(.13f,.24f,.075f),.02f);
        bark=Mat("Living roots",new Color(.19f,.15f,.085f),.02f);
        crack=Mat("Deep fissures",new Color(.07f,.09f,.065f),0);
        greens=new[]{Mat("Leaf emerald",new Color(.095f,.27f,.09f),.21f),Mat("Leaf sage",new Color(.22f,.37f,.12f),.19f),Mat("Leaf fresh tips",new Color(.34f,.48f,.13f),.18f)};
        foreach(var leaf in greens){leaf.SetFloat("_SpecularHighlights",0);leaf.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(leaf);}
        Grain(dirty,"Damp plaster",true);Grain(dirtyFloor,"Earth on stone",true);Grain(oak,"Oak grain",false);Grain(bark,"Root grain",true);
        var root=new GameObject("Tutorial 2 - Office through time");
        try
        {
            Transform shared=Group(root.transform,"01 Architecture and wayfinding");
            Transform abandoned=Group(root.transform,"02 Overgrown - MASK OFF");
            Transform restored=Group(root.transform,"03 Restored office - MASK ON");
            var world=root.AddComponent<InversionOfficeWorld>();
            world.overgrownWorld=abandoned.gameObject;world.restoredWorld=restored.gameObject;
            world.abandonedWall=dirty;world.restoredWall=white;world.abandonedFloor=dirtyFloor;world.restoredFloor=stone;world.restoredBridge=stone;
            foreach(float z in new[]{-.8f,3.3f,14.3f,18.8f,26.5f,30.8f})
            {
                OfficeBay(restored,z,false);OfficeBay(abandoned,z,true);
            }
            Architecture(restored,false);Architecture(abandoned,true);
            LockedDoor(restored,false);LockedDoor(abandoned,true);
            Board(shared,"PALIMPSEST",new Vector3(-3.36f,2.8f,-1.5f),Quaternion.Euler(0,-90,0),.09f);
            Board(shared,"01 / РЕЦЕПЦИЯ",new Vector3(0,3.75f,4.9f),Quaternion.identity,.052f);
            Board(shared,"02 / АРХИВ",new Vector3(0,3.75f,15.8f),Quaternion.identity,.052f);
            Board(shared,"03 / ВЫХОД",new Vector3(0,3.75f,28.5f),Quaternion.identity,.052f);
            Board(shared,"M  /  ДРУГОЕ ВРЕМЯ",new Vector3(0,2.7f,-5.16f),Quaternion.Euler(0,180,0),.07f);
            Board(shared,"01 / КОРНИ\nНАДЕНЬ МАСКУ — M",new Vector3(-3.30f,2.5f,3.5f),Quaternion.Euler(0,-90,0),.045f);
            Board(shared,"02 / СТЕНА ОФИСА\nСНИМИ МАСКУ — M",new Vector3(-3.30f,2.5f,14.8f),Quaternion.Euler(0,-90,0),.045f);
            Board(shared,"03 / ПОЛ ОФИСА\nНАДЕНЬ МАСКУ",new Vector3(-3.30f,2.5f,19.2f),Quaternion.Euler(0,-90,0),.045f);
            foreach(int side in new[]{-1,1})
            {
                for(float z=-3.3f;z<33;z+=1.6f)
                {
                    // These are trim details only, never a substitute for a missing bridge.
                    if(IsPit(z)||side==1)continue;
                    Box(restored,"Floor joint",new Vector3(0,.003f,z),new Vector3(7.04f,.003f,.014f),metal);
                    Box(abandoned,"Worn tile joint",new Vector3(0,.006f,z),new Vector3(7.04f,.005f,.018f),crack);
                }
                for(float z=-3;z<33;z+=3.1f)
                    if(!IsPit(z))Vegetation(abandoned,new Vector3(side*2.92f,.02f,z),side);
            }
            HangingCanopy(abandoned);
            RootWall(abandoned);
            BrokenBridgeEdges(abandoned);
            RuinDetails(abandoned);
            Lighting(restored,false);Lighting(abandoned,true);
            OfficeAtmosphereBuilder.ApplyTo(root);
            OfficeSceneryOptimizer.Optimize(root);
            abandoned.gameObject.SetActive(true);restored.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("OFFICE TWO WORLDS BUILT: original route, root wall, two bridge states, locked side door.");
        }
        finally {Object.DestroyImmediate(root);Random.state=randomState;}
    }
    static bool IsPit(float z)=>z>9.45f&&z<12.35f||z>20.45f&&z<23.55f;
    static Transform Group(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g.transform;}
    static Material Mat(string name,Color color,float smooth,Color? emission=null)
    {
        string path=Folder+"/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Standard");
        if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
        m.name=name;m.enableInstancing=true;
        if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
        if(m.HasProperty("_Color"))m.SetColor("_Color",color);
        if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);
        if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",smooth);
        if(emission.HasValue){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission.Value);}
        EditorUtility.SetDirty(m);return m;
    }
    static void Grain(Material mat,string name,bool rough)
    {
        string path=Folder+"/Materials/"+name+".asset";var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(!t){t=new Texture2D(128,128,TextureFormat.RGB24,true);AssetDatabase.CreateAsset(t,path);}
        var pixels=new Color[128*128];
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            float a=Mathf.PerlinNoise(x*(rough?.07f:.3f),y*(rough?.07f:.012f));
            float b=Mathf.PerlinNoise(x*.34f+23,y*.34f+71);
            pixels[y*128+x]=Color.white*Mathf.Lerp(rough?.42f:.66f,1.1f,a*.74f+b*.26f);
        }
        t.SetPixels(pixels);t.Apply();t.wrapMode=TextureWrapMode.Repeat;EditorUtility.SetDirty(t);
        if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",t);if(mat.HasProperty("_MainTex"))mat.SetTexture("_MainTex",t);
        EditorUtility.SetDirty(mat);
    }
    static GameObject Box(Transform p,string name,Vector3 at,Vector3 scale,Material mat,Quaternion? rot=null)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);
        g.transform.localPosition=at;g.transform.localScale=scale;if(rot.HasValue)g.transform.localRotation=rot.Value;
        Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;return g;
    }
    static void Board(Transform p,string text,Vector3 at,Quaternion rot,float size)
    {
        var g=new GameObject(text);g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localRotation=rot;
        var t=g.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(.78f,.79f,.63f);
        string path=Folder+"/Materials/Depth tested lettering.mat";
        var lettering=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!lettering){lettering=new Material(t.font.material);AssetDatabase.CreateAsset(lettering,path);}
        lettering.shader=Shader.Find("MaskInversion/Office Lettering");lettering.mainTexture=t.font.material.mainTexture;EditorUtility.SetDirty(lettering);
        g.GetComponent<MeshRenderer>().sharedMaterial=lettering;
        t.font.RequestCharactersInTexture(text,t.fontSize,t.fontStyle);
        float width=0;
        foreach(string line in text.Split('\n'))
        {
            float advance=0;foreach(char c in line)if(t.font.GetCharacterInfo(c,out CharacterInfo info,t.fontSize,t.fontStyle))advance+=info.advance;
            width=Mathf.Max(width,advance*size*.1f);
        }
        float height=t.fontSize*size*.1f*text.Split('\n').Length+.13f;
        Box(p,"Wayfinding plaque",at+rot*new Vector3(0,0,.032f),new Vector3(width+.22f,height,.035f),black,rot);
    }
    static void Architecture(Transform p,bool old)
    {
        Material frame=old?rust:metal;
        foreach(int side in new[]{-1,1})
        {
            Box(p,"Continuous skirting",new Vector3(side*3.49f,.14f,14.4f),new Vector3(.10f,.28f,38.8f),frame);
            Box(p,"Upper cornice",new Vector3(side*3.49f,3.9f,14.4f),new Vector3(.16f,.18f,38.8f),frame);
            for(float z=-3;z<33;z+=4)
            {
                Box(p,"Structural pilaster",new Vector3(side*3.43f,2.1f,z),new Vector3(.15f,4.1f,.13f),old?dirty:white);
                for(int j=0;j<7;j++)Box(p,"Oak acoustic slat",new Vector3(side*3.40f,1.7f,z+.4f+j*.11f),new Vector3(.10f,2.8f,.043f),old?oldWood:oak);
            }
        }
        for(float z=-3;z<34;z+=3.1f)
        {
            Box(p,"Ceiling crossbeam",new Vector3(0,4.23f,z),new Vector3(7.1f,.22f,.12f),frame);
            if(!old)
            {
                Box(p,"Recessed luminaire",new Vector3(0,4.34f,z+1.25f),new Vector3(1.5f,.055f,.48f),glow);
                foreach(float x in new[]{-2.4f,2.4f})Box(p,"Ceiling panel seam",new Vector3(x,4.389f,z+1.4f),new Vector3(.018f,.012f,2.95f),metal);
            }
            else if(((int)(z*10))%3==0)
                Box(p,"Broken ceiling fragment",new Vector3(-1.9f,4.18f,z+.8f),new Vector3(2.2f,.10f,1.3f),dirty,Quaternion.Euler(0,0,-8));
        }
        if(old)
        {
            // Outside foliage silhouettes beyond the missing ceiling.
            var sky=Box(p,"Daylight above atrium",new Vector3(0,9,14),new Vector3(18,.05f,48),Mat("Soft exterior sky",new Color(.55f,.65f,.62f),0,new Color(.65f,.75f,.66f)));
            sky.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        }
    }
    static void OfficeBay(Transform p,float z,bool old)
    {
        var bay=Group(p,(old?"Ruined":"Restored")+" workstation "+z);
        bay.localPosition=new Vector3(0,0,z);
        Material surface=old?oldWood:oak,frame=old?rust:metal;
        foreach(int side in new[]{-1,1})
        {
            Box(bay,"Glazing panel",new Vector3(side*3.46f,2.1f,.25f),new Vector3(.025f,2.35f,1.7f),old?oldGlass:glass);
            for(int j=-1;j<=1;j++)Box(bay,"Window mullion",new Vector3(side*3.42f,2.1f,.25f+j*.83f),new Vector3(.065f,2.4f,.04f),frame);
            Box(bay,"Window sill",new Vector3(side*3.38f,.92f,.25f),new Vector3(.17f,.08f,1.83f),frame);
        }
        Box(bay,"Desk top",new Vector3(2.66f,.8f,0),new Vector3(1.18f,.085f,2.10f),surface,old?Quaternion.Euler(0,0,3):Quaternion.identity);
        foreach(float dz in new[]{-.82f,.82f})Box(bay,"Desk leg",new Vector3(2.81f,.39f,dz),new Vector3(.11f,.78f,.11f),frame);
        Box(bay,"Monitor stand",new Vector3(2.75f,1.01f,0),new Vector3(.08f,.34f,.09f),black);
        Box(bay,"Monitor",new Vector3(2.76f,1.23f,0),new Vector3(.065f,.44f,.68f),black,old?Quaternion.Euler(8,0,15):Quaternion.identity);
        Box(bay,"Display",new Vector3(2.72f,1.23f,0),new Vector3(.007f,.37f,.60f),old?oldGlass:screen);
        Box(bay,"Keyboard",new Vector3(2.36f,.862f,0),new Vector3(.22f,.025f,.60f),black);
        var chair=Group(bay,"Office chair");chair.localPosition=new Vector3(old?1.97f:1.78f,0,.13f);chair.localRotation=Quaternion.Euler(0,old?27:0,old?-18:0);
        Box(chair,"Seat",new Vector3(0,.48f,0),new Vector3(.48f,.10f,.48f),old?rust:black);
        Box(chair,"Back",new Vector3(-.22f,.81f,0),new Vector3(.07f,.58f,.45f),old?rust:black);
        Box(chair,"Chair pedestal",new Vector3(0,.23f,0),new Vector3(.065f,.46f,.065f),frame);
        Box(chair,"Chair base",new Vector3(0,.06f,0),new Vector3(.58f,.045f,.055f),frame);
        Box(chair,"Chair base",new Vector3(0,.06f,0),new Vector3(.055f,.045f,.58f),frame);
        Box(bay,"Archive cabinet",new Vector3(-3.02f,.57f,0),new Vector3(.8f,1.14f,1.65f),old?dirty:white);
        for(int j=0;j<3;j++)
        {
            Box(bay,"Cabinet seam",new Vector3(-2.611f,.24f+j*.33f,0),new Vector3(.007f,.012f,1.55f),frame);
            Box(bay,"Drawer handle",new Vector3(-2.585f,.34f+j*.33f,0),new Vector3(.04f,.03f,.22f),frame);
        }
        if(old)
        {
            var growth=NewPlants();
            for(int i=0;i<18;i++)BroadLeaf(growth,new Vector3(-2.8f,1.14f,Random.Range(-.8f,.8f))+bay.localPosition,Random.Range(.18f,.45f));
            SavePlants(p,"Plants reclaiming cabinet "+z,growth);
        }
    }
    static void LockedDoor(Transform p,bool old)
    {
        var d=Group(p,"Locked service door - decorative");d.localPosition=new Vector3(3.46f,0,15.75f);
        Box(d,"Door panel",new Vector3(0,1.4f,0),new Vector3(.05f,2.8f,1.30f),old?oldWood:oak);
        foreach(float z in new[]{-.72f,.72f})Box(d,"Door frame",new Vector3(-.03f,1.5f,z),new Vector3(.10f,3,.10f),old?rust:metal);
        Box(d,"Door header",new Vector3(-.03f,3,0),new Vector3(.10f,.12f,1.55f),old?rust:metal);
        Box(d,"Locked reader",new Vector3(-.10f,1.3f,-.5f),new Vector3(.06f,.22f,.09f),black);
        Box(d,"Reader status",new Vector3(-.14f,1.36f,-.5f),new Vector3(.02f,.027f,.06f),Mat("Access denied amber",new Color(.8f,.3f,.055f),.1f,new Color(.5f,.08f,0)));
        Board(d,"СЛУЖЕБНОЕ\nЗАКРЫТО",new Vector3(-.06f,2,0),Quaternion.Euler(0,90,0),.038f);
    }
    static Batch[] NewPlants()=>new[]{new Batch(),new Batch(),new Batch(),new Batch()};
    static void SavePlants(Transform p,string name,Batch[] b)
    {
        var group=Group(p,name);
        for(int i=0;i<4;i++)if(b[i].v.Count>0)MeshObject(group,i==3?"Woody stems":"Leaves "+i,b[i],i==3?bark:greens[i]);
    }
    static void BroadLeaf(Batch[] b,Vector3 origin,float size)
    {
        float a=Random.value*6.283f;Vector3 direction=new Vector3(Mathf.Cos(a)*.65f,Random.Range(.3f,1f),Mathf.Sin(a)*.65f).normalized;
        Vector3 tip=origin+direction*size;
        b[3].Tube(new[]{origin,tip},size*.017f);
        b[Random.Range(0,3)].Leaf(origin+direction*size*.32f,tip+direction*size*.7f,size*.28f,Random.value*120);
    }
    static void Vegetation(Transform p,Vector3 origin,int side)
    {
        var b=NewPlants();
        for(int plant=0;plant<7;plant++)
        {
            Vector3 o=origin+new Vector3(Random.Range(-.34f,.34f),0,Random.Range(-1.35f,1.35f));
            for(int frond=0;frond<5;frond++)
            {
                float a=Random.value*Mathf.PI*2;Vector3 dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                float len=Random.Range(.45f,.9f);var stem=new Vector3[9];
                for(int j=0;j<9;j++){float t=j/8f;stem[j]=o+dir*len*t+Vector3.up*(Mathf.Sin(t*Mathf.PI*.8f)*len*.85f);}
                b[3].Tube(stem,.009f);
                for(int j=1;j<8;j++)foreach(int hand in new[]{-1,1})
                {
                    float width=len*.22f*(1-j/9f);Vector3 cross=Vector3.Cross(dir,Vector3.up)*hand;
                    b[Random.Range(0,3)].Leaf(stem[j],stem[j]+cross*width+dir*.10f+Vector3.up*.025f,width*.24f,0);
                }
            }
            for(int j=0;j<3;j++)BroadLeaf(b,o,Random.Range(.3f,.85f));
        }
        SavePlants(p,"Ferns and broad leaves "+origin,b);
        Box(p,"Moss bed",origin+Vector3.up*.01f,new Vector3(.75f,.04f,2.6f),moss);
    }
    static void HangingCanopy(Transform p)
    {
        var b=NewPlants();
        for(int side=-1;side<=1;side+=2)for(float z=-3;z<34;z+=1.1f)
        {
            float length=Random.Range(.7f,2.2f);Vector3 start=new Vector3(side*Random.Range(2.75f,3.45f),4.38f,z);
            var line=new Vector3[13];
            for(int j=0;j<13;j++){float t=j/12f;line[j]=start+new Vector3(Mathf.Sin(t*5+z)*.10f,-t*length,Mathf.Sin(t*4)*.15f);}
            b[3].Tube(line,.022f);
            for(int j=1;j<13;j++)for(int k=0;k<2;k++)
            {
                Vector3 tip=line[j]+new Vector3(-side*Random.Range(.16f,.35f),-.10f,(k==0?-1:1)*.15f);
                b[Random.Range(0,3)].Leaf(line[j],tip,.12f,k*70);
            }
        }
        for(int i=0;i<20;i++)
        {
            float z=Random.Range(-3f,33f);var line=new Vector3[20];
            for(int j=0;j<20;j++){float t=j/19f;line[j]=new Vector3(Mathf.Lerp(-3.5f,3.5f,t),4.25f-Mathf.Sin(t*Mathf.PI)*Random.Range(.2f,.55f),z+Mathf.Sin(t*5)*.25f);}
            b[3].Tube(line,.034f);
            for(int j=0;j<20;j++)b[Random.Range(0,3)].Leaf(line[j],line[j]+new Vector3(.05f,-.2f,.3f),.14f,0);
        }
        SavePlants(p,"Hanging ivy and ceiling roots",b);
    }
    static void RootWall(Transform p)
    {
        var group=Group(p,"Wall 1 - living thicket - solid without mask");var b=NewPlants();
        // Interlaced stems fill the same footprint as the original blocking wall.
        for(int i=0;i<42;i++)
        {
            float x=-3.48f+i*6.96f/41;var line=new Vector3[17];
            for(int j=0;j<17;j++){float t=j/16f;line[j]=new Vector3(Mathf.Clamp(x+Mathf.Sin(t*9+i)*.16f,-3.52f,3.52f),t*4.3f,6+Mathf.Cos(t*8+i)*.10f);}
            b[3].Tube(line,Random.Range(.067f,.11f));
            for(int j=1;j<16;j++)
            {
                Vector3 tip=line[j]+new Vector3(Random.Range(-.25f,.25f),Random.Range(.08f,.3f),-.25f);
                b[Random.Range(0,3)].Leaf(line[j],tip,.14f,Random.Range(-45f,45f));
            }
        }
        for(int k=0;k<10;k++)
        {
            var line=new Vector3[20];for(int j=0;j<20;j++)line[j]=new Vector3(-3.5f+j*7f/19,k*.43f+.2f+Mathf.Sin(j*.7f+k)*.13f,5.96f);
            b[3].Tube(line,.07f);
        }
        SavePlants(group,"Interwoven roots and leaves",b);
    }
    static void BrokenBridgeEdges(Transform p)
    {
        var group=Group(p,"Grass at second pit edges");
        // The second pit is open in the overgrown state.
        foreach(float z in new[]{20.32f,23.68f})for(int i=0;i<20;i++)
        {
            var tuft=NewPlants();for(int j=0;j<5;j++)BroadLeaf(tuft,new Vector3(Random.Range(-3.4f,3.4f),.01f,z),Random.Range(.1f,.23f));
            SavePlants(group,"Grass at broken slab edge",tuft);
        }
    }
    static void RuinDetails(Transform p)
    {
        var cracks=new Batch();
        foreach(int side in new[]{-1,1})for(int i=0;i<55;i++)
        {
            float z=Random.Range(-4f,33f),y=Random.Range(.3f,3.7f);Vector3 a=new Vector3(side*3.51f,y,z);
            var line=new Vector3[6];for(int j=0;j<6;j++)line[j]=a+new Vector3(0,-j*.07f,Mathf.Sin(j*2+i)*.04f+j*.11f);
            cracks.Tube(line,.007f);
        }
        MeshObject(p,"Wall fissures",cracks,crack);
        for(int i=0;i<120;i++)
        {
            float z=Random.Range(-4.5f,33.4f);if(IsPit(z))continue;
            float side=Random.value<.5f?-1:1;
            Box(p,"Fallen plaster",new Vector3(side*Random.Range(2.7f,3.4f),.04f,z),new Vector3(Random.Range(.04f,.19f),.045f,Random.Range(.04f,.24f)),dirty,Quaternion.Euler(0,Random.value*180,Random.Range(-15,15)));
        }
    }
    static void Lighting(Transform p,bool old)
    {
        var sun=new GameObject(old?"Daylight through broken roof":"Office soft daylight");sun.transform.SetParent(p,false);
        var dl=sun.AddComponent<Light>();dl.type=LightType.Directional;dl.color=old?new Color(.98f,.99f,.92f):new Color(1,.91f,.77f);
        dl.intensity=old?1.25f:.60f;dl.shadows=LightShadows.Soft;dl.shadowStrength=.68f;sun.transform.rotation=Quaternion.Euler(48,-28,0);
        for(float z=-2;z<34;z+=5)
        {
            var lamp=new GameObject(old?"Cool ambient bounce":"Warm ceiling light");lamp.transform.SetParent(p,false);lamp.transform.localPosition=new Vector3(0,3.6f,z);
            var l=lamp.AddComponent<Light>();l.type=LightType.Point;l.color=old?new Color(.91f,.94f,.89f):new Color(1,.88f,.70f);l.range=8f;l.intensity=old?1.6f:2.8f;l.shadows=LightShadows.None;
        }
    }
    static void MeshObject(Transform p,string name,Batch b,Material mat)
    {
        if(b.v.Count==0)return;
        string path=Folder+"/Meshes/"+(meshIndex++).ToString("D4")+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=!mesh;if(create)mesh=new Mesh();else mesh.Clear();
        mesh.name=name;mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(b.v);mesh.SetTriangles(b.t,0);mesh.SetUVs(0,b.uv);mesh.RecalculateNormals();mesh.RecalculateBounds();
        if(create)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
        var go=new GameObject(name);go.transform.SetParent(p,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
    }
    sealed class Batch
    {
        public readonly List<Vector3> v=new List<Vector3>();public readonly List<int> t=new List<int>();public readonly List<Vector2> uv=new List<Vector2>();
        public void Tube(Vector3[] points,float radius)
        {
            int start=v.Count;const int sides=7;
            for(int j=0;j<points.Length;j++)
            {
                Vector3 axis=(points[Mathf.Min(points.Length-1,j+1)]-points[Mathf.Max(0,j-1)]).normalized;
                Vector3 x=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized;Vector3 y=Vector3.Cross(axis,x);
                for(int k=0;k<sides;k++){float a=k*Mathf.PI*2/sides;v.Add(points[j]+(x*Mathf.Cos(a)+y*Mathf.Sin(a))*radius);uv.Add(new Vector2(k/(float)sides,j*.2f));}
                if(j==0)continue;
                for(int k=0;k<sides;k++){int a=start+(j-1)*sides+k,b=start+(j-1)*sides+(k+1)%sides,c=b+sides,d=a+sides;t.AddRange(new[]{a,b,c,a,c,d});}
            }
        }
        public void Leaf(Vector3 from,Vector3 to,float width,float twist)
        {
            Vector3 axis=to-from;Vector3 side=Vector3.Cross(axis,Vector3.up).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;
            side=Quaternion.AngleAxis(twist,axis.normalized)*side;Vector3 normal=Vector3.Cross(side,axis).normalized;
            int start=v.Count,firstTriangle=t.Count;const int rows=6;
            for(int j=0;j<=rows;j++)
            {
                float f=j/(float)rows,w=Mathf.Pow(Mathf.Max(0,Mathf.Sin(f*Mathf.PI)),.75f)*width;
                for(int k=0;k<3;k++){float a=k-1;v.Add(Vector3.Lerp(from,to,f)+side*w*a+normal*Mathf.Sin(f*Mathf.PI)*width*(k==1?.24f:-.14f));uv.Add(new Vector2(k*.5f,f));}
                if(j==0)continue;
                for(int k=0;k<2;k++){int a=start+(j-1)*3+k,b=a+1,c=b+3,d=a+3;t.AddRange(new[]{a,b,c,a,c,d});}
            }
            int count=v.Count-start,lastTriangle=t.Count;
            for(int i=0;i<count;i++){v.Add(v[start+i]);uv.Add(uv[start+i]);}
            for(int i=firstTriangle;i<lastTriangle;i+=3)t.AddRange(new[]{t[i]+count,t[i+2]+count,t[i+1]+count});
        }
    }
}
