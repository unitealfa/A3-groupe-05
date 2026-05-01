# Reference Technique Complete du Projet EasySave

## But du document

Ce fichier sert de memoire technique persistante pour ce depot. Il synthétise le comportement reel du code tel qu'il est ecrit aujourd'hui, sans supposer des choses non visibles.

Il a 4 objectifs:

1. Garder une vue exacte de l'architecture.
2. Eviter d'inventer des comportements non implementes.
3. Retrouver rapidement quel fichier fait quoi.
4. Servir de base de reference pour les prochaines modifications.

## Vue d'ensemble

Le depot contient une solution .NET 8 nommee `EasySave` avec 4 projets:

- `EasySave.Console`: interface utilisateur console et point d'entree.
- `EasySave.Core`: logique metier, configuration, strategies de sauvegarde, et gestion d'etat.
- `EasyLog`: DLL de journalisation JSON.
- `EasySave.Tests`: tests xUnit couvrant les chemins applicatifs, le parseur CLI et une partie de l'infrastructure.

Le programme permet de:

- definir jusqu'a 5 travaux de sauvegarde;
- stocker leur configuration en JSON;
- executer une sauvegarde complete ou differentielle;
- produire un etat courant dans `state.json`;
- produire un journal quotidien en JSON dans `logs/yyyy-MM-dd.json`.

## Arborescence utile

```text
EasySave.sln
EasySave.Console/
EasySave.Core/
EasyLog/
EasySave.Tests/
docs/
publish/
```

## Dependances entre projets

Le chainage reel est:

- `EasySave.Console` reference `EasySave.Core`
- `EasySave.Core` reference `EasyLog`
- `EasySave.Tests` reference `EasySave.Core`
- `EasySave.Tests` inclut aussi `CliArgumentParser.cs` du projet console en lien compile

Cela veut dire:

- l'UI depend du coeur metier;
- le coeur metier depend de la journalisation;
- les tests ne lancent pas le menu console complet, mais testent une partie du code console en reimportant le parseur CLI.

## Point d'entree exact

Fichier: `EasySave.Console/Program.cs`

Ordre d'execution:

1. `AppPaths.EnsureDirectories()` cree les dossiers `config`, `logs` et `state`.
2. Creation de `StateManager` avec `AppPaths.StateFilePath`.
3. Creation de `BackupJobRepository` avec `AppPaths.JobsFilePath`.
4. Creation de `BackupJobService`.
5. Creation de `BackupManager` avec le dossier de logs.
6. Creation de `LanguageSelector` avec `AppPaths.SettingsFilePath`.
7. `InitializeAsync()` charge la langue sauvegardee puis les traductions.
8. Si `args.Length > 0`, le programme passe en mode CLI.
9. Sinon il lance `ConsoleMenu.RunAsync()`.

Comportement CLI:

- lit tous les jobs;
- parse `args[0]`;
- accepte `all`, une plage `1-3`, ou une liste `1;3`;
- execute les jobs demandes;
- retourne `1` en cas d'erreur de parsing;
- retourne `0` sinon.

Comportement interactif:

- force une selection de langue au lancement du menu;
- affiche les choix creer, lister, executer un job, executer tous les jobs, quitter.

## Emplacements reels des donnees

Fichier: `EasySave.Core/Configuration/AppPaths.cs`

L'application construit ses chemins sous:

`<LocalApplicationData>/ProSoft/EasySave`

Sous ce dossier:

- `config/jobs.json`
- `config/settings.json`
- `logs/yyyy-MM-dd.json`
- `state/state.json`

Particularite importante:

- si la variable d'environnement `EASYSAVE_APPDATA` est definie et non vide, elle remplace `LocalApplicationData`.
- c'est ce qui permet aux tests de travailler dans un dossier temporaire.

## Syntaxe C# utile pour lire ce projet

Pour ne pas halluciner sur le sens des lignes:

