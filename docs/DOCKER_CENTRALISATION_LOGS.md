# Centralisation des fichiers log journalier sous Docker

## Objectif exact

Le besoin demandé est le suivant :

- EasySave peut être déployé sur plusieurs postes ou serveurs.
- Il faut un service Docker de centralisation des logs en temps réel.
- Le client doit pouvoir choisir entre 3 modes :
  - `local` : logs uniquement sur le poste utilisateur
  - `central` : logs uniquement sur le serveur Docker
  - `both` : logs à la fois sur le poste utilisateur et sur le serveur Docker
- Le serveur Docker doit garder **un seul fichier journalier par jour**, quel que soit le nombre de machines ou d’utilisateurs.
- Les entrées doivent permettre d’identifier au minimum :
  - l’utilisateur
  - la machine
  - le job de sauvegarde
  - le timestamp

Ce document décrit précisément :

- l’état actuel du projet
- ce qu’il faut ajouter
- où il faut modifier le code
- comment structurer Docker
- quel format de log centralisé adopter
- comment tester le tout

---

## État actuel du projet

### Emplacement actuel des logs

Aujourd’hui, le projet écrit les logs localement dans :

- [EasySave.Core/Configuration/AppPaths.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.Core/Configuration/AppPaths.cs)

La constante actuelle est :

- `AppPaths.LogsDirectory = Path.Combine(BaseDirectory, "logs")`

Donc les logs sont écrits dans le dossier du projet :

- `/home/mourad/Bureau/A3-groupe-05/logs`

### Services de logs actuellement présents

Les services actuels sont :

- [EasyLog/JsonLoggerService.cs](/home/mourad/Bureau/A3-groupe-05/EasyLog/JsonLoggerService.cs)
- [EasyLog/XmlLoggerService.cs](/home/mourad/Bureau/A3-groupe-05/EasyLog/XmlLoggerService.cs)
- [EasyLog/LogEntry.cs](/home/mourad/Bureau/A3-groupe-05/EasyLog/LogEntry.cs)
- [EasyLog/ILoggerService.cs](/home/mourad/Bureau/A3-groupe-05/EasyLog/ILoggerService.cs)

### Limite actuelle

La version actuelle :

- sait écrire localement en `JSON`
- sait écrire localement en `XML`
- ne sait pas envoyer les logs à un serveur central
- ne différencie pas explicitement l’utilisateur et la machine dans le modèle `LogEntry`

Donc, **le besoin Docker de centralisation n’est pas encore couvert par le code actuel**.

---

## Décision d’architecture recommandée

### Décision principale

Il faut séparer 2 choses :

1. le **format local**
2. le **format centralisé**

### Pourquoi

Le local doit rester compatible avec le besoin déjà fait :

- `JSON`
- `XML`

Le centralisé doit, lui, être :

- unique
- append-safe
- simple à verrouiller
- indépendant du poste client

### Recommandation

Conserver :

- `JSON/XML` pour le local

Utiliser :

- `JSON` pour le centralisé

### Conséquence

Le central Docker doit produire **un seul fichier journalier JSON**, par exemple :

- `/var/lib/easysave-centralizer/logs/2026-05-12.json`

Le local peut continuer à produire :

- `logs/2026-05-12.json`
- ou `logs/2026-05-12.xml`

suivant le choix de l’utilisateur.

---

## Modes à ajouter dans l’application

Il faut ajouter un vrai mode de stockage des logs dans les paramètres.

### Nouveau champ recommandé

Dans [EasySave.Core/Models/AppSettings.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.Core/Models/AppSettings.cs), ajouter :

```csharp
public string LogStorageModeName { get; set; } = "local";
public string CentralLogServerUrl { get; set; } = string.Empty;
public string ClientDisplayName { get; set; } = string.Empty;
```

### Valeurs recommandées

- `local`
- `central`
- `both`

### Enum recommandée

Ajouter par exemple :

```csharp
public enum LogStorageMode
{
    Local,
    Central,
    Both
}
```

Puis un accès calculé :

```csharp
public LogStorageMode LogStorageMode => LogStorageModeName.ToLowerInvariant() switch
{
    "central" => LogStorageMode.Central,
    "both" => LogStorageMode.Both,
    _ => LogStorageMode.Local
};
```

