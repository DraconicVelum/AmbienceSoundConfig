using System;

namespace AmbienceSoundConfig
{
    // Coordinates are in the chosen host's local units, measured from its top.
    // No assumptions about the width of a native toggle/slider row.
    internal static class AudioMenuLayout
    {
        internal struct Area
        {
            internal float Width, Height, Top;
        }

        internal static bool TryCalculate(float hostWidth, float hostHeight, float controlsBottom, float buttonsTop, out Area area)
        {
            area = default;
            if (!Finite(hostWidth) || !Finite(hostHeight) || !Finite(controlsBottom) || !Finite(buttonsTop)) return false;
            float top = Math.Max(12f, controlsBottom + 12f);
            float bottom = Math.Min(hostHeight - 12f, buttonsTop - 12f);
            float width = Math.Min(760f, hostWidth - 32f);
            float height = Math.Min(320f, bottom - top);
            if (width < 280f || height < 40f) return false;
            area = new Area { Width = width, Height = height, Top = top };
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
