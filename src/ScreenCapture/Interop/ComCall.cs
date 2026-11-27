using System.Runtime.InteropServices;

namespace ScreenCapture.Interop;

/// <summary>
/// Minimal helpers for calling COM interfaces through their vtables with function pointers.
/// Used for Media Foundation and WASAPI: no runtime-callable wrappers, deterministic Release, AOT friendly.
/// </summary>
internal static unsafe class ComCall
{
    /// <summary>Address of vtable slot <paramref name="index"/> of a COM interface pointer.</summary>
    public static void* Slot(nint instance, int index) => (*(void***)instance)[index];

    public static void Release(ref nint instance)
    {
        if (instance != 0)
        {
            ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(instance, 2))(instance);
            instance = 0;
        }
    }

    public static void Check(int hr, string operation)
    {
        if (hr < 0)
        {
            throw new COMException($"{operation} failed (0x{hr:X8}).", hr);
        }
    }

    public static nint QueryInterface(nint instance, Guid iid)
    {
        nint result;
        int hr = ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(instance, 0))(instance, &iid, &result);
        Check(hr, "QueryInterface");
        return result;
    }
}
