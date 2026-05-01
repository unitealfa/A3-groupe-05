# EasySave v1.0 - ProSoft

EasySave est une application console C# / .NET 8 permettant de configurer et d'exécuter jusqu'à 5 travaux de sauvegarde.

Un travail de sauvegarde contient :

- un nom ;
- un dossier source ;
- un dossier cible ;
- un type de sauvegarde : complète ou différentielle.

Le projet est structuré pour séparer :

- l'interface console ;
- la logique métier ;
- la DLL de journalisation `EasyLog.dll` ;
- les tests.

Cette structure permet de préparer une future version WPF/MVVM.

---

## Prérequis

Avant de lancer le projet, il faut installer :

- .NET SDK 8.0 ;
- Visual Studio 2022 ou supérieur, ou la CLI `dotnet` ;
- Git ;
- Windows, Linux ou macOS compatible .NET 8.

Pour vérifier si .NET est installé :

```bash
dotnet --version
```

Si la commande retourne une version comme `8.x.x`, c'est bon.

---

## Commandes utilisées pour créer la solution

Ces commandes servent uniquement si on veut recréer le projet depuis zéro.

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

Si le projet est déjà cloné depuis GitHub, il ne faut pas relancer ces commandes.

---

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
│       ├── BackupStrategyFactory.cs
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
│   ├── BackupExecutionTests.cs
│   ├── BackupInfrastructureTests.cs
│   └── CliArgumentParserTests.cs
├── docs
│   ├── RELEASE_NOTES_v1.0.md
│   ├── SUPPORT.md
│   ├── USER_MANUAL.md
│   └── CODEBASE_REFERENCE.md
└── diagrames UML
    ├── Diagramme d’activité — exécution exacte d’un travail de sauvegarde.png
    ├── Diagramme de classes.png
    ├── Diagramme de cas d’utilisation.png
    ├── Diagramme de composants — Architecture globale EasySave v1.0.png
    └── Diagramme de séquence - Exécution d'une sauvegarde EasySave v1.0.png
