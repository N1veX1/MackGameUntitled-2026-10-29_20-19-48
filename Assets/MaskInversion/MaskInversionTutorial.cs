using System.Collections.Generic;
using UnityEngine;

public sealed class MaskInversionTutorial : MonoBehaviour
{
    [SerializeField] MaskInversionPlayer player;
    [SerializeField] MaskInversionGate[] sequence = new MaskInversionGate[4];
    [SerializeField] float[] clearZ = { 6.5f, 13.5f, 18.6f, 25.5f };
    [SerializeField] float finishZ = 29f;
    [SerializeField, Range(0f, .15f)] float wallContactMargin = .06f;
    int step;
    int passedWalls;
    Vector3 levelStart;
    Quaternion startRotation;
    float deathNoticeUntil;
    string deathNotice;
    public int PassedWalls => passedWalls;
    public bool OwnsGate(MaskInversionGate gate) => System.Array.IndexOf(sequence, gate) >= 0;
    bool complete;
    GUIStyle heading, body, hint;
    static readonly string[] Names = { "СТЕНА 1 · МАСКА НАДЕТА", "МОСТ 1 · МАСКА СНЯТА", "СТЕНА 2 · МАСКА СНЯТА", "МОСТ 2 · МАСКА НАДЕТА" };
    static readonly string[] Hints = {
        "Корни закрыли проход. Нажми M: в восстановленном офисе их нет.",
        "Сними маску перед провалом: корни образуют живой мост.",
        "Дверь справа закрыта. Сними маску, чтобы пройти сквозь исчезнувшую стену офиса.",
        "Надень маску у второй пропасти: в офисе здесь появляется целый пол."
    };

    void Start()
    {
        if (!player) player = FindAnyObjectByType<MaskInversionPlayer>();
        if (!player) { Debug.LogError("Level 2 tutorial needs a MaskInversionPlayer.", this); enabled = false; return; }
        for (int i = 0; i < sequence.Length; i++)
            if (sequence[i]) sequence[i].Apply(player.MaskOn);
        levelStart = player.transform.position;
        startRotation = player.transform.rotation;
        BindPlayer();
    }

    void OnEnable()
    {
        if (!player) player = FindAnyObjectByType<MaskInversionPlayer>();
        BindPlayer();
    }

    void BindPlayer()
    {
        if (!player) return;
        player.MaskStateChanged -= OnMaskChanged;
        player.RespawnStarted -= RollBackOneWall;
        player.MaskStateChanged += OnMaskChanged;
        player.RespawnStarted += RollBackOneWall;
    }

    void OnDisable()
    {
        if (!player) return;
        player.MaskStateChanged -= OnMaskChanged;
        player.RespawnStarted -= RollBackOneWall;
    }

    void OnMaskChanged(bool maskOn)
    {
        Physics.SyncTransforms();
        bool crushed = false;
        if (!player.IsRespawning)
            foreach (MaskInversionGate gate in sequence)
                if (gate && gate.BlocksWithMask(maskOn) && gate.TouchesPlayer(player.Controller, wallContactMargin))
                    crushed = true;
        foreach (MaskInversionGate gate in sequence) if (gate) gate.Apply(maskOn);
        Physics.SyncTransforms();
        if (crushed) player.Respawn();
    }

    void RollBackOneWall()
    {
        var walls = new List<int>();
        for (int i = 0; i < sequence.Length; i++)
            if (sequence[i] && sequence[i].Kind == MaskInversionGate.GateKind.Wall) walls.Add(i);
        if (walls.Count == 0) { player.SetRespawnPoint(levelStart, startRotation, false); return; }
        passedWalls = Mathf.Clamp(passedWalls - 1, 0, walls.Count - 1);
        int targetIndex = walls[passedWalls];
        MaskInversionGate target = sequence[targetIndex];
        Vector3 position = passedWalls == 0 ? levelStart : target.transform.position + Vector3.back * 1.8f;
        position.y = levelStart.y;
        // Arrive with the target wall closed so that it must actually be passed again.
        player.SetRespawnPoint(position, startRotation, !target.ExistsWithMaskOn);
        step = targetIndex;
        complete = false;
        deathNotice = "ТЫ ПОГИБ · ВОЗВРАТ ПЕРЕД СТЕНОЙ " + (passedWalls + 1);
        deathNoticeUntil = Time.unscaledTime + 3.5f;
    }

    void Update()
    {
        if (!player || complete || player.Transitioning || !player.Controller.isGrounded) return;
        while (step < sequence.Length && step < clearZ.Length && player.transform.position.z > clearZ[step])
        {
            var gate = sequence[step];
            if (gate && gate.Kind == MaskInversionGate.GateKind.Wall)
            {
                if (gate.IsBlocking) break;
                passedWalls++;
            }
            step++;
        }
        if (step >= sequence.Length && player.transform.position.z > finishZ) complete = true;
    }
    void EnsureStyles()
    {
        if (heading != null) return;
        heading = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
        heading.normal.textColor = Color.white;
        body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        body.normal.textColor = new Color(.91f, .9f, .96f);
        hint = new GUIStyle(body) { fontSize = 15 };
        hint.normal.textColor = new Color(.77f, .69f, 1f);
    }

    void OnGUI()
    {
        if (!player) return;
        EnsureStyles();
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600f * scale) * .5f, (Screen.height - 900f * scale) * .5f, 0), Quaternion.identity, Vector3.one * scale);
        Color oldColor = GUI.color;
        GUI.color = new Color(.035f, .04f, .055f, .88f);
        GUI.DrawTexture(new Rect(28, 24, 1544, 110), Texture2D.whiteTexture);
        GUI.color = oldColor;
        GUI.Label(new Rect(52, 33, 620, 42), "TUTORIAL 2 · ИНВЕРСИОННАЯ МАСКА", heading);
        string status = player.MaskOn ? "МАСКА НАДЕТА · ОФИС · M — СНЯТЬ" : "МАСКА СНЯТА · ЗАРОСЛИ · M — НАДЕТЬ";
        GUI.Label(new Rect(54, 83, 565, 28), status, hint);
        if (Time.unscaledTime < deathNoticeUntil)
        {
            GUI.Label(new Rect(620, 42, 850, 68), deathNotice, body);
        }
        else if (complete)
        {
            GUI.Label(new Rect(620, 42, 700, 68), "ОБУЧЕНИЕ ПРОЙДЕНО · ПРОЙДИ ВЫХОД", body);
        }
        else
        {
            int index = Mathf.Clamp(step, 0, Names.Length - 1);
            GUI.Label(new Rect(685, 38, 820, 36), Names[index], body);
            GUI.Label(new Rect(685, 77, 820, 42), Hints[index], hint);
        }
        GUI.color = oldColor;
        GUI.matrix = old;
    }
}