- `;` termine une instruction en C#.
- `using` importe des namespaces.
- `namespace` range les types dans un espace de noms.
- `public sealed class` signifie classe publique non heritable.
- `readonly` signifie reference assignable seulement dans le constructeur ou a la declaration.
- `async Task` signifie methode asynchrone sans valeur retour metier.
- `await` suspend la methode jusqu'a la fin de l'operation asynchrone.
- `??` signifie "sinon cette valeur".
- `!` apres une expression indique a l'analyseur nullable "je sais que ce n'est pas null".
- `switch` choisit un resultat selon plusieurs cas.
- `=>` peut servir pour une expression lambda ou une methode courte.
- `[]` dans ce projet est la syntaxe collection expression de C# moderne pour creer une liste vide adaptee au type cible.

## Projet `EasySave.Console`

### `Program.cs`

Role:

- assembler toutes les dependances;
- choisir entre mode CLI et mode menu.

Le fichier ne contient pas de boucle metier ni de logique de copie. Il ne fait que composer les objets et deleguer.

### `ConsoleMenu.cs`

Role:

- interface texte interactive.

Responsabilites:

- demander la langue;
- afficher le menu;
- lire le choix utilisateur;
- appeler `BackupJobService` pour creer/lister;
- appeler `BackupManager` pour executer;
- traduire certaines exceptions en messages localises.

Flux important:

- `RunAsync()` tourne dans une boucle `while (shouldContinue)`.
- `HandleChoiceAsync()` retourne `false` uniquement pour le choix `5`.

Validation a la creation d'un job:

- nom obligatoire;
- dossier source obligatoire;
- dossier cible obligatoire;
- type de sauvegarde choisi par `1` ou `2`.

Traduction des erreurs:

- index hors limites;
- source inexistante;
- nom/source/cible/type invalides;
- depot de plus de 5 jobs;
- echec de creation du dossier cible.

Limite notable:

- la traduction depend de messages d'exception exacts en anglais.
- si ces messages changent dans `Core`, la correspondance peut casser.

### `CliArgumentParser.cs`

Role:

- transformer l'argument CLI en indexes de jobs a executer.

Formats acceptes:

- `all`
- `1-3`
- `1;3`

Verification effectuee:

- argument non vide;
- pas de doublons;
- indexes entre `1` et `maxJobCount` par defaut `5`;
- indexes ne depassant pas le nombre reel de jobs existants.

Comportement detaille:

- `ParseRange()` coupe sur `-`, attend exactement 2 bornes entieres, refuse `start > end`.
- `ParseList()` coupe sur `;`, retire les entrees vides, convertit chaque element en entier.

Point important:

- le parseur raisonne en indexes utilisateur de base 1, pas en index tableau de base 0.

### `CliParseResult`

Role:

- petit objet de resultat immutable en pratique apres construction.

Contient:

- `IsSuccess`
- `JobIndexes`
- `ErrorMessage`

### `LanguageSelector.cs`

Role:

- charger, sauvegarder et appliquer la langue de l'interface.

Fichiers touches:

- lecture/ecriture `settings.json`
- lecture `Resources/fr.json` ou `Resources/en.json` depuis le dossier de sortie de l'application

Comportement:

- `InitializeAsync()` lit la langue sauvegardee ou prend `en`.
- `SelectLanguageAsync()` affiche 2 choix, et toute reponse autre que `1` force `en`.
- `Text(key)` retourne la traduction si trouvee, sinon retourne la cle brute.

Point notable:

- en mode menu, meme si une langue est deja sauvegardee, l'utilisateur doit refaire un choix a chaque lancement.
- l'initialisation sert surtout a avoir une langue valide avant l'affichage du selecteur.

## Projet `EasySave.Core`

## Modeles

### `Models/BackupType.cs`

Enum a 2 valeurs:

- `Complete = 1`
- `Differential = 2`

Le menu console repose sur ces valeurs implicites `1` et `2`.

### `Models/BackupJob.cs`

Modele de configuration d'un travail:

