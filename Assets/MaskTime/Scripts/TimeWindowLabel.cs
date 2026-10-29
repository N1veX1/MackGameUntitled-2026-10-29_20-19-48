using UnityEngine;
namespace MaskTime
{
    public sealed class TimeWindowLabel : MonoBehaviour
    {
        [SerializeField] TimeWindowState window;
        [SerializeField] TextMesh label;
        [SerializeField] string activeText="ДОСТУП ОТКРЫТ",inactiveText="ОЖИДАНИЕ ВРЕМЕНИ";
        string previous;
        public void Configure(TimeWindowState source,TextMesh text,string active="ДОСТУП ОТКРЫТ",string inactive="ОЖИДАНИЕ ВРЕМЕНИ")
        {window=source;label=text;activeText=active;inactiveText=inactive;Refresh();}
        void LateUpdate()=>Refresh();
        void Refresh()
        {
            if(!window||!label)return;
            bool active=Application.isPlaying&&window.IsActive;
            string value=window.Schedule+"\n"+(active?activeText:inactiveText);
            if(value==previous)return;previous=value;label.text=value;label.color=active?new Color(.25f,1,.78f):new Color(1,.72f,.35f);
        }
    }
}
