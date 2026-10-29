using System;
using UnityEngine;
namespace MaskTime
{
    public enum ClockUnit { Hours, Minutes, Seconds }
    [DefaultExecutionOrder(-1000)]
    public sealed class TimeMaskClock : MonoBehaviour
    {
        [SerializeField] bool advanceEditedTime;
        double editedSeconds,realSeconds;
        public bool MaskOn { get; private set; }
        public ClockUnit SelectedUnit { get; private set; }
        public double SecondsOfDay => MaskOn?editedSeconds:realSeconds;
        public static double Wrap(double value)=>((value%86400)+86400)%86400;
        public static string Format(double value)
        {int s=(int)Wrap(value);return $"{s/3600:00}:{s/60%60:00}:{s%60:00}";}
        void Awake(){SyncRealTime();editedSeconds=realSeconds;}
        void Update(){SyncRealTime();if(MaskOn&&advanceEditedTime)editedSeconds=Wrap(editedSeconds+Time.deltaTime);}
        void SyncRealTime()=>realSeconds=DateTime.Now.TimeOfDay.TotalSeconds;
        public void SetMask(bool on)
        {
            if(MaskOn==on)return;
            SyncRealTime();if(on)editedSeconds=realSeconds;
            MaskOn=on;
        }
        public void SelectUnit(ClockUnit unit)=>SelectedUnit=unit;
        public bool Step(int ticks)
        {
            if(!MaskOn)return false;
            editedSeconds=Wrap(Math.Floor(editedSeconds)+ticks*(SelectedUnit==ClockUnit.Hours?3600:SelectedUnit==ClockUnit.Minutes?60:1));return true;
        }
        public void SetEditedTime(int hours,int minutes,int seconds){if(MaskOn)editedSeconds=Wrap(hours*3600d+minutes*60+seconds);}
        public void ResetToRealTime(){SyncRealTime();editedSeconds=realSeconds;}
    }
}
