using System;
using UnityEngine;

namespace MaskResize
{
    [Flags]
    public enum ResizeIconAxes { None = 0, Width = 1, Height = 2, Length = 4, All = 7 }

    public static class ResizeIconLibrary
    {
        static readonly Material[] materials = new Material[8];
        static Mesh quad;
        public static Mesh Quad => quad ? quad : quad = Resources.Load<Mesh>("ResizeIcons/SignQuad");
        public static Material Get(ResizeIconAxes axes)
        {
            int index = (int)axes & 7;
            if (index == 0) return null;
            if (!materials[index]) materials[index] = Resources.Load<Material>("ResizeIcons/Icon" + index);
            return materials[index];
        }
        public static ResizeIconAxes For(ResizableMaskObject target)
        {
            if (!target) return ResizeIconAxes.None;
            Vector3 range = target.MaxSize - target.MinSize;
            return (range.x > .0001f ? ResizeIconAxes.Width : 0) |
                   (range.y > .0001f ? ResizeIconAxes.Height : 0) |
                   (range.z > .0001f ? ResizeIconAxes.Length : 0);
        }
    }
}
