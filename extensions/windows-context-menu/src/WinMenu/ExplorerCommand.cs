using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Briefapp.WinMenu
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        [PreserveSig] int GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    public interface IShellItemArray
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppvOut);
        void GetPropertyStore(int flags, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetPropertyDescriptionList([MarshalAs(UnmanagedType.LPStruct)] Guid keyType, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetAttributes(int dwAttribFlags, uint sfgaoMask, out uint psfgaoAttribs);
        [PreserveSig] int GetCount(out uint pdwNumItems);
        [PreserveSig] int GetItemAt(uint dwIndex, out IShellItem ppsi);
        void EnumItems(out IntPtr ppenumShellItems);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("a08ce4d0-fa25-44ab-b57c-c7b1c323e099")]
    public interface IExplorerCommand
    {
        [PreserveSig] int GetTitle(IShellItemArray psiItemArray, out IntPtr ppszName);
        [PreserveSig] int GetIcon(IShellItemArray psiItemArray, out IntPtr ppszIcon);
        [PreserveSig] int GetToolTip(IShellItemArray psiItemArray, out IntPtr ppszInfotip);
        [PreserveSig] int GetCanonicalName(out Guid pguidCommandName);
        [PreserveSig] int GetState(IShellItemArray psiItemArray, bool fOkToBeSlow, out uint pcsFlags);
        [PreserveSig] int Invoke(IShellItemArray psiItemArray, IntPtr pbc);
        [PreserveSig] int GetFlags(out uint pFlags);
        [PreserveSig] int EnumSubCommands(out IntPtr ppEnum);
    }

    [ComVisible(true)]
    [Guid("0b01b630-14e4-4d1a-be10-d8a4ba5bbbf3")] 
    public class BriefappV3ContextMenu : IExplorerCommand
    {
        private const int S_OK = 0;
        private const int S_FALSE = 1;
        private const int E_NOTIMPL = unchecked((int)0x80004001);

        private string GetInstallDir()
        {
            // Lookup path securely defined by the python installer
            using (var key = Registry.LocalMachine.OpenSubKey(@"Software\Classes\CLSID\{0b01b630-14e4-4d1a-be10-d8a4ba5bbbf3}"))
            {
                if (key != null)
                {
                    return key.GetValue("InstallDir") as string ?? "";
                }
            }
            return "";
        }

        public int GetTitle(IShellItemArray psiItemArray, out IntPtr ppszName)
        {
            ppszName = Marshal.StringToCoTaskMemUni("📦 Pandorize This");
            return S_OK;
        }

        public int GetIcon(IShellItemArray psiItemArray, out IntPtr ppszIcon)
        {
            string installDir = GetInstallDir();
            if (!string.IsNullOrEmpty(installDir))
            {
                string iconPath = Path.Combine(installDir, "icons", "briefapp.ico");
                if (File.Exists(iconPath))
                {
                    ppszIcon = Marshal.StringToCoTaskMemUni(iconPath);
                    return S_OK;
                }
            }
            ppszIcon = IntPtr.Zero;
            return E_NOTIMPL;
        }

        public int GetToolTip(IShellItemArray psiItemArray, out IntPtr ppszInfotip)
        {
            ppszInfotip = Marshal.StringToCoTaskMemUni("Enviar para a Briefapp Context-Box");
            return S_OK;
        }

        public int GetCanonicalName(out Guid pguidCommandName)
        {
            pguidCommandName = Guid.Empty;
            return E_NOTIMPL;
        }

        public int GetState(IShellItemArray psiItemArray, bool fOkToBeSlow, out uint pcsFlags)
        {
            pcsFlags = 0; // ECS_ENABLED = 0
            return S_OK;
        }

        public int GetFlags(out uint pFlags)
        {
            pFlags = 0;
            return S_OK;
        }

        public int EnumSubCommands(out IntPtr ppEnum)
        {
            ppEnum = IntPtr.Zero;
            return E_NOTIMPL;
        }

        public int Invoke(IShellItemArray psiItemArray, IntPtr pbc)
        {
            try
            {
                if (psiItemArray != null)
                {
                    psiItemArray.GetCount(out uint count);
                    if (count > 0)
                    {
                        string arguments = "";
                        for (uint i = 0; i < count; i++)
                        {
                            psiItemArray.GetItemAt(i, out IShellItem item);
                            if (item != null)
                            {
                                // SIGDN_FILESYSPATH = 0x80058000
                                item.GetDisplayName(0x80058000, out IntPtr ppszName);
                                if (ppszName != IntPtr.Zero)
                                {
                                    string path = Marshal.PtrToStringUni(ppszName) ?? "";
                                    Marshal.FreeCoTaskMem(ppszName);
                                    arguments += $" \"{path}\"";
                                }
                            }
                        }

                        string installDir = GetInstallDir();
                        if (!string.IsNullOrEmpty(installDir))
                        {
                            string launcherPath = Path.Combine(installDir, "launcher.vbs");
                            if (File.Exists(launcherPath))
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = "wscript.exe",
                                    Arguments = $"\"{launcherPath}\"{arguments}",
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Briefapp Invoke Error: {ex.Message}");
            }
            return S_OK;
        }
    }
}
