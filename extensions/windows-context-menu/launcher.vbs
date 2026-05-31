Set objShell = CreateObject("WScript.Shell")
args = ""
For i = 0 To WScript.Arguments.Count - 1
  args = args & " """ & WScript.Arguments(i) & """"
Next
objShell.Run """C:\Users\alfre\AppData\Local\Microsoft\WindowsApps\pythonw.exe"" ""C:\projetos\todolist\extensions\windows-context-menu\egeria_handler.py""" & args, 0, False
