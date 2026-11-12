using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Westwind.SetResolution.CommandLine;
using static Westwind.SetResolution.DisplayManagerNative;

namespace Westwind.SetResolution
{
    public class SetResolutionProcessor
    {
        private SetResolutionCommandLineParser CommandLine { get; }

        public SetResolutionProcessor(SetResolutionCommandLineParser commandLine)
        {
            CommandLine= commandLine;
        }

        /// <summary>
        /// Runs the command and returns the exit code (see <see cref="ExitCodes"/>).
        /// </summary>
        public int Process()
        {
            string command = CommandLine.FirstParameter;
            try
            {
                if (Is(command, "Set"))
                    return SetResolution();

                if (Is(command, "Current"))
                    return ShowCurrent();

                if (Is(command, "Restore"))
                    return Restore();

                if (Is(command, "Save"))
                    return SaveActive();

                if (Is(command, "List"))
                    return ListDisplayModes(CommandLine.ListAll);

                if (Is(command, "Profiles"))
                {
                    ListProfiles();
                    return ExitCodes.Success;
                }

                if (Is(command, "CreateProfile"))
                    return CreateProfile();

                // No Action command (help is handled on startup)
                if (command.StartsWith("-"))
                {
                    // assume we're using SetResolution with parameters
                    return SetResolution();
                }

                // Launch Profile by name
                if (AppConfiguration.Current.Profiles.Any(pro => pro.Name.Equals(command, StringComparison.OrdinalIgnoreCase)))
                {
                    CommandLine.Profile = command;
                    return SetResolution();
                }

                ColorConsole.WriteError($"Unknown command or profile: {command}");
                return ExitCodes.BadArguments;
            }
            catch (Exception ex)
            {
                ColorConsole.WriteError("Unexpected error: " + ex.Message);
                return ExitCodes.UnexpectedError;
            }
        }

        private static bool Is(string command, string name)
        {
            return command.Equals(name, StringComparison.OrdinalIgnoreCase);
        }

        private int CreateProfile()
        {
            if (string.IsNullOrEmpty(CommandLine.Profile))
            {
                ColorConsole.WriteError("You have to specify a profile to create.");
                return ExitCodes.BadArguments;
            }

            if (CommandLine.Width == 0 || CommandLine.Height == 0)
            {
                ColorConsole.WriteError("Please specify at minimum a Width and Height for the profile.");
                return ExitCodes.BadArguments;
            }

            var profile = new DisplayProfile()
            {
                Name = CommandLine.Profile,
                Width = CommandLine.Width,
                Height = CommandLine.Height,
                Frequency = CommandLine.Frequency,
                BitSize = CommandLine.BitCount,
                Orientation = CommandLine.Orientation,
            };

            var set = AppConfiguration.Current.Profiles.FirstOrDefault(p =>
                p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase));
            if (set != null)
                AppConfiguration.Current.Profiles.Remove(set);

            AppConfiguration.Current.Profiles.Add(profile);
            AppConfiguration.Save();

            ColorConsole.WriteSuccess($"Created new Profile: {profile.Name}. {profile}");

            ListProfiles();
            return ExitCodes.Success;
        }

        /// <summary>
        /// SET, -w/-h or a profile: sets the mode and, unless -temp, saves it.
        /// </summary>
        private int SetResolution()
        {
            var monitor = SelectMonitor(out int error);
            if (monitor == null)
                return error;

            var request = BuildRequest(false, out error);
            if (request == null)
                return error;

            var previous = DisplayManager.GetCurrentDisplaySetting(monitor.DriverDeviceName);
            var set = FindMode(monitor, request, previous, out error);
            if (set == null)
                return error;

            int result = Apply(monitor, set);
            if (result != ExitCodes.Success)
                return result;

            ColorConsole.WriteEmbeddedColorLine($"Switched Display Mode on Monitor [green]{monitor.DisplayName}[/green] to:\n[green]{set.ToString()}[/green]" +
                                                (CommandLine.NoPersist ? " (not saved)" : ""));

            return Confirm(monitor, previous);
        }

        /// <summary>
        /// CURRENT: one line with the current and the saved mode.
        /// </summary>
        private int ShowCurrent()
        {
            var monitor = SelectMonitor(out int error);
            if (monitor == null)
                return error;

            var current = DisplayManager.GetCurrentDisplaySetting(monitor.DriverDeviceName);
            var saved = DisplayManager.GetRegistryDisplaySetting(monitor.DriverDeviceName);
            bool same = ModeSelector.SameMode(current, saved);

            Console.WriteLine($"current={current.Describe()} registry={(saved == null ? "none" : saved.Describe())} " +
                              $"{(same ? "same" : "different")} monitor={monitor.Index}");

            return same ? ExitCodes.Success : ExitCodes.Different;
        }

