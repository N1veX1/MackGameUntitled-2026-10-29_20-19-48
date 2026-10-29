using UnityEngine;

namespace MaskResize
{
    /// <summary>Dimensions use the object's local coordinates, independent of its world rotation.</summary>
    public enum ResizeDimension { Length = 0, Height = 1, Width = 2 }

    /// <summary>
    /// A resizable box with a fixed anchor. Keep this root and its parents at unit scale.
    /// The direct child Solid is a unit cube with a BoxCollider; scale only that child.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResizableMaskObject : MonoBehaviour
    {
        [SerializeField] Transform solid;
        [Tooltip("Current dimensions: X = width, Y = height, Z = length, in metres.")]
        [SerializeField] Vector3 size = Vector3.one;
        [SerializeField] Vector3 initialSize = Vector3.one;
        [SerializeField] Vector3 minSize = Vector3.one * .15f;
        [SerializeField] Vector3 maxSize = Vector3.one * 10f;
        [Tooltip("Grey out unavailable axes in the HUD for introductory exercises. Later puzzles use the object's signs instead.")]
        [SerializeField] bool showLockedAxesInHud;
        public bool ShowLockedAxesInHud => showLockedAxesInHud;
        public void ConfigureIntroductoryHints(bool enabled) => showLockedAxesInHud=enabled;
        [Tooltip("Fixed point relative to the box centre: -0.5 = negative face, +0.5 = positive face. Root position is this point.")]
        [SerializeField] Vector3 anchorNormalized = new Vector3(0, -.5f, 0);
        [SerializeField] LayerMask blockingLayers = ~0;
        [Tooltip("Tolerance for touching surfaces and existing shallow contact, in metres.")]
        [SerializeField, Range(.001f, .02f)] float contactTolerance = .005f;
        [SerializeField, Range(.02f, .2f)] float standingTolerance = .1f;
        [Header("Smooth resizing")]
        [SerializeField, Min(.02f)] float resizeSmoothSeconds = .12f;
        [SerializeField, Min(.1f)] float maxResizeSpeed = 3f;

        [Header("Возврат без внимания игрока")]
        [SerializeField] bool returnWhenIdle=true;
        [SerializeField, Min(0)] float idleReturnDelay=5;
        [SerializeField, Min(0)] float returnWarningSeconds=1.6f;
        [SerializeField, Min(.05f)] float returnStopSeconds=.3f;
        Vector3 homeSize, returnStartSize, returnStartOffset;
        float idleSeconds, returnProgress, returnTravel, retryReturn;
        float returnBlend,returnBlendVelocity,brakeElapsed,brakeInitialVelocity;
        bool observed, returning,brakingReturn;
        CharacterController returnPlayer;
        public bool IsReturning => returning;
        public void SetObserved(bool value,CharacterController player=null)
        {
            observed=value;
            if(player) returnPlayer=player;
            if(value)
            {
                idleSeconds=0;
                if(limitParticles) limitParticles.SetReturnWarning(this,false,0);
                BeginReturnBrake();
            }
        }
        void RegisterInteraction(CharacterController player)
        {
            idleSeconds=0;returning=false;retryReturn=0;brakingReturn=false;returnBlendVelocity=0;
            if(limitParticles) limitParticles.SetReturnWarning(this,false,0);
            if(player) returnPlayer=player;
        }
        void CaptureHome()
        { homeSize=size;idleSeconds=0;returning=false;observed=false;brakingReturn=false;returnBlendVelocity=0; }
        void BeginReturnBrake()
        {
            if(!returning || brakingReturn)return;
            if(returnBlendVelocity<=.000001f || retryReturn>0){RegisterInteraction(null);return;}
            brakingReturn=true;brakeElapsed=0;brakeInitialVelocity=returnBlendVelocity;
        }
        void AdvanceReturnBrake(float deltaTime)
        {
            float duration=Mathf.Max(.05f,returnStopSeconds);
            float previous=Mathf.Clamp01(brakeElapsed/duration);
            brakeElapsed=Mathf.Min(duration,brakeElapsed+Mathf.Min(deltaTime,.05f));
            float current=brakeElapsed/duration;
            // Integral of a squared falloff: retain the current velocity, then reach zero
            // smoothly in a finite time. Size and offset follow the same return path.
            float distance=brakeInitialVelocity*duration/3f*(Mathf.Pow(1-previous,3)-Mathf.Pow(1-current,3));
            float blend=Mathf.Min(1,returnBlend+distance);
            if(!TryApplySize(Vector3.Lerp(returnStartSize,homeSize,blend),returnPlayer,out _,
                Vector3.Lerp(returnStartOffset,Vector3.zero,blend)))
            {RegisterInteraction(null);return;}
            returnBlend=blend;
            if(brakeElapsed>=duration || blend>=1)RegisterInteraction(null);
        }
        void LateUpdate()=>AdvanceIdleReturn(Time.deltaTime);
        public void AdvanceIdleReturn(float deltaTime)
        {
            if(!Application.isPlaying || !isActiveAndEnabled || !returnWhenIdle || !Finite(deltaTime) || deltaTime<=0) return;
            if(resizeQueued || HasStandingPlayer(returnPlayer) || ResizeMaskPlayer.IsStandingOn(this))
            { RegisterInteraction(null);return; }
            if(observed){idleSeconds=0;BeginReturnBrake();}
            if(brakingReturn){AdvanceReturnBrake(deltaTime);return;}
            if(observed)return;
            if(!returning)
            {
                if((size-homeSize).sqrMagnitude<.00000001f &&
                    (LocalCentre(size)+Vector3.Scale(anchorNormalized,homeSize)).sqrMagnitude<.00000001f) return;
                idleSeconds+=deltaTime;
                float warningDuration=Mathf.Min(returnWarningSeconds,Mathf.Max(0,idleReturnDelay));
                float warningStart=Mathf.Max(0,idleReturnDelay)-warningDuration;
                if(warningDuration>0 && idleSeconds>=warningStart && idleSeconds<idleReturnDelay)
                {
                    EnsureFeedback();
                    limitParticles.SetReturnWarning(this,true,(idleSeconds-warningStart)/warningDuration);
                }
                if(idleSeconds<Mathf.Max(0,idleReturnDelay))return;
                if(limitParticles) limitParticles.SetReturnWarning(this,false,0);
                Vector3 centre=LocalCentre(size);
                CancelResize();sideModes=sideMinimum=fixedPartLength=Vector3.zero;
                resizeOffset=centre+Vector3.Scale(anchorNormalized,size);
                returnStartSize=size;returnStartOffset=resizeOffset;returnProgress=0;returnBlend=returnBlendVelocity=0;
                returnTravel=Mathf.Max(.1f,(homeSize-size).magnitude+2*resizeOffset.magnitude);
                returning=true;
            }
            if(retryReturn>0){retryReturn-=deltaTime;return;}
            float nextProgress=Mathf.MoveTowards(returnProgress,1,Mathf.Min(deltaTime,.05f)*maxResizeSpeed/(1.5f*returnTravel));
            float t=Mathf.SmoothStep(0,1,nextProgress);
            Vector3 nextSize=Vector3.Lerp(returnStartSize,homeSize,t);
            Vector3 nextOffset=Vector3.Lerp(returnStartOffset,Vector3.zero,t);
            if(!TryApplySize(nextSize,returnPlayer,out _,nextOffset))
            { retryReturn=.2f;returnBlendVelocity=0;return; } // Wait for room; never restore through a player or an obstacle.
            returnBlendVelocity=(t-returnBlend)/Mathf.Min(deltaTime,.05f);returnBlend=t;
            returnProgress=nextProgress;
            if(returnProgress>=1){returning=false;idleSeconds=0;}
        }

        internal bool HasStandingPlayer(CharacterController player)
        {
            if(!player || !player.enabled || !player.gameObject.activeInHierarchy || !SolidCollider || !SolidCollider.enabled) return false;
            GetPlayerCapsule(player,out Vector3 centre,out Quaternion rotation,out float radius,out float height);
            Vector3 up=rotation*Vector3.up;
            if(Vector3.Dot(up,transform.up)<.98f || Vector3.Dot(player.velocity,up)>.5f) return false;
            Vector3 feet=transform.InverseTransformPoint(centre-up*(height*.5f));
            Vector3 boxCentre=PhysicalCentre(size), dimensions=PhysicalSize(size);
            float top=boxCentre.y+dimensions.y*.5f;
            if(Mathf.Abs(feet.y-top)>standingTolerance) return false;
            // Include support at an edge, where the capsule overlaps but its centre is outside.
            Vector3 scale=transform.lossyScale;
            float dx=Mathf.Max(0,Mathf.Abs(feet.x-boxCentre.x)-dimensions.x*.5f)*Mathf.Abs(scale.x);
            float dz=Mathf.Max(0,Mathf.Abs(feet.z-boxCentre.z)-dimensions.z*.5f)*Mathf.Abs(scale.z);
            float edgeReach=Mathf.Sqrt(Mathf.Max(0,2*radius*standingTolerance-standingTolerance*standingTolerance));
            return dx*dx+dz*dz<=(edgeReach+contactTolerance)*(edgeReach+contactTolerance);
        }

        [Header("Тонкая платформа")]
        [SerializeField] bool thinPlatform;
        [SerializeField, Min(.05f)] float platformThickness=.5f;
        public bool IsThinPlatform => thinPlatform;
        public Vector3 GeometrySize => PhysicalSize(size);
        Vector3 PhysicalSize(Vector3 dimensions)
        { if(thinPlatform) dimensions.y=Mathf.Max(.05f,platformThickness); return dimensions; }
        Vector3 PhysicalCentre(Vector3 dimensions)
        {
            Vector3 centre=LocalCentre(dimensions);
            if(thinPlatform) centre.y+=(dimensions.y-PhysicalSize(dimensions).y)*.5f;
            return centre;
        }
        public void ConfigureThinPlatform(bool enabled,float thickness=.5f)
        {
            thinPlatform=enabled;platformThickness=Mathf.Max(.05f,thickness);
            if(!ConvertThinPlatformHeight()) ApplyGeometry();
        }

        /// <summary>Upgrade a legacy hovering deck to a real resizable volume, preserving
        /// its initial surface and the reachable top heights. Fixed-height bridges stay thin.</summary>
        public bool ConvertThinPlatformHeight()
        {
            if(!thinPlatform || !CanResize(ResizeDimension.Height)) return false;
            float delta=Mathf.Max(.05f,platformThickness)-initialSize.y;
            transform.position-=transform.up*((.5f-anchorNormalized.y)*delta);
            size.y=Mathf.Max(.05f,size.y+delta);
            initialSize.y=Mathf.Max(.05f,initialSize.y+delta);
            minSize.y=Mathf.Max(.05f,minSize.y+delta);
            maxSize.y=Mathf.Max(minSize.y,maxSize.y+delta);
            thinPlatform=false;
            sideModes=sideMinimum=resizeOffset=fixedPartLength=Vector3.zero;
            NormalizeSettings();CancelResize();ApplyGeometry();CaptureHome();
            return true;
        }

        Vector3 requestedSize, resizeVelocity;
        CharacterController resizingPlayer;
        bool resizeQueued;
        ResizeLimitParticles limitParticles;
        Vector3 sideModes, sideMinimum, resizeOffset, fixedPartLength;
        Vector3 EffectiveAnchor => new Vector3(
            sideModes.x == 0 ? anchorNormalized.x : -.5f*sideModes.x,
            sideModes.y == 0 ? anchorNormalized.y : -.5f*sideModes.y,
            sideModes.z == 0 ? anchorNormalized.z : -.5f*sideModes.z);
        Vector3 LocalCentre(Vector3 dimensions) => resizeOffset-Vector3.Scale(EffectiveAnchor,dimensions);
        // These bounds are rooted in the authored mount, never in the last selected face.
        public Vector3 LimitCentre => CoreCentre;
        public Vector3 LimitSize => maxSize*2-minSize;
        // The total length is still capped by maxSize. Either end can use the remaining span
        // around the fixed minimum core; changing ends never translates that core.
        public float ResizeLimitFace(int axis,int side,int selectedEnd)
        {
            if(thinPlatform && axis==1)
            {
                AxisRange(axis,out float lowHeight,out float highHeight);
                Vector3 dimensions=size;dimensions.y=side<0?lowHeight:highHeight;
                return PhysicalCentre(dimensions).y+PhysicalSize(dimensions).y*.5f;
            }
            float a=selectedEnd==0?anchorNormalized[axis]:-.5f*selectedEnd;
            float offset=LocalCentre(size)[axis]+a*size[axis];
            float high=maxSize[axis];
            float lowEdge=LimitCentre[axis]-LimitSize[axis]*.5f;
            float highEdge=LimitCentre[axis]+LimitSize[axis]*.5f;
            if(.5f+a>.00001f) high=Mathf.Min(high,(offset-lowEdge)/(.5f+a));
            if(.5f-a>.00001f) high=Mathf.Min(high,(highEdge-offset)/(.5f-a));
            high=Mathf.Max(high,size[axis]);
            return offset-a*high+side*high*.5f;
        }
        public Vector3 CoreCentre => -Vector3.Scale(anchorNormalized,minSize);
        public bool CanResize(ResizeDimension dimension)
        {
            int axis=dimension==ResizeDimension.Length?2:dimension==ResizeDimension.Height?1:0;
            return maxSize[axis]-minSize[axis]>.0001f;
        }
        void AxisRange(int axis,out float low,out float high)
        {
            low=minSize[axis]; high=maxSize[axis];
            float a=EffectiveAnchor[axis], offset=resizeOffset[axis];
            float coreLow=CoreCentre[axis]-minSize[axis]*.5f;
            float coreHigh=CoreCentre[axis]+minSize[axis]*.5f;
            float limitLow=LimitCentre[axis]-LimitSize[axis]*.5f;
            float limitHigh=LimitCentre[axis]+LimitSize[axis]*.5f;
            float negative=.5f+a, positive=.5f-a;
            if(negative>.00001f)
            {
                low=Mathf.Max(low,(offset-coreLow)/negative);
                high=Mathf.Min(high,(offset-limitLow)/negative);
            }
            if(positive>.00001f)
            {
                low=Mathf.Max(low,(coreHigh-offset)/positive);
                high=Mathf.Min(high,(limitHigh-offset)/positive);
            }
            if(sideModes[axis]!=0) low=Mathf.Max(low,sideMinimum[axis]);
            // Current geometry is valid; round-off must not force a jump on mode changes.
            low=Mathf.Min(low,size[axis]); high=Mathf.Max(high,size[axis]);
        }
        float MinimumForAxis(int axis) { AxisRange(axis,out float low,out _); return low; }
        float MaximumForAxis(int axis) { AxisRange(axis,out _,out float high); return high; }
        public void ShowResizeGuide(int axis,bool visible,int selectedEnd=0)
        {
            if(!Application.isPlaying) return;
            if(visible) EnsureFeedback();
            if(limitParticles) limitParticles.SetFocus(this,axis,visible,selectedEnd);
        }
        void EnsureFeedback()
        {
            if(!limitParticles) limitParticles=GetComponent<ResizeLimitParticles>();
            if(!limitParticles) limitParticles=gameObject.AddComponent<ResizeLimitParticles>();
        }

        /// <summary>0 = normal resizing; -1/+1 = move only the selected end in local coordinates.</summary>
        public void SetResizeSide(ResizeDimension dimension, int side)
        {
            int axis=dimension==ResizeDimension.Length?2:dimension==ResizeDimension.Height?1:0;
            if(!CanResize(dimension)) return;
            // A rigid thin deck changes elevation as a whole; E still selects its horizontal ends.
            if(thinPlatform && axis==1) side=0;
            side=Mathf.Clamp(side,-1,1);
            if(sideModes[axis]==side) return;
            Vector3 centre=LocalCentre(size);
            float split=HalfSplitCoordinate(axis);
            CancelResize(); // Never execute an old queued movement using a new fixed face.
            sideModes[axis]=side;
            // Keep the same split when moving from one end to the other. Re-halving the
            // stretched box made the guide and aim selection disagree and swallowed one half.
            fixedPartLength[axis]=side==0?size[axis]*.5f:
                size[axis]*.5f+side*(split-centre[axis]);
            sideMinimum[axis]=fixedPartLength[axis]+Mathf.Min(.02f,size[axis]*.25f);
            resizeOffset=centre+Vector3.Scale(EffectiveAnchor,size);
            // Changing mode does not move any geometry or change the global Min/Max Size.
        }
        public float HalfSplitCoordinate(int axis)
        {
            return LocalCentre(size)[axis]-sideModes[axis]*(size[axis]*.5f-fixedPartLength[axis]);
        }
        public void SidePreviewBounds(int axis,int side,out Vector3 centre,out Vector3 half)
        {
            float fixedLength=size[axis]*.5f+side*(HalfSplitCoordinate(axis)-LocalCentre(size)[axis]);
            centre=LocalCentre(size); centre[axis]+=side*fixedLength*.5f;
            half=size*.5f; half[axis]=Mathf.Max(.001f,(size[axis]-fixedLength)*.5f);
        }

        void ShowSizeLimit(int axis)
        {
            if (!Application.isPlaying) return;
            EnsureFeedback();
            limitParticles.Pulse(this, axis);
        }

        BoxCollider solidCollider;
        GameObject probeObject;
        BoxCollider boxProbe;
        BoxCollider previousBoxProbe;
        CapsuleCollider capsuleProbe;
        readonly Collider[] overlaps = new Collider[128];
        readonly RaycastHit[] sweepHits = new RaycastHit[64];

        public Vector3 Size => size;
        public Vector3 RequestedSize => resizeQueued ? requestedSize : size;
        public bool IsResizing => resizeQueued;
        public Vector3 InitialSize => initialSize;
        public Vector3 MinSize => minSize;
        public Vector3 MaxSize => maxSize;
        public Vector3 AnchorNormalized => EffectiveAnchor;
        public Transform Solid => solid;
        public BoxCollider SolidCollider
        {
            get
            {
                if (solid && (!solidCollider || solidCollider.transform != solid))
                    solidCollider = solid.GetComponent<BoxCollider>();
                return solidCollider;
            }
        }

        public void Configure(Transform solid, Vector3 initialSize, Vector3 minSize,
            Vector3 maxSize, Vector3 anchorNormalized)
        {
            this.solid = solid;
            solidCollider = solid ? solid.GetComponent<BoxCollider>() : null;
            this.minSize = minSize;
            this.maxSize = maxSize;
            this.initialSize = initialSize;
            this.anchorNormalized = anchorNormalized;
            sideModes=sideMinimum=resizeOffset=Vector3.zero;
            NormalizeSettings();
            size = this.initialSize;
            ApplyGeometry();
            CancelResize();
            CaptureHome();
        }

        void Awake()
        {
            NormalizeSettings();
            ConvertThinPlatformHeight();
            ApplyGeometry();
            CancelResize();
            CaptureHome();
        }

        void OnDisable() { CancelResize();observed=false;RegisterInteraction(null); }

        void OnValidate()
        {
            NormalizeSettings();
            // Serialized Size is authoritative: recompilation/scene reload never restores initialSize.
            if (!Application.isPlaying) ApplyGeometry();
        }

        void NormalizeSettings()
        {
            for (int i = 0; i < 3; i++)
            {
                minSize[i] = Finite(minSize[i]) ? Mathf.Max(.02f, minSize[i]) : .02f;
                maxSize[i] = Finite(maxSize[i]) ? Mathf.Max(minSize[i], maxSize[i]) : minSize[i];
                initialSize[i] = Mathf.Clamp(Finite(initialSize[i]) ? initialSize[i] : minSize[i], minSize[i], maxSize[i]);
                size[i] = Mathf.Clamp(Finite(size[i]) ? size[i] : initialSize[i], minSize[i], maxSize[i]);
                anchorNormalized[i] = Mathf.Clamp(Finite(anchorNormalized[i]) ? anchorNormalized[i] : 0, -.5f, .5f);
            }
        }

        public bool TryResize(ResizeDimension dimension, float deltaMetres,
            CharacterController player, out string reason)
        {
            int axis = dimension == ResizeDimension.Length ? 2 : dimension == ResizeDimension.Height ? 1 :
                dimension == ResizeDimension.Width ? 0 : -1;
            if (axis < 0 || !Finite(deltaMetres))
            {
                reason = "Некорректный размер.";
                return false;
            }
            if(!CanResize(dimension)) { reason="Этот размер закреплён. Смотри стрелки на объекте."; return false; }
            RegisterInteraction(player);
            Vector3 next = size;
            next[axis] = Mathf.Clamp(size[axis] + deltaMetres, MinimumForAxis(axis), MaximumForAxis(axis));
            if (Mathf.Abs(next[axis] - size[axis]) < .00001f)
            {
                if (Mathf.Abs(deltaMetres) > .00001f) ShowSizeLimit(axis);
                reason = "Достигнут предел размера.";
                return false;
            }
            bool applied = SetSize(next, player, out reason);
            if (applied && (Mathf.Abs(size[axis]-MinimumForAxis(axis)) < .00001f || Mathf.Abs(size[axis]-MaximumForAxis(axis)) < .00001f)) ShowSizeLimit(axis);
            return applied;
        }

        public bool RestoreInitialSize(CharacterController player, out string reason)
            => SetSize(initialSize, player, out reason);

        /// <summary>Accumulate wheel input without moving the solid in the input frame.</summary>
        public bool RequestResize(ResizeDimension dimension, float deltaMetres,
            CharacterController player, out string reason)
        {
            RegisterInteraction(player);
            if (!ValidateSetup(out reason)) return false;
            int axis = dimension == ResizeDimension.Length ? 2 : dimension == ResizeDimension.Height ? 1 :
                dimension == ResizeDimension.Width ? 0 : -1;
            if (axis < 0 || !Finite(deltaMetres)) { reason = "Некорректный размер."; return false; }
            if(!CanResize(dimension)) { reason="Этот размер закреплён. Смотри стрелки на объекте."; return false; }
            Vector3 goal = RequestedSize;
            // Reverse immediately instead of making the user unwind an invisible wheel backlog.
            bool reverse = deltaMetres * (goal[axis] - size[axis]) < 0;
            float origin = reverse ? size[axis] : goal[axis];
            float value = Mathf.Clamp(origin + deltaMetres, MinimumForAxis(axis), MaximumForAxis(axis));
            if (Mathf.Abs(value - origin) < .00001f)
            {
                // A queued target may be at its limit while the visible solid is still moving.
                if (Mathf.Abs(deltaMetres) > .00001f && Mathf.Abs(size[axis]-value) < .00001f) ShowSizeLimit(axis);
                reason = "Достигнут предел размера."; return false;
            }
            if (reverse || deltaMetres * resizeVelocity[axis] < 0) resizeVelocity[axis] = 0;
            goal[axis] = value;
            requestedSize = goal; resizingPlayer = player; resizeQueued = true;
            reason = string.Empty;
            return true;
        }

        /// <summary>Called by the player before CharacterController.Move, once per frame.</summary>
        public bool AdvanceResize(float deltaTime, out string reason)
        {
            reason = string.Empty;
            if (!resizeQueued || !Finite(deltaTime) || deltaTime <= 0) return true;
            // Avoid a visible jump after an unusually long frame.
            float dt = Mathf.Min(deltaTime, .05f);
            Vector3 next = Vector3.SmoothDamp(size, requestedSize, ref resizeVelocity,
                Mathf.Max(.02f, resizeSmoothSeconds), Mathf.Max(.1f, maxResizeSpeed), dt);
            bool submicronStep = (next - size).sqrMagnitude < .000000000001f;
            // At very high frame rates a final sub-micron step can fall below the
            // geometry update threshold. Finish within 1 mm instead of queuing forever.
            if ((requestedSize - next).sqrMagnitude < .00000001f ||
                (submicronStep && (requestedSize - size).sqrMagnitude < .000001f)) next = requestedSize;
            Vector3 before = size;
            if (!TryApplySize(next, resizingPlayer, out reason))
            {
                CancelResize();
                return false;
            }
            for (int axis=0; axis<3; axis++)
                if (Mathf.Abs(size[axis]-before[axis]) > .0000001f &&
                    (Mathf.Abs(size[axis]-MinimumForAxis(axis)) < .00001f || Mathf.Abs(size[axis]-MaximumForAxis(axis)) < .00001f)) ShowSizeLimit(axis);
            if ((requestedSize - size).sqrMagnitude < .00000001f) CancelResize();
            return true;
        }

        public void CancelResize()
        {
            resizeQueued = false; requestedSize = size;
            resizeVelocity = Vector3.zero; resizingPlayer = null;
        }

        public bool SetSize(Vector3 requestedSize, CharacterController player, out string reason)
        {
            RegisterInteraction(player);
            CancelResize();
            return TryApplySize(requestedSize, player, out reason);
        }

        bool TryApplySize(Vector3 requestedSize, CharacterController player, out string reason,Vector3? restoredOffset=null)
        {
            if (!ValidateSetup(out reason)) return false;
            if (!Finite(requestedSize.x) || !Finite(requestedSize.y) || !Finite(requestedSize.z))
            {
                reason = "Некорректный размер.";
                return false;
            }
            Vector3 next = requestedSize;
            for (int i = 0; i < 3; i++) next[i] = Mathf.Clamp(next[i],
                restoredOffset.HasValue?minSize[i]:MinimumForAxis(i),restoredOffset.HasValue?maxSize[i]:MaximumForAxis(i));
            Vector3 offsetDelta=restoredOffset.HasValue?restoredOffset.Value-resizeOffset:Vector3.zero;
            // Keep sub-frame steps below 0.01 mm: ignoring them can leave SmoothDamp
            // permanently short of its target at high frame rates.
            if ((next - size).sqrMagnitude + offsetDelta.sqrMagnitude < .000000000001f) return true;

            EnsureProbes();
            // Prepare both poses together. ComputePenetration uses explicit poses, so
            // old/new contact tests can share one sync instead of rebuilding one shape repeatedly.
            previousBoxProbe.size = PhysicalSize(size);
            boxProbe.size = PhysicalSize(next);
            bool checkPlayer = player && player.enabled && player.gameObject.activeInHierarchy;
            Vector3 playerCentre = Vector3.zero;
            Quaternion playerRotation = Quaternion.identity;
            float radius = 0, height = 0;
            if (checkPlayer)
            {
                GetPlayerCapsule(player, out playerCentre, out playerRotation, out radius, out height);
                capsuleProbe.radius = radius; capsuleProbe.height = height;
            }
            Physics.SyncTransforms();
            Vector3 oldCentre = BoxCentre(size);
            Vector3 newCentre = BoxCentre(next)+transform.TransformVector(offsetDelta);
            Quaternion boxRotation = transform.rotation;
            if (!CheckThinSweep(next,oldCentre,newCentre,boxRotation,player,out reason)) return false;
            if (!CheckSolids(next, oldCentre, newCentre, boxRotation, player, out reason)) return false;

            Vector3 riderDisplacement = Vector3.zero;
            if (checkPlayer)
            {
                Vector3 playerUp = playerRotation * Vector3.up;
                Vector3 feet = playerCentre - playerUp * (height * .5f);
                if (IsStandingOnTop(player, feet, playerUp, next,offsetDelta))
                    riderDisplacement = transform.up * ((.5f - EffectiveAnchor.y) * (next.y - size.y)+offsetDelta.y);

                Vector3 destination = playerCentre + riderDisplacement;
                float oldPlayerDepth = BoxCapsuleDepth(size, oldCentre, boxRotation, playerCentre, playerRotation);
                float newPlayerDepth = BoxCapsuleDepth(next, newCentre, boxRotation, destination, playerRotation);
                if (newPlayerDepth > Mathf.Max(contactTolerance, oldPlayerDepth))
                {
                    reason = "Нет места: объект заденет игрока.";
                    return false;
                }
                if (riderDisplacement.sqrMagnitude > .00000001f &&
                    !CheckRiderSpace(player, playerCentre, destination, playerRotation, radius, height,
                        riderDisplacement, out reason)) return false;
            }

            Vector3 previousSize=size;
            size = next;
            resizeOffset+=offsetDelta;
            ApplyGeometry();
            if(Application.isPlaying)
            {
                EnsureFeedback();
                for(int axis=0;axis<3;axis++) if(Mathf.Abs(size[axis]-previousSize[axis])>.000001f)
                    limitParticles.Moving(this,axis);
            }
            if (player && riderDisplacement.sqrMagnitude > .00000001f)
            {
                // Move the checked capsule together with its support. Move() would collide with the
                // old top when shrinking, or apply the motor's collision response a second time.
                bool wasEnabled = player.enabled;
                player.enabled = false;
                player.transform.position += riderDisplacement;
                player.enabled = wasEnabled;
            }
            Physics.SyncTransforms();
            reason = string.Empty;
            return true;
        }

        bool ValidateSetup(out string reason)
        {
            reason = string.Empty;
            if (!solid || solid.parent != transform || !SolidCollider)
            {
                reason = "Нужен дочерний Solid с BoxCollider.";
                return false;
            }
            for (Transform ancestor = transform; ancestor; ancestor = ancestor.parent)
            {
                if ((ancestor.localScale - Vector3.one).sqrMagnitude <= .0000001f) continue;
                reason = "У корня и родителей Scale должен быть (1, 1, 1). Размер задаётся компонентом.";
                return false;
            }
            return true;
        }

        bool CheckThinSweep(Vector3 next,Vector3 oldCentre,Vector3 newCentre,Quaternion rotation,
            CharacterController player,out string reason)
        {
            reason=string.Empty;
            Vector3 travel=newCentre-oldCentre;float distance=travel.magnitude;
            if(!thinPlatform || distance<.0001f)return true;
            Vector3 half=Vector3.Min(PhysicalSize(size),PhysicalSize(next))*.5f-Vector3.one*contactTolerance;
            half=Vector3.Max(half,Vector3.one*.001f);
            int count=Physics.BoxCastNonAlloc(oldCentre,half,travel/distance,sweepHits,rotation,distance,
                blockingLayers,QueryTriggerInteraction.Ignore);
            if(count==sweepHits.Length){reason="Слишком много препятствий рядом.";return false;}
            for(int i=0;i<count;i++)
            {
                var hit=sweepHits[i];
                if(IgnoreCollider(hit.collider,player)||hit.distance>=distance-contactTolerance)continue;
                // Existing contact is handled by the destination penetration check.
                if(hit.distance<=contactTolerance && BoxDepth(size,oldCentre,rotation,hit.collider)>contactTolerance)continue;
                if(Vector3.Dot(travel/distance,hit.normal)>=-.01f)continue;
                reason="Нет места: платформа встретила препятствие.";return false;
            }
            return true;
        }

        bool CheckSolids(Vector3 next, Vector3 oldCentre, Vector3 newCentre, Quaternion rotation,
            CharacterController player, out string reason)
        {
            reason = string.Empty;
            int count = Physics.OverlapBoxNonAlloc(newCentre, PhysicalSize(next) * .5f, overlaps, rotation,
                blockingLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length)
            {
                reason = "Слишком много препятствий рядом.";
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = overlaps[i];
                if (IgnoreCollider(obstacle, player)) continue;
                float nextDepth = BoxDepth(next, newCentre, rotation, obstacle);
                if (nextDepth <= contactTolerance) continue;
                float previousDepth = BoxDepth(size, oldCentre, rotation, obstacle);
                // The tolerance is absolute. Adding it every frame would let slow growth
                // accumulate penetration and eventually pass through an obstacle.
                if (nextDepth <= previousDepth) continue;
                reason = "Нет места: мешает другой объект.";
                return false;
            }
            return true;
        }

        bool IsStandingOnTop(CharacterController player, Vector3 feet, Vector3 playerUp, Vector3 next,Vector3 offsetDelta)
        {
            if (Vector3.Dot(transform.up, playerUp) < .98f || Vector3.Dot(player.velocity, playerUp) > .5f)
                return false;
            Vector3 localFeet = transform.InverseTransformPoint(feet);
            float top = LocalCentre(size).y + .5f * size.y;
            if (Mathf.Abs(localFeet.y - top) > standingTolerance) return false;
            // Deliberately use the foot centre, not the old footprint: a platform shrinking away
            // from the player must let them fall, rather than drag them back onto it.
            return WithinFootprint(localFeet, size) && WithinFootprint(localFeet-offsetDelta, next);
        }

        bool WithinFootprint(Vector3 point, Vector3 dimensions)
        {
            Vector3 centre = LocalCentre(dimensions);
            return Mathf.Abs(point.x - centre.x) <= dimensions.x * .5f + contactTolerance &&
                Mathf.Abs(point.z - centre.z) <= dimensions.z * .5f + contactTolerance;
        }

        bool CheckRiderSpace(CharacterController player, Vector3 oldCentre, Vector3 destination,
            Quaternion rotation, float radius, float height, Vector3 displacement, out string reason)
        {
            reason = string.Empty;
            Vector3 up = rotation * Vector3.up;
            float halfLine = Mathf.Max(0, height * .5f - radius);
            int count = Physics.OverlapCapsuleNonAlloc(destination - up * halfLine, destination + up * halfLine,
                radius, overlaps, blockingLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length)
            {
                reason = "Слишком много препятствий над платформой.";
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = overlaps[i];
                if (IgnoreCollider(obstacle, player)) continue;
                float nextDepth = CapsuleDepth(destination, rotation, obstacle);
                float previousDepth = CapsuleDepth(oldCentre, rotation, obstacle);
                if (nextDepth <= Mathf.Max(contactTolerance, previousDepth)) continue;
                reason = "Нет места для игрока над платформой.";
                return false;
            }

            float distance = displacement.magnitude;
            Vector3 direction = displacement / distance;
            int hits = Physics.CapsuleCastNonAlloc(oldCentre - up * halfLine, oldCentre + up * halfLine,
                Mathf.Max(.001f, radius - contactTolerance), direction, sweepHits, distance,
                blockingLayers, QueryTriggerInteraction.Ignore);
            if (hits == sweepHits.Length)
            {
                reason = "Слишком много препятствий на пути игрока.";
                return false;
            }
            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = sweepHits[i];
                if (IgnoreCollider(hit.collider, player) || hit.distance >= distance - contactTolerance ||
                    Vector3.Dot(direction, hit.normal) >= -.01f) continue;
                reason = "Платформа прижмёт игрока к препятствию.";
                return false;
            }
            return true;
        }

