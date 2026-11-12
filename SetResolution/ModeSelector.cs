using System;
using System.Collections.Generic;
using System.Linq;

namespace Westwind.SetResolution
{
    /// <summary>
    /// The display mode asked for by -w/-h, a profile or the Racecab witness file.
    /// </summary>
    public class DisplayRequest
    {
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// 0: not given. 60 Hz is then preferred, but any frequency of the mode is accepted.
        /// </summary>
        public int Frequency { get; set; }

        public int BitCount { get; set; } = 32;
        public Orientation Orientation { get; set; } = Orientation.Default;

        public override string ToString()
        {
            return Width + "x" + Height +
                   (Frequency > 0 ? "@" + Frequency : "") +
                   (Orientation != Orientation.Default ? "," + Orientation : "");
        }
    }

    /// <summary>
    /// Chooses and compares display modes, without touching the display.
    /// </summary>
    public static class ModeSelector
    {
        public const int PreferredFrequency = 60;

        /// <summary>
        /// True when the mode has the requested size and orientation, and
        /// the requested frequency when one was given.
        /// </summary>
        public static bool Matches(DisplaySettings mode, DisplayRequest request)
        {
            return mode != null &&
                   mode.Width == request.Width &&
                   mode.Height == request.Height &&
                   mode.Orientation == request.Orientation &&
                   (request.Frequency <= 0 || mode.Frequency == request.Frequency);
        }

        /// <summary>
        /// True when both modes have the same size, frequency and orientation.
        /// </summary>
        public static bool SameMode(DisplaySettings a, DisplaySettings b)
        {
            return a != null && b != null &&
                   a.Width == b.Width &&
                   a.Height == b.Height &&
                   a.Frequency == b.Frequency &&
                   a.Orientation == b.Orientation;
        }

        /// <summary>
        /// Picks the driver mode to apply: same size, bit count and orientation;
        /// the frequency asked for, otherwise 60 Hz, otherwise the current
        /// frequency, otherwise the frequency nearest to 60 Hz. Null if none.
        /// </summary>
        public static DisplaySettings Find(IEnumerable<DisplaySettings> modes, DisplayRequest request, int currentFrequency = 0)
        {
            var candidates = modes.Where(m => m.Width == request.Width &&
                                              m.Height == request.Height &&
                                              m.BitCount == request.BitCount &&
                                              m.Orientation == request.Orientation)
                                  .ToList();

            if (request.Frequency > 0)
                return candidates.FirstOrDefault(m => m.Frequency == request.Frequency);

            return candidates.FirstOrDefault(m => m.Frequency == PreferredFrequency)
                   ?? (currentFrequency > 0 ? candidates.FirstOrDefault(m => m.Frequency == currentFrequency) : null)
                   ?? candidates.OrderBy(m => Math.Abs(m.Frequency - PreferredFrequency)).FirstOrDefault();
        }
    }
}
