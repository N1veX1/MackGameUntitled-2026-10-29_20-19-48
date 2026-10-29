# Детали заброшенного комплекса

Сцена: Assets/Scenes/Level01_Tutorial.unity.

В Hierarchy найдите **Abandoned complex details**:
- **Instructions / editable TextMesh** — X на левой стене, Y на полу, Z перед левым поворотом. Текст меняется в компоненте Text Mesh у дочерних объектов Axis heading, Heading и Explanation.
- **Old pipes and cable trays / no colliders** — трубы, шкаф и крепления.
- **Dim service lights** — яркость, цвет и радиус отдельных светильников.
- **Sparse floating dust** — объёмы пыли.
- Компонент **Mask Ambient Details** на корне: Enable Dust, Dust Amount, Light Variation. Свет меняется медленно, без резких вспышек.

Материалы и текстуры находятся в Assets/MaskEnvironment. AgedConcrete общий у препятствий и соседних поверхностей. Это сохраняет одинаковый цвет при слиянии.

На Level 1 Tutorial Controller параметр Instructions On Surfaces скрывает большие экранные объяснения. Кнопки маски остаются доступны.

Декор не имеет Collider и не находится внутри препятствий. Сохраняйте открытый потолок над Y и проход к порталу при дальнейших изменениях.

Tools → Mask Demo → Refresh Abandoned Details пересоздаёт группу декора в открытой сцене Level01_Tutorial. Ручные изменения внутри этой группы будут заменены; геометрия уровня и механика испытаний сохраняются. После команды сохраните сцену. Для обычной настройки пересоздание не требуется.
