/*
 * Based on original code from:
 *
 * (c) Mohammad Elsheimy
 * Changing Display Settings Programmatically
 *
 * https://www.c-sharpcorner.com/uploadfile/GemingLeader/changing-display-settings-programmatically/
 *
 * Added support for:
 *
 * * Listing Monitors and Monitor specific display modes
 * * Set display mode for a specific monitor/driver
 *
*/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static Westwind.SetResolution.DisplayManagerNative;

namespace Westwind.SetResolution
{
    /// <summary>
    /// Represents a wrapper to the native methods.
    /// </summary>
    public static class DisplayManager
    {
        /// <summary>
        /// Returns a DisplaySettings object encapsulates the current display settings.
        /// </summary>
        public static DisplaySettings GetCurrentSettings(string deviceName = null)
        {
            return CreateDisplaySettingsObject(-1, GetDeviceMode(deviceName));
        }

        /// <summary>
        /// Changes the current display settings with the new settings provided. May throw InvalidOperationException if failed. Check the exception message for more details.
        /// </summary>
        /// <param name="set">The new settings.</param>
        /// <remarks>
        /// Internally calls ChangeDisplaySettings() native function.
        /// </remarks>
        public static void SetDisplaySettings(DisplaySettings set, string deviceName = null)
        {
            // force to persist settings in registry
            DisplayChangeResult result = ChangeMode(set, deviceName, set.NoPersist ? 0u : CDS_UPDATEREGISTRY);

            string msg = Describe(result);
            if (msg != null)
                throw new InvalidOperationException(msg);
        }

        /// <summary>
        /// Applies a mode and returns the answer of Windows.
        /// </summary>
        /// <param name="flags">0: dynamic change only; CDS_UPDATEREGISTRY: also saved.</param>
        /// <remarks>The interlaced flag is not passed: the driver chooses, as before.</remarks>
        internal static DisplayChangeResult ChangeMode(DisplaySettings set, string deviceName, uint flags)
        {
            DisplayManagerNative.DEVMODE mode = GetDeviceMode(deviceName);

            mode.dmPelsWidth = (uint)set.Width;
            mode.dmPelsHeight = (uint)set.Height;
            mode.dmDisplayOrientation = (uint)set.Orientation;
            mode.dmBitsPerPel = (uint)set.BitCount;
            mode.dmDisplayFrequency = (uint)set.Frequency;
            mode.dmFields = DmFlags.DM_PELSWIDTH | DmFlags.DM_PELSHEIGHT | DmFlags.DM_DISPLAYFREQUENCY | DmFlags.DM_BITSPERPEL | DmFlags.DM_DISPLAYORIENTATION;

            return (DisplayChangeResult)DisplayManagerNative.ChangeDisplaySettingsEx(deviceName, ref mode, IntPtr.Zero, flags, IntPtr.Zero);
        }

        /// <summary>
        /// Returns the mode saved by Windows for the display, the one it restores
        /// when a full screen game exits, or null when there is none.
        /// </summary>
        public static DisplaySettings GetRegistryDisplaySetting(string deviceName = null)
        {
            var mode = new DisplayManagerNative.DEVMODE();
            mode.Initialize();

            if (!DisplayManagerNative.EnumDisplaySettings(DisplayManagerNative.ToLPTStr(deviceName), ENUM_REGISTRY_SETTINGS, ref mode))
                return null;

            return CreateDisplaySettingsObject(-2, mode);
        }

        /// <summary>
        /// Saves the active display configuration (topology and modes of all
        /// displays, as they are on screen) in the Windows display database
        /// with SetDisplayConfig. Returns 0 or a Win32 error code.
        /// </summary>
        public static int SaveActiveConfigToDatabase()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                int error = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint pathCount, out uint modeCount);
                if (error != 0)
                    return error;

                var paths = new byte[pathCount * DISPLAYCONFIG_PATH_INFO_SIZE];
                var modes = new byte[modeCount * DISPLAYCONFIG_MODE_INFO_SIZE];
                error = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (error == ERROR_INSUFFICIENT_BUFFER)
                    continue; // a display changed in between
                if (error != 0)
                    return error;