---

## Métadonnées à ajouter dans chaque log

Aujourd’hui, [EasyLog/LogEntry.cs](/home/mourad/Bureau/A3-groupe-05/EasyLog/LogEntry.cs) contient :

- `Timestamp`
- `BackupName`
- `SourceFilePath`
- `DestinationFilePath`
- `FileSize`
- `TransferTimeMs`
- `EncryptionTimeMs`
- `Status`
- `ErrorMessage`

Pour satisfaire le besoin multi-utilisateur / multi-machine, il faut ajouter :

```csharp
public string MachineName { get; set; } = string.Empty;
public string UserName { get; set; } = string.Empty;
public string ClientDisplayName { get; set; } = string.Empty;
public string LogOrigin { get; set; } = string.Empty;
```

### Signification

- `MachineName`
  - nom de la machine qui a généré le log
- `UserName`
  - nom de l’utilisateur système
- `ClientDisplayName`
  - nom configuré par le client si plusieurs postes doivent être différenciés clairement
- `LogOrigin`
  - par exemple `local` ou `central-forwarded`

### Valeurs recommandées au runtime

- `MachineName = Environment.MachineName`
- `UserName = Environment.UserName`
- `ClientDisplayName = settings.ClientDisplayName`

---

## Services à créer

## 1. Logger HTTP central

Créer un service par exemple :

- `EasyLog/HttpCentralLoggerService.cs`

Responsabilité :

- sérialiser une `LogEntry`
- l’envoyer au service Docker via HTTP

### Contrat recommandé

- `POST /api/logs`
- body : JSON

Exemple :

```json
{
  "timestamp": "2026-05-12T19:45:12.123Z",
  "backupName": "Documents Perso",
  "sourceFilePath": "/home/mourad/Documents/a.txt",
  "destinationFilePath": "/mnt/backup/a.txt",
  "fileSize": 1204,
  "transferTimeMs": 12,
  "encryptionTimeMs": 0,
  "status": "Success",
  "errorMessage": null,
  "machineName": "POSTE-01",
  "userName": "mourad",
  "clientDisplayName": "Poste direction",
  "logOrigin": "client"
}
```

### Comportement recommandé

- timeout court
- pas de crash de sauvegarde si le serveur central tombe
- remonter une erreur fonctionnelle si le mode est `central` strict et que l’envoi échoue
- si le mode est `both`, le local doit rester écrit même si le central échoue

---

## 2. Logger composite

Créer un service :

- `EasyLog/CompositeLoggerService.cs`

Responsabilité :

- recevoir une `LogEntry`
- la redistribuer à plusieurs loggers concrets

### Exemple

En mode :

- `local` : utilise seulement `JsonLoggerService` ou `XmlLoggerService`
- `central` : utilise seulement `HttpCentralLoggerService`
- `both` : utilise local + central

---

## 3. Factory de logger unifiée

Aujourd’hui, la création des loggers est dispersée :

- [EasySave.Console/Program.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.Console/Program.cs)
- [EasySave.GUI/ViewModels/MainWindowViewModel.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.GUI/ViewModels/MainWindowViewModel.cs)

Il faut centraliser ça dans une vraie factory, par exemple :

- `EasyLog/LoggerFactory.cs`

Responsabilité :

- lire `AppSettings`
- choisir le bon logger
- assembler `CompositeLoggerService` si besoin

---

## Service Docker de centralisation

## But du service

Le service Docker doit :

- recevoir les logs de plusieurs clients
- écrire un seul fichier journalier par jour
- supporter les écritures concurrentes
- garder les fichiers sur un volume Docker persistant

### Format recommandé du fichier central

Fichier :

- `/var/lib/easysave-centralizer/logs/2026-05-12.json`

Contenu :

- tableau JSON

Exemple :

```json
[
  {
    "timestamp": "2026-05-12T19:45:12.123Z",
    "backupName": "Docs RH",
    "sourceFilePath": "C:\\Users\\amine\\Documents\\a.txt",
    "destinationFilePath": "D:\\Backup\\a.txt",
    "fileSize": 1204,
    "transferTimeMs": 18,
    "encryptionTimeMs": 0,
    "status": "Success",
    "errorMessage": null,
    "machineName": "PC-RH-01",
    "userName": "amine",
    "clientDisplayName": "Poste RH",
    "logOrigin": "client"
  }
]
```

