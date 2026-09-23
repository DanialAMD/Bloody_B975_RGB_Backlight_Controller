using System.ComponentModel;
using System.Runtime.InteropServices;
using B975RgbApp;

namespace B975RgbApp.Services;

internal sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkhfExtended = 0x00000001;

    private readonly LowLevelKeyboardProc _callback;
    private IntPtr _hookHandle;
    private bool _disposed;

    public KeyboardHook()
    {
        _callback = HookCallback;
    }

    public event Action<int>? LedPressed;

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsRunning)
        {
            return;
        }

        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(null), 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                AppLanguage.T(
                    "شنود سراسری کیبورد فعال نشد.",
                    "The global keyboard hook could not be started."));
        }
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam.ToInt64() == WmKeyDown || wParam.ToInt64() == WmSysKeyDown))
        {
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var ledIndex = KeyboardLayoutMap.ToLedIndex((int)data.VirtualKey, (int)data.ScanCode, data.Flags);
            if (ledIndex >= 0)
            {
                LedPressed?.Invoke(ledIndex);
            }
        }

        return CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private static class KeyboardLayoutMap
    {
        public static int ToLedIndex(int key, int scanCode, uint flags)
        {
            var extended = (flags & LlkhfExtended) != 0;

            if (key == 0x1B) return 0;
            if (key is >= 0x70 and <= 0x7B) return 1 + key - 0x70;
            if (key == 0x2C) return 13;
            if (key == 0x91) return 14;
            if (key == 0x13) return 15;

            if (key == 0xC0) return 16;
            if (key is >= 0x31 and <= 0x39) return 17 + key - 0x31;
            if (key == 0x30) return 26;
            if (key == 0xBD) return 27;
            if (key == 0xBB) return 28;
            if (key == 0x08) return 29;
            if (key == 0x2D) return 30;
            if (key == 0x24) return 31;
            if (key == 0x21) return 32;
            if (key == 0x90) return 33;
            if (key == 0x6F) return 34;
            if (key == 0x6A) return 35;
            if (key == 0x6D) return 36;

            if (key == 0x09) return 37;
            if (key == 0x51) return 38;
            if (key == 0x57) return 39;
            if (key == 0x45) return 40;
            if (key == 0x52) return 41;
            if (key == 0x54) return 42;
            if (key == 0x59) return 43;
            if (key == 0x55) return 44;
            if (key == 0x49) return 45;
            if (key == 0x4F) return 46;
            if (key == 0x50) return 47;
            if (key == 0xDB) return 48;
            if (key == 0xDD) return 49;
            if (key == 0xDC) return 50;
            if (key == 0x2E) return 51;
            if (key == 0x23) return 52;
            if (key == 0x22) return 53;
            if (key is >= 0x67 and <= 0x69) return 54 + key - 0x67;
            if (key == 0x6B) return 57;

            if (key == 0x14) return 58;
            if (key == 0x41) return 59;
            if (key == 0x53) return 60;
            if (key == 0x44) return 61;
            if (key == 0x46) return 62;
            if (key == 0x47) return 63;
            if (key == 0x48) return 64;
            if (key == 0x4A) return 65;
            if (key == 0x4B) return 66;
            if (key == 0x4C) return 67;
            if (key == 0xBA) return 68;
            if (key == 0xDE) return 69;
            if (key == 0x0D && !extended) return 70;
            if (key is >= 0x64 and <= 0x66) return 71 + key - 0x64;

            if (key == 0xA0 || key == 0x10 && scanCode != 0x36) return 74;
            if (key == 0x5A) return 75;
            if (key == 0x58) return 76;
            if (key == 0x43) return 77;
            if (key == 0x56) return 78;
            if (key == 0x42) return 79;
            if (key == 0x4E) return 80;
            if (key == 0x4D) return 81;
            if (key == 0xBC) return 82;
            if (key == 0xBE) return 83;
            if (key == 0xBF) return 84;
            if (key == 0xA1 || key == 0x10 && scanCode == 0x36) return 85;
            if (key == 0x26) return 86;
            if (key is >= 0x61 and <= 0x63) return 87 + key - 0x61;
            if (key == 0x0D && extended) return 90;

            if (key == 0xA2 || key == 0x11 && !extended) return 91;
            if (key == 0x5B) return 92;
            if (key == 0xA4 || key == 0x12 && !extended) return 93;
            if (key == 0x20) return 94;
            if (key == 0xA5 || key == 0x12 && extended) return 95;
            if (key == 0x5C) return 96;
            if (key == 0x5D) return 97;
            if (key == 0xA3 || key == 0x11 && extended) return 98;
            if (key == 0x25) return 99;
            if (key == 0x28) return 100;
            if (key == 0x27) return 101;
            if (key == 0x60) return 102;
            if (key == 0x6E) return 103;

            return -1;
        }
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProc callback,
        IntPtr moduleHandle,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hookHandle,
        int code,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
