EasySave - Methode de travail GitHub

Ce document explique comment l'equipe doit travailler avec GitHub pour le projet EasySave.
Le but est d'eviter les conflits, de proteger la branche principale et de garder un projet propre pour le livrable.

REGLE PRINCIPALE

Personne ne doit travailler directement sur la branche main.

La branche main doit contenir uniquement une version stable, propre et livrable du projet.

Chaque membre du groupe doit travailler sur sa propre branche feature/..., puis creer une Pull Request vers `main` ou `release/v1.0` selon le stade du livrable.

Le responsable du repo verifie le code, puis valide la Pull Request.

ORGANISATION DES BRANCHES

main
feature/core-backup
feature/easylog
feature/state-manager
feature/pre-release-fixes
feature/publish-artifacts
feature/fix-push
release/v1.0

ROLE DES BRANCHES

main
= version finale stable du projet
= branche protegee
= personne ne push directement dessus

feature/...
= branche de travail personnelle pour chaque fonctionnalite

release/v1.0
= branche utilisee pour preparer la livraison finale du livrable 1

REPARTITION DU TRAVAIL

feature/core-backup
= BackupJob, BackupManager, sauvegarde complete, sauvegarde differentielle

feature/easylog
= EasyLog.dll, JsonLoggerService, LogEntry, logs JSON journaliers

feature/state-manager
= StateManager, state.json, etat temps reel des sauvegardes

feature/pre-release-fixes
= corrections de fin de livrable, localisation, validation et tests d'infrastructure

feature/publish-artifacts
= ajout des artefacts publies dans `publish/`

feature/fix-push
= corrections et ajustements divers du depot

DEBUTER SON TRAVAIL

Avant de commencer a coder, chaque membre doit recuperer la derniere version de `main`.

Commande :

git checkout main
git pull origin main

Ensuite, il faut creer sa branche de travail.

Exemple :

git checkout -b feature/easylog

Autres exemples :

git checkout -b feature/core-backup
git checkout -b feature/state-manager
git checkout -b feature/pre-release-fixes
git checkout -b feature/publish-artifacts

TRAVAILLER SUR SA BRANCHE

Apres avoir modifie les fichiers, verifier l'etat du projet :

git status

Ajouter les fichiers modifies :

git add .

Faire un commit clair :

git commit -m "feat(easylog): add daily JSON logger"

Envoyer la branche sur GitHub :

git push origin feature/easylog

FORMAT DES COMMITS

Les commits doivent etre clairs, courts et en anglais.

Format conseille :

type(scope): message

Exemples :

git commit -m "feat(backup): add complete backup strategy"
git commit -m "feat(easylog): add JSON daily log service"
git commit -m "feat(state): update real-time backup state"
git commit -m "fix(cli): handle invalid backup range"
git commit -m "docs(readme): add GitHub workflow"
git commit -m "test(cli): add argument parser tests"
git commit -m "refactor(core): improve backup manager structure"

Types possibles :

feat     = nouvelle fonctionnalite
fix      = correction de bug
docs     = documentation
test     = tests
refactor = amelioration du code sans changer le comportement
chore    = configuration ou taches diverses

CREER UNE PULL REQUEST

Quand une fonctionnalite est terminee, il faut creer une Pull Request sur GitHub.

Chemin :

GitHub
-> Pull requests
-> New pull request
-> base: main
-> compare: feature/nom-de-la-branche
-> Create pull request

Exemple :

base: main
compare: feature/easylog

La Pull Request doit expliquer ce qui a ete fait.

Exemple de description :

Travail realise :
- Ajout de EasyLog.dll
- Creation de JsonLoggerService
- Creation du modele LogEntry
- Ecriture des logs journaliers en JSON indente

Tests realises :
- dotnet build OK
- Verification de la creation du fichier log OK
- Verification du JSON indente OK

Remarques :
- Aucune erreur connue

AVANT DE CREER UNE PULL REQUEST

Chaque membre doit verifier que le projet compile.

dotnet restore
dotnet build
dotnet test

Si une commande echoue, il faut corriger avant de creer la Pull Request.

REGLES INTERDITES

Ne jamais travailler directement sur main.

Ne jamais push directement sur main.

Commande interdite :

git push origin main

Ne jamais faire de force push sur une branche commune.

Commande interdite :

git push --force

Ne jamais supprimer une branche importante sans prevenir le groupe.

Ne jamais modifier une partie qui appartient a un autre membre sans lui demander.

REGLES OBLIGATOIRES

Avant de commencer a travailler :

git checkout main
git pull origin main

Avant de pousser son travail :

dotnet build

Avant de creer une Pull Request :

dotnet test

Chaque fonctionnalite doit etre faite dans une branche separee.

Chaque Pull Request doit etre relue avant d'etre fusionnee.

Le code doit rester clair, propre et en anglais.

Les noms de classes, methodes, variables et commentaires doivent etre en anglais.

Il faut eviter les copier-coller inutiles.

MISE A JOUR DE SA BRANCHE AVEC MAIN