- `Name`
- `SourceDirectory`
- `TargetDirectory`
- `Type`

Ce type ne valide rien par lui-meme. La validation est faite dans `BackupJobService.ValidateJob`.

### `Models/BackupState.cs`

Modele de suivi d'execution.

Champs:

- `Name`: nom du job
- `LastActionTimestamp`: horodatage de la derniere mise a jour
- `State`: `Inactive`, `Active`, `Error`, `Finished` selon le moment
- `TotalFilesToCopy`
- `TotalFilesSize`
- `Progression`
- `RemainingFiles`
- `RemainingSize`
- `CurrentSourceFilePath`
- `CurrentDestinationFilePath`

Remarque:

- `Inactive` n'est pas applique automatiquement par le moteur. C'est juste la valeur par defaut du modele.

## Configuration et persistance

### `Configuration/BackupJobRepository.cs`

Role:

- persister la liste complete des jobs dans `jobs.json`.

Comportement:

- cree le dossier parent au constructeur;
- `GetAllAsync()` retourne une liste vide si le fichier n'existe pas;
- `SaveAllAsync()` remplace completement le contenu du fichier.

Point important:

- il n'y a pas de sauvegarde incrementale du fichier config.
- chaque ecriture recree le fichier entier.

### `Services/BackupJobService.cs`

Role:

- couche metier autour du repository.

Constante metier:

- `MaxJobs = 5`

Methodes:

- `GetJobsAsync()` relaye la lecture repository.
- `AddJobAsync()` valide le job, recharge la liste complete, verifie la limite, ajoute, puis sauvegarde.
- `ValidateJob()` centralise toutes les regles metier de validite.

Regles de `ValidateJob()`:

- job non null;
- `Name` non vide;
- `SourceDirectory` non vide;
- le dossier source doit exister;
- `TargetDirectory` non vide;
- le dossier cible doit pouvoir etre cree;
- `Type` doit etre une valeur definie de l'enum.

Point important:

- la validation cree deja le dossier cible si besoin.
- donc meme avant execution reelle d'une sauvegarde, l'ajout d'un job peut materialiser le dossier cible sur disque.

## Gestionnaire d'execution

### `Services/BackupManager.cs`

Role:

- point d'entree metier pour lancer une ou plusieurs sauvegardes.

Methodes:

- `ExecuteJobAsync(int jobIndex)`: charge les jobs, valide l'index 1-based, puis execute le job correspondant.
- `ExecuteAllJobsAsync()`: execute tous les jobs dans l'ordre de la liste.
- `ExecuteJobsAsync(IEnumerable<int>)`: execute chaque index dans l'ordre recu.

Implementation:

- revalide le job via `BackupJobService.ValidateJob`;
- choisit la strategie via `BackupStrategyFactory.Create(job.Type)`;
- cree un `BackupExecutionContext` contenant `StateManager` et `Logger`;
- execute la strategie.

Important:

- les jobs sont executes sequentiellement, jamais en parallele.
- si un job leve une exception avant la boucle de copie, l'exception remonte.

### `Services/StateManager.cs`

Role:

- maintenir `state.json` a jour de facon thread-safe.

Mecanisme:

- utilise `SemaphoreSlim writeLock = new(1, 1)`;
- serialize toujours l'ensemble des etats.

Comportement de `UpdateAsync()`:

1. charge la liste existante;
2. cherche un etat de meme nom sans tenir compte de la casse;
3. remplace ou ajoute;
4. ecrase `LastActionTimestamp` avec `DateTime.Now`;
5. reecrit tout le fichier.

Point important:

- deux jobs de meme nom se marcheraient dessus dans `state.json`.
- le nom du job est donc implicitement traite comme cle fonctionnelle, sans contrainte explicite d'unicite.

## Strategies de sauvegarde

### `Strategies/IBackupStrategy.cs`

Contrat:

- toute strategie doit implementer `ExecuteAsync(BackupJob, BackupExecutionContext, CancellationToken)`.

