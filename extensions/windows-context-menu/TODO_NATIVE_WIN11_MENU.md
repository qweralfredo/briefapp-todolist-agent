# TODO: Integração Nativa no Windows 11 Context Menu (Top Level)

**Objetivo:** Elevar o `Pandorize This` da gaveta de "Mostrar mais opções" (segunda página) diretamente para o nível raiz do menu de contexto moderno do Windows 11.

## Arquitetura Necessária

O Windows 11 desabilitou a leitura de chaves simples `HKEY_CLASSES_ROOT\*\shell` no seu menu moderno (Sparse/Acrylic UI). Para aparecer ao lado de apps como WinMerge, VSCode e Notepad++, a aplicação obrigatoriamente deve implementar objetos COM validados.

Temos dois caminhos de implementação para a V2 desta extensão:

### Opção 1: Shell Extension DLL in C++ (Abordagem Tradicional MS)
- Criar um projeto C++ (DLL) no Visual Studio nativo e implementar a interface COM `IExplorerCommand`.
- Assinar a DLL digitalmente (opcional mas recomendado localmente).
- Esta DLL não faz nada complexo: ela apenas extrai o arquivo selecionado (`IShellItemArray`) e executa internamente o comando `pythonw.exe "\path\to\egeria_handler.py" file.txt`.
- Registrar a classe (CLSID) no registro sob `CommandStore` ou nativamente na raiz.

### Opção 2: Sparse Package (Unpackaged App Identity - Win11 22H2+) - [IMPLEMENTADA ✔️]
- Criar um `AppxManifest.xml` configurando uma identidade de aplicativo (Sparse Package).
- Definir pontos de extensão para o Windows File Explorer (`windows.fileExplorerContextMenus`).
- O pacote registra dinamicamente o Python e a interface JSON nativa no menu arredondado do Win11.
- Requer comandos do PowerShell (`Add-AppxPackage`) no `install.py` usando flag `Register` ou API de pacote escasso.

## Esforço e Estimativa
- **Complexidade:** Alta (Envolve código no baixo nível do S.O. e integração C++/COM com Python)
- **Tempo Previsto:** 2 a 3 Sprints.

## Tarefas de Execução (Roadmap)
- [ ] 1. POC da DLL `BriefappShellContext.dll` implementando `IExplorerCommand` e `IExplorerCommandState`.

- [ ] 2. Compilação híbrida com injeção do ambiente python dinâmico.
- [ ] 3. Refatorar `install.py` para injetar a DLL no sistema via `regsvr32` ou Registry customizado (`InProcServer32`).
- [ ] 4. Atualizar o script de Uninstaller para derrubar o CLSID no registro e desinjetar a DLL da memória do Explorer (`taskkill /f /im explorer.exe`).