                return SetDisplayConfig(pathCount, paths, modeCount, modes,
                    SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_SAVE_TO_DATABASE | SDC_ALLOW_CHANGES);
            }

            return ERROR_INSUFFICIENT_BUFFER;
        }

        /// <summary>
        /// Message for a failed ChangeDisplaySettingsEx, null on success.
        /// </summary>
        internal static string Describe(DisplayChangeResult result)
        {
            string msg = null;
            switch (result)
            {
                case DisplayChangeResult.BadDualView:
                    msg = Properties.Resources.InvalidOperation_Disp_Change_BadDualView;
                    break;
                case DisplayChangeResult.BadParam:
                    msg = Properties.Resources.InvalidOperation_Disp_Change_BadParam;
                    break;
                case DisplayChangeResult.BadFlags:
                    msg = Properties.Resources.InvalidOperation_Disp_Change_BadFlags;
                    break;
                case DisplayChangeResult.NotUpdated:
                    msg = Properties.Resources.InvalidOperation_Disp_Change_NotUpdated;
                    break;
                case DisplayChangeResult.BadMode:
                    msg = Properties.Resources.InvalidOperation_Disp_Change_BadMode;
                    break;
                case DisplayChangeResult.Failed:
                    msg = Properties.Resources.InvalidOperation_Disp_Change_Failed;
                    break;
                case DisplayChangeResult.Restart:
                    msg = Properties.Resources.InvalidOperation_Disp_Change_Restart;
                    break;
            }

            return msg;
        }


        /// <summary>
        /// Returns the current display mode setting
        /// </summary>
        /// <returns></returns>
        public static DisplaySettings GetCurrentDisplaySetting(string deviceName = null)
        {
            var mode = GetDeviceMode(deviceName);
            return CreateDisplaySettingsObject(0, mode);
        }

        /// <summary>
        /// Returns a list of all the display settings
        /// </summary>
        /// <returns></returns>
        public static List<DisplaySettings> GetAllDisplaySettings(string deviceName = null)
        {
            var list = new List<DisplaySettings>();
            DisplayManagerNative.DEVMODE mode = new DisplayManagerNative.DEVMODE();

            mode.Initialize();

            int idx = 0;
            
            while (DisplayManagerNative.EnumDisplaySettings(DisplayManagerNative.ToLPTStr(deviceName), idx, ref mode))
            //while (DisplayManagerNative.EnumDisplaySettings(deviceName, idx, ref mode))
                    list.Add(CreateDisplaySettingsObject(idx++, mode));

            return list;
        }

        public static List<DisplayDevice> GetAllDisplayDevices()
        {
            var list = new List<DisplayDevice>();
            uint idx = 0;
            uint size = 256;

            var device = new DisplayManagerNative.DISPLAY_DEVICE();
            device.cb = Marshal.SizeOf(device);
            int displayIndex = 0;

            while (DisplayManagerNative.EnumDisplayDevices(null, idx, ref device, size) )
            {
                if (device.StateFlags.HasFlag(DisplayManagerNative.DisplayDeviceStateFlags.AttachedToDesktop))
                {
                    var isPrimary = device.StateFlags.HasFlag(DisplayManagerNative.DisplayDeviceStateFlags.PrimaryDevice);
                    var deviceName = device.DeviceName;
                    
                    DisplayManagerNative.EnumDisplayDevices(device.DeviceName, 0, ref device, 0);
                    displayIndex++;
                    var dev = new DisplayDevice()
                    {
                        Index = displayIndex,
                        Id = device.DeviceID,

                        MonitorDeviceName = device.DeviceName,
                        DriverDeviceName = deviceName,
                        
                        DisplayName = device.DeviceString,
                        IsPrimary = isPrimary,
                        IsSelected = isPrimary
                    };
                    list.Add(dev);
                }

                idx++;

                device = new DisplayManagerNative.DISPLAY_DEVICE();
                device.cb = Marshal.SizeOf(device);
            }

            return list;
        }

        public class DisplayDevice
        {
            public int Index { get; set; }
            public string MonitorDeviceName { get; set;  }
            public string Id { get; set; }
            public string DriverDeviceName { get; set; }
            public string DisplayName { get; set; }
            public bool IsPrimary { get; set; }
            public bool IsSelected { get; set; }

            public override string ToString()
            {
                return $"{Index} {DisplayName}{(IsSelected ? " *" : "")}{(IsPrimary ? " (Main)" : "")}";
            }
        }

        /// <summary>
        /// Rotates the screen from its current location by 90 degrees either clockwise or anti-clockwise.
        /// </summary>
        /// <param name="clockwise">Set to true to rotate the screen 90 degrees clockwise from its current location, or false to rotate it anti-clockwise.</param>
        public static void RotateScreen(bool clockwise)
        {
            DisplaySettings set = DisplayManager.GetCurrentSettings();

            int tmp = set.Height;
            set.Height = set.Width;
            set.Width = tmp;

            if (clockwise)
                set.Orientation++;
            else
                set.Orientation--;

            if (set.Orientation < Orientation.Default)
                set.Orientation = Orientation.Rotate270;
            else if (set.Orientation > Orientation.Rotate270)
                set.Orientation = Orientation.Default;

            SetDisplaySettings(set);
        }


        /// <summary>
        /// A private helper methods used to derive a DisplaySettings object from the DEVMODE structure.
        /// </summary>
        /// <param name="idx">The mode index attached with the settings. Starts form zero. Is -1 for the current settings.</param>
        /// <param name="mode">The current DEVMODE object represents the display information to derive the DisplaySettings object from.</param>
        private static DisplaySettings CreateDisplaySettingsObject(int idx, DisplayManagerNative.DEVMODE mode)
        {
            return new DisplaySettings()
            {
                Index = idx,
                Width = (int)mode.dmPelsWidth,
                Height = (int)mode.dmPelsHeight,
                Orientation = (Orientation)mode.dmDisplayOrientation,
                BitCount = (int)mode.dmBitsPerPel,
                Frequency = (int)mode.dmDisplayFrequency,
                Interlaced = (mode.dmDisplayFlags & DM_INTERLACED) != 0
            };
        }

        /// <summary>
        /// A private helper method used to retrieve current display settings as a DEVMODE object.
        /// </summary>
        /// <remarks>
        /// Internally calls EnumDisplaySettings() native function with the value ENUM_CURRENT_SETTINGS (-1) to retrieve the current settings.
        /// </remarks>
        private static DisplayManagerNative.DEVMODE GetDeviceMode(string deviceName = null)
        {
            var mode = new DisplayManagerNative.DEVMODE();

            mode.Initialize();

            if (DisplayManagerNative.EnumDisplaySettings(DisplayManagerNative.ToLPTStr(deviceName), DisplayManagerNative.ENUM_CURRENT_SETTINGS, ref mode))
                return mode;
            else
                throw new InvalidOperationException(GetLastError());
        }

        private static string GetLastError()
        {
            int err = Marshal.GetLastWin32Error();

            if (DisplayManagerNative.FormatMessage(DisplayManagerNative.FORMAT_MESSAGE_FLAGS,
                    DisplayManagerNative.FORMAT_MESSAGE_FROM_HMODULE,
                    (uint)err, 0, out var msg, 0, 0) == 0)
                return Properties.Resources.InvalidOperation_FatalError;
            else
                return msg;
        }
    }

    public enum Orientation
    {
        Default = 0,
        Rotate90 = 1,
        Rotate180 = 2,
        Rotate270 = 3
    }

    public class DisplaySettings
    {
        public int Index { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public Orientation Orientation { get; set; }
        public int BitCount { get; set; }
        public int Frequency { get; set; }

        /// <summary>
        /// Interlaced mode (DM_INTERLACED). Only shown: not part of Equals.
        /// </summary>
        public bool Interlaced { get; set; }

        /// <summary>
        /// Determines whether the settings are stored in the registry for
        /// persistence for a reboot.
        /// </summary>
        public bool NoPersist { get; set; }

        /// <summary>
        /// Short form for scripts: 640x480@60, 1280x480@60i, 1920x1080@60,Rotate90
        /// </summary>
        public string Describe()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}x{1}@{2}{3}{4}",
                Width, Height, Frequency, Interlaced ? "i" : "",
                Orientation != Orientation.Default ? "," + Orientation : "");
        }

        /// <summary>
        /// Display Mode string display with full detail
        /// </summary>
        public override string ToString()
        {
            return ToString(false);
        }

        /// <summary>
        /// Display Mode string display
        /// </summary>
        /// <param name="noDetails">only return height and width</param>
        /// <returns></returns>
        public string ToString(bool noDetails)
        {
            if (noDetails)
            {
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, $"{Width} x {Height}");
            }

            return string.Format(System.Globalization.CultureInfo.CurrentCulture,
                $"{Width} x {Height}, {Frequency}hz{(Interlaced ? " interlaced" : "")}, {BitCount}bit{(Orientation != Orientation.Default ? ", " + Orientation.ToString() : "")}");
        }


        public override bool Equals(object d)
        {
            var disp = d as DisplaySettings;
            return (disp.Width == Width && disp.Height == Height &&
                    disp.Frequency == Frequency &&
                    disp.BitCount == BitCount &&
                    disp.Orientation == Orientation);
        }


        public override int GetHashCode()
        {
            return ("" + "W" + Width + "H" + Height + "F" + Frequency + "B" + BitCount + "O" + Orientation)
                .GetHashCode();
        }
    }

    enum DisplayChangeResult
    {
        /// <summary>
        /// Windows XP: The settings change was unsuccessful because system is DualView capable.
        /// </summary>
        BadDualView = -6,
        /// <summary>
        /// An invalid parameter was passed in. This can include an invalid flag or combination of flags.
        /// </summary>
        BadParam = -5,
        /// <summary>
        /// An invalid set of flags was passed in.
        /// </summary>
        BadFlags = -4,
        /// <summary>
        /// Windows NT/2000/XP: Unable to write settings to the registry.
        /// </summary>
        NotUpdated = -3,
        /// <summary>
        /// The graphics mode is not supported.
        /// </summary>
        BadMode = -2,
        /// <summary>
        /// The display driver failed the specified graphics mode.
        /// </summary>
        Failed = -1,
        /// <summary>
        /// The settings change was successful.
        /// </summary>
        Successful = 0,
        /// <summary>
        /// The computer must be restarted in order for the graphics mode to work.
        /// </summary>
        Restart = 1
    }


}