        /// <summary>
        /// RESTORE: puts the mode back only if the display or the saved mode is wrong.
        /// </summary>
        private int Restore()
        {
            var monitor = SelectMonitor(out int error);
            if (monitor == null)
                return error;

            var request = BuildRequest(true, out error);
            if (request == null)
                return error;

            string device = monitor.DriverDeviceName;
            var current = DisplayManager.GetCurrentDisplaySetting(device);
            bool currentRight = ModeSelector.Matches(current, request);

            if (currentRight && (CommandLine.NoPersist || ModeSelector.Matches(DisplayManager.GetRegistryDisplaySetting(device), request)))
            {
                Console.WriteLine($"Display Mode of Monitor {monitor.DisplayName} already {current.Describe()}" +
                                  (CommandLine.NoPersist ? "" : " and saved") + ": nothing changed.");
                return ExitCodes.Success;
            }

            if (currentRight)
            {
                // Only the saved mode is wrong: save the modes on screen without switching the display.
                // (ChangeDisplaySettingsEx with CDS_UPDATEREGISTRY | CDS_NORESET does not change the saved mode.)
                var saved = DisplayManager.GetRegistryDisplaySetting(device);
                int saveError = DisplayManager.SaveActiveConfigToDatabase();
                var nowSaved = DisplayManager.GetRegistryDisplaySetting(device);
                if (saveError != 0 || !ModeSelector.SameMode(nowSaved, current))
                {
                    ColorConsole.WriteError($"The display is in {current.Describe()} but Windows did not save it " +
                                            $"(saved: {(nowSaved == null ? "none" : nowSaved.Describe())}, SetDisplayConfig: {saveError}). " +
                                            "Windows may set another mode when a full screen game exits.");
                    return ExitCodes.NotSaved;
                }

                ColorConsole.WriteSuccess($"Saved Display Mode {current.Describe()} of Monitor {monitor.DisplayName} " +
                                          $"(was saved as {(saved == null ? "none" : saved.Describe())}), display not switched.");
                return ExitCodes.SavedOnly;
            }

            var set = FindMode(monitor, request, current, out error);
            if (set == null)
                return error;

            int applied = Apply(monitor, set);
            if (applied != ExitCodes.Success)
                return applied;

            ColorConsole.WriteSuccess($"Restored Display Mode {set.Describe()} on Monitor {monitor.DisplayName} (was {current.Describe()})" +
                                      (CommandLine.NoPersist ? ", not saved." : "."));

            int confirmed = Confirm(monitor, current);
            return confirmed == ExitCodes.Success ? ExitCodes.Restored : confirmed;
        }

        /// <summary>
        /// SAVE: saves the display configuration as it is on screen (all monitors)
        /// with SetDisplayConfig, without switching, then checks the monitor.
        /// </summary>
        private int SaveActive()
        {
            var monitor = SelectMonitor(out int error);
            if (monitor == null)
                return error;

            var before = DisplayManager.GetRegistryDisplaySetting(monitor.DriverDeviceName);
            error = DisplayManager.SaveActiveConfigToDatabase();
            var current = DisplayManager.GetCurrentDisplaySetting(monitor.DriverDeviceName);
            var saved = DisplayManager.GetRegistryDisplaySetting(monitor.DriverDeviceName);

            if (error != 0 || !ModeSelector.SameMode(current, saved))
            {
                ColorConsole.WriteError($"Windows did not save the display configuration (SetDisplayConfig: {error}; " +
                                        $"current={current.Describe()} registry={(saved == null ? "none" : saved.Describe())}).");
                return ExitCodes.NotSaved;
            }

            ColorConsole.WriteSuccess($"Saved the display configuration: Monitor {monitor.DisplayName} {saved.Describe()} " +
                                      $"(was saved as {(before == null ? "none" : before.Describe())}).");
            return ExitCodes.Success;
        }

