using UnityEngine;
namespace MaskTime
{
    [RequireComponent(typeof(TimeWindowState),typeof(BoxCollider))]
    public sealed class TimeHazard : MonoBehaviour
    {
        [SerializeField,Min(.1f)] float exposureSeconds=.65f;
        [SerializeField] ParticleSystem dust;
        [SerializeField] Light glow;
        [SerializeField] Color dangerColor=new Color(1,.32f,.12f),safeColor=new Color(.15f,.9f,.65f);
        TimeWindowState window;BoxCollider area;TimeMaskPlayer player;float exposure;bool initialized,lastSafe;
        public void Configure(ParticleSystem particles,Light light){dust=particles;glow=light;}
        void Awake(){window=GetComponent<TimeWindowState>();area=GetComponent<BoxCollider>();area.isTrigger=true;player=FindAnyObjectByType<TimeMaskPlayer>();}
        void LateUpdate()=>Evaluate(Time.deltaTime);
        public void Evaluate(float dt)
        {
            bool safe=window.IsActive;
            if(!initialized||lastSafe!=safe)
            {
                initialized=true;lastSafe=safe;
                if(dust){var main=dust.main;main.startColor=safe?safeColor:dangerColor;var emission=dust.emission;emission.rateOverTime=safe?5:38;dust.Clear();}
                if(glow){glow.color=safe?safeColor:dangerColor;glow.intensity=safe?1.2f:3;}
            }
            if(!player||!player.Controller)return;
            if(safe||!area.bounds.Intersects(player.Controller.bounds)){exposure=0;return;}
            exposure+=dt;player.Notify("ВРЕМЕННАЯ АНОМАЛИЯ · выставь безопасное время",.2f);
            if(exposure>=exposureSeconds){exposure=0;player.Respawn("Аномалия вернула тебя на безопасную площадку.");}
        }
    }
}
