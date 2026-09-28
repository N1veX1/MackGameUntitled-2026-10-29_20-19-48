using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MaskLevelPortal : MonoBehaviour
{
    [SerializeField] string destinationScene = "Level02_Entry";
    [SerializeField] MaskTutorialLevel tutorial;
    Transform player;
    bool loading;
    public bool IsLoading => loading;
    void Start()
    {
        if(!tutorial) tutorial=FindAnyObjectByType<MaskTutorialLevel>();
        var movement=FindAnyObjectByType<MaskDemoPlayer>();
        if(movement) player=movement.transform;
    }
    void Update()
    {
        if(loading || !player || (tutorial && !tutorial.PortalReady)) return;
        var trigger=GetComponent<Collider>();
        if(trigger && trigger.bounds.Contains(player.position))
        {
            if(!Application.CanStreamedLevelBeLoaded(destinationScene)) { Debug.LogError("Portal scene missing: "+destinationScene,this); enabled=false; return; }
            loading=true;
            SceneManager.LoadSceneAsync(destinationScene);
        }
    }
}