```

---

## Emplacements des fichiers

Les chemins sont portables et basés sur `LocalApplicationData`.

```text
%LocalAppData%/ProSoft/EasySave/config/jobs.json
%LocalAppData%/ProSoft/EasySave/config/settings.json
%LocalAppData%/ProSoft/EasySave/logs/yyyy-MM-dd.json
%LocalAppData%/ProSoft/EasySave/state/state.json
```

Sous Linux, `LocalApplicationData` correspond au dossier local utilisateur utilisé par .NET.

Le projet n'utilise pas de chemin fixe comme `c:\temp`.

---

# Comment mettre le projet en marche

Toutes les commandes doivent être lancées depuis la racine du projet.

La racine du projet est le dossier où se trouve le fichier :

```text
EasySave.sln
```

Exemple de structure attendue :

```text
EasySave.sln
EasySave.Console/
EasySave.Core/
EasyLog/
EasySave.Tests/
README.md
```

Si vous n'êtes pas dans ce dossier, placez-vous dedans avec la commande `cd`.

Exemple :

```bash
cd chemin/vers/A3-GROUPE-05
```

---

## 1. Récupérer le projet depuis GitHub

Si le projet n'est pas encore sur votre machine :

```bash
git clone URL_DU_REPO
```

Puis entrer dans le dossier :

```bash
cd ../A3-GROUPE-05
```

Si le projet est déjà sur votre machine, récupérer la dernière version :

```bash
git pull origin main
```

Cette commande sert à récupérer les dernières modifications validées par le groupe.

---

## 2. Restaurer les dépendances

Après avoir récupéré le projet, lancer :

```bash
dotnet restore EasySave.sln
```

Cette commande prépare les dépendances du projet.

Elle est à lancer :

- après un `git clone` ;
- après un gros `git pull` ;
- quand un package NuGet a été ajouté ;
- quand le projet ne build pas à cause de dépendances manquantes.

---

## 3. Compiler le projet

Après le restore, lancer :

```bash
dotnet build EasySave.sln -m:1
```

Cette commande vérifie que le projet compile correctement.

Si le terminal affiche :

```text
Build succeeded
```

le projet est prêt à être lancé.

Le `-m:1` force une compilation séquentielle. Cela peut éviter certains problèmes de compilation dans certains environnements.

---

# Lancement interactif

Pour lancer EasySave avec le menu :

```bash
dotnet run --project EasySave.Console
```

Cette commande ouvre le programme normalement.

Elle est à utiliser quand on veut :

- choisir la langue FR ou EN ;
- créer un travail de sauvegarde ;
- afficher les travaux existants ;
- exécuter un travail précis ;
- exécuter tous les travaux ;
- quitter.

Au lancement, l'utilisateur choisit la langue FR ou EN.

Ensuite, il peut créer ses travaux de sauvegarde.

Important : avant d'utiliser les commandes CLI, il faut d'abord avoir créé au moins un travail de sauvegarde avec le menu interactif.

---

# Exécution CLI

Le mode CLI permet de lancer directement une ou plusieurs sauvegardes sans ouvrir le menu.

Il faut utiliser ce mode seulement après avoir déjà créé des travaux de sauvegarde.

Ordre normal :

```text
1. Lancer le programme en mode menu
2. Créer les sauvegardes
3. Fermer le programme
4. Relancer avec une commande CLI
```

---

## Lancer la sauvegarde numéro 1

```bash
dotnet run --project EasySave.Console -- 1
```

Cette commande lance uniquement le travail de sauvegarde numéro 1.

---

## Lancer les sauvegardes 1 à 3

```bash
dotnet run --project EasySave.Console -- 1-3
```

Cette commande lance les sauvegardes 1, 2 et 3 dans l'ordre.

---

## Lancer les sauvegardes 1 et 3

Sur Windows PowerShell :

```powershell
dotnet run --project EasySave.Console -- "1;3"
```

Sur Linux, Ubuntu ou macOS :

```bash
dotnet run --project EasySave.Console -- '1;3'
```

Cette commande lance uniquement les sauvegardes 1 et 3.

Les guillemets sont importants, car le caractère `;` peut être compris par le terminal comme une séparation de commandes.

---

## Lancer toutes les sauvegardes

```bash
dotnet run --project EasySave.Console -- all
```

Cette commande lance tous les travaux de sauvegarde enregistrés.

---

# Différence entre Windows, Linux et macOS

Pendant le développement, les commandes `dotnet` sont presque les mêmes sur tous les systèmes.

Sur Windows :

```powershell
dotnet run --project EasySave.Console
```

Sur Linux ou macOS :

```bash
dotnet run --project EasySave.Console
```

La seule différence importante concerne la commande avec `1;3`.

Sur Windows PowerShell :

```powershell
dotnet run --project EasySave.Console -- "1;3"
```

Sur Linux ou macOS :

```bash
dotnet run --project EasySave.Console -- '1;3'
```

Important : iOS signifie iPhone/iPad. Pour ce projet, on parle de Windows, Linux, Ubuntu ou macOS.

---

# Lancer après publication en fichier exécutable

Pendant le développement, on utilise surtout :

```bash
dotnet run --project EasySave.Console
```

Après publication, sur Windows, on peut obtenir un fichier :

```text
EasySave.exe
```

Dans ce cas, les arguments peuvent être passés directement à l'exécutable :

```powershell
EasySave.exe
EasySave.exe 1
EasySave.exe 1-3
EasySave.exe "1;3"
EasySave.exe all
```

Sur Linux ou macOS, pendant le développement, on utilise généralement :

```bash
dotnet run --project EasySave.Console
```

---

# Build et tests

## Restaurer les dépendances

```bash
dotnet restore EasySave.sln
```

Cette commande prépare les dépendances.

---

## Compiler le projet

```bash
dotnet build EasySave.sln -m:1
```

Cette commande vérifie que le projet compile.

---

## Lancer les tests

```bash
dotnet test EasySave.sln -m:1
```

Cette commande lance les tests unitaires.

Elle doit être utilisée :

- avant de faire une Pull Request ;
- avant de merge dans `main` ou `release/v1.0` ;
- avant de livrer le projet ;
- après une grosse modification.

Les tests couvrent le parser CLI, les chemins portables, la limite des 5 jobs, la validation d'un dossier source invalide, la sauvegarde complète, la sauvegarde différentielle, l'écriture des logs et le fichier `state.json`.

---

# Résumé des commandes importantes

## Première utilisation

```bash
git clone URL_DU_REPO
cd EasySave
dotnet restore EasySave.sln
dotnet build EasySave.sln -m:1
```

## Récupérer la dernière version

```bash
git pull origin main
```

## Lancer le menu

```bash
dotnet run --project EasySave.Console
```

## Lancer la sauvegarde 1

```bash
dotnet run --project EasySave.Console -- 1
```

## Lancer les sauvegardes 1 à 3

```bash
dotnet run --project EasySave.Console -- 1-3
```

## Lancer les sauvegardes 1 et 3 sur Windows

```powershell
dotnet run --project EasySave.Console -- "1;3"
```

## Lancer les sauvegardes 1 et 3 sur Linux/macOS

```bash
dotnet run --project EasySave.Console -- '1;3'
```

## Lancer toutes les sauvegardes

```bash
dotnet run --project EasySave.Console -- all
```

## Lancer les tests

```bash
dotnet test EasySave.sln -m:1
```

---

# À quoi sert chaque commande ?

```text
git clone URL_DU_REPO
= récupère le projet depuis GitHub sur votre ordinateur.

