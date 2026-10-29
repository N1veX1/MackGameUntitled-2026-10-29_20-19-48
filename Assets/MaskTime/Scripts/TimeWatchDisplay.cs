using UnityEngine;
namespace MaskTime
{
    public sealed class TimeWatchDisplay : MonoBehaviour
    {
        [SerializeField] TimeMaskClock clock;
        [SerializeField] TextMesh digits,mode,units;
        string previous;bool initialized,lastMask;ClockUnit lastUnit;
        public void Configure(TimeMaskClock source,TextMesh display,TextMesh status,TextMesh labels){clock=source;digits=display;mode=status;units=labels;}
        void Awake(){if(!clock)clock=FindAnyObjectByType<TimeMaskClock>();}
        void LateUpdate()=>Refresh();
        public void Refresh()
        {
            if(!clock||!digits)return;
            string time=TimeMaskClock.Format(clock.SecondsOfDay);
            if(initialized&&time==previous&&clock.MaskOn==lastMask&&clock.SelectedUnit==lastUnit)return;
            initialized=true;previous=time;lastMask=clock.MaskOn;lastUnit=clock.SelectedUnit;
            string[] parts=time.Split(':');
            if(lastMask)parts[(int)lastUnit]="<color=#79FFE3>"+parts[(int)lastUnit]+"</color>";
            digits.text=string.Join(":",parts);
            if(mode){mode.text=lastMask?"TIME / CONTROL":"LOCAL / LIVE";mode.color=lastMask?new Color(.45f,1,.84f):new Color(.8f,.86f,.88f);}
            if(units)units.text=lastMask?"1 H    2 M    3 S":"24 H / REAL TIME";
        }
    }
}
