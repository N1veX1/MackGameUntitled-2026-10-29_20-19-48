using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MaskInversionGate))]
[CanEditMultipleObjects]
public sealed class GateInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var kind = serializedObject.FindProperty("kind");
        EditorGUILayout.PropertyField(kind, new GUIContent("Тип: стена / мост"));
        var state = serializedObject.FindProperty("existsWithMaskOn");
        bool wall = kind.enumValueIndex == (int)MaskInversionGate.GateKind.Wall;
        bool visibleWithMask = wall ? !state.boolValue : state.boolValue;
        var label = new GUIContent("Когда объект виден");
        var rect = EditorGUILayout.GetControlRect();
        EditorGUI.BeginProperty(rect, label, state);
        EditorGUI.showMixedValue = state.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int value = EditorGUI.Popup(rect, label.text, visibleWithMask ? 1 : 0, new[] { "Без маски", "В маске" });
        if (EditorGUI.EndChangeCheck()) state.boolValue = wall ? value == 0 : value == 1;
        EditorGUI.showMixedValue = false; EditorGUI.EndProperty();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("player"), new GUIContent("Игрок (можно оставить пустым)"));
        if (wall) EditorGUILayout.PropertyField(serializedObject.FindProperty("wallContactMargin"), new GUIContent("Допуск контакта при возвращении"));
        EditorGUILayout.HelpBox("Видимость и коллайдеры переключаются вместе. Заменяйте внешний вид дочерними объектами, сохраняя компонент на корне.", MessageType.Info);
        serializedObject.ApplyModifiedProperties();
    }
}