        /// <summary>
        /// The monitor of -m, else the main one. Null, with the exit code, when there is none.
        /// </summary>
        private DisplayManager.DisplayDevice SelectMonitor(out int error)
        {
            var devices = DisplayManager.GetAllDisplayDevices();
            if (devices.Count == 0)
            {
                ColorConsole.WriteError("No active display: Windows reports no monitor attached to the desktop " +
                                        "(session without display, such as a service or a remote shell?).");
                error = ExitCodes.NoDisplay;
                return null;
            }

            var monitor = devices.FirstOrDefault(d => d.IsSelected) ?? devices[0];  // main monitor
            if (CommandLine.MonitorId > 0)
            {
                monitor = devices.FirstOrDefault(d => d.Index == CommandLine.MonitorId);
                if (monitor == null)
                {
                    ColorConsole.WriteError($"Monitor {CommandLine.MonitorId} not found: there are {devices.Count} active monitors (see LIST).");
                    error = ExitCodes.NoDisplay;
                    return null;
                }
            }

            devices.ForEach(d => d.IsSelected = d == monitor);
            _devices = devices;
            error = ExitCodes.Success;
            return monitor;
        }

        /// <summary>Monitors found by the last <see cref="SelectMonitor"/>.</summary>
        private List<DisplayManager.DisplayDevice> _devices = new List<DisplayManager.DisplayDevice>();

        /// <summary>
        /// The mode asked for by -w/-h or a profile, or, for RESTORE without them,
        /// by the Racecab witness file. Null, with the exit code, when there is none.
        /// </summary>
        private DisplayRequest BuildRequest(bool allowWitness, out int error)
        {
            error = ExitCodes.BadArguments;

            if (!string.IsNullOrEmpty(CommandLine.Profile))
            {
                var profile = AppConfiguration.Current.Profiles.FirstOrDefault(p=> p.Name.Equals(CommandLine.Profile, StringComparison.OrdinalIgnoreCase));
                if (profile == null)
                {
                    ColorConsole.WriteError($"Couldn't find Display Profile {CommandLine.Profile}");
                    Console.WriteLine();
                    ListProfiles();
                    return null;
                }

                profile.UpdateCommandLine(CommandLine);
            }

            var request = new DisplayRequest
            {
                Width = CommandLine.Width,
                Height = CommandLine.Height,
                Frequency = CommandLine.FrequencyGiven ? CommandLine.Frequency : 0,
                BitCount = CommandLine.BitCount,
                Orientation = CommandLine.Orientation
            };

            if (request.Width > 0 && request.Height > 0)
            {
                error = ExitCodes.Success;
                return request;
            }

            if (!allowWitness || request.Width != 0 || request.Height != 0)
            {
                ColorConsole.WriteError("Please specify at minimum Width and Height parameters.");
                return null;
            }

            var witness = RacecabWitness.FromFolder(CommandLine.WitnessFolder, out string file, out string witnessError);
            if (witness == null)
            {
                ColorConsole.WriteError(witnessError);
                return null;
            }

            request.Width = witness.Width;
            request.Height = witness.Height;
            Console.WriteLine($"Resolution {request} read from {file}");
            error = ExitCodes.Success;
            return request;
        }

        /// <summary>
        /// The driver mode for the request. Null, with the exit code, when there is none.
        /// </summary>
        private DisplaySettings FindMode(DisplayManager.DisplayDevice monitor, DisplayRequest request, DisplaySettings current, out int error)
        {
            var modes = DisplayManager.GetAllDisplaySettings(monitor.DriverDeviceName);
            var set = ModeSelector.Find(modes, request, current?.Frequency ?? 0);

            if (set == null)
            {
                ColorConsole.WriteError($"Couldn't find a matching Display Mode for {request} ({request.BitCount}bit) on Monitor {monitor.DisplayName}.");
                Console.WriteLine();
                ListDisplayModes();
                error = ExitCodes.ModeNotFound;
                return null;
            }

            if (request.Frequency <= 0 && set.Frequency != ModeSelector.PreferredFrequency)
                ColorConsole.WriteWarning($"No {ModeSelector.PreferredFrequency} Hz mode for {request}: using {set.Frequency} Hz.");

            error = ExitCodes.Success;
            return set;
        }

        /// <summary>
        /// Sets the mode and, unless -temp, checks that Windows saved it. Returns the exit code.
        /// </summary>
        private int Apply(DisplayManager.DisplayDevice monitor, DisplaySettings set)
        {
            uint flags = CommandLine.NoPersist ? 0u : CDS_UPDATEREGISTRY;
            var result = DisplayManager.ChangeMode(set, monitor.DriverDeviceName, flags);
            if (result != DisplayChangeResult.Successful)
            {
                ColorConsole.WriteError($"Unable to set Display Mode to {set}\nError: {DisplayManager.Describe(result)}");
                return ExitCodes.FromDisplayChange(result);
            }

            if (CommandLine.NoPersist)
                return ExitCodes.Success;

            return CheckSaved(monitor, set);
        }

