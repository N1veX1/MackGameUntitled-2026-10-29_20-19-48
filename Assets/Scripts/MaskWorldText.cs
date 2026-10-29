using UnityEngine;

/// <summary>World text respects walls and follows the dynamic Cyrillic font atlas.</summary>
[ExecuteAlways, RequireComponent(typeof(TextMesh)), DisallowMultipleComponent]
public sealed class MaskWorldText : MonoBehaviour
{
    [SerializeField] Shader depthTestedShader;
    TextMesh text;
    MeshRenderer surface;
    Material instance, original;

    public void Configure(Shader shader) { depthTestedShader=shader; Rebuild(); }
    void OnEnable() { Font.textureRebuilt+=FontChanged; Rebuild(); }
    void FontChanged(Font font) { if(text && text.font==font) UpdateAtlas(); }
    void Rebuild()
    {
        if(!depthTestedShader) return;
        text=GetComponent<TextMesh>();surface=GetComponent<MeshRenderer>();
        if(!text.font || !surface) return;
        if(!instance)
        {
            original=surface.sharedMaterial;
            instance=new Material(depthTestedShader) { name="Depth tested instruction text", hideFlags=HideFlags.HideAndDontSave };
        }
        instance.shader=depthTestedShader;
        surface.sharedMaterial=instance;UpdateAtlas();
    }
    void UpdateAtlas() { if(instance && text && text.font) instance.mainTexture=text.font.material.mainTexture; }
    void OnDisable()
    {
        Font.textureRebuilt-=FontChanged;
        if(surface && surface.sharedMaterial==instance) surface.sharedMaterial=original ? original : (text && text.font ? text.font.material : null);
        if(instance) { if(Application.isPlaying) Destroy(instance); else DestroyImmediate(instance); }
        instance=null;
    }
}