        bool IgnoreCollider(Collider obstacle, CharacterController player)
        {
            if (!obstacle || obstacle == boxProbe || obstacle == previousBoxProbe || obstacle == capsuleProbe ||
                obstacle.transform.IsChildOf(transform)) return true;
            return player && (obstacle == player || obstacle.transform.IsChildOf(player.transform));
        }

        float BoxDepth(Vector3 dimensions, Vector3 centre, Quaternion rotation, Collider obstacle)
        {
            BoxCollider shape = dimensions.Equals(size) ? previousBoxProbe : boxProbe;
            return Physics.ComputePenetration(shape, centre, rotation, obstacle,
                obstacle.transform.position, obstacle.transform.rotation, out _, out float depth) ? depth : 0;
        }

        float BoxCapsuleDepth(Vector3 dimensions, Vector3 centre, Quaternion rotation,
            Vector3 capsuleCentre, Quaternion capsuleRotation)
        {
            BoxCollider shape = dimensions.Equals(size) ? previousBoxProbe : boxProbe;
            return Physics.ComputePenetration(shape, centre, rotation, capsuleProbe,
                capsuleCentre, capsuleRotation, out _, out float depth) ? depth : 0;
        }

        float CapsuleDepth(Vector3 centre, Quaternion rotation, Collider obstacle)
            => Physics.ComputePenetration(capsuleProbe, centre, rotation, obstacle,
                obstacle.transform.position, obstacle.transform.rotation, out _, out float depth) ? depth : 0;

