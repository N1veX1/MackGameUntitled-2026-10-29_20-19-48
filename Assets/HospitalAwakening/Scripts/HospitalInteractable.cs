using UnityEngine;

public sealed class HospitalInteractable : MonoBehaviour
{
    public enum ActionType { Document, Glass, Door }
    public ActionType action;
    public string title;
    [TextArea(5, 25)] public string body;
    public Renderer[] glassPanels;
    public Material clearGlass, privacyGlass;
    public Transform doorLeaf;
    public bool privacyEnabled;
    bool doorOpen;
    Vector3 closedPosition;
    void Awake() { if (doorLeaf) closedPosition = doorLeaf.localPosition; }
    public void Activate(HospitalExplorer explorer)
    {
        if (action == ActionType.Document) explorer.Read(title, body);
        if (action == ActionType.Glass) {
            privacyEnabled = !privacyEnabled;
            foreach (var panel in glassPanels) if (panel) panel.sharedMaterial = privacyEnabled ? privacyGlass : clearGlass;
        }
        if (action == ActionType.Door && doorLeaf) {
            doorOpen = !doorOpen;
            doorLeaf.localPosition = closedPosition + (doorOpen ? Vector3.right * 1.55f : Vector3.zero);
        }
    }
}
