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
        [Tooltip("Fixed point relative to the box centre: -0.5 = negative face, +0.5 = positive face. Root position is this point.")]
        [SerializeField] Vector3 anchorNormalized = new Vector3(0, -.5f, 0);
        [SerializeField] LayerMask blockingLayers = ~0;
        [Tooltip("Tolerance for touching surfaces and existing shallow contact, in metres.")]
        [SerializeField, Range(.001f, .02f)] float contactTolerance = .005f;
        [SerializeField, Range(.02f, .2f)] float standingTolerance = .1f;
        [Header("Smooth resizing")]
        [SerializeField, Min(.02f)] float resizeSmoothSeconds = .12f;
        [SerializeField, Min(.1f)] float maxResizeSpeed = 3f;

        Vector3 requestedSize, resizeVelocity;
        CharacterController resizingPlayer;
        bool resizeQueued;

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
        public Vector3 AnchorNormalized => anchorNormalized;
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
            NormalizeSettings();
            size = this.initialSize;
            ApplyGeometry();
            CancelResize();
        }

        void Awake()
        {
            NormalizeSettings();
            ApplyGeometry();
            CancelResize();
        }

        void OnDisable() { CancelResize(); }

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
            Vector3 next = size;
            next[axis] = Mathf.Clamp(size[axis] + deltaMetres, minSize[axis], maxSize[axis]);
            if (Mathf.Abs(next[axis] - size[axis]) < .00001f)
            {
                reason = "Достигнут предел размера.";
                return false;
            }
            return SetSize(next, player, out reason);
        }

        public bool RestoreInitialSize(CharacterController player, out string reason)
            => SetSize(initialSize, player, out reason);

        /// <summary>Accumulate wheel input without moving the solid in the input frame.</summary>
        public bool RequestResize(ResizeDimension dimension, float deltaMetres,
            CharacterController player, out string reason)
        {
            if (!ValidateSetup(out reason)) return false;
            int axis = dimension == ResizeDimension.Length ? 2 : dimension == ResizeDimension.Height ? 1 :
                dimension == ResizeDimension.Width ? 0 : -1;
            if (axis < 0 || !Finite(deltaMetres)) { reason = "Некорректный размер."; return false; }
            Vector3 goal = RequestedSize;
            // Reverse immediately instead of making the user unwind an invisible wheel backlog.
            bool reverse = deltaMetres * (goal[axis] - size[axis]) < 0;
            float origin = reverse ? size[axis] : goal[axis];
            float value = Mathf.Clamp(origin + deltaMetres, minSize[axis], maxSize[axis]);
            if (Mathf.Abs(value - origin) < .00001f) { reason = "Достигнут предел размера."; return false; }
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
            if (!TryApplySize(next, resizingPlayer, out reason))
            {
                CancelResize();
                return false;
            }
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
            CancelResize();
            return TryApplySize(requestedSize, player, out reason);
        }

        bool TryApplySize(Vector3 requestedSize, CharacterController player, out string reason)
        {
            if (!ValidateSetup(out reason)) return false;
            if (!Finite(requestedSize.x) || !Finite(requestedSize.y) || !Finite(requestedSize.z))
            {
                reason = "Некорректный размер.";
                return false;
            }
            Vector3 next = requestedSize;
            for (int i = 0; i < 3; i++) next[i] = Mathf.Clamp(next[i], minSize[i], maxSize[i]);
            // Keep sub-frame steps below 0.01 mm: ignoring them can leave SmoothDamp
            // permanently short of its target at high frame rates.
            if ((next - size).sqrMagnitude < .000000000001f) return true;

            EnsureProbes();
            // Prepare both poses together. ComputePenetration uses explicit poses, so
            // old/new contact tests can share one sync instead of rebuilding one shape repeatedly.
            previousBoxProbe.size = size;
            boxProbe.size = next;
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
            Vector3 newCentre = BoxCentre(next);
            Quaternion boxRotation = transform.rotation;
            if (!CheckSolids(next, oldCentre, newCentre, boxRotation, player, out reason)) return false;

            Vector3 riderDisplacement = Vector3.zero;
            if (checkPlayer)
            {
                Vector3 playerUp = playerRotation * Vector3.up;
                Vector3 feet = playerCentre - playerUp * (height * .5f);
                if (IsStandingOnTop(player, feet, playerUp, next))
                    riderDisplacement = transform.up * ((.5f - anchorNormalized.y) * (next.y - size.y));

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

            size = next;
            ApplyGeometry();
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

        bool CheckSolids(Vector3 next, Vector3 oldCentre, Vector3 newCentre, Quaternion rotation,
            CharacterController player, out string reason)
        {
            reason = string.Empty;
            int count = Physics.OverlapBoxNonAlloc(newCentre, next * .5f, overlaps, rotation,
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

        bool IsStandingOnTop(CharacterController player, Vector3 feet, Vector3 playerUp, Vector3 next)
        {
            if (Vector3.Dot(transform.up, playerUp) < .98f || Vector3.Dot(player.velocity, playerUp) > .5f)
                return false;
            Vector3 localFeet = transform.InverseTransformPoint(feet);
            float top = (.5f - anchorNormalized.y) * size.y;
            if (Mathf.Abs(localFeet.y - top) > standingTolerance) return false;
            // Deliberately use the foot centre, not the old footprint: a platform shrinking away
            // from the player must let them fall, rather than drag them back onto it.
            return WithinFootprint(localFeet, size) && WithinFootprint(localFeet, next);
        }

        bool WithinFootprint(Vector3 point, Vector3 dimensions)
        {
            Vector3 centre = -Vector3.Scale(anchorNormalized, dimensions);
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
            => transform.TransformPoint(-Vector3.Scale(anchorNormalized, dimensions));

        void ApplyGeometry()
        {
            if (!solid || solid.parent != transform) return;
            solid.localPosition = -Vector3.Scale(anchorNormalized, size);
            solid.localRotation = Quaternion.identity;
            solid.localScale = size;
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
            Gizmos.DrawWireCube(-Vector3.Scale(anchorNormalized, size), size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(Vector3.zero, .075f);
            Gizmos.matrix = saved;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
