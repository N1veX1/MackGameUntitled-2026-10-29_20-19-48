using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Level 1: learn the single mask, all six views, then pass the wall.</summary>
public sealed class MaskTutorialLevel : MonoBehaviour
{
    [SerializeField] private MaskAxisCamera mask;
    [SerializeField] private Transform player;
    [SerializeField] private float approachZ = 0.25f;
    [SerializeField] private float exitZ = 12.2f;
    [SerializeField] private Color purple = new Color(.64f, .39f, 1f);

    [Header("World instructions")]
    [SerializeField] private bool instructionsOnSurfaces;
    private int step;
    private Vector3 spawnPosition;
    private GUIStyle titleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle statusStyle;

    private static readonly string[] Directions = { "1  X / -X", "2  Y / -Y", "3  Z / -Z" };
    [SerializeField, Min(2f)] private float testSpacing = 4f;
    private readonly MaskProximityWall[] gates = new MaskProximityWall[3];
    private readonly float[] gateZ = new float[3];
    [Header("Assigned scene obstacles; independent of object names")]
    [SerializeField] GameObject yObstacle;
    [SerializeField] GameObject zObstacle;
    [SerializeField] Renderer xMergeSurface;
    [SerializeField] Renderer yMergeSurface;
    [SerializeField] Renderer zMergeSurface;
    [SerializeField] Transform[] checkpoints = new Transform[3];
    [Tooltip("Extend the visible grey wall across the Z merge, including its texture and lighting.")]
    [SerializeField] bool matchZWallLighting = true;
    [Tooltip("Visible neighbouring face to extend visually. The physical backing stays assigned separately.")]
    [SerializeField] Renderer zAppearanceReference;
    private MaskDemoPlayer movement;
    private bool initialized;
    private bool gateOpened;
    public bool PortalReady => step >= 5;
    public MaskProximityWall Gate(int axis) => gates[axis];
    private static readonly string[] Instructions =
    {
        "WASD — ходьба по взгляду. Подойдите к стене X.",
        "Нажмите M. Пока доступна только ось X.",
        "ТЕСТ X · 1 — X/−X. Дождитесь камеры и прижмитесь к стене. W — вперёд, A/D — в стороны.",
        "ОСЬ Y ОТКРЫТА · 2 — Y/−Y. Пройдите следующую стену. Над стеной открытый потолок: используйте Y сверху.",
        "ОСЬ Z ОТКРЫТА · Поверните налево. Нажмите 3 и подойдите вплотную к стене.",
        "Все три испытания пройдены! Идите в фиолетовый портал слева — на уровень 2.",
        "УРОВЕНЬ 1 ПРОЙДЕН — X, Y и Z освоены!"
    };
    private void Start()
    {
        if (!mask) mask = FindAnyObjectByType<MaskAxisCamera>();
        movement = player ? player.GetComponent<MaskDemoPlayer>() : FindAnyObjectByType<MaskDemoPlayer>();
        if (!player && movement) player = movement.transform;
        if (!mask || !player || !movement) { Debug.LogError("Tutorial player references missing.", this); return; }
        spawnPosition = player.position;
        movement.Respawned += OnRespawn;
        mask.UnlockThrough(0);
        gates[0] = mask.GetComponent<MaskProximityWall>();
        if (gates[0]) gates[0].ConfigureMerge(xMergeSurface);
        gates[1] = InitializeObstacle(yObstacle, yMergeSurface);
        gates[2] = InitializeObstacle(zObstacle, zMergeSurface, true, matchZWallLighting, zAppearanceReference);
        initialized = gates[0] && gates[1] && gates[2];
        if (!initialized) Debug.LogError("Assign all three tutorial obstacle references in the Inspector.", this);
    }
    private MaskProximityWall InitializeObstacle(GameObject root, Renderer backing, bool requireNearPlayer = true, bool matchAppearance = false, Renderer appearanceReference = null)
    {
        if (!root || !backing) return null;
        var gate = root.GetComponent<MaskProximityWall>();
        if (!gate) gate = root.AddComponent<MaskProximityWall>();
        gate.Initialize(root.GetComponentsInChildren<Renderer>(true), root.GetComponentsInChildren<Collider>(true), player, mask.GetComponent<MaskViewEffects>());
        gate.ConfigureMerge(backing, requireNearPlayer, matchAppearance, appearanceReference);
        return gate;
    }
    private void OnRespawn()
    {
        gateOpened = false;
        foreach (var gate in gates) if (gate) gate.Restore();
    }
    private void OnDestroy() { if (movement) movement.Respawned -= OnRespawn; }
    private void Update()
    {
        if (!mask || !player) return;
        int selected = (int)mask.CurrentDirection / 2;
        for (int axis=1; axis<3; axis++)
            if (gates[axis]) gates[axis].SetDimension(mask.MaskEnabled && selected == axis && mask.UnlockedAxis >= axis);
        if (!initialized) return;
        if (step == 0 && player.position.z >= approachZ) step=1;
        if (step == 1 && mask.MaskEnabled) step=2;
        if (step < 2 || step > 4) return;
        int current = step - 2;
        if (mask.MaskEnabled && selected == current && gates[current].Opacity < .01f) gateOpened = true;
        Collider obstacle = current == 0 ? mask.ObstacleColliders[0] : (current == 1 ? yObstacle : zObstacle).GetComponentInChildren<Collider>();
        if (!obstacle) return;
        // Crossing is recorded when the whole character clears the actual wall, even if the mask is removed immediately afterwards.
        Bounds bounds = gates[current].ObstacleBounds;
        float clearance = movement.GetComponent<CharacterController>().radius + .03f;
        bool crossed = current == 2 ? player.position.x < bounds.min.x - clearance : player.position.z > bounds.max.z + clearance;
        if (gateOpened && crossed)
        {
            step++; gateOpened=false;
            mask.UnlockThrough(Mathf.Min(current + 1, 2));
            if (checkpoints != null && current < checkpoints.Length && checkpoints[current]) movement.SetCheckpoint(checkpoints[current].position);
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        titleStyle.normal.textColor = Color.white;
        bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter, wordWrap = true };
        bodyStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 21, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter };
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.white;
        statusStyle = new GUIStyle(GUI.skin.label) { fontSize = 19,
            alignment = TextAnchor.MiddleCenter };
        statusStyle.normal.textColor = new Color(.9f, .86f, 1f);
    }

    private void OnGUI()
    {
        if (mask == null) return;
        EnsureStyles();
        Matrix4x4 oldMatrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600f * scale) * .5f,
            (Screen.height - 900f * scale) * .5f, 0f), Quaternion.identity, Vector3.one * scale);

        Panel(new Rect(20, 20, 1560, 125), new Color(.08f, .08f, .12f, .95f));
        GUI.Label(new Rect(42, 28, 545, 44), "УРОВЕНЬ 1 · ОБУЧЕНИЕ", titleStyle);
        GUI.Label(new Rect(42, 79, 545, 36), "ОДНА МАСКА — ШЕСТЬ ОБЗОРОВ", statusStyle);

        GUI.backgroundColor = mask.MaskEnabled ? purple : new Color(.38f, .28f, .52f);
        if (GUI.Button(new Rect(600, 48, 213, 59),
            mask.MaskEnabled ? "СНЯТЬ МАСКУ" : "НАДЕТЬ МАСКУ", buttonStyle)) mask.ToggleMask();

        GUI.enabled = mask.MaskEnabled;
        for (int i = 0; i < Directions.Length; i++)
        {
            GUI.enabled = mask.MaskEnabled && i <= mask.UnlockedAxis;
            GUI.backgroundColor = mask.MaskEnabled && (int)mask.CurrentDirection / 2 == i
                ? purple : new Color(.27f, .27f, .33f);
            if (GUI.Button(new Rect(846 + i * 228, 49, 212, 56), Directions[i], buttonStyle))
            {
                switch (i)
                {
                    case 0: mask.SelectX(); break;
                    case 1: mask.SelectY(); break;
                    case 2: mask.SelectZ(); break;
                }
            }
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        if (!instructionsOnSurfaces)
        {
        Panel(new Rect(275, 735, 1050, 145), new Color(.08f, .08f, .12f, .94f));
        GUI.color = purple;
        GUI.Label(new Rect(302, 746, 996, 31), "ШАГ " + Mathf.Min(step + 1, 6) + " / 6", statusStyle);
        GUI.color = Color.white;
        string instruction = step >= 2 && step < 6 && !mask.MaskEnabled
            ? "Наденьте маску снова, чтобы продолжить обучение."
            : Instructions[step];
        GUI.Label(new Rect(300, 779, 1000, 62), instruction, bodyStyle);
        }
        if (step == 6)
        {
            GUI.backgroundColor = purple;
            if (GUI.Button(new Rect(1260, 645, 250, 64), "ПОВТОРИТЬ", buttonStyle))
                SceneManager.LoadScene(gameObject.scene.name);
            GUI.backgroundColor = Color.white;
        }

        GUI.Label(new Rect(30, 688, 475, 32), "WASD — ходьба   ПКМ — обзор без маски", statusStyle);
        GUI.matrix = oldMatrix;
    }

    private static void Panel(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
}



