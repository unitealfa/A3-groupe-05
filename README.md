# EasySave v1.0 - ProSoft

EasySave est une application console C# / .NET 8 permettant de configurer et d'exécuter jusqu'à 5 travaux de sauvegarde. Un travail contient un nom, un dossier source, un dossier cible et un type de sauvegarde : complète ou différentielle.

Le projet est structuré pour séparer l'interface console, la logique métier et la DLL de journalisation `EasyLog.dll`, afin de préparer une future version WPF/MVVM.

## Prérequis

- .NET SDK 8.0
- Visual Studio 2022 ou supérieur, ou la CLI `dotnet`
- Windows, Linux ou macOS compatible .NET 8

## Commandes utilisées pour créer la solution

```bash
dotnet new sln -n EasySave
dotnet new console -n EasySave.Console -f net8.0
dotnet new classlib -n EasySave.Core -f net8.0
dotnet new classlib -n EasyLog -f net8.0
dotnet new xunit -n EasySave.Tests -f net8.0
dotnet sln EasySave.sln add EasySave.Console/EasySave.Console.csproj EasySave.Core/EasySave.Core.csproj EasyLog/EasyLog.csproj EasySave.Tests/EasySave.Tests.csproj
dotnet add EasySave.Core/EasySave.Core.csproj reference EasyLog/EasyLog.csproj
dotnet add EasySave.Console/EasySave.Console.csproj reference EasySave.Core/EasySave.Core.csproj
dotnet add EasySave.Tests/EasySave.Tests.csproj reference EasySave.Core/EasySave.Core.csproj
```

## Architecture

```text
EasySave.sln
├── EasySave.Console
│   ├── Program.cs
│   ├── ConsoleMenu.cs
│   ├── CliArgumentParser.cs
│   ├── LanguageSelector.cs
│   └── Resources
│       ├── en.json
│       └── fr.json
├── EasySave.Core
│   ├── Configuration
│   │   ├── AppPaths.cs
│   │   └── BackupJobRepository.cs
│   ├── Models
│   │   ├── BackupJob.cs
│   │   ├── BackupState.cs
│   │   └── BackupType.cs
│   ├── Services
│   │   ├── BackupJobService.cs
│   │   ├── BackupManager.cs
│   │   └── StateManager.cs
│   └── Strategies
│       ├── BackupExecutionContext.cs
│       ├── BackupStrategyRunner.cs
│       ├── CompleteBackupStrategy.cs
│       ├── DifferentialBackupStrategy.cs
│       └── IBackupStrategy.cs
├── EasyLog
│   ├── ILoggerService.cs
│   ├── JsonLoggerService.cs
│   └── LogEntry.cs
├── EasySave.Tests
│   ├── AppPathsTests.cs
│   └── CliArgumentParserTests.cs
└── docs
    └── UML
        ├── ActivityBackupProcess.puml
        ├── ClassDiagram.puml
        ├── SequenceExecuteBackup.puml
        └── UseCaseDiagram.puml
```

## Emplacements des fichiers

Les chemins sont portables et basés sur `LocalApplicationData`.

```text
%LocalAppData%/ProSoft/EasySave/config/jobs.json
%LocalAppData%/ProSoft/EasySave/config/settings.json
%LocalAppData%/ProSoft/EasySave/logs/yyyy-MM-dd.json
%LocalAppData%/ProSoft/EasySave/state/state.json
```

Sous Linux, `LocalApplicationData` correspond au dossier local utilisateur utilisé par .NET.

## Lancement interactif

```bash
dotnet run --project EasySave.Console
```

Au lancement, l'utilisateur choisit la langue FR ou EN, puis peut :

- créer un travail de sauvegarde ;
- afficher les travaux existants ;
- exécuter un travail précis ;
- exécuter tous les travaux ;
- quitter.

## Exécution CLI

```bash
dotnet run --project EasySave.Console -- 1
dotnet run --project EasySave.Console -- 1-3
dotnet run --project EasySave.Console -- '1;3'
dotnet run --project EasySave.Console -- all
```

Après publication, les mêmes arguments peuvent être passés à l'exécutable :

```bash
EasySave.exe 1-3
EasySave.exe "1;3"
```

## Build et tests

```bash
dotnet restore EasySave.sln
dotnet build EasySave.sln -m:1
dotnet test EasySave.sln -m:1
```

`-m:1` force un build séquentiel. Il évite certains échecs MSBuild parallèles silencieux observés dans des environnements Linux sandboxés.

## Fonctionnalités terminées

- Architecture solution complète `Console / Core / EasyLog / Tests`.
- Menu console FR/EN basé sur fichiers JSON.
- Création de 5 travaux maximum.
- Configuration sauvegardée en JSON indenté.
- Sauvegarde complète récursive avec conservation de l'arborescence.
- Sauvegarde différentielle sur fichiers absents, plus récents ou de taille différente.
- Gestion des erreurs fichier par fichier sans arrêt complet de la sauvegarde.
- `EasyLog.dll` séparée avec logs journaliers JSON indentés.
- `state.json` mis à jour au début, pendant et à la fin d'une sauvegarde.
- Parser CLI pour index unique, plage, liste et `all`.
- Tests unitaires initiaux pour CLI et chemins portables.
- Diagrammes PlantUML alignés sur le code.

## À finaliser par la deuxième personne

- Ajouter davantage de tests d'intégration sur de gros dossiers et lecteurs réseau.
- Améliorer l'ergonomie console, par exemple modification/suppression de travaux.
- Ajouter une gestion plus avancée des messages i18n.
- Préparer les vues et view-models de la future version WPF/MVVM.
- Compléter la documentation utilisateur finale si le livrable demande un manuel séparé.

## Checklist de validation

- [x] Solution .NET 8 créée
- [x] Architecture Console/Core/EasyLog séparée
- [x] Pas de logique métier dans `Program.cs`
- [x] Pas de `Console.WriteLine` dans `EasySave.Core`
- [x] Chemins portables sans `c:\temp`
- [x] JSON indenté avec `System.Text.Json`
- [x] Sauvegarde complète fonctionnelle
- [x] Sauvegarde différentielle fonctionnelle
- [x] Logs journaliers JSON
- [x] `state.json` temps réel
- [x] CLI `1`, `1-3`, `1;3`, `all`
- [x] Ressources FR/EN
- [x] Tests unitaires de base
- [x] UML PlantUML
