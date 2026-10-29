using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public sealed class HospitalExplorer : MonoBehaviour
{
    public Camera eyes;
    CharacterController controller;
    HospitalInteractable target;
    float pitch, fall;
    string heading, document;
    bool reading;
    GUIStyle titleStyle, bodyStyle, hintStyle;
    void Start() { controller = GetComponent<CharacterController>(); Lock(true); }
    void Lock(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
    public void Read(string title, string text) { heading = title; document = text; reading = true; Lock(false); }
    void Update()
    {
        var k = Keyboard.current; var m = Mouse.current;
        if (k == null || m == null) return;
        if (reading) { if (k.escapeKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame) { reading = false; Lock(true); } return; }
        if (k.escapeKey.wasPressedThisFrame) Lock(false);
        if (Cursor.lockState != CursorLockMode.Locked) { if (m.leftButton.wasPressedThisFrame) Lock(true); return; }
        Vector2 delta = m.delta.ReadValue() * .085f;
        transform.Rotate(0, delta.x, 0); pitch = Mathf.Clamp(pitch - delta.y, -80, 80); eyes.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0);
        float z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
        Vector3 move = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1) * 2.8f;
        fall = controller.isGrounded ? -2 : fall - 18 * Time.deltaTime;
        controller.Move((move + Vector3.up * fall) * Time.deltaTime);
        target = null;
        if (Physics.Raycast(eyes.ViewportPointToRay(new Vector3(.5f,.5f)), out var hit, 3f)) target = hit.collider.GetComponentInParent<HospitalInteractable>();
        if (target && k.eKey.wasPressedThisFrame) target.Activate(this);
    }
    void OnGUI()
    {
        if (titleStyle == null) {
            var font = Font.CreateDynamicFontFromOSFont("Arial", 20);
            titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 26, wordWrap = true, normal = { textColor = new Color(.3f,.9f,.95f) } };
            bodyStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 19, wordWrap = true, normal = { textColor = Color.white } };
            hintStyle = new GUIStyle(bodyStyle) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        }
        GUI.Label(new Rect(15, Screen.height - 42, Screen.width - 30, 32), "WASD — движение  •  Мышь — осмотр  •  E — взаимодействие  •  Esc — курсор", hintStyle);
        if (!reading) {
            GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 25), "+", hintStyle);
            if (target) GUI.Label(new Rect(0, Screen.height * .59f, Screen.width, 35), "[E] " + target.title, hintStyle);
            return;
        }
        float w = Mathf.Min(860, Screen.width - 60), h = Mathf.Min(680, Screen.height - 90);
        var area = new Rect((Screen.width-w)/2, (Screen.height-h)/2, w,h);
        GUI.Box(area, GUIContent.none);
        GUILayout.BeginArea(new Rect(area.x+30,area.y+24,w-60,h-48));
        GUILayout.Label(heading, titleStyle); GUILayout.Space(18); GUILayout.Label(document, bodyStyle);
        GUILayout.FlexibleSpace(); if (GUILayout.Button("Закрыть [E / Esc]", GUILayout.Height(36))) { reading=false; Lock(true); }
        GUILayout.EndArea();
    }
    void OnDisable() { Lock(false); }
}
