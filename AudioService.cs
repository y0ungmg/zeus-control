using System.Runtime.InteropServices;

namespace ZeusControl;

internal enum AudioFlow { Render, Capture, All }
internal enum AudioRole { Console, Multimedia, Communications }

[Flags]
internal enum ClsCtx : uint { InprocServer = 0x1, All = 0x17 }

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(AudioFlow flow, int stateMask, out IntPtr devices);
    [PreserveSig] int GetDefaultAudioEndpoint(AudioFlow flow, AudioRole role, out IMMDevice device);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, ClsCtx clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    [PreserveSig] int GetState(out int state);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint count);
    [PreserveSig] int GetAt(uint index, out PropertyKey key);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
    [PreserveSig] int Commit();
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
    [PreserveSig] int GetChannelCount(out uint count);
    [PreserveSig] int SetMasterVolumeLevel(float db, Guid context);
    [PreserveSig] int SetMasterVolumeLevelScalar(float value, Guid context);
    [PreserveSig] int GetMasterVolumeLevel(out float db);
    [PreserveSig] int GetMasterVolumeLevelScalar(out float value);
    [PreserveSig] int SetChannelVolumeLevel(uint channel, float db, Guid context);
    [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float value, Guid context);
    [PreserveSig] int GetChannelVolumeLevel(uint channel, out float db);
    [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float value);
    [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid context);
    [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

[StructLayout(LayoutKind.Sequential)]
internal struct PropertyKey { public Guid FormatId; public uint PropertyId; }

[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropVariant
{
    [FieldOffset(0)] public ushort VariantType;
    [FieldOffset(8)] public IntPtr PointerValue;
}

internal sealed record AudioSnapshot(bool Available, float OutputVolume, bool OutputMuted, float MicVolume, bool MicMuted, string OutputName, string MicName, string? Error)
{
    public bool IsZeus => (OutputName + " " + MicName).Contains("zeus", StringComparison.OrdinalIgnoreCase)
        || (OutputName + " " + MicName).Contains("h510", StringComparison.OrdinalIgnoreCase)
        || (OutputName + " " + MicName).Contains("redragon", StringComparison.OrdinalIgnoreCase)
        || (OutputName + " " + MicName).Contains("xiisound", StringComparison.OrdinalIgnoreCase);
}

internal static class AudioService
{
    private static readonly Guid EndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static PropertyKey FriendlyNameKey = new() { FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), PropertyId = 14 };

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    public static AudioSnapshot Read()
    {
        try
        {
            var output = ReadEndpoint(AudioFlow.Render);
            var mic = ReadEndpoint(AudioFlow.Capture);
            return new(true, output.volume, output.muted, mic.volume, mic.muted, output.name, mic.name, null);
        }
        catch (Exception ex)
        {
            return new(false, 0, false, 0, false, "Brak domyślnego wyjścia", "Brak domyślnego mikrofonu", ex.Message);
        }
    }

    public static void SetVolume(AudioFlow flow, float value) => WithVolume(flow, v => Marshal.ThrowExceptionForHR(v.SetMasterVolumeLevelScalar(Math.Clamp(value, 0, 1), Guid.Empty)));
    public static void SetMute(AudioFlow flow, bool muted) => WithVolume(flow, v => Marshal.ThrowExceptionForHR(v.SetMute(muted, Guid.Empty)));

    private static (float volume, bool muted, string name) ReadEndpoint(AudioFlow flow)
    {
        float volume = 0; bool muted = false; string name = "Nieznane urządzenie";
        WithDevice(flow, device =>
        {
            name = ReadName(device);
            object instance;
            var iid = EndpointVolumeId;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, ClsCtx.All, IntPtr.Zero, out instance));
            var endpoint = (IAudioEndpointVolume)instance;
            try
            {
                Marshal.ThrowExceptionForHR(endpoint.GetMasterVolumeLevelScalar(out volume));
                Marshal.ThrowExceptionForHR(endpoint.GetMute(out muted));
            }
            finally { Marshal.FinalReleaseComObject(endpoint); }
        });
        return (volume, muted, name);
    }

    private static void WithVolume(AudioFlow flow, Action<IAudioEndpointVolume> action)
    {
        WithDevice(flow, device =>
        {
            object instance; var iid = EndpointVolumeId;
            Marshal.ThrowExceptionForHR(device.Activate(ref iid, ClsCtx.All, IntPtr.Zero, out instance));
            var endpoint = (IAudioEndpointVolume)instance;
            try { action(endpoint); }
            finally { Marshal.FinalReleaseComObject(endpoint); }
        });
    }

    private static void WithDevice(AudioFlow flow, Action<IMMDevice> action)
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        IMMDevice? device = null;
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(flow, AudioRole.Multimedia, out device));
            action(device);
        }
        finally
        {
            if (device != null) Marshal.FinalReleaseComObject(device);
            Marshal.FinalReleaseComObject(enumerator);
        }
    }

    private static string ReadName(IMMDevice device)
    {
        IPropertyStore? store = null; PropVariant value = default;
        try
        {
            Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out store));
            var key = FriendlyNameKey;
            Marshal.ThrowExceptionForHR(store.GetValue(ref key, out value));
            return value.VariantType == 31 && value.PointerValue != IntPtr.Zero ? Marshal.PtrToStringUni(value.PointerValue) ?? "Urządzenie audio" : "Urządzenie audio";
        }
        finally
        {
            PropVariantClear(ref value);
            if (store != null) Marshal.FinalReleaseComObject(store);
        }
    }
}