### `Strategies/BackupExecutionContext.cs`

Petit conteneur d'injection:

- `StateManager`
- `ILoggerService`

### `Strategies/BackupStrategyFactory.cs`

Role:

- instancier la bonne strategie selon `BackupType`.

Mappings:

- `Complete` -> `CompleteBackupStrategy`
- `Differential` -> `DifferentialBackupStrategy`

Erreur:

- toute autre valeur leve `ArgumentOutOfRangeException`.

### `Strategies/CompleteBackupStrategy.cs`

Role:

- copier tous les fichiers sans condition.

Implementation:

- delegue a `BackupStrategyRunner.ExecuteAsync()` avec `ShouldCopy => true`.

### `Strategies/DifferentialBackupStrategy.cs`

Role:

- copier uniquement les fichiers absents ou modifies.

Condition exacte:

- si le fichier destination n'existe pas: copier;
- sinon copier si `sourceFile.LastWriteTimeUtc > destinationFile.LastWriteTimeUtc`;
- sinon copier si `sourceFile.Length != destinationFile.Length`.

Interpretation:

- le differentiel n'est pas base sur une sauvegarde precedente explicite en base;
- il compare simplement source et destination a l'instant de l'execution.

### `Strategies/BackupStrategyRunner.cs`

C'est la piece centrale du systeme.

Role:

- enumerer les fichiers source;
- filtrer ceux a copier selon la strategie;
- mettre a jour l'etat;
- copier les fichiers;
- journaliser chaque resultat;
- cloturer l'etat final.

Flux exact:

1. normalise `SourceDirectory` et `TargetDirectory` via `Path.GetFullPath`.
2. enumere tous les fichiers source recursivement.
3. construit `plannedFiles` selon la predicate `shouldCopy`.
4. cree le dossier racine cible.
5. calcule le nombre total et la taille totale planifiee.
6. initialise un `BackupState` en `Active`.
7. sauvegarde cet etat via `StateManager`.
8. pour chaque fichier planifie:
9. verifie l'annulation via `cancellationToken.ThrowIfCancellationRequested()`.
10. calcule la destination en conservant le chemin relatif.
11. met a jour `CurrentSourceFilePath` et `CurrentDestinationFilePath`.
12. relance une sauvegarde d'etat.
13. demarre un `Stopwatch`.
14. cree le dossier parent du fichier destination.
15. copie le fichier avec `overwrite: true`.
16. met a jour progression et taille restante.
17. ecrit un log `Success`.
18. met a jour l'etat.
19. si une exception non annulation survient:
20. marque `state.State = "Error"`;
21. ecrit un log `Error` avec `ErrorMessage`;
22. continue quand meme avec les fichiers suivants.
23. en fin de boucle:
24. `state.State = hasError ? "Error" : "Finished"`;
25. vide les chemins courants;
26. force `RemainingFiles = 0` et `RemainingSize = 0`;
27. si aucun fichier n'etait planifie, force `Progression = 100`;
28. ecrit l'etat final.

Details structurants:

- un echec sur un fichier ne stoppe pas le job complet;
- un `OperationCanceledException` n'est pas absorbe par le `catch`;
- la progression est calculee en pourcentage sur le nombre de fichiers, pas sur le volume;
- la taille restante suit le volume restant en octets;
- la copie est synchrone (`sourceFile.CopyTo`) meme si elle vit dans une methode async;
- il n'y a ni buffer personnalise, ni reprise, ni parallellisme.

Calcul du chemin cible:

- `GetDestinationPath()` prend le chemin relatif depuis la racine source puis le combine a la racine cible.

Exemple:

- source root: `/src`
- fichier: `/src/a/b.txt`
- cible root: `/backup`
- destination: `/backup/a/b.txt`

Gestion des erreurs:

- les erreurs de copie par fichier sont journalisees et n'arretent pas le reste;
- les erreurs de validation avant la boucle ou d'enumeration initiale remontent.

