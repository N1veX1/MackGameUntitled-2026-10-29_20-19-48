using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CornerTraversalValidation
{
    const string PlayCheckKey = "MaskTraversal.PlayCheck";
    static int captureStage = -1;
    static double captureTime;
    static CornerTraversalValidation() { EditorApplication.update += CheckPlayMode; }
    public static void BuildAndCheckPlayMode()
    {
        CornerTraversalSceneBuilder.Build();
        Validate();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Preview();
        SessionState.SetBool(PlayCheckKey, true);
        EditorApplication.EnterPlaymode();
    }
    static void CheckPlayMode()
    {
        if (captureStage >= 0) { CaptureRunningScene(); return; }
        if (!SessionState.GetBool(PlayCheckKey, false) || !EditorApplication.isPlaying || Time.timeSinceLevelLoad < .5f) return;
        SessionState.SetBool(PlayCheckKey, false);
        try
        {
            Validate();
            CheckRealLanding();
            Debug.Log("LONG_GRAVITY_PLAYMODE_OK: gameplay checks passed in running scene.");
            captureStage = 0; captureTime = EditorApplication.timeSinceStartup + .3;
        }
        catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); }
    }
    static void CaptureRunningScene()
    {
        if (EditorApplication.timeSinceStartup < captureTime) return;
        var player = UnityEngine.Object.FindFirstObjectByType<CornerMaskTraversalController>();
        var camera = player.GetComponentInChildren<Camera>();
        string[] files = { "start.png", "inside-mask.png", "wearing-mask.png", "wall-room.png" };
        var texture = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var old = RenderTexture.active;
        try
        {
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            string folder = System.IO.Path.GetFullPath("../../outputs/long-gravity-course"); System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, files[captureStage]), image.EncodeToPNG());
        }
        finally { camera.targetTexture = null; RenderTexture.active = old; UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(image); }
        captureStage++;
        if (captureStage == 1) player.SetMaskState(true);
        else if (captureStage == 2) camera.GetComponent<OrnamentMaskView>().AnimateDonning(.15f, true);
        else if (captureStage == 3)
        {
            var corner = UnityEngine.Object.FindObjectsByType<RoundedCornerTransition>(FindObjectsSortMode.None).OrderBy(c => c.startDistance).First();
            player.SetMaskState(true); WalkTo(player, corner.endDistance + 1); player.SetMaskState(false);
        }
        else { captureStage = -1; if (Application.isBatchMode) EditorApplication.Exit(0); return; }
        captureTime = EditorApplication.timeSinceStartup + .4;
    }
    [MenuItem("Tools/Mask Traversal/Validate Current Course")]
    public static void Validate()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<CornerMaskTraversalController>();
        Require(player != null, "Player is missing.");
        var corners = UnityEngine.Object.FindObjectsByType<RoundedCornerTransition>(FindObjectsSortMode.None).OrderBy(c => c.startDistance).ToArray();
        Require(corners.Length == 4, "Four turns must be present.");
        player.InitializeRoute();
        Require(player.RouteLength > 120 && player.RouteLength < 123, "Expected the continuous course across front, side and return walls.");
        Vector3 worldGravity = Physics.gravity;
        // No mask: even a very large movement cannot skip the first curve.
        player.MoveOnPlatform(Vector2.up, 100);
        Require(Mathf.Abs(player.RouteDistance - corners[0].startDistance) < .025f, "Unmasked player must stop at the first bend.");
        Require(player.GravityChangeBlocked && Vector3.Angle(player.GravityDirection, Vector3.down) < .02f, "The bend changed gravity without the mask.");
        player.SetMaskState(true);
        WalkTo(player, (corners[0].startDistance + corners[0].endDistance) * .5f);
        Require(!player.SetMaskState(false) && player.MaskOn, "Removing the mask inside a rounded transition must be blocked.");
        Require(!player.TryToggleMask(), "The keyboard/API toggle must also refuse mid-transition removal.");
        for (int i = 0; i < corners.Length; i++)
        {
            player.SetMaskState(true);
            WalkTo(player, corners[i].endDistance + .4f);
            Vector3 gravity = player.GravityDirection;
            player.SetMaskState(false);
            float before = player.RouteDistance;
            player.MoveOnPlatform(Vector2.up, .15f);
            Require(!player.IsStationary && !player.TryToggleMask(), "Changing the mask while walking must be rejected.");
            Require(player.RouteDistance > before + .1f, "Walking on the new floor should work without a mask.");
            Require(Vector3.Angle(gravity, player.GravityDirection) < .01f, "Removing the mask reset the selected gravity.");
            if (i + 1 < corners.Length)
            {
                player.MoveOnPlatform(Vector2.up, 100);
                Require(Mathf.Abs(player.RouteDistance - corners[i + 1].startDistance) < .03f, "An unmasked player crossed the next bend.");
            }
        }
        player.MoveOnPlatform(Vector2.up, 100);
        Require(player.Complete, "Exit should be reachable.");
        player.SetMaskState(true);
        for (int i = 0; i < 1000; i++) player.MoveOnPlatform(Vector2.down, .05f);
        Require(player.RouteDistance < .01f, "The entire level must also work in reverse.");
        Require(Physics.gravity == worldGravity, "World gravity must remain independent of the player mask.");
        // Real mesh collisions: each marked route sample has a surface under the player's feet.
        Physics.SyncTransforms();
        var platforms = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
            .Where(c => c.sharedMesh && c.sharedMesh.name.StartsWith("WalkModule")).ToArray();
        Require(platforms.Length == 13, "The floor, flat bends and four gravity corners must be separate modules.");
        var routeRoot = GameObject.Find("Ordered editable route samples").transform;
        for (int i = 0; i < routeRoot.childCount - 1; i += 7)
        {
            var node = routeRoot.GetChild(i);
            var next = routeRoot.GetChild(i + 1);
            Vector3 origin = (node.position + next.position) * .5f;
            Vector3 up = (node.up + next.up).normalized;
            RaycastHit hit = default; bool found = false;
            foreach (var platform in platforms)
                if (platform.Raycast(new Ray(origin, -up), out var candidate, 1.1f) && (!found || candidate.distance < hit.distance)) { hit = candidate; found = true; }
            Require(found, "Platform has a hole or inverted triangles at " + node.name);
            Require(Vector3.Dot(hit.normal, up) > .99f, "Walking surface normal is wrong at " + node.name);
            // Near a gravity bend, looking up looks along the next room rather than at a low ceiling.
            Require(Physics.Raycast(origin, up, 40, ~0, QueryTriggerInteraction.Ignore), "Open room envelope at " + node.name);
            foreach (int side in new[] { -1, 1 })
                Require(Physics.Raycast(origin, node.right * side, 5, ~0, QueryTriggerInteraction.Ignore), "Side wall gap at " + node.name);
        }
        for (int i = 3; i < routeRoot.childCount - 3; i += 7)
        {
            var node = routeRoot.GetChild(i);
            var overlaps = Physics.OverlapCapsule(node.position + node.up * .55f, node.position - node.up * .55f, .23f, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !c.transform.IsChildOf(player.transform)).ToArray();
            Require(overlaps.Length == 0, "The walking corridor intersects solid geometry at " + node.name + ": " + string.Join(", ", overlaps.Select(c => c.name)));
        }
        player.InitializeRoute();
        Require(UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Length == 0, "Old luminous road borders remain.");
        player.SetMaskState(true);
        Require(!player.GetComponentInChildren<OrnamentMaskView>().Visible, "The worn mask must not obstruct the first-person view.");
        var mask = player.GetComponentsInChildren<MeshFilter>().Where(f => f.name.Contains("shell") || f.name.Contains("Ivory")).ToArray();
        Require(mask.Length == 2 && mask.All(f => f.sharedMesh && f.sharedMesh.vertexCount > 10000), "Original ornament meshes have broken asset references.");
        Require(mask.All(f => f.GetComponent<Renderer>().sharedMaterials.All(m => m && m.shader && m.shader.isSupported)), "Original ornament material references are missing.");
        player.InitializeRoute();
        Debug.Log("INTEGRATED_GRAVITY_VALIDATION_OK: four compact corners, bidirectional course, no removal on turns, stationary mask gating, retained gravity, concrete floor continuity, clear masked view.");
    }
    static void CheckRealLanding()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<CornerMaskTraversalController>();
        var corner = UnityEngine.Object.FindObjectsByType<RoundedCornerTransition>(FindObjectsSortMode.None).OrderBy(c => c.startDistance).First();
        var originalMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        try
        {
            foreach (float distance in new[] { 7f, corner.endDistance + 1 })
            {
                player.InitializeRoute(); player.SetMaskState(true); WalkTo(player, distance); player.SetMaskState(false);
                Vector3 gravity = player.GravityDirection;
                var body = player.GetComponent<Rigidbody>();
                player.ReleaseToPhysics(); body.position = player.transform.position - gravity * 2;
                Physics.SyncTransforms();
                for (int step = 0; step < 180 && !player.IsAttached; step++)
                {
                    body.AddForce(gravity * 18, ForceMode.Acceleration); Physics.Simulate(.02f);
                }
                Require(player.IsAttached, "A real fall onto the platform did not land.");
                Require(Mathf.Abs(player.RouteDistance - distance) < .2f, "Landing incorrectly respawned the player at a checkpoint.");
                Require(Vector3.Angle(gravity, player.GravityDirection) < .01f, "Landing reset the player's selected gravity.");
            }
            player.InitializeRoute();
            Debug.Log("REAL_LANDING_OK: dropped onto both the normal floor and the wall-floor without respawn or gravity reset.");
        }
        finally { Physics.simulationMode = originalMode; }
    }
    public static void BuildAndValidate()
    {
        try
        {
            CornerTraversalSceneBuilder.Build();
            Validate();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Preview();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
    }
    static void WalkTo(CornerMaskTraversalController player, float target)
    {
        int limit = 10000;
        while (player.RouteDistance < target - .002f && --limit > 0)
            player.MoveOnPlatform(Vector2.up, Mathf.Min(.025f, (target - player.RouteDistance) / 3.3f));
        Require(limit > 0, "Player stopped unexpectedly during a masked traversal.");
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception("GRAVITY VALIDATION: " + message); }
    static void Preview()
    {
        var camera = UnityEngine.Object.FindFirstObjectByType<CornerMaskTraversalController>().GetComponentInChildren<Camera>();
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        var texture = new RenderTexture(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        try
        {
            camera.targetTexture = texture; camera.Render(); camera.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            string folder = System.IO.Path.GetFullPath("../../outputs/long-gravity-course"); System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "start.png"), image.EncodeToPNG());
            var player = camera.GetComponentInParent<CornerMaskTraversalController>();
            player.SetMaskState(true); camera.Render(); camera.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "inside-mask.png"), image.EncodeToPNG());
            camera.GetComponent<OrnamentMaskView>().AnimateDonning(.15f, true);
            camera.Render(); RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "wearing-mask.png"), image.EncodeToPNG());
            player.SetMaskState(false);
            camera.transform.position = new Vector3(3, 12.7f, 23);
            camera.transform.LookAt(new Vector3(0, 10.7f, 37));
            camera.Render(); RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "upper-gallery.png"), image.EncodeToPNG());
            player.InitializeRoute(); player.SetMaskState(true);
            var corner = UnityEngine.Object.FindObjectsByType<RoundedCornerTransition>(FindObjectsSortMode.None).OrderBy(c => c.startDistance).First();
            WalkTo(player, corner.endDistance + 1); player.SetMaskState(false);
            camera.Render(); camera.Render(); RenderTexture.active = texture; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "wall-room.png"), image.EncodeToPNG());
            player.InitializeRoute();
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation); camera.targetTexture = null; RenderTexture.active = active;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