        /// <summary>
        /// Checks that Windows saved the mode; if not, saves the active display
        /// configuration with SetDisplayConfig and checks again.
        /// </summary>
        private int CheckSaved(DisplayManager.DisplayDevice monitor, DisplaySettings set)
        {
            var saved = DisplayManager.GetRegistryDisplaySetting(monitor.DriverDeviceName);
            if (ModeSelector.SameMode(saved, set))
                return ExitCodes.Success;

            ColorConsole.WriteWarning($"Windows saved {(saved == null ? "no mode" : saved.Describe())} instead of {set.Describe()}: " +
                                      "saving the active display configuration (SetDisplayConfig).");
            int error = DisplayManager.SaveActiveConfigToDatabase();
            saved = DisplayManager.GetRegistryDisplaySetting(monitor.DriverDeviceName);
            if (error == 0 && ModeSelector.SameMode(saved, set))
                return ExitCodes.Success;

            ColorConsole.WriteError($"The display is in {set.Describe()} but Windows did not save it " +
                                    $"(saved: {(saved == null ? "none" : saved.Describe())}, SetDisplayConfig: {error}). " +
                                    "Windows may set another mode when a full screen game exits.");
            return ExitCodes.NotSaved;
        }

        /// <summary>
        /// Without -noprompt, waits 5 seconds for a key and otherwise sets the previous mode back.
        /// </summary>
        private int Confirm(DisplayManager.DisplayDevice monitor, DisplaySettings previous)
        {
            if (CommandLine.NoPrompt)
                return ExitCodes.Success;

            ColorConsole.WriteLine("\npress any key to confirm new resolution within 5 seconds.",ConsoleColor.Yellow);
            bool keyPressed = false;
            if (!Console.IsInputRedirected)
            {
                for (int i = 0; i < 55; i++)
                {
                    Thread.Sleep(100);
                    if (Console.KeyAvailable)
                    {
                        Console.ReadKey(true);
                        keyPressed = true;
                        break;
                    }
                }
            }

            if (keyPressed)
            {
                ColorConsole.WriteSuccess("Successfully changed resolution.");
                return ExitCodes.Success;
            }

            ColorConsole.WriteWarning("No key pressed: Resetting Display Mode to previous settings.");
            previous.NoPersist = CommandLine.NoPersist;
            DisplayManager.SetDisplaySettings(previous, deviceName: monitor.DriverDeviceName);
            return ExitCodes.NotConfirmed;
        }

        private int ListDisplayModes(bool showAll = false)
        {
            string text;
            var monitor = SelectMonitor(out int error);
            if (monitor == null)
                return error;

            var devices = _devices;
            var displayModes = DisplayManager.GetAllDisplaySettings(monitor.DriverDeviceName);
            var current = DisplayManager.GetCurrentDisplaySetting(monitor.DriverDeviceName);

            ColorConsole.WriteLine("Available Monitors", ConsoleColor.Yellow);
            ColorConsole.WriteLine("------------------", ConsoleColor.Yellow);
            foreach (var device in devices)
            {
                if (device.IsSelected)
                    ColorConsole.WriteLine(device.ToString(), ConsoleColor.Green);
                else
                    Console.WriteLine(device);
            }
            Console.WriteLine();


            IList<DisplaySettings> filtered = displayModes;
            if (!CommandLine.ListAll)
            {
                filtered = displayModes.Where(d =>
                        d.Width >= AppConfiguration.Current.MinResolutionWidth &&
                        d.Frequency == current.Frequency &&
                        d.Orientation == current.Orientation)
                    .OrderByDescending(d=> d.Width)
                    // unique
                    .GroupBy(d => new {d.Width, d.Height, d.Frequency, d.Orientation})
                    .Select(g => g.First())
                    .ToList();
            }
            else
            {
                filtered = filtered.OrderByDescending(d => d.Width).ToList();
            }

            text = $"Available Display Modes ({filtered.Count})" ;
            ColorConsole.WriteLine(text, ConsoleColor.Yellow);
            ColorConsole.WriteLine(new string('-', text.Length), ConsoleColor.Yellow);


            foreach (var set in filtered)
            {
                if (set.Equals(current))
                    ColorConsole.WriteLine(set.ToString(!CommandLine.ListAll) + " *", ConsoleColor.Green);
                else
                    Console.WriteLine(set.ToString(!CommandLine.ListAll));
            }

            return ExitCodes.Success;
        }

        private void ListProfiles()
        {
            ColorConsole.WriteLine("Available Profiles", ConsoleColor.Yellow);
            ColorConsole.WriteLine("-----------------------", ConsoleColor.Yellow);


            foreach (var profile in AppConfiguration.Current.Profiles)
            {
                Console.WriteLine(profile.ToString());
            }
        }
    }
}
