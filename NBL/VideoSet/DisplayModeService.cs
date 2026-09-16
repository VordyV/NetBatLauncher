using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace NBL.Services;
public class DisplayModeService
{
    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int ENUM_REGISTRY_SETTINGS = -2;
    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(
        string? deviceName,
        int modeNum,
        ref DevMode devMode);
    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(
        string? deviceName,
        uint deviceNum,
        ref DisplayDevice displayDevice,
        uint flags);
    private const int DM_PELSWIDTH = 0x80000;
    private const int DM_PELSHEIGHT = 0x100000;
    private const int DM_DISPLAYFREQUENCY = 0x400000;
    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }
    public List<DisplayMode> GetModes()
    {
        List<DisplayMode> modes = new();
        string? deviceName =
            GetPrimaryDisplayDeviceName();
        if (deviceName == null)
            return modes;
        int modeIndex = 0;
        while (true)
        {
            DevMode mode = new();
            mode.dmSize =
                (short)Marshal.SizeOf<DevMode>();
            if (!EnumDisplaySettings(
                    deviceName,
                    modeIndex,
                    ref mode))
            {
                break;
            }
            if (mode.dmPelsWidth > 0
                && mode.dmPelsHeight > 0
                && mode.dmDisplayFrequency > 0)
            {
                modes.Add(
                    new DisplayMode(
                        mode.dmPelsWidth,
                        mode.dmPelsHeight,
                        mode.dmDisplayFrequency));
            }
            modeIndex++;
        }
        return modes;
    }
    private static string? GetPrimaryDisplayDeviceName()
    {
        DisplayDevice device = new();
        device.cb =
            Marshal.SizeOf<DisplayDevice>();
        if (!EnumDisplayDevices(
                null,
                0,
                ref device,
                0))
        {
            return null;
        }
        return device.DeviceName;
    }
}
public record DisplayMode(
    int Width,
    int Height,
    int RefreshRate);