## Projet `EasyLog`

### `ILoggerService.cs`

Contrat minimal:

- `LogAsync(LogEntry entry, CancellationToken cancellationToken = default)`

### `LogEntry.cs`

Modele d'une ligne de journal:

- `Timestamp`
- `BackupName`
- `SourceFilePath`
- `DestinationFilePath`
- `FileSize`
- `TransferTimeMs`
- `Status`
- `ErrorMessage`

### `JsonLoggerService.cs`

Role:

- ecrire les logs dans un fichier JSON quotidien.

Comportement:

- cree le dossier de logs au constructeur;
- si aucun dossier n'est fourni, utilise le dossier par defaut sous `LocalApplicationData/ProSoft/EasySave/logs`;
- verrouille les ecritures avec `SemaphoreSlim`;
- lit le fichier du jour entier, ajoute une entree, reecrit le fichier complet.

Nom de fichier:

- `yyyy-MM-dd.json` base sur `DateTime.Now`.

Consequences:

- toutes les entrees du meme jour sont stockees dans un tableau JSON unique;
- plus le fichier grossit, plus chaque ecriture coute lecture + reserialisation completes.

## Ressources de langue

Fichiers:

- `EasySave.Console/Resources/fr.json`
- `EasySave.Console/Resources/en.json`

Usage:

- copies dans le dossier de sortie via `CopyToOutputDirectory="PreserveNewest"`.

Les cles couvrent:

- titres et libelles du menu;
- messages d'erreur CLI;
- messages d'erreur de validation;
- messages de fin et d'absence de jobs.

Point notable:

- en anglais, `LanguageFrench` vaut `"French"` au lieu de `"Français"`, ce qui est coherent avec une UI anglaise.

## Tests

### `AppPathsTests.cs`

Verifie:

- l'override `EASYSAVE_APPDATA`;
- la creation des dossiers;
- la construction des chemins.

### `BackupInfrastructureTests.cs`

Verifie:

- rejet d'un source directory inexistant;
- rejet du 6e job;
- creation et contenu minimal de `state.json`;
- creation d'un log JSON journalier.

### `BackupExecutionTests.cs`

Verifie:

- execution complete d'une sauvegarde avec vrais fichiers copies;
- conservation de l'arborescence en sauvegarde complete;
- creation des logs pendant l'execution;
- mise a jour finale du `state.json` apres sauvegarde complete;
- comportement de la strategie differentielle sur fichiers inchanges, modifies et manquants.

### `CliArgumentParserTests.cs`

Verifie:

- parsing de plage `1-3`;
- parsing de liste `1;3`;
- rejet des doublons;
- rejet des indexes hors limites.

### Couverture manquante ou faible

Non verifies directement par tests:

- traduction des messages dans `ConsoleMenu`;
- persistance de `jobs.json`;
- annulation via `CancellationToken`;
- comportement si deux jobs ont le meme nom;
- comportement sur erreurs de permissions ou chemins invalides;
- fonctionnement du menu interactif bout en bout.

Ecart documentation/code:

- la couverture de sauvegarde complete et differentielle est maintenant presente via `BackupExecutionTests.cs`;
- les sources UML restent exportees en PNG dans le depot, pas en `.puml`.

## Formats de donnees attendus

### `jobs.json`

Tableau JSON de `BackupJob`.

Exemple:

```json
[
  {
    "Name": "Docs",
    "SourceDirectory": "/home/user/docs",
    "TargetDirectory": "/mnt/backup/docs",
    "Type": 1
  }
]
```

Remarque:

- `Type` est serialize comme entier de l'enum, pas comme texte.

### `settings.json`

Objet JSON simple.

Exemple:

```json
{
  "Language": "fr"
}
```

### `state.json`

Tableau JSON de `BackupState`.

Exemple indicatif:

