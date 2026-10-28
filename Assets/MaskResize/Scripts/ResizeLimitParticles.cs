using UnityEngine;
using UnityEngine.Rendering;

namespace MaskResize
{
    [DisallowMultipleComponent]
    public sealed class ResizeLimitParticles : MonoBehaviour
    {
        [Header("Мягкие границы выбранного объекта")]
        [SerializeField, Range(8,48)] int particlesPerFace=32;
        [SerializeField, Range(.01f,.06f)] float particleSize=.038f;
        [SerializeField, Range(.1f,1)] float repeatDelay=.28f;
        [SerializeField] Color widthColor=new Color(1,.16f,.1f);
        [SerializeField] Color heightColor=new Color(.15f,1,.32f);
        [SerializeField] Color lengthColor=new Color(.18f,.5f,1);
        static Material sharedMaterial;
        ParticleSystem effect;
        readonly ParticleSystem.Particle[] dots=new ParticleSystem.Particle[1280];
        ResizableMaskObject owner;
        int focusAxis,movingAxis,pulseAxis,selectedEnd,dotCount;
        bool focused;
        float movingUntil=-1,pulseUntil=-1,lastPulse=-100;
        float focusFade,movingFade,pulseFade;
        bool returnWarning;
        float warningProgress,warningFade;
        public bool ReturnWarningActive => returnWarning;
        public void SetReturnWarning(ResizableMaskObject target,bool active,float progress)
        {
            if(!active){returnWarning=false;return;}
            owner=target;returnWarning=true;warningProgress=Mathf.Clamp01(progress);
        }
        public ParticleSystem Effect=>effect;
        public int BurstCount { get; private set; }
        public void SetFocus(ResizableMaskObject target,int axis,bool visible,int end=0)
        {
            // Hide requests carry no new selection. Keep the last geometry while fading it out.
            if(!visible){focused=false;return;}
            if(owner!=target) focusFade=0;
            owner=target;focusAxis=axis;focused=true;selectedEnd=end;
        }
        public void Moving(ResizableMaskObject target,int axis)
        {owner=target;movingAxis=axis;movingUntil=Time.unscaledTime+.12f;}
        public void Pulse(ResizableMaskObject target,int axis)
        {
            if(!Application.isPlaying || !target || axis<0 || axis>2) return;
            if(Time.unscaledTime-lastPulse<repeatDelay) return;
            owner=target;pulseAxis=axis;lastPulse=Time.unscaledTime;pulseUntil=lastPulse+.4f;BurstCount++;
        }
        void LateUpdate()=>UpdateFeedback(Time.unscaledDeltaTime);
        void UpdateFeedback(float deltaTime)
        {
            if(!Application.isPlaying || !isActiveAndEnabled || !owner || !owner.Solid)return;
            float dt=Mathf.Min(deltaTime,.05f),now=Time.unscaledTime;
            focusFade=Mathf.MoveTowards(focusFade,focused?1:0,dt*(focused?2.8f:2f));
            movingFade=Mathf.MoveTowards(movingFade,now<movingUntil?1:0,dt*4);
            pulseFade=Mathf.MoveTowards(pulseFade,now<pulseUntil?1:0,dt*4);
            warningFade=Mathf.MoveTowards(warningFade,returnWarning?1:0,dt*4);
            if(focusFade+movingFade+pulseFade+warningFade<.001f){if(effect)effect.Clear();return;}
            EnsureEffect();if(!effect)return;
            dotCount=0;
            if(focusFade>.001f)
            {
                DottedOutline(focusAxis,selectedEnd,.48f*Smooth(focusFade));
                Edges(focusAxis,true,particlesPerFace,.40f*Smooth(focusFade),particleSize*.75f);
            }
            if(movingFade>.001f) Edges(movingAxis,false,8,.32f*Smooth(movingFade),particleSize*.6f);
            if(pulseFade>.001f) Edges(pulseAxis,false,particlesPerFace,.70f*Smooth(pulseFade),particleSize*1.05f);
            if(warningFade>.001f)
            {
                // Two gentle breaths on the real platform, never on its maximum-size boundary.
                float breath=.25f+.75f*(.5f-.5f*Mathf.Cos(warningProgress*Mathf.PI*4));
                DottedOutline(focusAxis,0,.55f*Smooth(warningFade)*breath);
            }
            // Persistent points, updated in-place each frame: no respawn flicker or detached trails.
            effect.SetParticles(dots,dotCount);
        }
        void DottedOutline(int axis,int end,float alpha)
        {
            Vector3 center=owner.Solid.localPosition,half=owner.GeometrySize*.5f;
            if(end!=0 && !(owner.IsThinPlatform && axis==1))
            {
                owner.SidePreviewBounds(axis,end,out center,out half);
                if(owner.IsThinPlatform){center.y=owner.Solid.localPosition.y;half.y=owner.GeometrySize.y*.5f;}
            }
            Color color=axis==0?widthColor:axis==1?heightColor:lengthColor;
            // Twelve actual edges, sampled without changing the box or extending onto adjacent floors.
            for(int direction=0;direction<3;direction++)
            {
                int u=(direction+1)%3,v=(direction+2)%3;
                int count=Mathf.Clamp(Mathf.CeilToInt(half[direction]*2/.18f),3,40);
                for(int a=-1;a<=1;a+=2)for(int b=-1;b<=1;b+=2)
                for(int i=0;i<count;i++)
                {
                    Vector3 p=center;
                    p[direction]+=Mathf.Lerp(-half[direction],half[direction],(i+.5f)/count);
                    p[u]+=a*half[u];p[v]+=b*half[v];
                    Dot(p,color,alpha*.18f,.025f);
                    Dot(p,color,alpha,.009f);
                }
            }
        }
        void Edges(int axis,bool limit,int count,float alpha,float dotSize)
        {
            Vector3 size=owner.GeometrySize,center=owner.Solid.localPosition;
            Color color=axis==0?widthColor:axis==1?heightColor:lengthColor;
            int u=(axis+1)%3,v=(axis+2)%3;
            for(int side=-1;side<=1;side+=2)
            {
                if(limit && selectedEnd!=0 && side!=selectedEnd && !(owner.IsThinPlatform && axis==1))continue;
                if(!limit && Mathf.Abs(side*.5f-owner.AnchorNormalized[axis])<.001f)continue;
                for(int i=0;i<count;i++)
                {
                    float t=(i+.5f)/count*(2*(size[u]+size[v]));
                    Vector3 p=center;
                    p[axis]=limit?owner.ResizeLimitFace(axis,side,selectedEnd):center[axis]+side*size[axis]*.5f;
                    if(t<size[u]){p[u]+=t-size[u]*.5f;p[v]-=size[v]*.5f;}
                    else if((t-=size[u])<size[v]){p[u]+=size[u]*.5f;p[v]+=t-size[v]*.5f;}
                    else if((t-=size[v])<size[u]){p[u]+=size[u]*.5f-t;p[v]+=size[v]*.5f;}
                    else{t-=size[u];p[u]-=size[u]*.5f;p[v]+=size[v]*.5f-t;}
                    // Wide low-opacity halo plus a small readable centre, without HDR flashes.
                    Dot(p,color,alpha*.24f,dotSize*4.2f);
                    Dot(p,Color.Lerp(color,Color.white,.04f),alpha,dotSize);
                }
            }
        }
        static float Smooth(float value)=>value*value*(3-2*value);
        void Dot(Vector3 position,Color color,float alpha,float size)
        {
            if(dotCount>=dots.Length)return;
            color.a=alpha;
            dots[dotCount++]=new ParticleSystem.Particle {position=position,velocity=Vector3.zero,
                startColor=color,startSize=size,startLifetime=1,remainingLifetime=1};
        }
        void EnsureEffect()
        {
            if(effect)return;
            if(!sharedMaterial)sharedMaterial=Resources.Load<Material>("ResizeFeedback/LimitDust");
            if(!sharedMaterial)return;
            var go=new GameObject("Границы этой платформы — мягкое свечение");go.transform.SetParent(transform,false);
            effect=go.AddComponent<ParticleSystem>();effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=effect.main;main.playOnAwake=false;main.loop=true;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.maxParticles=dots.Length;main.startSpeed=0;
            main.useUnscaledTime=true;
            var emission=effect.emission;emission.enabled=false;
            var shape=effect.shape;shape.enabled=false;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sharedMaterial;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            effect.Play();
        }
        void OnDisable()
        {
            if(effect)effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            focused=false;movingUntil=pulseUntil=-1;focusFade=movingFade=pulseFade=0;
            returnWarning=false;warningFade=warningProgress=0;
        }
    }
}
