using System;
using HtmlPackager.ConsoleApp;

namespace Westwind.SetResolution
{
    public class SetResolutionCommandLineParser : CommandLineParser
    {

        public int Width { get; set; }

        public int Height { get; set;  }

        public int Frequency { get; set; } = 60;

        /// <summary>
        /// True when the frequency was given (-f or a profile). Otherwise
        /// 60 Hz is preferred but another frequency of the mode is accepted.
        /// </summary>
        public bool FrequencyGiven { get; set; }

        public int BitCount { get; set; } = 32;

        public Orientation Orientation { get; set; } = 0;

        public string Profile { get; set;  }


        public bool Help { get; set;  }

        public bool StartTray { get; set; }

        public bool CreateProfile { get; set; }
        public bool ShowResolutions { get; set; }
        public bool ListAll { get; set; }

        public int MonitorId { get; set; }

        /// <summary>
        /// Don't ask for confirmation
        /// </summary>
        public bool NoPrompt { get; set; }

        /// <summary>
        /// Don't save the mode in the Windows display database (-nopersist or -temp)
        /// </summary>
        public bool NoPersist { get; set; }

        /// <summary>
        /// Folder holding the Racecab witness file Configured_for_WxH.txt (RESTORE without -w/-h)
        /// </summary>
        public string WitnessFolder { get; set; } = RacecabWitness.DefaultFolder;


        public SetResolutionCommandLineParser(string[] args = null, string cmdLine = null)
            : base(args, cmdLine)
        {
        }

        public override void Parse()
        {
            Width = ParseIntParameterSwitch("-w", 0);
            Height = ParseIntParameterSwitch("-h", 0);
            int frequency = ParseIntParameterSwitch("-f", 0);
            FrequencyGiven = frequency > 0;
            Frequency = FrequencyGiven ? frequency : 60;
            BitCount = ParseIntParameterSwitch("-b", 32);
            int or = ParseIntParameterSwitch("-o", 0);
            Orientation = (Orientation) or;
            ListAll = ParseParameterSwitch("-la");

            Profile = ParseStringParameterSwitch("-p");
            MonitorId = ParseIntParameterSwitch("-m");

            NoPrompt = ParseParameterSwitch("-noprompt");
            NoPersist = ParseParameterSwitch("-nopersist") || ParseParameterSwitch("-temp");
            WitnessFolder = ParseStringParameterSwitch("-dir", RacecabWitness.DefaultFolder);
            Help = ParseParameterSwitch("-h");
        }


    }
}
