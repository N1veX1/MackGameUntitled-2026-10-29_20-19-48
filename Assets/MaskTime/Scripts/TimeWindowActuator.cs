using UnityEngine;
namespace MaskTime
{
    [RequireComponent(typeof(TimeWindowState))]
    public sealed class TimeWindowActuator : MonoBehaviour
    {
        [SerializeField] Transform movingPart;
        [SerializeField] Vector3 closedPosition,openPosition,closedEuler,openEuler;
        [SerializeField,Min(.1f)] float moveSeconds=1.2f;
        [Tooltip("Для раскладного моста: опора включается только после полного раскрытия.")]
        [SerializeField] bool supportOnlyWhenOpen;
        [SerializeField] Collider[] supportColliders;
        [Tooltip("Для дверей: не закрываться, пока игрок находится в этом проёме.")]
        [SerializeField] BoxCollider closingSafety;
        TimeWindowState window;TimeMaskPlayer player;
        float openness;
        public float Openness=>openness;
        public void Configure(Transform part,Vector3 from,Vector3 to,Vector3 fromAngles,Vector3 toAngles,bool bridge=false,BoxCollider safety=null)
        {
            movingPart=part;closedPosition=from;openPosition=to;closedEuler=fromAngles;openEuler=toAngles;
            supportOnlyWhenOpen=bridge;closingSafety=safety;
            supportColliders=part.GetComponentsInChildren<Collider>();ApplyPose();
        }
        void Awake(){window=GetComponent<TimeWindowState>();player=FindAnyObjectByType<TimeMaskPlayer>();ApplyPose();}
        void Update()=>Advance(Time.deltaTime);
        public void Advance(float dt)
        {
            if(!window)window=GetComponent<TimeWindowState>();
            bool open=window.IsActive;
            if(!open&&closingSafety&&player&&player.Controller&&closingSafety.bounds.Intersects(player.Controller.bounds))return;
            openness=Mathf.MoveTowards(openness,open?1:0,Mathf.Min(dt,.05f)/Mathf.Max(.1f,moveSeconds));ApplyPose();
        }
        void ApplyPose()
        {
            if(!movingPart)return;
            float t=Mathf.SmoothStep(0,1,openness);
            movingPart.localPosition=Vector3.Lerp(closedPosition,openPosition,t);
            movingPart.localRotation=Quaternion.Slerp(Quaternion.Euler(closedEuler),Quaternion.Euler(openEuler),t);
            if(supportOnlyWhenOpen&&supportColliders!=null)foreach(var c in supportColliders)if(c)c.enabled=openness>=.9999f;
        }
    }
}
