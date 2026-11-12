using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Westwind.SetResolution
{
    /// <summary>
    /// Reads the resolution chosen for a Racecab image from the name of its
    /// witness file, C:\Racecab\Configured_for_640x480.txt for instance
    /// (written by Build-Config, which deletes the previous one).
    /// </summary>
    public static class RacecabWitness
    {
        public const string DefaultFolder = @"C:\Racecab";

        private static readonly Regex FileNamePattern = new Regex(
            @"^Configured_for_?(\d+)x(\d+)\.txt$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Resolution given by the witness files of a folder, or null with the reason in <paramref name="error"/>.
        /// </summary>
        public static DisplayRequest FromFolder(string folder, out string file, out string error)
        {
            file = null;
            if (!Directory.Exists(folder))
            {
                error = $"No -w/-h given and the Racecab folder {folder} does not exist.";
                return null;
            }

            return FromFileNames(Directory.GetFiles(folder, "Configured_for*.txt"), folder, out file, out error);
        }

        /// <summary>
        /// Resolution given by witness file names. Fails when there is none, or when several
        /// give different resolutions: a wrong mode can be out of range for a CRT.
        /// </summary>
        public static DisplayRequest FromFileNames(IEnumerable<string> paths, string folder, out string file, out string error)
        {
            var found = paths.Select(p => new { Path = p, Match = FileNamePattern.Match(Path.GetFileName(p)) })
                             .Where(f => f.Match.Success)
                             .Select(f => new
                             {
                                 f.Path,
                                 Width = int.Parse(f.Match.Groups[1].Value),
                                 Height = int.Parse(f.Match.Groups[2].Value)
                             })
                             .ToList();

            file = null;
            if (found.Count == 0)
            {
                error = $"No -w/-h given and no witness file Configured_for_<width>x<height>.txt in {folder}.";
                return null;
            }

            var sizes = found.Select(f => f.Width + "x" + f.Height).Distinct().ToList();
            if (sizes.Count > 1)
            {
                error = $"Several witness files in {folder} give different resolutions ({string.Join(", ", sizes)}): nothing changed.";
                return null;
            }

            error = null;
            file = found[0].Path;
            return new DisplayRequest { Width = found[0].Width, Height = found[0].Height };
        }
    }
}
