using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>One continuous tutorial: X, -X, Y, Z, -Z; the sixth view reveals a clue beneath spawn.</summary>
public sealed class MaskSixTrialLevel : MonoBehaviour
{
    [Serializable]
    public sealed class Trial
    {
        public MaskAxisCamera.ViewDirection direction;
        public GameObject obstacle;
        public Renderer mergeSurface;
        public Renderer appearanceReference;
        public Vector3 travelDirection = Vector3.forward;
        public Transform approachPoint;
        [NonSerialized] public MaskProximityWall gate;
    }

    [SerializeField] MaskAxisCamera mask;
    [SerializeField] MaskDemoPlayer player;
    [SerializeField] Trial[] trials = new Trial[5];
    [Header("Spawn underside clue and final door")]
    [SerializeField] Transform startingPoint;
    [SerializeField] Transform codeRoomArrival;
    [SerializeField] Transform finalRoomArrival;
    [SerializeField] Transform toCodePortal;
    [SerializeField] Transform fromCodePortal;
    [SerializeField] TextMesh codeText;
    [SerializeField] GameObject paintCover;
    [SerializeField] Transform door;
    [SerializeField] Collider doorCollider;
    [SerializeField] Transform victoryArea;

    int stage;
    bool gateOpened;
    bool codeReady;
    bool keypadOpen;
    bool doorUnlocked;
    bool won;
    string code;
    string entered = "";
    string keypadMessage = "";
    Vector3 closedDoorPosition;
    float portalCooldown;
    GUIStyle heading, body, button, small;

    static readonly Key[] NumberKeys = {
        Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
        Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
    };
    static readonly Key[] KeypadKeys = {
        Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4,
        Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9
    };

    void Start()
    {
        if (!mask) mask = FindAnyObjectByType<MaskAxisCamera>();
        if (!player) player = FindAnyObjectByType<MaskDemoPlayer>();
        if (!mask || !player || trials == null || trials.Length == 0)
        {
            Debug.LogError("Tutorial requires a mask, player and configured trials.", this);
            enabled = false;
            return;
        }
        var effects = mask.GetComponent<MaskViewEffects>();
        for (int i = 0; i < trials.Length; i++)
        {
            var trial = trials[i];
            if (!trial.obstacle || !trial.mergeSurface || !trial.approachPoint)
            {
                Debug.LogError("Missing wall, backing or approach point in trial " + i, this);
                enabled = false;
                return;
            }
            trial.gate = trial.obstacle.GetComponent<MaskProximityWall>();
            if (!trial.gate) trial.gate = trial.obstacle.AddComponent<MaskProximityWall>();
            trial.gate.Initialize(trial.obstacle.GetComponentsInChildren<Renderer>(true),
                trial.obstacle.GetComponentsInChildren<Collider>(true), player.transform, effects);
            trial.gate.ConfigureMerge(trial.mergeSurface, true, true,
                trial.appearanceReference ? trial.appearanceReference : trial.mergeSurface);
        }
        mask.UnlockThrough(0);
        if (startingPoint) player.SetCheckpoint(startingPoint.position);
        if (codeText) codeText.gameObject.SetActive(false);
        if (paintCover) paintCover.SetActive(true);
        if (door) closedDoorPosition = door.localPosition;
        player.Respawned += OnRespawn;
    }

    void OnDestroy()
    {
        if (player) player.Respawned -= OnRespawn;
        if (mask) mask.InputLocked = false;
    }

    void OnRespawn()
    {
        gateOpened = false;
        keypadOpen = false;
        if (mask) mask.InputLocked = false;
        foreach (var trial in trials) if (trial.gate) trial.gate.Restore();
        UpdateUnlockedAxes();
    }

