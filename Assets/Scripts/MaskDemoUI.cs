using UnityEngine;

/// <summary>Playable overlay for the sample scene. All positions/colors can be edited here.</summary>
public sealed class MaskDemoUI : MonoBehaviour
{
    [SerializeField] private MaskAxisCamera mask;
    [SerializeField] private Color purple = new Color(0.64f, 0.39f, 1f);

    private GUIStyle heading;
    private GUIStyle button;
    private GUIStyle caption;
    private GUIStyle small;

    private void Awake()
    {
        if (mask == null) mask = FindAnyObjectByType<MaskAxisCamera>();
    }

    private void EnsureStyles()
    {
        if (heading != null) return;
        heading = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
        heading.normal.textColor = Color.white;
        button = new GUIStyle(GUI.skin.button)
        {
            fontSize = 23, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        button.normal.textColor = Color.white;
        button.hover.textColor = Color.white;
        caption = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        caption.normal.textColor = Color.white;
        small = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        small.normal.textColor = new Color(0.88f, 0.85f, 0.94f);
    }

    private void OnGUI()
    {
        if (mask == null) return;
        EnsureStyles();

        Matrix4x4 previousMatrix = GUI.matrix;
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.TRS(
            new Vector3((Screen.width - 1600f * scale) * .5f,
                (Screen.height - 900f * scale) * .5f, 0f),
            Quaternion.identity, Vector3.one * scale);

        Panel(new Rect(20, 20, 1560, 118), new Color(0.08f, 0.08f, 0.12f, .94f));
        GUI.Label(new Rect(45, 36, 360, 42), mask.MaskEnabled ? "МАСКА" : "БЕЗ МАСКИ", heading);
        GUI.Label(new Rect(46, 88, 560, 32), "WASD — движение     M — надеть / снять маску", small);

        GUI.backgroundColor = mask.MaskEnabled ? purple : new Color(.37f, .27f, .52f);
        if (GUI.Button(new Rect(630, 43, 200, 62), mask.MaskEnabled ? "СНЯТЬ МАСКУ" : "НАДЕТЬ МАСКУ", button))
            mask.ToggleMask();

        string[] labels = { "1  X / -X", "2  Y / -Y", "3  Z / -Z" };
        GUI.enabled = mask.MaskEnabled;
        for (int i = 0; i < labels.Length; i++)
        {
            GUI.backgroundColor = mask.MaskEnabled && (int)mask.CurrentDirection / 2 == i
                ? purple : new Color(.28f, .27f, .33f);
            if (GUI.Button(new Rect(864 + i * 220, 48, 205, 54), labels[i], button))
                SelectAxis(i);
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        Panel(new Rect(360, 816, 880, 64), new Color(.08f, .08f, .12f, .92f));
        GUI.Label(new Rect(370, 822, 860, 50),
            mask.MaskEnabled ? Caption() + "     • 1: ПРОХОД В X/−X" : "СТЕНА БЛОКИРУЕТ ПУТЬ",
            caption);
        GUI.color = purple;
        GUI.Label(new Rect(20, 766, 600, 38), "ОДНА МАСКА — ШЕСТЬ ОБЗОРОВ", caption);
        GUI.color = Color.white;
        GUI.matrix = previousMatrix;
    }

    private void SelectAxis(int index)
    {
        switch (index)
        {
            case 0: mask.SelectX(); break;
            case 1: mask.SelectY(); break;
            case 2: mask.SelectZ(); break;
        }
    }

    private string Caption()
    {
        switch (mask.CurrentDirection)
        {
            case MaskAxisCamera.ViewDirection.Y: return "ВИД СВЕРХУ ВДОЛЬ ОСИ Y";
            case MaskAxisCamera.ViewDirection.MinusY: return "ВИД СНИЗУ ВДОЛЬ ОСИ -Y";
            case MaskAxisCamera.ViewDirection.X: return "ВИД СБОКУ ВДОЛЬ ОСИ X";
            case MaskAxisCamera.ViewDirection.MinusX: return "ВИД СБОКУ ВДОЛЬ ОСИ -X";
            case MaskAxisCamera.ViewDirection.Z: return "ВИД СПЕРЕДИ ВДОЛЬ ОСИ Z";
            default: return "ВИД СЗАДИ ВДОЛЬ ОСИ -Z";
        }
    }

    private static void Panel(Rect area, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
