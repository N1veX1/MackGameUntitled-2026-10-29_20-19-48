using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Dimensioned, bevelled furniture; all objects remain individually editable.
public static class HospitalFurniture
{
    const string Folder="Assets/HospitalAwakening/";
    static Material M(string name,Color color,float smooth=.3f,float metal=0) {
        string path=Folder+"Materials/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",smooth); m.SetFloat("_Metallic",metal); EditorUtility.SetDirty(m); return m;
    }
    static Material Shell=>M("Furniture_Warm polymer",new Color(.74f,.76f,.75f),.35f);
    static Material Alloy=>M("Furniture_Satin alloy",new Color(.42f,.47f,.49f),.6f,.75f);
    static Material Graphite=>M("Furniture_Graphite",new Color(.045f,.055f,.06f),.35f);
    static Material Fabric=>M("Furniture_Soft upholstery",new Color(.13f,.24f,.26f),.12f);
    static Material Cotton=>M("Furniture_Cotton",new Color(.88f,.87f,.8f),.05f);
    static Material Blanket=>M("Furniture_Blanket",new Color(.33f,.4f,.42f),.09f);
    static Transform G(string n,Vector3 p,Transform parent) { var g=new GameObject(n).transform; g.SetParent(parent,false); g.localPosition=p; return g; }
    static GameObject R(string n,Vector3 p,Vector3 size,float radius,Material mat,Transform parent,bool collider=true) {
        var t=G(n,p,parent); var mesh=RoundedMesh(size,radius);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh; t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;
        if(collider) t.gameObject.AddComponent<BoxCollider>().size=size;
        return t.gameObject;
    }
    static Mesh RoundedMesh(Vector3 size,float radius) {
        string key=string.Format(CultureInfo.InvariantCulture,"R_{0:F3}_{1:F3}_{2:F3}_{3:F3}",size.x,size.y,size.z,radius);
        string path=Folder+"Meshes/"+key+".asset"; var found=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(found) return found;
        System.IO.Directory.CreateDirectory(Folder+"Meshes");
        var v=new List<Vector3>(); var norm=new List<Vector3>(); var uv=new List<Vector2>(); var tri=new List<int>();
        var half=size*.5f; radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z))*.99f); var inner=half-Vector3.one*radius;
        const int divisions=8;
        for(int face=0;face<6;face++) {
            int axis=face/2; float sign=face%2==0?1:-1; int u=(axis+1)%3,w=(axis+2)%3; int offset=v.Count;
            for(int y=0;y<=divisions;y++) for(int x=0;x<=divisions;x++) {
                var p=Vector3.zero; p[axis]=half[axis]*sign; p[u]=Mathf.Lerp(-half[u],half[u],x/(float)divisions); p[w]=Mathf.Lerp(-half[w],half[w],y/(float)divisions);
                var nearest=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z)); var normal=(p-nearest).normalized;
                v.Add(nearest+normal*radius); norm.Add(normal); uv.Add(new Vector2(x/(float)divisions*size[u],y/(float)divisions*size[w]));
            }
            for(int y=0;y<divisions;y++) for(int x=0;x<divisions;x++) {
                int a=offset+y*(divisions+1)+x,b=a+1,c=a+divisions+2,d=a+divisions+1;
                if(sign>0) tri.AddRange(new[]{a,b,c,a,c,d}); else tri.AddRange(new[]{a,c,b,a,d,c});
            }
        }
        var mesh=new Mesh { name=key }; mesh.SetVertices(v); mesh.SetNormals(norm); mesh.SetUVs(0,uv); mesh.SetTriangles(tri,0); mesh.RecalculateBounds(); mesh.RecalculateTangents(); AssetDatabase.CreateAsset(mesh,path); return mesh;
    }
    static GameObject Cylinder(string n,Vector3 p,float radius,float length,Material mat,Transform parent,Quaternion? rotation=null) {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder); o.name=n; o.transform.SetParent(parent,false); o.transform.localPosition=p; o.transform.localScale=new Vector3(radius*2,length*.5f,radius*2); o.transform.localRotation=rotation??Quaternion.identity; o.GetComponent<Renderer>().sharedMaterial=mat; UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>()); return o;
    }
    static void Pipe(string n,Vector3 a,Vector3 b,float radius,Material mat,Transform parent) => Cylinder(n,(a+b)*.5f,radius,Vector3.Distance(a,b),mat,parent,Quaternion.FromToRotation(Vector3.up,b-a));
    static void Label(string n,Vector3 p,float height,Color color,Transform parent) {
        var t=G("Print / "+n,p,parent); var text=t.gameObject.AddComponent<TextMesh>(); text.text=n; text.font=AssetDatabase.LoadAssetAtPath<Font>(Folder+"HospitalArial.ttf"); text.fontSize=72; text.characterSize=height; text.anchor=TextAnchor.MiddleCenter; text.alignment=TextAlignment.Center; text.color=color; t.gameObject.GetComponent<Renderer>().sharedMaterial=text.font.material;
    }
    public static void Bed(Vector3 pos,Transform parent,string id) {
        var g=G("Long-stay bed "+id,pos,parent); var shell=Shell; var cotton=Cotton; var metal=Alloy;
        R("Recessed bed base",new Vector3(0,.22f,0),new Vector3(1.16f,.3f,2.16f),.055f,Graphite,g);
        R("Upholstered bed frame",new Vector3(0,.43f,0),new Vector3(1.42f,.23f,2.5f),.055f,shell,g);
        R("Deep supportive mattress",new Vector3(0,.65f,0),new Vector3(1.36f,.28f,2.38f),.1f,cotton,g);
        R("Fitted sheet",new Vector3(0,.79f,0),new Vector3(1.35f,.045f,2.36f),.02f,cotton,g,false);
        R("Padded headboard",new Vector3(0,.88f,1.26f),new Vector3(1.48f,1.14f,.12f),.055f,Fabric,g);
        R("Headboard outer frame",new Vector3(0,.9f,1.32f),new Vector3(1.54f,1.25f,.1f),.04f,shell,g);
        R("Full-size pillow",new Vector3(0,.9f,.79f),new Vector3(.98f,.19f,.53f),.09f,cotton,g,false);
        R("Pillow seam",new Vector3(0,.9f,.79f),new Vector3(1.0f,.022f,.54f),.009f,Shell,g,false);
        R("Soft duvet",new Vector3(0,.86f,-.33f),new Vector3(1.37f,.15f,1.52f),.065f,Blanket,g,false);
        R("Folded duvet edge",new Vector3(0,.95f,.4f),new Vector3(1.38f,.06f,.19f),.025f,cotton,g,false);
        foreach(float x in new[]{-.69f,.69f}) R("Hanging duvet edge",new Vector3(x,.7f,-.35f),new Vector3(.045f,.3f,1.47f),.02f,Blanket,g,false);
        foreach(float x in new[]{-.57f,.57f}) foreach(float z in new[]{-.92f,.94f}) {
            Cylinder("Recessed support foot",new Vector3(x,.1f,z),.045f,.2f,metal,g);
            Cylinder("Rubber foot",new Vector3(x,.025f,z),.06f,.05f,Graphite,g);
        }
        // A low conventional foot end keeps the mattress visible from the entrance.
        R("Low rounded foot end",new Vector3(0,.47f,-1.26f),new Vector3(1.45f,.36f,.09f),.035f,shell,g);
        Label(id,new Vector3(0,1.2f,1.188f),.007f,new Color(.8f,.85f,.85f),g);
    }
    public static void Desktop(Transform g) {
        R("Wide rounded desktop / 2.3 m",new Vector3(0,.79f,0),new Vector3(2.3f,.075f,.95f),.032f,Shell,g);
        R("Desktop edge trim",new Vector3(0,.743f,0),new Vector3(2.26f,.025f,.91f),.01f,Alloy,g,false);
        foreach(float x in new[]{-1.0f,1.0f}) R("Table trestle",new Vector3(x,.37f,0),new Vector3(.075f,.74f,.72f),.025f,Alloy,g);
        R("Modesty panel",new Vector3(0,.52f,.35f),new Vector3(1.93f,.35f,.035f),.012f,Graphite,g);
        R("Under-table drawer",new Vector3(.7f,.66f,0),new Vector3(.55f,.15f,.7f),.025f,Shell,g);
        R("Recessed drawer handle",new Vector3(.7f,.66f,-.357f),new Vector3(.24f,.027f,.018f),.007f,Graphite,g,false);
        Cylinder("Cable grommet",new Vector3(-.92f,.832f,.29f),.036f,.007f,Graphite,g);
    }
    public static void Chair(Vector3 pos,Transform parent) {
        var g=G("Ergonomic padded armchair",pos,parent); var cloth=Fabric; var dark=Graphite; var metal=Alloy;
        R("Contoured seat cushion",new Vector3(0,.51f,0),new Vector3(.62f,.15f,.58f),.07f,cloth,g);
        R("Seat underside",new Vector3(0,.42f,0),new Vector3(.54f,.07f,.51f),.025f,dark,g);
        var back=R("High curved padded back",new Vector3(0,.91f,-.27f),new Vector3(.61f,.77f,.13f),.06f,cloth,g); back.transform.localRotation=Quaternion.Euler(-9,0,0);
        R("Lumbar cushion",new Vector3(0,.75f,-.16f),new Vector3(.46f,.2f,.085f),.04f,cloth,g,false);
        R("Headrest",new Vector3(0,1.29f,-.32f),new Vector3(.43f,.19f,.14f),.06f,cloth,g);
        foreach(float x in new[]{-.34f,.34f}) {
            Pipe("Armrest upright",new Vector3(x,.45f,-.1f),new Vector3(x,.73f,-.1f),.018f,metal,g);
            R("Soft arm pad",new Vector3(x,.76f,.015f),new Vector3(.085f,.065f,.43f),.029f,dark,g);
        }
        Cylinder("Gas lift",new Vector3(0,.25f,0),.033f,.32f,metal,g);
        Cylinder("Gas lift shroud",new Vector3(0,.17f,0),.055f,.15f,dark,g);
        for(int i=0;i<5;i++) {
            float angle=i*Mathf.PI*2/5; var tip=new Vector3(Mathf.Cos(angle)*.36f,.1f,Mathf.Sin(angle)*.36f);
            Pipe("Five-star base spoke",new Vector3(0,.14f,0),tip,.025f,metal,g);
            Cylinder("Twin caster",tip+Vector3.down*.045f,.052f,.07f,dark,g,Quaternion.Euler(0,-i*72,90));
        }
        var collider=g.gameObject.AddComponent<BoxCollider>(); collider.center=new Vector3(0,.62f,0); collider.size=new Vector3(.73f,1.25f,.73f);
    }
    public static void Equipment(Vector3 pos,Transform parent) {
        var g=G("Patient monitoring station / detailed",pos,parent); var shell=Shell; var metal=Alloy; var dark=Graphite;
        var screen=M("Equipment_Display black",new Color(.008f,.025f,.03f),.35f);
        var mint=M("Equipment_Signal mint",new Color(.15f,.95f,.63f),.25f); mint.EnableKeyword("_EMISSION"); mint.SetColor("_EmissionColor",new Color(.08f,.7f,.35f));
        var amber=M("Equipment_Amber indicator",new Color(.9f,.6f,.08f));
        var blue=M("Equipment_Blue connectors",new Color(.08f,.31f,.43f));
        R("Wheeled pedestal",new Vector3(0,.16f,0),new Vector3(.75f,.12f,.68f),.05f,dark,g);
        foreach(float x in new[]{-.29f,.29f}) foreach(float z in new[]{-.25f,.25f}) Cylinder("Locking caster",new Vector3(x,.075f,z),.065f,.065f,dark,g,Quaternion.Euler(0,0,90));
        R("Equipment enclosure",new Vector3(0,.67f,0),new Vector3(.59f,.9f,.49f),.045f,shell,g);
        R("Supply drawer",new Vector3(0,.42f,-.252f),new Vector3(.5f,.22f,.018f),.008f,shell,g);
        R("Recessed drawer pull",new Vector3(0,.45f,-.266f),new Vector3(.23f,.026f,.015f),.006f,dark,g,false);
        R("Instrument tray",new Vector3(0,1.16f,0),new Vector3(.74f,.045f,.61f),.02f,metal,g);
        for(int i=0;i<8;i++) R("Ventilation grille",new Vector3(-.18f+i*.05f,.82f,-.252f),new Vector3(.02f,.17f,.008f),.003f,dark,g,false);
        Pipe("Monitor mounting arm",new Vector3(0,1.17f,.13f),new Vector3(0,1.43f,.13f),.028f,metal,g);
        R("Bevelled monitor shell",new Vector3(0,1.58f,0),new Vector3(.78f,.56f,.13f),.045f,shell,g);
        R("Monitor rubber bezel",new Vector3(-.015f,1.59f,-.073f),new Vector3(.7f,.46f,.025f),.015f,dark,g,false);
        R("Monitor glass display",new Vector3(-.055f,1.59f,-.089f),new Vector3(.57f,.385f,.008f),.003f,screen,g,false);
        Label("72",new Vector3(.13f,1.67f,-.098f),.012f,new Color(.2f,1,.6f),g);
        Label("98%",new Vector3(.13f,1.52f,-.098f),.008f,new Color(.4f,.8f,1),g);
        Label("ECG   SpO2",new Vector3(-.17f,1.74f,-.099f),.003f,Color.white,g);
        for(int row=0;row<2;row++) for(int i=0;i<38;i++) {
            float x=-.3f+i*.008f; float spike=i%15==7?.065f:i%15==8?-.035f:Mathf.Sin(i*.65f)*.008f;
            float next=(i+1)%15==7?.065f:(i+1)%15==8?-.035f:Mathf.Sin((i+1)*.65f)*.008f;
            Pipe("ECG waveform",new Vector3(x,1.63f-row*.14f+spike,-.099f),new Vector3(x+.008f,1.63f-row*.14f+next,-.099f),.0016f,mint,g);
        }
        for(int i=0;i<3;i++) Cylinder("Monitor soft key",new Vector3(.314f,1.7f-i*.1f,-.087f),.015f,.012f,i==0?mint:dark,g,Quaternion.Euler(90,0,0));
        Cylinder("Monitor rotary dial",new Vector3(.29f,1.37f,-.087f),.027f,.025f,metal,g,Quaternion.Euler(90,0,0));
        Label("LIFE SUPPORT / 01",new Vector3(0,1.18f,-.315f),.003f,new Color(.2f,.26f,.28f),g);
        // A separate infusion pump with line routing, connectors and hanging fluid bag.
        Pipe("Infusion mast",new Vector3(.62f,.12f,.12f),new Vector3(.62f,2.19f,.12f),.016f,metal,g);
        Pipe("Infusion crossbar",new Vector3(.38f,2.19f,.12f),new Vector3(.83f,2.19f,.12f),.013f,metal,g);
        R("Infusion controller",new Vector3(.62f,1.22f,.06f),new Vector3(.26f,.37f,.18f),.025f,shell,g);
        R("Pump display",new Vector3(.62f,1.29f,-.037f),new Vector3(.19f,.1f,.01f),.004f,screen,g,false);
        Label("125 ml/h",new Vector3(.62f,1.29f,-.045f),.003f,new Color(.4f,1,.8f),g);
        for(int i=0;i<3;i++) Cylinder("Infusion key",new Vector3(.55f+i*.065f,1.14f,-.04f),.012f,.008f,i==0?amber:blue,g,Quaternion.Euler(90,0,0));
        var bag=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Materials/07 Smart glass clear.mat");
        R("Fluid bag",new Vector3(.43f,1.94f,.12f),new Vector3(.2f,.33f,.075f),.03f,bag,g,false);
        R("Bag printed label",new Vector3(.43f,1.97f,.076f),new Vector3(.12f,.12f,.004f),.001f,shell,g,false);
        Label("NaCl\n0.9%",new Vector3(.43f,1.97f,.07f),.003f,new Color(.15f,.2f,.22f),g);
        Pipe("Bag hanger",new Vector3(.43f,2.1f,.12f),new Vector3(.43f,2.19f,.12f),.008f,metal,g);
        var points=new[]{new Vector3(.43f,1.78f,.12f),new Vector3(.43f,1.58f,.1f),new Vector3(.56f,1.43f,.02f),new Vector3(.55f,1.03f,-.03f),new Vector3(.45f,.8f,-.08f),new Vector3(.24f,.73f,-.29f)};
        for(int i=0;i<points.Length-1;i++) Pipe("Infusion tubing",points[i],points[i+1],.004f,blue,g);
        // Oxygen bottle with retaining straps, regulator, gauge and coiled supply line.
        Cylinder("Oxygen bottle",new Vector3(-.49f,.68f,.13f),.105f,.88f,blue,g);
        R("Bottle upper shoulder",new Vector3(-.49f,1.12f,.13f),new Vector3(.17f,.12f,.17f),.05f,blue,g,false);
        foreach(float y in new[]{.38f,.92f}) R("Bottle retaining strap",new Vector3(-.49f,y,.13f),new Vector3(.225f,.04f,.225f),.015f,dark,g,false);
        Pipe("Oxygen regulator",new Vector3(-.49f,1.15f,.13f),new Vector3(-.49f,1.28f,.13f),.025f,metal,g);
        Cylinder("Valve wheel",new Vector3(-.49f,1.3f,.13f),.048f,.02f,blue,g);
        Cylinder("Pressure gauge rim",new Vector3(-.4f,1.23f,.045f),.046f,.02f,metal,g,Quaternion.Euler(90,0,0));
        Cylinder("Pressure gauge face",new Vector3(-.4f,1.23f,.03f),.038f,.005f,shell,g,Quaternion.Euler(90,0,0));
        Pipe("Gauge needle",new Vector3(-.4f,1.23f,.025f),new Vector3(-.38f,1.25f,.025f),.002f,dark,g);
        Label("O2",new Vector3(-.49f,.7f,.022f),.007f,Color.white,g);
        for(int i=0;i<18;i++) { float a=i*.5f,b=(i+1)*.5f; Pipe("Coiled sensor cable",new Vector3(-.23f+Mathf.Sin(a)*.035f,.96f-i*.018f,-.31f),new Vector3(-.23f+Mathf.Sin(b)*.035f,.96f-(i+1)*.018f,-.31f),.0035f,dark,g); }
    }
    public static void Sanitary(Vector3 position,float angle,Transform parent) {
        var g=G("Sanitary station / toilet and washbasin",position,parent); g.localRotation=Quaternion.Euler(0,angle,0);
        var ceramic=M("Sanitary white ceramic",new Color(.88f,.89f,.87f),.7f); var chrome=Alloy;
        R("Toilet pedestal",new Vector3(-.65f,.2f,-.18f),new Vector3(.3f,.4f,.46f),.1f,ceramic,g);
        R("Toilet cistern",new Vector3(-.65f,.69f,.24f),new Vector3(.46f,.5f,.19f),.055f,ceramic,g);
        R("Cistern lid",new Vector3(-.65f,.95f,.24f),new Vector3(.48f,.035f,.21f),.016f,ceramic,g);
        Cylinder("Flush button",new Vector3(-.65f,.974f,.24f),.025f,.012f,chrome,g);
        Bowl("Toilet bowl",new Vector3(-.65f,.49f,-.18f),.25f,.36f,.2f,ceramic,g);
        Bowl("Toilet seat rim",new Vector3(-.65f,.515f,-.18f),.26f,.37f,.025f,Shell,g);
        R("Raised toilet lid",new Vector3(-.65f,.73f,.12f),new Vector3(.43f,.43f,.035f),.016f,Shell,g);
        var c=g.gameObject.AddComponent<BoxCollider>(); c.center=new Vector3(-.65f,.4f,-.16f); c.size=new Vector3(.52f,.8f,.75f);
        R("Washbasin wall bracket",new Vector3(.65f,.66f,.16f),new Vector3(.5f,.25f,.29f),.04f,ceramic,g);
        Bowl("Washbasin",new Vector3(.65f,.87f,-.12f),.36f,.28f,.18f,ceramic,g);
        R("Basin back shelf",new Vector3(.65f,.85f,.21f),new Vector3(.72f,.06f,.16f),.025f,ceramic,g);
        Pipe("Faucet riser",new Vector3(.65f,.88f,.2f),new Vector3(.65f,1.1f,.2f),.018f,chrome,g);
        Pipe("Faucet spout",new Vector3(.65f,1.1f,.2f),new Vector3(.65f,1.1f,-.02f),.018f,chrome,g);
        Pipe("Faucet outlet",new Vector3(.65f,1.1f,-.02f),new Vector3(.65f,1.05f,-.02f),.018f,chrome,g);
        R("Mirror frame",new Vector3(.65f,1.6f,.35f),new Vector3(.74f,.9f,.045f),.02f,chrome,g);
        R("Mirror surface",new Vector3(.65f,1.6f,.321f),new Vector3(.68f,.84f,.01f),.004f,M("Mirror placeholder",new Color(.55f,.67f,.7f),.95f,.9f),g,false);
        R("Soap dispenser",new Vector3(1.2f,1.22f,.22f),new Vector3(.14f,.24f,.12f),.025f,Shell,g);
        Pipe("Towel rail",new Vector3(.35f,.56f,-.32f),new Vector3(.95f,.56f,-.32f),.012f,chrome,g);
        R("Hand towel",new Vector3(.65f,.4f,-.33f),new Vector3(.32f,.32f,.025f),.01f,Cotton,g,false);
    }
    static void Bowl(string name,Vector3 position,float rx,float rz,float depth,Material mat,Transform parent) {
        var t=G(name,position,parent); var vertices=new List<Vector3>(); var triangles=new List<int>();
        var profile=new[]{new Vector2(.6f,-depth),new Vector2(.95f,-depth*.4f),new Vector2(1,0),new Vector2(.85f,0),new Vector2(.72f,-depth*.55f),new Vector2(.15f,-depth*.85f)};
        for(int j=0;j<profile.Length;j++) for(int i=0;i<=64;i++) { float a=i*Mathf.PI*2/64; vertices.Add(new Vector3(Mathf.Cos(a)*rx*profile[j].x,profile[j].y,Mathf.Sin(a)*rz*profile[j].x)); }
        for(int j=0;j<profile.Length-1;j++) for(int i=0;i<64;i++) { int a=j*65+i,b=a+1,c=a+66,d=a+65; triangles.AddRange(new[]{a,c,b,a,d,c}); }
        var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path=Folder+"Meshes/"+name.Replace(' ','_')+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(existing) { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; } else AssetDatabase.CreateAsset(mesh,path);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh; t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;
        Cylinder("Drain",position+Vector3.down*(depth*.85f),.025f,.008f,Alloy,parent);
    }
    public static void TextureReady(Transform architecture) {
        // Give architectural surfaces UVs in metres rather than stretching 0..1 over an entire wall.
        int surfaceIndex=0;
        foreach(var f in architecture.GetComponentsInChildren<MeshFilter>()) {
            if(!(f.name.Contains("wall")||f.name.Contains("Wall")||f.name=="Front lintel"||f.name.Contains("Floor tile")||f.name.Contains("ceiling panel"))) continue;
            var source=f.sharedMesh; if(!source) continue; var mesh=UnityEngine.Object.Instantiate(source); mesh.name="UV metres / "+f.name;
            var vertices=mesh.vertices; var normals=mesh.normals; var uv=new Vector2[vertices.Length];
            for(int i=0;i<uv.Length;i++) { var world=f.transform.TransformPoint(vertices[i]); var n=f.transform.TransformDirection(normals[i]);
                uv[i]=Mathf.Abs(n.y)>.5f?new Vector2(world.x,world.z):Mathf.Abs(n.x)>.5f?new Vector2(world.z,world.y):new Vector2(world.x,world.y);
            }
            mesh.uv=uv; mesh.RecalculateTangents(); string path=Folder+"Meshes/Surface_"+(surfaceIndex++).ToString("D4")+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(existing) { EditorUtility.CopySerialized(mesh,existing); UnityEngine.Object.DestroyImmediate(mesh); mesh=existing; } else AssetDatabase.CreateAsset(mesh,path); f.sharedMesh=mesh;
        }
    }
}