### Règle importante

Il faut **un verrou d’écriture** côté serveur central.

Sinon :

- plusieurs clients peuvent casser le JSON du fichier

La logique doit être :

- lire le fichier du jour
- désérialiser la liste
- ajouter l’entrée
- réécrire
- le tout sous `SemaphoreSlim` ou verrou équivalent

---

## Structure Docker recommandée dans ce dépôt

Créer un dossier :

- `docker/log-centralizer/`

Avec :

- `docker/log-centralizer/Dockerfile`
- `docker/log-centralizer/docker-compose.yml`
- `docker/log-centralizer/README.md`

### Service conseillé

Le plus simple dans cet écosystème .NET :

- un petit service ASP.NET Core minimal API

Nom recommandé :

- `EasySave.LogCentralizer`

Arborescence recommandée :

```text
EasySave.LogCentralizer/
  EasySave.LogCentralizer.csproj
  Program.cs
  Models/CentralLogEntry.cs
  Services/CentralLogWriter.cs
docker/log-centralizer/
  Dockerfile
  docker-compose.yml
```

---

## Exemple de `docker-compose.yml`

Exemple recommandé :

```yaml
version: "3.9"

services:
  easysave-log-centralizer:
    build:
      context: ../..
      dockerfile: docker/log-centralizer/Dockerfile
    container_name: easysave-log-centralizer
    ports:
      - "5080:8080"
    volumes:
      - easysave_central_logs:/var/lib/easysave-centralizer/logs
    restart: unless-stopped

volumes:
  easysave_central_logs:
```

### Résultat

Les logs centralisés seront stockés dans le volume Docker :

- `easysave_central_logs`

et dans le conteneur au chemin :

- `/var/lib/easysave-centralizer/logs`

---

## Exemple de `Dockerfile`

Exemple recommandé :

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish EasySave.LogCentralizer/EasySave.LogCentralizer.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

VOLUME ["/var/lib/easysave-centralizer/logs"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "EasySave.LogCentralizer.dll"]
```

---

## Contrat HTTP recommandé

### Endpoint principal

- `POST /api/logs`

### Réponses recommandées

- `202 Accepted` : log accepté
- `400 Bad Request` : payload invalide
- `500 Internal Server Error` : échec écriture serveur

### Endpoint de santé

- `GET /health`

Réponse :

```json
{
  "status": "ok"
}
```

---

## Intégration exacte dans EasySave

## Fichiers à modifier

### Configuration métier

- [EasySave.Core/Models/AppSettings.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.Core/Models/AppSettings.cs)
- [EasySave.Core/Configuration/AppSettingsRepository.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.Core/Configuration/AppSettingsRepository.cs)

À ajouter :

- `LogStorageModeName`
- `CentralLogServerUrl`
- `ClientDisplayName`

### Modèle de log

- [EasyLog/LogEntry.cs](/home/mourad/Bureau/A3-groupe-05/EasyLog/LogEntry.cs)

À ajouter :

- `MachineName`
- `UserName`
- `ClientDisplayName`
- `LogOrigin`

### Création de loggers

- [EasySave.Console/Program.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.Console/Program.cs)
- [EasySave.GUI/ViewModels/MainWindowViewModel.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.GUI/ViewModels/MainWindowViewModel.cs)

À modifier :

- remplacer la création directe `JsonLoggerService` / `XmlLoggerService`
- utiliser une factory unique

### UI et CLI

Il faut aussi exposer les nouveaux paramètres :

- mode de stockage des logs
- URL du serveur central
- nom d’affichage du client

Donc, modifier :

- [EasySave.GUI/ViewModels/MainWindowViewModel.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.GUI/ViewModels/MainWindowViewModel.cs)
- [EasySave.GUI/Views/MainWindow.axaml](/home/mourad/Bureau/A3-groupe-05/EasySave.GUI/Views/MainWindow.axaml)
- [EasySave.Console/ConsoleMenu.cs](/home/mourad/Bureau/A3-groupe-05/EasySave.Console/ConsoleMenu.cs)
- [EasySave.Console/Resources/fr.json](/home/mourad/Bureau/A3-groupe-05/EasySave.Console/Resources/fr.json)
- [EasySave.Console/Resources/en.json](/home/mourad/Bureau/A3-groupe-05/EasySave.Console/Resources/en.json)

---

## Comportement attendu par mode

## 1. Mode `local`

### Attendu

- le log est écrit seulement sur le poste client
- aucun appel HTTP n’est fait au serveur Docker

### Fichiers créés

- `logs/yyyy-MM-dd.json`
- ou `logs/yyyy-MM-dd.xml`

---

## 2. Mode `central`

### Attendu

- aucun log local n’est écrit
- chaque entrée est envoyée au serveur Docker
- le serveur Docker l’ajoute dans l’unique fichier journalier du jour

### Fichier créé

- `/var/lib/easysave-centralizer/logs/yyyy-MM-dd.json`

---

## 3. Mode `both`

### Attendu

- le log local est écrit
- le même log est envoyé au serveur Docker

### Fichiers créés

Client :

- `logs/yyyy-MM-dd.json`
- ou `logs/yyyy-MM-dd.xml`

Serveur central :

- `/var/lib/easysave-centralizer/logs/yyyy-MM-dd.json`

---

## Différenciation utilisateur / machine

Le besoin dit explicitement :

- permettre de différencier l’utilisateur

Il faut donc absolument avoir dans le log central au minimum :

- `UserName`
- `MachineName`

Option recommandée en plus :

- `ClientDisplayName`

Ainsi, si 10 postes envoient leurs logs dans le même fichier du jour, le fichier reste exploitable.

---

## Stratégie de test recommandée

## Test 1. Mode local

### Préparation

- `LogStorageMode = local`

### Résultat attendu

- un fichier local est créé
- rien n’est écrit dans Docker

---

## Test 2. Mode central

### Préparation

- lancer Docker :

```bash
cd /home/mourad/Bureau/A3-groupe-05/docker/log-centralizer
docker compose up -d --build
```

- configurer :
  - `LogStorageMode = central`
  - `CentralLogServerUrl = http://localhost:5080`

