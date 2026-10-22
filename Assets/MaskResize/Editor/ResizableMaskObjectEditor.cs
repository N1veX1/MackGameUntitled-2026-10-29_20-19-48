using UnityEditor;
using UnityEngine;

namespace MaskResize.Editor
{
    [CustomEditor(typeof(ResizableMaskObject)), CanEditMultipleObjects]
    public sealed class ResizableMaskObjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Размер в метрах: X — ширина (3), Y — высота (2), Z — длина (1).\nПоворачивайте и переносите корень. Scale корня и родителей оставьте (1, 1, 1).", MessageType.Info);
            Field("solid", "Геометрия / Solid");
            Field("size", "Текущий размер");
            Field("initialSize", "Исходный размер");
            Field("minSize", "Минимальный размер");
            Field("maxSize", "Максимальный размер");
            Field("resizeSmoothSeconds", "Плавность изменения (сек.)");
            Field("maxResizeSpeed", "Скорость изменения (м/сек.)");
            Field("anchorNormalized", "Закреплённая точка");
            EditorGUILayout.HelpBox("Якорь: −0.5 — нижняя/левая/задняя грань; +0.5 — верхняя/правая/передняя. 0 — центр.\nМост: (0, 0.5, −0.5). Подъёмная платформа: (0, −0.5, 0).", MessageType.None);
            Field("blockingLayers", "Слои препятствий");
            Field("contactTolerance", "Допуск соприкосновения");
            Field("standingTolerance", "Допуск ног над платформой");
            serializedObject.ApplyModifiedProperties();
        }
        void Field(string field, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(field), new GUIContent(label));
    }
}
