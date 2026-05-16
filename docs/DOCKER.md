# Docker

Cette configuration Docker cible `EasySave.Console`.

La GUI Avalonia Windows n'est pas embarquee dans le conteneur.
Le service `easysave-logs` peut aussi suivre les logs ecrits par `publish/EasySave.exe`.

## Dossiers utilises

Les volumes Docker sont relies a :

- `./docker-data/config` -> `/app/config`
- `./docker-data/logs` -> `/app/logs`
- `./docker-data/state` -> `/app/state`
- `./docker-data/source` -> `/workspace/source`
- `./docker-data/target` -> `/workspace/target`

Donc :

- les fichiers de config restent persistants ;
- les logs restent persistants ;
- `state.json` reste persistant ;
- vous pouvez deposer vos fichiers de test dans `docker-data/source`.
- le conteneur CLI Docker utilise ses propres fichiers dans `docker-data` ;
- le raccourci Windows `EasySave - Raccourci.lnk` continue d'utiliser `publish`.

## Logs du raccourci Windows dans Docker Desktop

Le service `easysave-logs` monte seulement :

- `./publish/logs` -> `/app/logs`

Il est en lecture seule et reste volontairement actif pour permettre la consultation dans Docker Desktop.

Donc :

- vous pouvez lancer `EasySave - Raccourci.lnk` ;
- puis lancer `easysave-logs` pour voir les fichiers dans Docker Desktop ;
- sans partager `config` ni `state` avec Docker.

## Build

```bash
docker compose build
```

## Lancer le menu console

```bash
docker compose run --rm -it easysave-cli
```

## Lancer des jobs CLI

Exemple :

```bash
docker compose run --rm easysave-cli 1
docker compose run --rm easysave-cli 1-3
docker compose run --rm easysave-cli "1;3"
docker compose run --rm easysave-cli all
```

## Consulter les logs depuis Docker

Afficher les logs deja ecrits :

```bash
docker compose run --rm easysave-cli logs
```

Monter le dossier de logs dans Docker Desktop :

```bash
docker compose up -d easysave-logs
```

Important :

- `easysave-logs` sert seulement a exposer les logs du raccourci Windows dans Docker Desktop ;
- `easysave-cli` sert seulement a executer la version console dans Docker ;
- ne pas utiliser `easysave-cli` si vous voulez juste observer les logs du raccourci.

## Consulter les logs depuis l'hote

Les fichiers sont aussi visibles ici :

```text
publish/logs
```

## Emplacements utiles dans le conteneur

- Config : `/app/config`
- Logs : `/app/logs`
- Etat : `/app/state`
- Sources de test : `/workspace/source`
- Cibles de test : `/workspace/target`