### Résultat attendu

- pas de fichier local
- un fichier du jour existe dans le volume Docker

### Vérification

```bash
docker ps
docker exec -it easysave-log-centralizer sh
ls /var/lib/easysave-centralizer/logs
cat /var/lib/easysave-centralizer/logs/2026-05-12.json
```

---

## Test 3. Mode both

### Préparation

- `LogStorageMode = both`

### Résultat attendu

- le fichier local existe
- le fichier central existe aussi

---

## Test 4. Plusieurs utilisateurs

### Préparation

- démarrer plusieurs clients
- faire exécuter plusieurs sauvegardes le même jour

### Résultat attendu

- un seul fichier central du jour
- plusieurs entrées
- chaque entrée contient `MachineName` et `UserName`

---

## Point critique de cohérence

### Ce qu’il ne faut pas faire

- créer un fichier par utilisateur côté central
- créer un fichier par machine côté central
- utiliser du XML central si plusieurs clients écrivent en parallèle sans stratégie forte de verrouillage

### Ce qu’il faut faire

- un seul fichier journalier côté central
- verrou d’écriture côté serveur
- métadonnées machine/utilisateur dans chaque entrée

---

## Ordre de développement recommandé

1. étendre `AppSettings`
2. étendre `LogEntry`
3. créer `HttpCentralLoggerService`
4. créer `CompositeLoggerService`
5. créer une factory de logger unique
6. brancher GUI + CLI sur les nouveaux paramètres
7. créer le projet `EasySave.LogCentralizer`
8. créer `Dockerfile` + `docker-compose.yml`
9. ajouter les tests
10. valider les 3 modes

---

## Conclusion

Pour satisfaire exactement le besoin, il faut :

- conserver les logs locaux actuels
- ajouter un service HTTP central sous Docker
- ajouter un mode de stockage `local / central / both`
- écrire un seul fichier journalier côté Docker
- enrichir chaque entrée avec l’utilisateur et la machine

Le projet actuel possède déjà une base propre pour ça :

- `ILoggerService`
- `LogEntry`
- `AppSettings`
- une factory de log quasi prête à être centralisée

La centralisation Docker doit maintenant s’ajouter **au-dessus** de cette base, sans casser le local existant.
