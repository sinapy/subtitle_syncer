using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SubtitleSyncer.Vad;

internal static partial class TenVadNative
{
    private const string LibraryName = "ten_vad";

    static TenVadNative() => NativeLibrary.SetDllImportResolver(typeof(TenVadNative).Assembly, Resolve);

    internal static void EnsureLoaded() { }

    [LibraryImport(LibraryName, EntryPoint = "ten_vad_create")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Create(out IntPtr handle, nuint hopSize, float threshold);

    [LibraryImport(LibraryName, EntryPoint = "ten_vad_process")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Process(IntPtr handle, [In] short[] audioData, nuint audioDataLength,
        out float probability, out int flag);

    [LibraryImport(LibraryName, EntryPoint = "ten_vad_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Destroy(ref IntPtr handle);

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        var relativePath = OperatingSystem.IsWindows()
            ? Path.Combine("runtimes", "win-x64", "native", "ten_vad.dll")
            : OperatingSystem.IsLinux()
                ? Path.Combine("runtimes", "linux-x64", "native", "libten_vad.so")
                : throw new PlatformNotSupportedException("TEN VAD is bundled for Linux x64 and Windows x64 only.");
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new DllNotFoundException($"The TEN VAD runtime was not found at '{fullPath}'.");
        }

        if (OperatingSystem.IsLinux())
        {
            var nativeDirectory = Path.GetDirectoryName(fullPath)!;
            NativeLibrary.Load(Path.Combine(nativeDirectory, "libunwind.so.1"));
            NativeLibrary.Load(Path.Combine(nativeDirectory, "libc++abi.so.1"));
            NativeLibrary.Load(Path.Combine(nativeDirectory, "libc++.so.1"));
        }

        return NativeLibrary.Load(fullPath);
    }
}