git pull origin main
= récupère les dernières modifications de la branche main.

dotnet restore EasySave.sln
= prépare les dépendances du projet.

dotnet build EasySave.sln -m:1
= compile le projet et vérifie qu'il n'y a pas d'erreur.

dotnet run --project EasySave.Console
= lance le programme avec le menu interactif.

dotnet run --project EasySave.Console -- 1
= lance directement la sauvegarde numéro 1.

dotnet run --project EasySave.Console -- 1-3
= lance directement les sauvegardes 1, 2 et 3.

dotnet run --project EasySave.Console -- "1;3"
= lance directement les sauvegardes 1 et 3 sur Windows.

dotnet run --project EasySave.Console -- '1;3'
= lance directement les sauvegardes 1 et 3 sur Linux ou macOS.

dotnet run --project EasySave.Console -- all
= lance directement toutes les sauvegardes enregistrées.

dotnet test EasySave.sln -m:1
= lance les tests unitaires du projet.
```

---

# Ordre conseillé pendant le développement

Quand un membre du groupe récupère le projet :

```bash
git pull origin main
dotnet restore EasySave.sln
dotnet build EasySave.sln -m:1
dotnet run --project EasySave.Console
```

Quand il a fini une modification :

```bash
dotnet build EasySave.sln -m:1
dotnet test EasySave.sln -m:1
git status
git add .
git commit -m "feat(scope): message"
git push origin feature/nom-de-la-branche
```



---

# Fonctionnalités terminées

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
- Tests unitaires pour CLI, chemins portables, sauvegarde complète, sauvegarde différentielle, validation d'infrastructure, logs et state.
- Documentation utilisateur, fiche support et release note.
- Diagrammes UML exportés en PNG.

---

# À finaliser par la deuxième personne

- Ajouter des tests manuels sur vrais lecteurs réseau selon l'environnement de l'école.
- Améliorer l'ergonomie console, par exemple modification/suppression de travaux.
- Préparer les vues et view-models de la future version WPF/MVVM.

---

# Documentation de livraison

```text
docs/USER_MANUAL.md
docs/SUPPORT.md
docs/RELEASE_NOTES_v1.0.md
docs/CODEBASE_REFERENCE.md
diagrames UML/Diagramme de cas d’utilisation.png
diagrames UML/Diagramme de classes.png
diagrames UML/Diagramme de séquence - Exécution d'une sauvegarde EasySave v1.0.png
diagrames UML/Diagramme d’activité — exécution exacte d’un travail de sauvegarde.png
```

---

# Gestion Git proposée

Branches attendues :

- `main`
- `feature/core-backup`
- `feature/easylog`
- `feature/state-manager`
- `feature/pre-release-fixes`
- `feature/publish-artifacts`
- `feature/fix-push`
- `release/v1.0`

Exemples de commits Conventional Commits :

- `feat(core): add recursive backup execution`
- `feat(easylog): write daily json logs`
- `feat(state): update real-time backup state`
- `test(core): cover complete and differential backups`
- `docs(release): add v1.0 delivery documentation`

Commandes de tag :

```bash
git tag -a v1.0 -m "Livrable 1 - EasySave v1.0"
git push origin v1.0
```

---

# Checklist de validation

- [x] Git OK
- [x] Solution .NET 8 créée
- [x] Architecture Console/Core/EasyLog séparée
- [x] Compilation OK
- [x] Tests OK
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
- [x] Tests unitaires parser, chemins, sauvegardes, validation d'infrastructure, logs et state
- [x] Documentation utilisateur
- [x] Fiche support
- [x] Release note
- [x] UML en PNG

---

# Règle simple à retenir

Pour ouvrir le programme normalement :

```bash
dotnet run --project EasySave.Console
```

Pour lancer directement des sauvegardes déjà créées :

```bash
dotnet run --project EasySave.Console -- 1-3
```

Pour vérifier que le projet est propre :

```bash
dotnet build EasySave.sln -m:1
dotnet test EasySave.sln -m:1
```
