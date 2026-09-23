using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace B975RgbApp.Services;

internal sealed class B975HidDevice : IDisposable
{
    private const ushort VendorId = 0x09DA;
    private const ushort ProductId = 0xFA10;
    private const uint DigcfPresent = 0x00000002;
    private const uint DigcfDeviceInterface = 0x00000010;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const int ErrorNoMoreItems = 259;
    private const int ReportLength = 64;
    private const int LedValueCount = 116;

    private readonly SafeFileHandle _handle;
    private readonly int _featureReportLength;
    private bool _disposed;

    private B975HidDevice(SafeFileHandle handle, int featureReportLength, string path)
    {
        _handle = handle;
        _featureReportLength = featureReportLength;
        DevicePath = path;
    }

    public string DevicePath { get; }

    public static B975HidDevice OpenPreferred()
    {
        var candidate = FindPreferredInterface()
                        ?? throw new InvalidOperationException(
                            "رابط نورپردازی Bloody B975 پیدا نشد. اتصال USB و بسته‌بودن KeyDominator را بررسی کن.");

        var handle = OpenForFeatureWrite(candidate.Path);
        return new B975HidDevice(handle, candidate.FeatureReportLength, candidate.Path);
    }

    public void InitializeLighting()
    {
        ThrowIfDisposed();
        SendReports(BuildInitializationReports(), delayMilliseconds: 8);
        SendSolid(Color.Black, delayMilliseconds: 1);
    }

    public void SendSolid(Color color, int delayMilliseconds = 1)
    {
        var reds = FilledValues(color.R);
        var greens = FilledValues(color.G);
        var blues = FilledValues(color.B);
        SendFrame(reds, greens, blues, delayMilliseconds);
    }

    public void SendFrame(byte[] reds, byte[] greens, byte[] blues, int delayMilliseconds = 1)
    {
        ThrowIfDisposed();

        if (reds.Length < LedValueCount || greens.Length < LedValueCount || blues.Length < LedValueCount)
        {
            throw new ArgumentException("هر کانال رنگ باید 116 مقدار داشته باشد.");
        }

        var reports = new[]
        {
            ChannelPacket(0x07, reds, 0),
            ChannelPacket(0x08, reds, 58),
            ChannelPacket(0x09, greens, 0),
            ChannelPacket(0x0A, greens, 58),
            ChannelPacket(0x0B, blues, 0),
            ChannelPacket(0x0C, blues, 58),
            Packet(0x07, 0x05)
        };

        SendReports(reports, delayMilliseconds);
    }

    private void SendReports(IEnumerable<byte[]> reports, int delayMilliseconds)
    {
        var reportNumber = 0;
        foreach (var source in reports)
        {
            reportNumber++;
            var report = ResizeReport(source);
            if (!HidD_SetFeature(_handle, report, report.Length))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"ارسال Feature Report شماره {reportNumber} ناموفق بود.");
            }

