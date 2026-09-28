using UnityEngine;
public sealed class MaskLevelArrival : MonoBehaviour
{
    void OnGUI()
    {
        GUI.Box(new Rect(20,20,540,90),"УРОВЕНЬ 2 · СТАРТОВАЯ ПЛОЩАДКА\n\nWASD — ходьба · ПКМ — обзор · M — маска · 1/2/3 — оси");
    }
}
