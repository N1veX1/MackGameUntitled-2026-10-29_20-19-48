using System;
using UnityEngine;
namespace MaskTime
{
    [Serializable]
    public struct ClockTime
    {
        [Range(0,23)] public int hours;
        [Range(0,59)] public int minutes;
        [Range(0,59)] public int seconds;
        public double Value=>hours*3600d+minutes*60+seconds;
        public ClockTime(int h,int m,int s=0){hours=h;minutes=m;seconds=s;}
        public override string ToString()=>TimeMaskClock.Format(Value);
    }
    [DisallowMultipleComponent]
    public sealed class TimeWindowState : MonoBehaviour
    {
        [SerializeField] TimeMaskClock clock;
        [Header("Ежедневный интервал: начало включительно, конец не включительно")]
        [SerializeField] ClockTime start=new ClockTime(8,0);
        [SerializeField] ClockTime end=new ClockTime(8,15);
        [Tooltip("Явно включить весь день. Одинаковые начало и конец иначе означают закрытый интервал.")]
        [SerializeField] bool allDay;
        public string Schedule=>$"{start} — {end}";
        public TimeMaskClock Clock=>clock;
        public bool IsActive=>clock&&IsActiveAt(clock.SecondsOfDay);
        public bool IsActiveAt(double value)
        {
            if(allDay)return true;
            double t=TimeMaskClock.Wrap(value),a=start.Value,b=end.Value;
            return a<b?t>=a&&t<b:a>b&&(t>=a||t<b);
        }
        public void Configure(TimeMaskClock source,ClockTime from,ClockTime until)
        {clock=source;start=from;end=until;allDay=false;}
        void Awake(){if(!clock)clock=FindAnyObjectByType<TimeMaskClock>();}
    }
}