            if (delayMilliseconds > 0)
            {
                Thread.Sleep(delayMilliseconds);
            }
        }
    }

    private byte[] ResizeReport(byte[] source)
    {
        if (_featureReportLength < source.Length)
        {
            throw new InvalidOperationException("طول Feature Report کیبورد کمتر از 64 بایت است.");
        }

        if (_featureReportLength == source.Length)
        {
            return source;
        }

        var result = new byte[_featureReportLength];
        Buffer.BlockCopy(source, 0, result, 0, source.Length);
        return result;
    }

    private static byte[] FilledValues(byte value)
    {
        var values = new byte[LedValueCount];
        Array.Fill(values, value);
        return values;
    }

    private static byte[] ChannelPacket(byte channelCommand, byte[] values, int sourceOffset)
    {
        var report = Packet(0x07, 0x03, 0x06, channelCommand, 0x00, 0x00);
        Buffer.BlockCopy(values, sourceOffset, report, 6, 58);
        return report;
    }

    private static byte[] Packet(params byte[] prefix)
    {
        var report = new byte[ReportLength];
        Buffer.BlockCopy(prefix, 0, report, 0, prefix.Length);
        return report;
    }

    private static byte[][] BuildInitializationReports() =>
    [
        Packet(0x07, 0x1F),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x05),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x29),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x05),
        Packet(0x07, 0x07),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x2A),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x2A),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x1F),
        Packet(0x07, 0x29),
        Packet(0x07, 0x1E, 0x01),
        Packet(0x07, 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01),
        Packet(0x07, 0x05),
        Packet(0x07, 0x2F, 0x00, 0x2E),
        Packet(0x07, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x80),
        Packet(0x07, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x06, 0x80),
        Packet(0x07, 0x03, 0x06, 0x05),
        Packet(0x07, 0x06),
        Packet(0x07, 0x03, 0x06, 0x01),
        Packet(0x07, 0x03, 0x06, 0x01),
        Packet(0x07, 0x03, 0x06, 0x05),
        Packet(0x07, 0x03, 0x06, 0x01),
        Packet(0x07, 0x03, 0x06, 0x01)
    ];

    private static HidCandidate? FindPreferredInterface()
    {
        HidD_GetHidGuid(out var hidGuid);
        var infoSet = SetupDiGetClassDevs(
            ref hidGuid,
            IntPtr.Zero,
            IntPtr.Zero,
            DigcfPresent | DigcfDeviceInterface);

        if (infoSet == new IntPtr(-1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "امکان فهرست‌کردن HIDها وجود ندارد.");
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = new SpDeviceInterfaceData
                {
                    Size = Marshal.SizeOf<SpDeviceInterfaceData>()
                };

                if (!SetupDiEnumDeviceInterfaces(infoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == ErrorNoMoreItems)
                    {
                        break;
                    }

                    throw new Win32Exception(error);
                }

                SetupDiGetDeviceInterfaceDetail(
                    infoSet,
                    ref interfaceData,
                    IntPtr.Zero,
                    0,
                    out var requiredSize,
                    IntPtr.Zero);

                var detailBuffer = Marshal.AllocHGlobal(requiredSize);
                try
                {
                    Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(
                            infoSet,
                            ref interfaceData,
                            detailBuffer,
                            requiredSize,
                            out _,
                            IntPtr.Zero))
                    {
                        continue;
                    }

                    var path = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, 4));
                    if (string.IsNullOrWhiteSpace(path) ||
                        !path.Contains("vid_09da&pid_fa10&mi_02&col04", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var candidate = ReadCandidate(path);
                    if (candidate is { FeatureReportLength: >= ReportLength })
                    {
                        return candidate;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(infoSet);
        }

        return null;
    }

    private static HidCandidate? ReadCandidate(string path)
    {
        using var handle = CreateFile(
            path,
            0,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return null;
        }

        var attributes = new HiddAttributes { Size = Marshal.SizeOf<HiddAttributes>() };
        if (!HidD_GetAttributes(handle, ref attributes) ||
            attributes.VendorId != VendorId ||
            attributes.ProductId != ProductId)
        {
            return null;
        }

        if (!HidD_GetPreparsedData(handle, out var preparsedData))
        {
            return null;
        }

        try
        {
            return HidP_GetCaps(preparsedData, out var caps) >= 0
                ? new HidCandidate(path, caps.FeatureReportByteLength)
                : null;
        }
        finally
        {
            HidD_FreePreparsedData(preparsedData);
        }
    }

    private static SafeFileHandle OpenForFeatureWrite(string path)
    {
        var handle = CreateFile(
            path,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (!handle.IsInvalid)
        {
            return handle;
        }

        handle.Dispose();
        handle = CreateFile(
            path,
            GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);

        if (!handle.IsInvalid)
        {
            return handle;
        }

        var error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(
            error,
            "رابط نورپردازی باز نشد. KeyDominator را کامل ببند و برنامه را دوباره اجرا کن.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _handle.Dispose();
    }

    private sealed record HidCandidate(string Path, int FeatureReportLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct SpDeviceInterfaceData
    {
        public int Size;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HiddAttributes
    {
        public int Size;
        public ushort VendorId;
        public ushort ProductId;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidpCaps
    {
        public short Usage;
        public short UsagePage;
        public short InputReportByteLength;
        public short OutputReportByteLength;
        public short FeatureReportByteLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public short[] Reserved;

        public short NumberLinkCollectionNodes;
        public short NumberInputButtonCaps;
        public short NumberInputValueCaps;
        public short NumberInputDataIndices;
        public short NumberOutputButtonCaps;
        public short NumberOutputValueCaps;
        public short NumberOutputDataIndices;
        public short NumberFeatureButtonCaps;
        public short NumberFeatureValueCaps;
        public short NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        IntPtr enumerator,
        IntPtr parentWindow,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData interfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData interfaceData,
        IntPtr detailData,
        int detailDataSize,
        out int requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref HiddAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetFeature(SafeFileHandle handle, byte[] reportBuffer, int reportLength);
}
