Set objShell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
currentDir = fso.GetParentFolderName(WScript.ScriptFullName)

args = ""
For i = 0 To WScript.Arguments.Count - 1
  args = args & " """ & WScript.Arguments(i) & """"
Next
objShell.Run "pythonw.exe """ & currentDir & "\egeria_handler.py""" & args, 0, False
