using System;
using System.Runtime.InteropServices;

namespace DeckSurf.SDK.Models
{
    // Refer to Microsoft documentation on the enum:
    // https://docs.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitemimagefactory-getimage
    [Flags]
    public enum SIIGBF
    {
        SIIGBF_RESIZETOFIT = 0x00,
        SIIGBF_BIGGERSIZEOK = 0x01,
        SIIGBF_MEMORYONLY = 0x02,
        SIIGBF_ICONONLY = 0x04,
        SIIGBF_THUMBNAILONLY = 0x08,
        SIIGBF_INCACHEONLY = 0x10,
        SIIGBF_CROPTOSQUARE = 0x20,
        SIIGBF_WIDETHUMBNAILS = 0x40,
        SIIGBF_ICONBACKGROUND = 0x80,
        SIIGBF_SCALEUP = 0x100,
    }

    // Refer to PInvoke for details on this interface:
    // https://pinvoke.net/default.aspx/Interfaces/IShellItem.html
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    internal interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);

        void GetParent(out IShellItem ppsi);

        void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);

        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    // Refer to Microsoft documentation on the enum:
    // https://docs.microsoft.com/windows/win32/api/shobjidl_core/ne-shobjidl_core-sigdn
    internal enum SIGDN : uint
    {
        SIGDN_NORMALDISPLAY = 0x00000000,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001,
        SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
        SIGDN_PARENTRELATIVEEDITING = 0x80031001,
        SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
        SIGDN_FILESYSPATH = 0x80058000,
        SIGDN_URL = 0x80068000,
        SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
        SIGDN_PARENTRELATIVE = 0x80080001,
        SIGDN_PARENTRELATIVEFORUI = 0x80094001,
    }

    internal enum HResult
    {
        S_OK = 0x0000,
        S_FALSE = 0x0001,
        E_INVALIDARG = unchecked((int)0x80070057),
        E_OUTOFMEMORY = unchecked((int)0x8007000E),
        E_NOINTERFACE = unchecked((int)0x80004002),
        E_FAIL = unchecked((int)0x80004005),
        E_NOT_FOUND = unchecked((int)0x80070490),
        E_ELEMENTNOTFOUND = unchecked((int)0x8002802B),
        E_NOOBJECT = unchecked((int)0x800401E5),
        E_CANCELLED = unchecked((int)0x800704C7),
        E_BUSY = unchecked((int)0x800700AA),
        E_ACCESSDENIED = unchecked((int)0x80030005),
    }

    [ComImport()]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemImageFactory
    {
        [PreserveSig]
        HResult GetImage([In, MarshalAs(UnmanagedType.Struct)] SIZE size, [In] SIIGBF flags, [Out] out IntPtr phbm);
    }

    // Refer to Microsoft documentation on the struct:
    // https://docs.microsoft.com/previous-versions/dd145106(v=vs.85)
    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE
    {
        private int cx;
        private int cy;

        public int Width { set { this.cx = value; } }

        public int Height { set { this.cy = value; } }
    }

    // Refer to Microsoft documentation on the struct:
    // https://docs.microsoft.com/windows/win32/api/wingdi/ns-wingdi-rgbquad
    [StructLayout(LayoutKind.Sequential)]
    public struct RGBQUAD
    {
        public byte rgbBlue;
        public byte rgbGreen;
        public byte rgbRed;
        public byte rgbReserved;
    }
}
