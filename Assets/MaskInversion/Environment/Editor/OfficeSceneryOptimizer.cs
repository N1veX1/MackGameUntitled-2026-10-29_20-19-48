using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Object=UnityEngine.Object;

/// <summary>Combines static, opaque scenery per material and eight-metre section.</summary>
public static class OfficeSceneryOptimizer
{
 const string MeshPath="Assets/MaskInversion/Environment/CombinedScenery.asset";
 [MenuItem("Tools/Mask Inversion/Environment/Optimize Static Scenery")]
 public static void Run()
 {
  var root=PrefabUtility.LoadPrefabContents(InversionOfficeBuilder.PrefabPath);
  try{Optimize(root);PrefabUtility.SaveAsPrefabAsset(root,InversionOfficeBuilder.PrefabPath);AssetDatabase.SaveAssets();}
  finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 public static void Optimize(GameObject root)
 {
  int before=root.GetComponentsInChildren<MeshRenderer>(true).Length;
  if(!AssetDatabase.LoadMainAssetAtPath(MeshPath))AssetDatabase.CreateAsset(new Mesh {name="Combined scenery geometry"},MeshPath);
  var w=root.GetComponent<InversionOfficeWorld>();
  foreach(var state in new[]{w.overgrownWorld,w.restoredWorld})
  {
   var atmosphere=state.transform.Find("Atmosphere - windows and exterior");
   Combine(state.transform,atmosphere,state==w.overgrownWorld?"Night":"Office");
   if(atmosphere)Combine(atmosphere,null,state==w.overgrownWorld?"Night exterior":"Office exterior");
  }
  foreach(var light in root.GetComponentsInChildren<Light>(true))if(light.type==LightType.Spot)light.shadowCustomResolution=256;
  AssetDatabase.SaveAssets();
  int after=root.GetComponentsInChildren<MeshRenderer>(true).Length;
  Debug.Log($"SCENERY OPTIMIZATION: mesh renderers {before} -> {after}; separate mask states, 8m culling sections, transparent windows preserved.");
 }
 static void Combine(Transform parent,Transform excluded,string prefix)
 {
  var groups=new Dictionary<string,List<MeshFilter>>();
  foreach(var f in parent.GetComponentsInChildren<MeshFilter>(true))
  {
   var r=f.GetComponent<MeshRenderer>();
   if(!f.sharedMesh||!r||!r.enabled||!f.gameObject.activeSelf||r.sharedMaterials.Length!=1||!r.sharedMaterial||r.sharedMaterial.renderQueue>=2450)continue;
   if(excluded&&f.transform.IsChildOf(excluded))continue;
   if(f.name.StartsWith("Combined ")||f.name=="Glazing panel"||f.name.Contains("sky")||f.name.Contains("Sky"))continue;
   if(f.GetComponent<Collider>()||f.GetComponent<TextMesh>())continue;
   if(r.sharedMaterial.shader.name.Contains("Unlit"))continue;
   string key=prefix+" "+Mathf.FloorToInt(r.bounds.center.z/8)+" "+r.sharedMaterial.name+" "+(int)r.shadowCastingMode;
   if(!groups.ContainsKey(key))groups[key]=new List<MeshFilter>();groups[key].Add(f);
  }
  foreach(var pair in groups)
  {
   if(pair.Value.Count<2)continue;
   var original=pair.Value[0].GetComponent<MeshRenderer>();
   var mesh=new Mesh {name=pair.Key,indexFormat=IndexFormat.UInt32};
   mesh.CombineMeshes(pair.Value.Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=parent.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
   mesh.RecalculateBounds();
   var saved=AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>().FirstOrDefault(m=>m.name==mesh.name);
   if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;EditorUtility.SetDirty(mesh);}else AssetDatabase.AddObjectToAsset(mesh,MeshPath);
   var g=new GameObject("Combined "+pair.Key);g.transform.SetParent(parent,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
   var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=original.sharedMaterial;renderer.shadowCastingMode=original.shadowCastingMode;renderer.receiveShadows=original.receiveShadows;
   foreach(var f in pair.Value)
   {
    if(f.transform.childCount==0)Object.DestroyImmediate(f.gameObject);
    else{Object.DestroyImmediate(f.GetComponent<MeshRenderer>());Object.DestroyImmediate(f);}
   }
  }
  // Remove empty geometry-only grouping transforms, keeping named editable atmosphere groups.
  foreach(var t in parent.GetComponentsInChildren<Transform>(true).Reverse())
   if(t&&t!=parent&&t!=excluded&&t.childCount==0&&t.GetComponents<Component>().Length==1)Object.DestroyImmediate(t.gameObject);
 }
}
