namespace Westwind.SetResolution
{
    /// <summary>
    /// Exit codes. 0 to 2 are successes, 3 and above are failures.
    /// </summary>
    public static class ExitCodes
    {
        /// <summary>Done. RESTORE: nothing to change. CURRENT: current and saved modes are the same.</summary>
        public const int Success = 0;

        /// <summary>CURRENT: the current and saved modes differ (or no saved mode).</summary>
        public const int Different = 1;

        /// <summary>RESTORE: the mode was set again on the display, and saved.</summary>
        public const int Restored = 1;

        /// <summary>RESTORE: the display was already right, only the saved mode was corrected.</summary>
        public const int SavedOnly = 2;

        /// <summary>Wrong command line, unknown profile, or no usable witness file.</summary>
        public const int BadArguments = 3;

        /// <summary>No active display (session without display) or unknown -m monitor.</summary>
        public const int NoDisplay = 4;

        /// <summary>The driver has no matching display mode.</summary>
        public const int ModeNotFound = 5;

        /// <summary>Not confirmed at the prompt: the previous mode was set back.</summary>
        public const int NotConfirmed = 6;

        /// <summary>The mode is on the display but Windows did not save it.</summary>
        public const int NotSaved = 7;

        /// <summary>Unexpected error.</summary>
        public const int UnexpectedError = 9;

        /// <summary>
        /// 10: DISP_CHANGE_RESTART (Windows must restart for the mode),
        /// 11 to 16: DISP_CHANGE_FAILED, BADMODE, NOTUPDATED, BADFLAGS, BADPARAM, BADDUALVIEW.
        /// </summary>
        internal static int FromDisplayChange(DisplayChangeResult result)
        {
            if (result == DisplayChangeResult.Successful)
                return Success;
            if (result == DisplayChangeResult.Restart)
                return 10;
            return 10 - (int)result;
        }
    }
}
