# Release Notes - EasySave v1.0

Release date: 2026-04-25

## Delivered Features

- Console application in C# / .NET 8.
- French and English interactive interface.
- Up to 5 backup jobs.
- Complete backup with recursive file copy.
- Differential backup based on missing files, `LastWriteTimeUtc` and size.
- Sequential execution of one job, all jobs, CLI ranges and CLI lists.
- Portable JSON configuration in `LocalApplicationData`.
- Separate `EasyLog.dll` library for daily JSON logs.
- Real-time `state.json` updated during backup execution.
- Strategy-based backup architecture ready for future WPF/MVVM.
- Unit tests for CLI parsing, portable paths, complete backup, differential backup, logs and state.
- PlantUML diagrams aligned with the implementation.

## Known Limits

- EasySave v1.0 has no graphical interface.
- Jobs can be created and listed, but not edited or deleted from the console menu.
- Backups run sequentially.
- Network paths depend on operating system permissions and connectivity.

## Compatibility

- .NET 8.
- Visual Studio 2022 or newer.
- Windows, Linux and macOS supported by .NET 8.
- Windows executable examples use `EasySave.exe`; Linux/macOS use the generated executable or `dotnet run`.

## Git Release Commands

```bash
git checkout main
git pull
git checkout -b release/v1.0
dotnet restore EasySave.sln
dotnet build EasySave.sln -m:1
dotnet test EasySave.sln -m:1
git add .
git commit -m "chore(release): prepare EasySave v1.0"
git tag -a v1.0 -m "Livrable 1 - EasySave v1.0"
git push origin release/v1.0
git push origin v1.0
```