    void Update()
    {
        if (!player || !mask) return;
        if (portalCooldown > 0) portalCooldown -= Time.deltaTime;
        // Progress is permanent for this run. Every completed wall remains usable
        // in both travel directions; the selected camera sign still has to match.
        for (int i = 0; i < trials.Length; i++)
            if (trials[i].gate)
                trials[i].gate.SetDimension(i <= stage && mask.MaskEnabled &&
                    mask.CurrentDirection == trials[i].direction);
        if (stage < trials.Length) WatchCurrentTrial();
        UpdateReturnCheckpoint();
        UpdateClueVisibility();

        if (doorUnlocked && door)
        {
            door.localPosition = Vector3.MoveTowards(door.localPosition,
                closedDoorPosition + Vector3.up * 3.5f, Time.deltaTime * 2.5f);
            if (doorCollider && Vector3.Distance(door.localPosition,
                closedDoorPosition + Vector3.up * 3.5f) < .06f) doorCollider.enabled = false;
        }

        if (victoryArea && doorUnlocked && !won &&
            Vector3.Distance(player.transform.position, victoryArea.position) < 2f) won = true;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (codeReady && portalCooldown <= 0 && keyboard.eKey.wasPressedThisFrame)
        {
            if (Near(toCodePortal, 1.7f)) Teleport(codeRoomArrival);
            else if (Near(fromCodePortal, 1.7f)) Teleport(finalRoomArrival);
            else if (Near(door, 2.2f) && !doorUnlocked) ToggleKeypad();
        }
        if (keypadOpen)
        {
            for (int digit = 0; digit <= 9; digit++)
            {
                if (keyboard[NumberKeys[digit]].wasPressedThisFrame ||
                    keyboard[KeypadKeys[digit]].wasPressedThisFrame)
                    EnterDigit(digit);
            }
            if (keyboard.backspaceKey.wasPressedThisFrame && entered.Length > 0)
                entered = entered.Substring(0, entered.Length - 1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                SubmitCode();
            if (keyboard.escapeKey.wasPressedThisFrame || !Near(door, 3.5f))
                CloseKeypad();
        }
        if (won && keyboard.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(gameObject.scene.name);
    }

    void WatchCurrentTrial()
    {
        Trial trial = trials[stage];
        if (mask.MaskEnabled && mask.CurrentDirection == trial.direction &&
            trial.gate.Opacity <= .01f) gateOpened = true;
        if (!gateOpened) return;
        Bounds bounds = trial.gate.ObstacleBounds;
        Vector3 direction = trial.travelDirection.normalized;
        float projectedExtent = Mathf.Abs(direction.x) * bounds.extents.x +
            Mathf.Abs(direction.z) * bounds.extents.z;
        float clearance = player.GetComponent<CharacterController>().radius + .03f;
        if (Vector3.Dot(player.transform.position - bounds.center, direction) <= projectedExtent + clearance)
            return;

        stage++;
        gateOpened = false;
        UpdateUnlockedAxes();
        if (stage == trials.Length) RevealCode();
    }

    void UpdateUnlockedAxes()
    {
        int unlocked = 0;
        for (int i = 0; i <= stage && i < trials.Length; i++)
            unlocked = Mathf.Max(unlocked, (int)trials[i].direction / 2);
        mask.UnlockThrough(unlocked);
    }

    void UpdateReturnCheckpoint()
    {
        // Derive the occupied room from the route, including the journey back.
        // Falling never erases completed trials or the discovered password.
        if (player.transform.position.y < .3f) return;
        int room = 0;
        for (int i = 0; i < trials.Length; i++)
        {
            Bounds bounds = trials[i].gate.ObstacleBounds;
            Vector3 direction = trials[i].travelDirection.normalized;
            float extent = Mathf.Abs(direction.x) * bounds.extents.x +
                Mathf.Abs(direction.z) * bounds.extents.z;
            if (Vector3.Dot(player.transform.position - bounds.center, direction) <= extent + .3f) break;
            room++;
        }
        Transform checkpoint = room == 0 ? startingPoint : trials[room - 1].approachPoint;
        if (checkpoint) player.SetCheckpoint(checkpoint.position);
    }

    bool CanReadClue()
    {
        var effects = mask ? mask.GetComponent<MaskViewEffects>() : null;
        return codeReady && mask && mask.MaskEnabled &&
            mask.CurrentDirection == MaskAxisCamera.ViewDirection.MinusY &&
            effects && effects.IsViewSettled && Near(startingPoint, 3f);
    }

    void UpdateClueVisibility()
    {
        if (codeText) codeText.gameObject.SetActive(CanReadClue());
    }

    void RevealCode()
    {
        if (codeReady) return;
        code = UnityEngine.Random.Range(0, 10000).ToString("D4");
        codeReady = true;
        if (codeText)
        {
            codeText.text = "КОД: " + code;
            codeText.gameObject.SetActive(false);
        }
        if (paintCover) paintCover.SetActive(false);
    }

    bool Near(Transform target, float distance) =>
        target && Vector3.Distance(player.transform.position, target.position) <= distance;

    void Teleport(Transform target)
    {
        if (!target) return;
        CloseKeypad();
        player.TeleportTo(target.position, target.rotation);
        player.SetCheckpoint(target.position);
        UpdateReturnCheckpoint();
        portalCooldown = 1f;
    }

    void ToggleKeypad()
    {
        keypadOpen = !keypadOpen;
        mask.InputLocked = keypadOpen;
        entered = "";
        keypadMessage = "";
    }

    void CloseKeypad()
    {
        keypadOpen = false;
        if (mask) mask.InputLocked = false;
    }

    void EnterDigit(int digit)
    {
        if (entered.Length < 4) entered += digit.ToString();
    }

    void SubmitCode()
    {
        if (entered.Length != 4)
        {
            keypadMessage = "Введите четыре цифры.";
            return;
        }
        if (entered != code)
        {
            keypadMessage = "Неверный код. Ищите его под спавном через −Y.";
            entered = "";
            return;
        }
        doorUnlocked = true;
        CloseKeypad();
    }

    void Styles()
    {
        if (heading != null) return;
        heading = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
        body = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
        button = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
        small = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        heading.normal.textColor = body.normal.textColor = small.normal.textColor = Color.white;
    }

    void OnGUI()
    {
        if (!enabled || !mask) return;
        Styles();
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1600f * scale) / 2,
            (Screen.height - 900f * scale) / 2, 0), Quaternion.identity, Vector3.one * scale);
        Panel(new Rect(18, 18, 1564, 106));
        GUI.Label(new Rect(40, 25, 380, 36), "МАСКА · ПЯТЬ СТЕН", heading);
        string status = stage < trials.Length ? "Испытание " + (stage + 1) + "/" + trials.Length + " · " + trials[stage].direction.ToString().Replace("Minus", "−") :
            doorUnlocked ? "ДВЕРЬ ОТКРЫТА" : "КОД ПОД СПАВНОМ · СМОТРИТЕ ЧЕРЕЗ −Y";
        GUI.Label(new Rect(40, 66, 700, 34), status, body);

