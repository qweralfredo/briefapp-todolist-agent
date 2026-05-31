import os
import sys
import winreg
import ctypes
import subprocess
from pathlib import Path

def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except:
        return False

def set_reg(key_path, value_name, value):
    winreg.SetValueEx(key_path, value_name, 0, winreg.REG_SZ, value)

def build_com_dll(script_dir):
    print("Compilando IExplorerCommand C# (.NET 10 COM Host)...")
    csproj_path = os.path.join(script_dir, "src", "WinMenu", "Briefapp.WinMenu.csproj")
    subprocess.run(["dotnet", "build", csproj_path, "-c", "Release"], check=True)
    comhost_path = os.path.join(script_dir, "src", "WinMenu", "bin", "Release", "net10.0-windows", "win-x64", "Briefapp.WinMenu.comhost.dll")
    if not os.path.exists(comhost_path):
        raise Exception("comhost.dll nao foi gerado.")
    return comhost_path

def install():
    if not is_admin():
        print("Requer privilegios de administrador. Solicitando elevacao...")
        ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, " ".join(['"'+__file__+'"'] + sys.argv[1:]), None, 1)
        return

    script_dir = os.path.dirname(os.path.abspath(__file__))
    python_exe = sys.executable
    pythonw_exe = python_exe.replace("python.exe", "pythonw.exe")
    windows_apps_pyw = os.path.join(os.environ.get('LOCALAPPDATA', ''), 'Microsoft', 'WindowsApps', 'pythonw.exe')
    if "WindowsApps" in python_exe and os.path.exists(windows_apps_pyw):
        pythonw_exe = windows_apps_pyw
    elif not os.path.exists(pythonw_exe):
        pythonw_exe = python_exe

    script_path = os.path.join(script_dir, "egeria_handler.py")
    
    # 1. Compila a DLL nativa COM
    comhost_path = build_com_dll(script_dir)

    # 2. Cria o launcher VBS pro invoke fire-and-forget
    vbs_path = os.path.join(script_dir, "launcher.vbs")
    with open(vbs_path, "w", encoding="utf-8") as f:
        f.write('Set objShell = CreateObject("WScript.Shell")\n')
        f.write('args = ""\n')
        f.write('For i = 0 To WScript.Arguments.Count - 1\n')
        f.write('  args = args & " """ & WScript.Arguments(i) & """"\n')
        f.write('Next\n')
        f.write(f'objShell.Run """{pythonw_exe}"" ""{script_path}""" & args, 0, False\n')

    # 3. Registra a DLL como InProcServer32 (COM Server nativo)
    print("Registrando COM Server C# no Registro...")
    clsid = "{0b01b630-14e4-4d1a-be10-d8a4ba5bbbf3}"
    clsid_path = rf"Software\Classes\CLSID\{clsid}"
    with winreg.CreateKey(winreg.HKEY_LOCAL_MACHINE, clsid_path) as clsid_key:
        set_reg(clsid_key, "", "Briefapp Win11 Context Menu")
        set_reg(clsid_key, "InstallDir", script_dir)
        with winreg.CreateKey(clsid_key, "InProcServer32") as inproc_key:
            set_reg(inproc_key, "", comhost_path)
            set_reg(inproc_key, "ThreadingModel", "Apartment")
            
    # 4. Registra no Context Menu AppxManifest (Win11 Sparse Package)
    print("Injetando Sparse Package no Menu do Win11 Top-Level...")
    manifest_path = os.path.join(script_dir, "AppxManifest.xml")
    if os.path.exists(manifest_path):
        subprocess.run(["powershell", "-NoProfile", "-Command", f"Add-AppxPackage -Register '{manifest_path}'"], check=False)
        
    print("\n✅ Context Menu Híbrido (Win11) instalado com sucesso!")
    print(f"   DLL  : {comhost_path}")
    print(f"   Appx : Registrado")
    input("\nPressione Enter para sair...")

def uninstall():
    if not is_admin():
        print("Requer privilegios de administrador. Solicitando elevacao...")
        ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, " ".join(['"'+__file__+'"'] + sys.argv[1:]), None, 1)
        return

    keys_to_delete = [
        (winreg.HKEY_CLASSES_ROOT, r"*\shellex\ContextMenuHandlers\BriefappV3"),
        (winreg.HKEY_CLASSES_ROOT, r"Directory\shellex\ContextMenuHandlers\BriefappV3"),
        (winreg.HKEY_CLASSES_ROOT, r"Directory\Background\shellex\ContextMenuHandlers\BriefappV3"),
    ]
    clsid_path = r"Software\Classes\CLSID\{0b01b630-14e4-4d1a-be10-d8a4ba5bbbf3}"

    print("Desinstalando Sparse Package do Win11...")
    subprocess.run(["powershell", "-NoProfile", "-Command", "Get-AppxPackage -Name Briefapp.ContextMenu | Remove-AppxPackage"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    
    print("Desinstalando Context Menu Nativo (COM)...")
    # Tenta dar kill no explorer para liberar a DLL
    subprocess.run(["taskkill", "/f", "/im", "explorer.exe"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    
    # Limpa legacy caso existam
    for hive, path in keys_to_delete:
        try:
            winreg.DeleteKey(hive, path)
            print(f"  Removido Handler Legado: {path}")
        except: pass

    try:
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, clsid_path, 0, winreg.KEY_ALL_ACCESS) as parent:
            try: winreg.DeleteKey(parent, "InProcServer32")
            except: pass
        winreg.DeleteKey(winreg.HKEY_LOCAL_MACHINE, clsid_path)
        print(f"  Removido CLSID: {clsid_path}")
    except: pass

    # Reinicia o explorer
    subprocess.Popen("explorer.exe")

    print("\n✅ Desinstalado com sucesso.")
    input("Pressione Enter para sair...")

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "uninstall":
        uninstall()
    else:
        install()
