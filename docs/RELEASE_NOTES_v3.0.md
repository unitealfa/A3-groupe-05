# Release Notes - EasySave v3.0

Release date: 2026-05-16

## Summary

EasySave v3.0 is the most complete project release so far.
It delivers the Windows desktop GUI, the CLI workflow, real-time execution tracking, runtime validation, CryptoSoft integration, priority rules, large-file coordination, pause / resume / stop controls, Docker support for the CLI and Docker Desktop log browsing for the published Windows app.

## Main Features Delivered

- Avalonia desktop GUI with Windows XP inspired interface.
- Console mode still available through `EasySave.Console`.
- Unlimited backup jobs.
- Complete and differential backup strategies.
- French, English and Japanese resources.
- Real-time execution state through `state.json`.
- Daily logs in `JSON` or `XML`.
- Conditional encryption through `CryptoSoft`.
- Business software blocking based on configured process names.
- Large-file coordination with one large transfer at a time.
- Priority extension rules for multi-run execution.
- Pause, resume and stop on running jobs.
- Differential preview before execution.
- Docker image for the CLI.
- Docker Desktop access to logs written by `publish/EasySave.exe`.

## Functional Improvements In v3.0

- Better validation when creating or editing jobs:
  - empty name refused
  - invalid characters refused
  - duplicate names refused
  - backup type required
  - source / target existence checked
- Clearer runtime error messages for:
  - missing source
  - missing target
  - invalid CryptoSoft path
  - encryption errors
  - copy errors
- Better UX in the jobs page and dashboard.
- Interactive dashboard details panel for the selected job.
- Exact timestamp with milliseconds in real-time state view.
- Better behavior when no file changed.
- Better behavior when a differential job has nothing to copy.
- Reset of multi-run checkboxes after execution.
- Published `publish/` artifacts kept aligned with the current GUI.

## Priority And Concurrency Rules

EasySave v3.0 applies the following execution model:

- if selected jobs contain priority files, priority jobs are executed first;
- non-priority jobs wait until all priority jobs are fully completed;
- inside a job, priority files are processed before non-priority files;
- large files are coordinated globally, with only one large file transfer at a time;
- encryption is mono-instance through CryptoSoft, so only one encryption runs at a time;
- `state.json` writes are concurrency-safe and serialized.

## Logging And State

- `state.json` is updated during execution and at the end of each job.
- log files are written daily in `JSON` or `XML`.
- encryption metrics follow the rule:
  - `0` = no encryption
  - `> 0` = encryption time
  - `< 0` = encryption error
- logger writes were hardened to better tolerate file access conflicts.

## Docker Support

Docker support in v3.0 targets the CLI, not the Windows desktop GUI.

Delivered Docker files:

- `Dockerfile`
- `docker-compose.yml`
- `docker/entrypoint.sh`
- `docs/DOCKER.md`

Docker support includes:

- containerized `EasySave.Console`
- persisted Docker CLI data in `docker-data/`
- Docker Desktop browsing of logs from `publish/logs`

## Published Artifacts

Windows published artifacts are available in:

- `publish/` for the GUI
- `publish-cli/` for the console publish

Expected executables:

- `publish/EasySave.exe`
- `publish-cli/EasySave.Cli.exe`
- `publish/CryptoSoft.exe`

## Compatibility

- .NET 8
- Windows desktop GUI through Avalonia
- CLI compatible with Windows, Linux and macOS through .NET 8
- Docker support for the console image

## Known Limits

- the full desktop GUI is not intended to run inside Docker;
- Docker support is focused on the console workflow and log exposure;
- CryptoSoft behavior still depends on file system permissions and path validity;
- execution order between multiple priority jobs follows the job order passed to multi-run.

## Recommended Validation Before Publishing

```bash
dotnet restore EasySave.sln
dotnet build EasySave.sln -m:1
dotnet test EasySave.sln -m:1
dotnet publish .\EasySave.GUI\EasySave.GUI.csproj -c Release -o .\publish
dotnet publish .\EasySave.Console\EasySave.Console.csproj -c Release -o .\publish-cli
docker compose build
```

## Suggested Git Release Flow

```bash
git checkout main
git pull
git checkout -b release/v3.0
dotnet restore EasySave.sln
dotnet build EasySave.sln -m:1
dotnet test EasySave.sln -m:1
git add .
git commit -m "chore(release): prepare EasySave v3.0"
git tag -a v3.0 -m "EasySave v3.0"
git push origin release/v3.0
git push origin v3.0
```

## Suggested GitHub Release Description

EasySave v3.0 delivers the complete desktop experience of the project with a richer GUI, stronger runtime validation, better execution control, real-time state tracking, CryptoSoft integration, multi-language support, Docker CLI packaging, and Docker Desktop visibility for published app logs.

Highlights:

- GUI + CLI workflows
- complete + differential backup
- FR / EN / JA
- real-time state and logs
- priority extension rules
- large-file coordination
- pause / resume / stop
- CryptoSoft integration
- Docker support for CLI and logs