Si `main` a change pendant que tu travailles, tu dois recuperer les changements.

git checkout feature/nom-de-la-branche
git pull origin main

Exemple :

git checkout feature/easylog
git pull origin main

Ensuite, s'il y a des conflits, il faut les corriger avant de continuer.

GESTION DES CONFLITS

Quand Git affiche un conflit, ouvrir les fichiers concernes.

Git montre souvent des zones comme ceci :

<<<<<<< HEAD
code actuel
=======
code venant de l'autre branche
>>>>>>> main

Il faut garder le bon code, supprimer les marqueurs, puis refaire un commit.

git add .
git commit -m "fix(conflict): resolve merge conflict"

Apres correction, tester le projet :

dotnet build
dotnet test

PROTECTION DE LA BRANCHE MAIN

La branche main doit etre protegee sur GitHub.

Configuration recommandee :

Branch name pattern:
main

A cocher :
[x] Require a pull request before merging
[x] Require approvals
[x] Dismiss stale pull request approvals when new commits are pushed
[x] Require approval of the most recent reviewable push
[x] Require conversation resolution before merging
[x] Do not allow bypassing the above settings

A ne pas cocher :
[ ] Require review from Code Owners
[ ] Require status checks to pass before merging
[ ] Require signed commits
[ ] Require linear history
[ ] Require deployments to succeed before merging
[ ] Lock branch
[ ] Allow force pushes
[ ] Allow deletions

Avec cette configuration, personne ne peut casser directement la branche main.

Tout doit passer par une Pull Request.

ARCHITECTURE ATTENDUE DU PROJET

Le projet doit respecter une structure propre.

EasySave.sln
|
|-- EasySave.Console
|   |-- Program.cs
|   |-- ConsoleMenu.cs
|   |-- CliArgumentParser.cs
|   |-- LanguageSelector.cs
|
|-- EasySave.Core
|   |-- Models
|   |-- Services
|   |-- Strategies
|   |-- Configuration
|
|-- EasyLog
|   |-- ILoggerService.cs
|   |-- JsonLoggerService.cs
|   |-- LogEntry.cs
|
|-- EasySave.Tests
|   |-- Tests unitaires
|
|-- docs
    |-- USER_MANUAL.md
    |-- SUPPORT.md
    |-- RELEASE_NOTES_v1.0.md
    |-- UML

COMMANDES UTILES

Voir la branche actuelle :

git branch

Voir les fichiers modifies :

git status

Changer de branche :

git checkout nom-de-la-branche

Creer une branche :

git checkout -b feature/nom-de-la-branche

Recuperer la derniere version de `main` :

git checkout main
git pull origin main

Envoyer sa branche sur GitHub :

git push origin feature/nom-de-la-branche

Compiler le projet :

dotnet build

Lancer les tests :

dotnet test

PREPARATION DE LA LIVRAISON V1.0

Quand toutes les fonctionnalites sont terminees et validees sur `main`, creer une branche de release.

git checkout main
git pull origin main
git checkout -b release/v1.0
git push origin release/v1.0

Sur cette branche, il faut uniquement corriger les derniers bugs et verifier les documents.

A verifier avant la livraison :

- Le projet compile
- Les tests passent
- EasyLog.dll fonctionne
- Les logs JSON sont crees
- Le fichier state.json est mis a jour
- La sauvegarde complete fonctionne
- La sauvegarde differentielle fonctionne
- Les commandes CLI fonctionnent
- Le mode francais/anglais fonctionne
- Le README est complet
- Le manuel utilisateur est present
- La fiche support est presente
- La release note est presente
- Les diagrammes UML sont presents

Quand tout est valide, fusionner release/v1.0 dans main.

git checkout main
git pull origin main
git merge release/v1.0
git push origin main

Creer ensuite le tag de livraison :

git tag -a v1.0 -m "Livrable 1 - EasySave v1.0"
git push origin v1.0

CHECKLIST FINALE

[ ] Le projet compile
[ ] Les tests passent
[ ] La branche main est protegee
[ ] Personne ne push directement sur main
[ ] Chaque membre travaille sur une branche feature
[ ] Les Pull Requests sont faites vers `main` ou `release/v1.0`
[ ] Le code est relu avant d'etre fusionne
[ ] EasyLog.dll est present
[ ] Les logs JSON fonctionnent
[ ] Le fichier state.json fonctionne
[ ] La sauvegarde complete fonctionne
[ ] La sauvegarde differentielle fonctionne
[ ] Les commandes CLI fonctionnent
[ ] Le mode FR/EN fonctionne
[ ] Le README est a jour
[ ] Le manuel utilisateur est present
[ ] La fiche support est presente
[ ] La release note est presente
[ ] Les diagrammes UML sont presents
[ ] Le tag v1.0 est cree

RESUME

main = version stable du depot
feature/... = travail de chaque membre
release/v1.0 = preparation finale du livrable

La regle la plus importante :

Personne ne push directement sur main.
Tout passe par une Pull Request.
Le responsable verifie et valide avant de merge.
