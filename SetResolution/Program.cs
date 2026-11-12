using System;
using System.IO;
using System.Reflection;
using Westwind.SetResolution.CommandLine;

namespace Westwind.SetResolution
{
    public class Program
    {
        public static string StartupPath { get; set; }

        static int Main(string[] args)
        {
            StartupPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            AppConfiguration.Load();

            var cmdLine = new SetResolutionCommandLineParser();
            cmdLine.Parse();

            // Scripts: no banner with -noprompt, and CURRENT prints a single line.
            bool quiet = cmdLine.NoPrompt || cmdLine.FirstParameter.Equals("CURRENT", StringComparison.OrdinalIgnoreCase);

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var ver = version.Major + "." + version.Minor + (version.Build > 0 ? "." + version.Build : string.Empty);

            if (!quiet)
            {
                string text = $"Set Resolution v{ver}";
                ColorConsole.WriteLine(text, ConsoleColor.Yellow);
                ColorConsole.WriteLine(new string('-', text.Length), ConsoleColor.Yellow);
                ColorConsole.WriteLine($"(c) West Wind Technologies, 2022-{DateTime.Now.Year}", ConsoleColor.DarkGray);
                ColorConsole.WriteLine("Saved modes, CURRENT and RESTORE: (c) Benjamin Maurin (njz3) 2026", ConsoleColor.DarkGray);
            }

            int exitCode = ExitCodes.Success;
            if (args == null || args.Length == 0 || args[0] == "HELP" || args[0] == "/?")
            {

                Console.WriteLine("\nSet Monitor Display Resolution to any available machine display mode.");


                string options = $@"
[cyan]Syntax[/cyan]
------
[yellow]SetResolution  <ProfileName>|SET|LIST|PROFILES|CREATEPROFILE|CURRENT|RESTORE|SAVE
               -w 1920 -h 1080 -f 60 -b 32 -o 0 -p ProfileName[/yellow]

[cyan]Commands[/cyan]
--------
HELP || /?          This help display
<ProfileName>       Apply Display settings from a named Profile
SET                 Sets Display Settings and saves them as the Windows mode -
                    provide either a profile (-p) or display options -w/-h/-f/-b/-o
LIST                Lists all available display modes and monitors
PROFILES            Lists all saved profiles (stored in SetResolution.xml)
CREATEPROFILE       Creates a new profile by specifying name and display options
                    - {Path.Combine(StartupPath,"SetResolution.xml")}
CURRENT             Prints on one line the current mode and the mode saved by Windows
                    (the one it sets back when a full screen game exits)
RESTORE             Sets and saves the mode of -w/-h or -p only if the display or the
                    saved mode differs: no display switch when both are right.
                    Without -w/-h/-p: resolution of {RacecabWitness.DefaultFolder}\Configured_for_<W>x<H>.txt
SAVE                Saves the modes on screen (all monitors) as the Windows modes,
                    without switching the display

[cyan]Display Settings[/cyan]
----------------
-w                  Display Width
-h                  Display Height
-f                  Display Frequency in Hertz (60* - another one if the mode has no 60)
-o                  Orientation - 0 (default*), 1 (90deg), 2 (180deg), 3 (270deg)
-p                  Profile name
-noprompt           Don't prompt for confirmation of new settings, no banner (scripts)
-nopersist, -temp   Don't save the mode: dynamic change only (default saves it)
-dir                Folder of the Configured_for_<W>x<H>.txt witness ({RacecabWitness.DefaultFolder}*)

[cyan]Command Modifiers[/cyan]
-----------------
-m                  Monitor Id  to apply command to (1,2,3 etc - use LIST to see Ids)
                    applies to: LIST, SET, CURRENT, RESTORE, SAVE. If not specified, Default monitor is used.
-la                 List all Display modes (LIST command). Default only shows current matches

[cyan]Exit codes[/cyan]
----------
0                   Done; RESTORE: nothing to change; CURRENT: current = saved mode
1                   CURRENT: current and saved modes differ; RESTORE: mode set again
2                   RESTORE: display already right, only the saved mode was corrected
3                   Wrong command line, unknown profile or no usable witness file
4                   No active display, or unknown -m monitor
5                   No matching display mode
6                   Not confirmed: previous mode set back
7                   Mode on screen but not saved by Windows
9                   Unexpected error
10 - 16             Windows refused the change: RESTART, FAILED, BADMODE, NOTUPDATED,
                    BADFLAGS, BADPARAM, BADDUALVIEW

[cyan]Examples[/cyan]
--------
SetResolution MyProfile
SetResolution SET -p MyProfile -m2
SetResolution SET -w 1920 -h 1080 -f 60 -m2 -noprompt
SetResolution LIST -m2
SetResolution PROFILES
SetResolution CREATEPROFILE -p ""My Profile"" -w 1920 -h 1080 -f 60
SetResolution CURRENT
SetResolution RESTORE -w 640 -h 480 -noprompt
";
                ColorConsole.WriteEmbeddedColorLine(options);
            }
            else
            {
                if (!quiet)
                    Console.WriteLine();

                var processor = new SetResolutionProcessor(cmdLine);
                exitCode = processor.Process();

                if (!quiet)
                    Console.WriteLine();
            }


            AppConfiguration.Save();
            return exitCode;
        }
    }
}
