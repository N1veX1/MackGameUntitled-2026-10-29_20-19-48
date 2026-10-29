using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Extend the visible neighbouring face across a merge without moving its physical backing.</summary>
public sealed class MaskBackingAppearance : System.IDisposable
{
    readonly Renderer backing;
    readonly Renderer reference;
    readonly Material[] backingOriginals;
    readonly Material[] backingProjected;
    readonly Material[][] obstacleProjected;
    readonly Vector3[] vertices;
    readonly int[] triangles;
    readonly Vector2[] uv;
    bool active;
    public bool Ready { get; }

    public MaskBackingAppearance(Renderer surface, Renderer[] obstacles, Renderer appearanceReference)
    {
        backing=surface;
        reference=appearanceReference;
        var filter=reference ? reference.GetComponent<MeshFilter>() : null;
        var shader=Resources.Load<Shader>("MaskBackingProjection");
        if(!backing || !reference || !filter || !filter.sharedMesh || !shader || !shader.isSupported) return;
        var mesh=filter.sharedMesh;
        vertices=mesh.vertices;triangles=mesh.triangles;uv=mesh.uv;
        backingOriginals=backing.sharedMaterials;
        Material appearance=reference.sharedMaterial;
        if(backingOriginals.Length==0 || !appearance) return;
        backingProjected=new Material[backingOriginals.Length];
        for(int i=0;i<backingProjected.Length;i++) backingProjected[i]=Create(shader,appearance,false);
        obstacleProjected=new Material[obstacles.Length][];
        for(int i=0;i<obstacles.Length;i++)
        {
            int count=obstacles[i] ? obstacles[i].sharedMaterials.Length : 0;
            obstacleProjected[i]=new Material[count];
            for(int j=0;j<count;j++) obstacleProjected[i][j]=Create(shader,appearance,true);
        }
        Ready=true;
    }

    static Material Create(Shader shader,Material source,bool transparent)
    {
        var material=new Material(shader) { name="Matched backing appearance", hideFlags=HideFlags.DontSave };
        // Preserve the original Lit settings, including specular and reflection properties.
        if(source) material.CopyPropertiesFromMaterial(source);
        material.SetFloat("_Opacity",1);
        material.SetFloat("_ZWrite",transparent?0:1);
        material.renderQueue=transparent?(int)RenderQueue.Transparent:(int)RenderQueue.Geometry;
        return material;
    }

    public bool Apply(Camera camera,float opacity)
    {
        if(!Ready || !camera || !FindReferenceFace(camera.transform.forward,out var plane,out var rowU,out var rowV))
        { Restore();return false; }
        foreach(var material in backingProjected) SetProjection(material,camera.transform.forward,plane,rowU,rowV,1);
        foreach(var group in obstacleProjected) foreach(var material in group) SetProjection(material,camera.transform.forward,plane,rowU,rowV,opacity);
        if(!active) { backing.sharedMaterials=backingProjected;active=true; }
        return true;
    }
    public Material[] ForObstacle(int index) => obstacleProjected[index];
    static void SetProjection(Material material,Vector3 direction,Vector4 plane,Vector4 rowU,Vector4 rowV,float opacity)
    {
        material.SetVector("_ProjectionDirection",direction);material.SetVector("_BackingPlane",plane);
        material.SetVector("_UvRowU",rowU);material.SetVector("_UvRowV",rowV);material.SetFloat("_Opacity",opacity);
    }

    bool FindReferenceFace(Vector3 direction,out Vector4 plane,out Vector4 rowU,out Vector4 rowV)
    {
        plane=rowU=rowV=Vector4.zero;
        if(uv.Length!=vertices.Length) return false;
        float best=.99f;bool found=false;
        for(int i=0;i<triangles.Length;i+=3)
        {
            int ia=triangles[i],ib=triangles[i+1],ic=triangles[i+2];
            Vector3 a=reference.transform.TransformPoint(vertices[ia]),b=reference.transform.TransformPoint(vertices[ib]),c=reference.transform.TransformPoint(vertices[ic]);
            Vector3 e1=b-a,e2=c-a,n=Vector3.Cross(e1,e2).normalized;
            float score=Vector3.Dot(n,-direction);
            if(score<best) continue;
            float aa=Vector3.Dot(e1,e1),ab=Vector3.Dot(e1,e2),bb=Vector3.Dot(e2,e2),det=aa*bb-ab*ab;
            if(det<.000001f) continue;
            Vector3 dual1=(bb*e1-ab*e2)/det,dual2=(aa*e2-ab*e1)/det;
            Vector2 u1=uv[ib]-uv[ia],u2=uv[ic]-uv[ia];
            Vector3 u=u1.x*dual1+u2.x*dual2,v=u1.y*dual1+u2.y*dual2;
            rowU=new Vector4(u.x,u.y,u.z,uv[ia].x-Vector3.Dot(u,a));
            rowV=new Vector4(v.x,v.y,v.z,uv[ia].y-Vector3.Dot(v,a));
            plane=new Vector4(n.x,n.y,n.z,Vector3.Dot(n,a));
            best=score;found=true;
        }
        return found;
    }
    public void Restore()
    {
        if(active && backing) backing.sharedMaterials=backingOriginals;
        active=false;
    }
    public void Dispose()
    {
        Restore();
        if(backingProjected!=null) foreach(var material in backingProjected) if(material) Object.Destroy(material);
        if(obstacleProjected!=null) foreach(var group in obstacleProjected) foreach(var material in group) if(material) Object.Destroy(material);
    }
}