```json
[
  {
    "Name": "Docs",
    "LastActionTimestamp": "2026-05-01T12:00:00+02:00",
    "State": "Finished",
    "TotalFilesToCopy": 12,
    "TotalFilesSize": 123456,
    "Progression": 100,
    "RemainingFiles": 0,
    "RemainingSize": 0,
    "CurrentSourceFilePath": "",
    "CurrentDestinationFilePath": ""
  }
]
```

### `logs/yyyy-MM-dd.json`

Tableau JSON de `LogEntry`.

Exemple indicatif:

```json
[
  {
    "Timestamp": "2026-05-01T12:00:00+02:00",
    "BackupName": "Docs",
    "SourceFilePath": "/home/user/docs/a.txt",
    "DestinationFilePath": "/mnt/backup/docs/a.txt",
    "FileSize": 245,
    "TransferTimeMs": 3,
    "Status": "Success",
    "ErrorMessage": null
  }
]
```

## Hypotheses confirmees par lecture + tests

- le projet cible `.NET 8`.
- le binaire final du projet console s'appelle `EasySave`.
- la journalisation et l'etat utilisent du JSON indente.
- la limite metier de 5 jobs est reelle et testee.
- l'override `EASYSAVE_APPDATA` est reel et teste.
- le parseur CLI travaille bien en index utilisateur base 1.
- le moteur continue apres une erreur de copie sur un fichier.

## Limitations observees

- pas de suppression de jobs;
- pas de modification de jobs;
- pas de pause;
- pas de reprise;
- pas de chiffrement;
- pas de parallellisation;
- pas de verification de collisions de noms de jobs;
- pas de logs par streaming append, tout est reserialize;
- pas de gestion speciale des liens symboliques ou fichiers verrouilles;
- pas de filtre d'extensions ou d'exclusions;
- pas de distinction metier entre "aucun fichier a copier" et "job termine normalement", les deux finissent a `100%`.

## Risques techniques

- `ConsoleMenu.TranslateExceptionMessage()` depend de chaines exactes d'exceptions anglaises: couplage fragile.
- `StateManager` utilise le nom du job comme cle implicite: collisions possibles.
- `JsonLoggerService` et `BackupJobRepository` reecrivent le fichier entier a chaque mise a jour: cout croissant avec la taille.
- `BackupStrategyRunner` fait une enumeration complete en memoire via `.ToList()`: peut etre lourd sur tres gros volumes.
- la copie utilise `FileInfo.CopyTo`, donc pas de progression intra-fichier ni d'async reel sur les gros fichiers.

## Ce que le projet ne fait pas, malgre ce qu'on pourrait imaginer

- il n'y a pas de base de donnees;
- il n'y a pas d'UI WPF dans cette version;
- il n'y a pas de backup parallele;
- il n'y a pas d'incremental complexe avec historique;
- il n'y a pas de suivi d'etat par ID unique;
- il n'y a pas de suppression automatique des fichiers supprimes cote source pendant un differentiel.

## Verification effectuee

Commande executee:

```bash
dotnet test EasySave.sln
```

Resultat observe:

- 9 tests passes;
- 0 echec;
- 0 ignore.

## Niveau de confiance

Confiance elevee sur:

- architecture globale;
- flux d'execution;
- persistance JSON;
- comportement du parseur CLI;
- mecanique des strategies;
- logique de journalisation et d'etat.

Confiance moyenne sur:

- comportement reel en environnement atypique avec permissions restreintes;
- comportement sur volumes massifs;
- details d'execution des binaires deja presents dans `publish/` puisque l'analyse est centree sur le code source.

## Resume ultra court

EasySave est une application console .NET 8 de sauvegarde simple, structuree proprement en couches. Le coeur utile est dans `BackupStrategyRunner`: il enumere les fichiers, decide quoi copier selon la strategie, met a jour `state.json`, et ecrit un log JSON par fichier. Les donnees sont stockees sous `LocalApplicationData/ProSoft/EasySave` ou sous `EASYSAVE_APPDATA` si cette variable est definie.
