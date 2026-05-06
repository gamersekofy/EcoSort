using System;
using System.Runtime.InteropServices;

namespace EcoSort.Services;

/// <summary>
/// Interop interface for accessing raw buffer bytes in WinRT.
/// </summary>
[ComImport]
[Guid("5B0D3235-4DBA-4DFF-B060-7F15D7F27B7D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMemoryBufferByteAccess
{
    void GetBuffer(out byte* buffer, out uint capacity);
}
