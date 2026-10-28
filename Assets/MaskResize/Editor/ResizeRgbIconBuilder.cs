using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MaskResize.Editor
{
    public static class ResizeRgbIconBuilder
    {
        const string ResourcesPath = "Assets/MaskResize/Resources/ResizeIcons";
        const string Kit = "Assets/Логика/4 уровень/Знаки размеров";
        static readonly string[] Names = { "", "Ширина", "Высота", "Ширина + высота", "Длина", "Ширина + длина", "Длина + высота", "Все три размера" };
        static readonly Vector2[] Polygon = {
            new Vector2(-.43f,0),new Vector2(-.29f,.10f),new Vector2(-.29f,.04f),
            new Vector2(.29f,.04f),new Vector2(.29f,.10f),new Vector2(.43f,0),
            new Vector2(.29f,-.10f),new Vector2(.29f,-.04f),new Vector2(-.29f,-.04f),new Vector2(-.29f,-.10f) };
        static readonly Color[] Colors = { new Color(.95f,.025f,.02f), new Color(.01f,.9f,.04f), new Color(.015f,.19f,1) };

        [MenuItem("Tools/Mask Resize/Update RGB icons and tutorial signs")]
        public static void Menu()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }
        public static void Build()
        {
            Directory.CreateDirectory(ResourcesPath); Directory.CreateDirectory(Kit); AssetDatabase.Refresh();
            CreateQuad();
            for (int mask=1; mask<=7; mask++) CreateTextureAndMaterial(mask);
            AssetDatabase.SaveAssets();
            for (int mask=1;mask<=7;mask++)
            {
                string path=Kit+"/"+Names[mask]+".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
                var go=new GameObject(Names[mask]); var sign=go.AddComponent<ResizeIconSign>();
                sign.axes=(ResizeIconAxes)mask; sign.Refresh();
                PrefabUtility.SaveAsPrefabAsset(go,path); Object.DestroyImmediate(go);
            }
            int prefabCount=0;
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Логика/4 уровень"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                if(path.StartsWith(Kit+"/")) continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int changes=Upgrade(root);
                    if(changes>0) { PrefabUtility.SaveAsPrefabAsset(root,path); prefabCount++; }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var scene=EditorSceneManager.OpenScene(ResizeTutorialBuilder.ScenePath);
            foreach(var root in scene.GetRootGameObjects()) Upgrade(root);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("RGB_ICONS_INSTALLED: seven icons, updated prefabs="+prefabCount+", tutorial="+scene.path);
        }
        static int Upgrade(GameObject root)
        {
            int changes=0;
            foreach(var marker in root.GetComponentsInChildren<ResizeObjectMarker>(true))
            {
                marker.RefreshIcons(); marker.SendMessage("LateUpdate"); EditorUtility.SetDirty(marker); changes++;
                foreach(var renderer in marker.GetComponentsInChildren<MeshRenderer>(true))
                { EditorUtility.SetDirty(renderer); PrefabUtility.RecordPrefabInstancePropertyModifications(renderer); }
                foreach(var mesh in marker.GetComponentsInChildren<MeshFilter>(true))
                { EditorUtility.SetDirty(mesh); PrefabUtility.RecordPrefabInstancePropertyModifications(mesh); }
            }
            foreach(var text in root.GetComponentsInChildren<TextMesh>(true).ToArray())
            {
                var match=Regex.Match(text.text??"",@"^(\d{2})\s*[/—]");
                if(!match.Success) continue;
                int trial=int.Parse(match.Groups[1].Value);
                int[] axes={0,4,1,2,5,3,7,5,3,7,7,5,7};
                if(trial<1||trial>=axes.Length) continue;
                var board=text.transform.parent;
                if(!board.Find("Board") || board.Find("RGB — подсказка размеров")) continue;
                AddHint(board,(ResizeIconAxes)axes[trial]); changes++;
            }
            return changes;
        }
        static void AddHint(Transform board,ResizeIconAxes axes)
        {
            var group=new GameObject("RGB — подсказка размеров").transform; group.SetParent(board,false);
            group.localPosition=new Vector3(0,1.7f,0);
            var panel=GameObject.CreatePrimitive(PrimitiveType.Cube); panel.name="Подложка"; panel.transform.SetParent(group,false);
            panel.transform.localScale=new Vector3(3.9f,1.12f,.045f); Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskResize/Materials/DarkSteel.mat");
            var symbol=new GameObject("Знак доступных размеров").transform; symbol.SetParent(group,false);
            symbol.localPosition=new Vector3(-1.28f,0,-.03f); symbol.localRotation=Quaternion.Euler(0,180,0); symbol.localScale=Vector3.one;
            var sign=symbol.gameObject.AddComponent<ResizeIconSign>(); sign.axes=axes; sign.Refresh();
            var label=new GameObject("Размеры и кнопки").transform; label.SetParent(group,false); label.localPosition=new Vector3(-.55f,.38f,-.035f);
            var text=label.gameObject.AddComponent<TextMesh>(); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=64; text.characterSize=.032f; text.anchor=TextAnchor.UpperLeft; text.color=new Color(.85f,.88f,.9f);
            string line="";
            if((axes&ResizeIconAxes.Length)!=0) line+="1 — ДЛИНА\n";
            if((axes&ResizeIconAxes.Height)!=0) line+="2 — ВЫСОТА\n";
            if((axes&ResizeIconAxes.Width)!=0) line+="3 — ШИРИНА\n";
            text.text=line+"Колесо — изменить";
            label.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/MaskResize/Materials/InstructionText.mat");
        }
        static void CreateQuad()
        {
            string path=ResourcesPath+"/SignQuad.asset";
            if(AssetDatabase.LoadAssetAtPath<Mesh>(path)) return;
            var mesh=new Mesh{name="Resize sign quad (+Z front)"};
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
            // Front is viewed toward -Z: local -X is screen right.
            mesh.uv=new[]{new Vector2(1,0),new Vector2(0,0),new Vector2(0,1),new Vector2(1,1)};
            mesh.triangles=new[]{0,1,2,0,2,3}; mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,path);
        }
        static void CreateTextureAndMaterial(int mask)
        {
            const int size=512;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                var p=new Vector2((x+.5f)/size-.5f,(y+.5f)/size-.5f);
                float closest=1, total=0; Color mix=Color.black;
                for(int axis=0;axis<3;axis++)
                {
                    if((mask&(1<<axis))==0) continue;
                    Vector2 q=axis==0?p:axis==1?new Vector2(p.y,-p.x):new Vector2((p.x+p.y)*.70710678f,(p.y-p.x)*.70710678f);
                    float distance=SignedDistance(q); closest=Mathf.Min(closest,distance);
                    // Smooth color mixing extends slightly beyond each shaft into the crossing.
                    float crossing=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.16f,.25f,p.magnitude));
                    float weight=distance<=0?1:Mathf.Exp(-distance*distance/(2*.027f*.027f))*crossing;
                    mix+=Colors[axis]*weight; total+=weight;
                }
                float coverage=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.0015f,.0015f,closest));
                float glow=.14f*Mathf.Exp(-Mathf.Max(0,closest)*Mathf.Max(0,closest)/(2*.012f*.012f));
                float alpha=Mathf.Max(coverage,glow);
                mix.r=Mathf.Min(1,mix.r); mix.g=Mathf.Min(1,mix.g); mix.b=Mathf.Min(1,mix.b);
                if(coverage<.01f && total>.001f) mix/=Mathf.Max(1,total);
                mix.a=alpha<.002f?0:alpha; pixels[y*size+x]=mix;
            }
            texture.SetPixels32(pixels); texture.Apply();
            string texturePath=ResourcesPath+"/Icon"+mask+".png"; File.WriteAllBytes(texturePath,texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType=TextureImporterType.Default; importer.alphaIsTransparency=true; importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Clamp; importer.filterMode=FilterMode.Trilinear; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            string materialPath=ResourcesPath+"/Icon"+mask+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool create=!material; if(create) material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.name=Names[mask]; material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)); material.SetColor("_BaseColor",Color.white);
            material.SetFloat("_Surface",1); material.SetFloat("_Blend",0); material.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha); material.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite",0); material.SetInt("_Cull",(int)CullMode.Off); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue=(int)RenderQueue.Transparent; material.SetOverrideTag("RenderType","Transparent");
            if(create) AssetDatabase.CreateAsset(material,materialPath); else EditorUtility.SetDirty(material);
        }
        static float SignedDistance(Vector2 p)
        {
            float squared=float.MaxValue; bool inside=false;
            for(int i=0,j=Polygon.Length-1;i<Polygon.Length;j=i++)
            {
                Vector2 a=Polygon[j],b=Polygon[i],edge=b-a;
                Vector2 delta=p-a-edge*Mathf.Clamp01(Vector2.Dot(p-a,edge)/edge.sqrMagnitude); squared=Mathf.Min(squared,delta.sqrMagnitude);
                if((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return Mathf.Sqrt(squared)*(inside?-1:1);
        }
    }
}