        static void GetPlayerCapsule(CharacterController player, out Vector3 centre,
            out Quaternion rotation, out float radius, out float height)
        {
            Vector3 scale = player.transform.lossyScale;
            radius = player.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            height = Mathf.Max(radius * 2, player.height * Mathf.Abs(scale.y));
            centre = player.transform.TransformPoint(player.center);
            rotation = player.transform.rotation;
        }

        Vector3 BoxCentre(Vector3 dimensions)
            => transform.TransformPoint(PhysicalCentre(dimensions));

        void ApplyGeometry()
        {
            if (!solid || solid.parent != transform) return;
            solid.localPosition = PhysicalCentre(size);
            solid.localRotation = Quaternion.identity;
            solid.localScale = PhysicalSize(size);
            if (SolidCollider)
            {
                solidCollider.center = Vector3.zero;
                solidCollider.size = Vector3.one;
            }
        }

        void EnsureProbes()
        {
            if (probeObject) return;
            // Keep valid PhysX shapes: disabled colliders can retain an absent/stale native shape
            // after size changes. Trigger probes at a remote position never support or push the
            // player and are excluded from gameplay raycasts and all of our overlap queries.
            // ComputePenetration uses the explicit checked poses instead of this remote pose.
            probeObject = new GameObject("Resize collision probes")
                { hideFlags = HideFlags.HideAndDontSave, layer = 2 };
            probeObject.transform.position = new Vector3(0, -100000, 0);
            boxProbe = probeObject.AddComponent<BoxCollider>();
            boxProbe.isTrigger = true;
            previousBoxProbe = probeObject.AddComponent<BoxCollider>();
            previousBoxProbe.isTrigger = true;
            capsuleProbe = probeObject.AddComponent<CapsuleCollider>();
            capsuleProbe.isTrigger = true;
            capsuleProbe.direction = 1;
            Physics.SyncTransforms();
        }

        void OnDestroy()
        {
            if (!probeObject) return;
            if (Application.isPlaying) Destroy(probeObject);
            else DestroyImmediate(probeObject);
        }

        void OnDrawGizmosSelected()
        {
            Matrix4x4 saved = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.68f, .4f, 1f, 1);
            Gizmos.DrawWireCube(PhysicalCentre(size), PhysicalSize(size));
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(Vector3.zero, .075f);
            Gizmos.matrix = saved;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
