using System;

namespace Tanglebeep.Audio {
    /// <summary>
    /// Constant-power stereo panning. A pan position in [-1, 1] (-1 hard left, 0 center,
    /// +1 hard right) maps to an angle θ = (pan + 1)·π/4 ∈ [0, π/2]; the left gain is
    /// cos θ and the right gain is sin θ. Because cos²θ + sin²θ = 1 the total power is
    /// constant across the pan sweep (center sits at √½ ≈ 0.707 per side, not 0.5).
    /// </summary>
    public static class PanLaw {
        public static void Compute(double pan, out float left, out float right) {
            if (pan < -1.0) {
                pan = -1.0;
            } else if (pan > 1.0) {
                pan = 1.0;
            }
            double theta = (pan + 1.0) * (Math.PI / 4.0);
            left = (float)Math.Cos(theta);
            right = (float)Math.Sin(theta);
        }
    }
}