        GUI.enabled = !keypadOpen;
        if (GUI.Button(new Rect(770, 43, 185, 50), mask.MaskEnabled ? "СНЯТЬ M" : "НАДЕТЬ M", button))
            mask.ToggleMask();
        string[] axes = { "1  X / −X", "2  Y / −Y", "3  Z / −Z" };
        for (int i = 0; i < 3; i++)
        {
            GUI.enabled = !keypadOpen && mask.MaskEnabled && i <= mask.UnlockedAxis;
            Color oldColor = GUI.backgroundColor;
            GUI.backgroundColor = mask.MaskEnabled && (int)mask.CurrentDirection / 2 == i
                ? new Color(.65f, .35f, 1f) : Color.gray;
            if (GUI.Button(new Rect(975 + i * 197, 43, 185, 50), axes[i], button))
            {
                if (i == 0) mask.SelectX();
                if (i == 1) mask.SelectY();
                if (i == 2) mask.SelectZ();
            }
            GUI.backgroundColor = oldColor;
        }
        GUI.enabled = true;
        if (!codeReady && stage < trials.Length)
            GUI.Label(new Rect(30, 829, 1450, 48),
                "WASD — движение · ПКМ — обзор без маски · M — маска · Повторное 1/2/3 меняет знак оси · Подойдите к стене вплотную", small);
        else if (Near(toCodePortal, 1.7f))
            GUI.Label(new Rect(480, 760, 700, 45), "E — вернуться к началу за кодом", heading);
        else if (Near(fromCodePortal, 1.7f))
            GUI.Label(new Rect(480, 760, 700, 45), "E — вернуться к двери", heading);
        else if (Near(door, 2.2f) && !doorUnlocked)
            GUI.Label(new Rect(480, 760, 700, 45), "E — ввести код двери", heading);
        if (CanReadClue())
            GUI.Label(new Rect(565, 710, 550, 48), "КОД ДВЕРИ: " + code, heading);

        if (keypadOpen)
        {
            Panel(new Rect(605, 205, 390, 510));
            GUI.Label(new Rect(640, 225, 330, 40), "ДВЕРЬ · 4 ЦИФРЫ", heading);
            GUI.Label(new Rect(700, 270, 210, 40), entered.PadRight(4, '•'), heading);
            for (int n = 1; n <= 9; n++)
            {
                int col = (n - 1) % 3, row = (n - 1) / 3;
                if (GUI.Button(new Rect(650 + col * 100, 325 + row * 75, 80, 60), n.ToString(), button)) EnterDigit(n);
            }
            if (GUI.Button(new Rect(650, 555, 80, 60), "←", button) && entered.Length > 0)
                entered = entered.Substring(0, entered.Length - 1);
            if (GUI.Button(new Rect(750, 555, 80, 60), "0", button)) EnterDigit(0);
            if (GUI.Button(new Rect(850, 555, 80, 60), "OK", button)) SubmitCode();
            GUI.Label(new Rect(635, 630, 340, 55), keypadMessage, small);
        }
        if (won)
        {
            Panel(new Rect(460, 320, 680, 215));
            GUI.Label(new Rect(540, 345, 560, 55), "КОМПЛЕКС ПРОЙДЕН", heading);
            GUI.Label(new Rect(540, 405, 560, 35), "Пять стен пройдены, секрет −Y найден.", body);
            if (GUI.Button(new Rect(485, 465, 290, 50), "УРОВЕНЬ 2", button))
                SceneManager.LoadScene("Level02_Entry");
            if (GUI.Button(new Rect(790, 465, 290, 50), "НОВАЯ ИГРА · КОД", button))
                SceneManager.LoadScene(gameObject.scene.name);
        }
        GUI.matrix = old;
    }

    static void Panel(Rect rect)
    {
        Color old = GUI.color;
        GUI.color = new Color(.035f, .04f, .06f, .91f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
