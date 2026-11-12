using System;
using System.Collections.Generic;

namespace HtmlPackager.ConsoleApp
{
    /// <summary>
    /// Basic Command Line Parser class that can deal with simple
    /// switch based command line arguments
    ///
    /// supports: FirstParm (first commandline argument)
    /// -p String, -pString or -p"String"
    /// -f     switch/flag parameters
    ///
    /// Switches are looked up in the arguments only, never in the path of
    /// the executable, and a flag must be a whole argument.
    /// </summary>
    public abstract class CommandLineParser
    {
        /// <summary>
        /// The Command Line arguments string array.
        /// Note unlike Environment.GetCommandLineArguments() this
        /// holds only the arguments and not the executable
        /// </summary>
        public string[] Arguments { get; set; }

        /// <summary>
        /// The full command line including the executable
        /// </summary>
        public string CommandLine { get; set; }

        /// <summary>
        /// The first argument (if any). Useful for a
        /// command/action parameter
        /// </summary>
        public string FirstParameter { get; set; }

        public CommandLineParser(string[] args = null, string cmdLine = null)
        {
            if (string.IsNullOrEmpty(cmdLine))
                CommandLine = Environment.CommandLine;
            else
                CommandLine = cmdLine;

            if (args == null)
                args = Environment.GetCommandLineArgs();

            List<string> argList = new List<string>(args);

            if (argList.Count > 1)
            {
                FirstParameter = argList[1];

                // argument array contains startup exe - remove
                argList.RemoveAt(0);
                Arguments = argList.ToArray();
            }
            else
            {
                FirstParameter = string.Empty;
                // empty array - not null to match args array
                Arguments = new string[0];
            }
        }

        /// <summary>
        /// Override to provide parse switches\parameter
        /// into object structure
        /// </summary>
        public abstract void Parse();


        /// <summary>
        /// Parses a string Parameter switch in the format of:
        ///
        /// -p "c:\temp files\somefile.txt"
        /// -p"c:\temp files\somefile.txt"
        /// -pc:\somefile.txt
        /// </summary>
        /// <param name="parm">parameter switch key</param>
        /// <param name="nonMatchingValue">Value to return if no match is found</param>
        /// <returns>Match or non-matching value</returns>
        protected string ParseStringParameterSwitch(string parm, string nonMatchingValue = null)
        {
            // value in the next argument: -p MyProfile
            for (int i = 0; i < Arguments.Length; i++)
            {
                if (!Arguments[i].Equals(parm, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (i + 1 < Arguments.Length && !IsSwitch(Arguments[i + 1]))
                    return Arguments[i + 1].Trim('"', '\'');

                return nonMatchingValue;
            }

            // value attached to the switch: -m2, -p"My Profile"
            foreach (string arg in Arguments)
            {
                if (arg.Length > parm.Length && arg.StartsWith(parm, StringComparison.OrdinalIgnoreCase))
                    return arg.Substring(parm.Length).Trim('"', '\'');
            }

            return nonMatchingValue;
        }

        protected int ParseIntParameterSwitch(string parm, int failedValue = -1)
        {
            string val = ParseStringParameterSwitch(parm);
            int res = failedValue;
            if (!int.TryParse(val, out res))
                res = failedValue;

            return res;
        }

        protected bool ParseParameterSwitch(string parm)
        {
            foreach (string arg in Arguments)
            {
                if (arg.Equals(parm, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsSwitch(string arg)
        {
            return arg.StartsWith("-") && !int.TryParse(arg, out _);
        }
    }
}
