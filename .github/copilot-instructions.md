# Copilot Instructions

## Diretrizes gerais
- Responda sempre em português do Brasil; o usuário não fala inglês.
- Use linguagem concisa, objetiva e em modo imperativo.
- Priorize instruções acionáveis e exemplos de código minimalistas quando necessário.
- Agrupe informações relacionadas e mantenha estrutura clara de cabeçalhos e listas.

## Diretrizes de projeto
- Forneça orientações de implementação compatíveis com .NET 8 e WPF.
- Respeite a arquitetura MVVM com CommunityToolkit.Mvvm.
- Siga convenções de pastas e nomes consistentes com o projeto existente.

## Projeto: GLauncher (memória do projeto)
- Local do projeto: C:\Users\Usuario\Documents\GitHub\GLauncher
- Repositório remoto: https://github.com/KleberFreitasGames/GLauncher
- Branch ativa: Feature/GLauncher
- Projeto principal: GameLauncher.csproj targeting .NET 8
- Tipo de aplicação: WPF em C# com arquitetura MVVM (CommunityToolkit.Mvvm)
- Estrutura de pastas principal:
  - Controls
  - Converters
  - Models
  - Services (ex.: DiscordService, SteamGridDbService, IgdbService, EpicGamesService, HardwareMonitorService, XInputService)
  - ViewModels (ex.: MainViewModel)
  - Views (diversos diálogos)
  - MainWindow.xaml
  - app.manifest
- Recursos principais:
  - Biblioteca de jogos
  - Integração Discord (OAuth2 + Rich Presence)
  - Importação Epic Games
  - SteamGridDB (capas, logos, fundos)
  - IGDB (metadados)
  - Monitor de hardware (LibreHardwareMonitorLib)
  - Suporte a gamepads (XInput/HID)
  - Fundos animados via SkiaSharp
  - Efeitos sonoros programáticos
- Persistência:
  - Pasta de dados: %AppData%\GameLauncher
  - Arquivos: games.json, settings.json, tokens, caches
- Dependências principais:
  - CommunityToolkit.Mvvm
  - MaterialDesignThemes
  - SkiaSharp
  - LibreHardwareMonitorLib
  - craftersmine.SteamGridDB.Net
  - Microsoft.Identity.Client
  - System.Drawing.Common
  - System.Management
- Ambiente de desenvolvimento:
  - IDE: Visual Studio Community 2026 (18.6.0-insiders)
- Observação: Analisar e guardar todo o conhecimento do sistema na memória do Copilot conforme solicitado.

## Regras específicas de assistência
- Forneça exemplos compatíveis com .NET 8 e padrões WPF/MVVM.
- Indique claramente onde alterar caminhos e nomes de arquivos (use %AppData% para persistência).
- Ao sugerir novos serviços, mantenha os nomes e localizações consistentes com a estrutura existente.
- Priorize APIs e bibliotecas já presentes nas dependências listadas.
- Ao propor mudanças em arquitetura ou dependências, explique impacto em persistência, autenticação e compatibilidade com Windows.

## Manutenção e contribuições
- Ao criar ou modificar arquivos, mantenha namespace e convenções do projeto.
- Escreva testes unitários para lógica em ViewModels e Services quando aplicável.
- Documente novas funcionalidades com comentários sucintos e atualize README do repositório.

## Notas finais
- Preserve a consistência do projeto; evite introduzir frameworks ou paradigmas incompatíveis com WPF/MVVM.
- Atualize estas instruções se houver mudanças relevantes no projeto (branch, dependências, estrutura).