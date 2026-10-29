using UnityEngine;
using UnityEngine.InputSystem;
namespace MaskTime
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class TimeMaskPlayer : MonoBehaviour
    {
        [SerializeField] TimeMaskClock clock;
        [SerializeField] Camera playerCamera;
        [SerializeField,Min(.1f)] float speed=4.2f;
        [SerializeField] float sensitivity=.11f,lookSmoothing=.045f,acceleration=28,fallHeight=-8;
        CharacterController controller;Vector3 velocity,checkpoint;Quaternion checkpointRotation;
        float yaw,pitch,smoothYaw,smoothPitch,verticalSpeed;
        bool captured,finished;string notice;float noticeUntil;GUIStyle small,title;
        public CharacterController Controller=>controller;
        public TimeMaskClock Clock=>clock;
        public Camera PlayerCamera=>playerCamera;
        public bool Finished=>finished;
        public void Configure(TimeMaskClock source,Camera view){clock=source;playerCamera=view;}
        void Awake()
        {
            controller=GetComponent<CharacterController>();controller.height=1.8f;controller.center=Vector3.up*.9f;controller.radius=.28f;controller.skinWidth=.02f;controller.stepOffset=.24f;
            if(!clock)clock=FindAnyObjectByType<TimeMaskClock>();if(!playerCamera)playerCamera=GetComponentInChildren<Camera>();
            checkpoint=transform.position;checkpointRotation=transform.rotation;yaw=smoothYaw=transform.eulerAngles.y;Capture(true);
        }
        void Update()
        {
            if(transform.position.y<fallHeight)Respawn("Ты упал. Возвращение к последней площадке.");
            var k=Keyboard.current;var m=Mouse.current;
            if(k!=null&&k.escapeKey.wasPressedThisFrame)Capture(false);
            if(!captured&&m!=null&&m.leftButton.wasPressedThisFrame){Capture(true);return;}
            if(captured&&k!=null)
            {
                if(k.mKey.wasPressedThisFrame)clock.SetMask(!clock.MaskOn);
                if(k.digit1Key.wasPressedThisFrame)clock.SelectUnit(ClockUnit.Hours);
                if(k.digit2Key.wasPressedThisFrame)clock.SelectUnit(ClockUnit.Minutes);
                if(k.digit3Key.wasPressedThisFrame)clock.SelectUnit(ClockUnit.Seconds);
                if(k.rKey.wasPressedThisFrame&&clock.MaskOn)clock.ResetToRealTime();
                if(m!=null){var d=m.delta.ReadValue()*sensitivity;yaw+=d.x;pitch=Mathf.Clamp(pitch-d.y,-85,85);float scroll=m.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f)clock.Step(scroll>0?1:-1);}
            }
            float blend=1-Mathf.Exp(-Time.unscaledDeltaTime/Mathf.Max(.001f,lookSmoothing));
            smoothYaw=Mathf.LerpAngle(smoothYaw,yaw,blend);smoothPitch=Mathf.Lerp(smoothPitch,pitch,blend);
            transform.rotation=Quaternion.Euler(0,smoothYaw,0);if(playerCamera)playerCamera.transform.localRotation=Quaternion.Euler(smoothPitch,0,0);
            Vector2 input=captured&&k!=null?new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0)):Vector2.zero;
            input=Vector2.ClampMagnitude(input,1);velocity=Vector3.MoveTowards(velocity,(transform.right*input.x+transform.forward*input.y)*speed,acceleration*Time.deltaTime);
            if(controller.isGrounded&&verticalSpeed<0)verticalSpeed=-2;verticalSpeed-=22*Time.deltaTime;
            controller.Move((velocity+Vector3.up*verticalSpeed)*Time.deltaTime);
        }
        public void SetCheckpoint(Transform point){if(point){checkpoint=point.position;checkpointRotation=point.rotation;}}
        public void Teleport(Vector3 feet,Quaternion rotation)
        {
            if(!controller)controller=GetComponent<CharacterController>();controller.enabled=false;transform.SetPositionAndRotation(feet,rotation);controller.enabled=true;
            velocity=Vector3.zero;verticalSpeed=0;pitch=smoothPitch=0;yaw=smoothYaw=rotation.eulerAngles.y;
            if(playerCamera)playerCamera.transform.localRotation=Quaternion.identity;Physics.SyncTransforms();
        }
        public void Respawn(string message){clock.SetMask(false);Teleport(checkpoint,checkpointRotation);Notify(message,3);}
        public void Complete(){finished=true;Notify("TUTORIAL 5 ПРОЙДЕН · часы управляют доступом",5);}
        public void Notify(string value,float seconds){notice=value;noticeUntil=Time.unscaledTime+seconds;}
        void Capture(bool value){captured=value;Cursor.lockState=value?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!value;}
        void OnDisable()=>Capture(false);
        void OnGUI()
        {
            if(Event.current.type!=EventType.Repaint)return;
            small??=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};title??=new GUIStyle(small){fontSize=23,fontStyle=FontStyle.Bold};
            var matrix=GUI.matrix;var color=GUI.color;float scale=Mathf.Clamp(Screen.height/900f,.65f,1.8f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);float w=Screen.width/scale,h=Screen.height/scale;
            GUI.color=new Color(.025f,.045f,.06f,.88f);GUI.DrawTexture(new Rect(24,24,410,98),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(40,32,390,32),"05 / ВРЕМЯ",title);GUI.Label(new Rect(40,67,380,48),clock.MaskOn?"МАСКА НАДЕТА · время под контролем":"РЕАЛЬНОЕ ВРЕМЯ · M — надеть маску",small);
            GUI.color=clock.MaskOn?new Color(.4f,1,.85f):Color.white;GUI.DrawTexture(new Rect(w/2-2,h/2-2,4,4),Texture2D.whiteTexture);
            GUI.color=Color.white;GUI.Label(new Rect(28,h-64,850,50),"WASD — идти · M — маска · 1/2/3 — часы/минуты/секунды\nКолесо — время · R — текущее время · Esc — курсор",small);
            if(Time.unscaledTime<noticeUntil)GUI.Label(new Rect(w/2-340,h-155,680,60),notice,small);
            if(finished)GUI.Label(new Rect(w/2-220,140,440,40),"ОБУЧЕНИЕ ВРЕМЕНИ ПРОЙДЕНО",title);
            GUI.matrix=matrix;GUI.color=color;
        }
    }
}
