# Release Notes - EasySave v2.0

Release date: 2026-05-02

## Delivered Features

- Graphical desktop application built with Avalonia UI.
- French and English interface across the GUI.
- Unlimited backup jobs.
- Complete and differential backup execution preserved from previous versions.
- Real-time backup state visualization based on `state.json`.
- Daily logs preview with JSON or XML support.
- CryptoSoft integration for user-configured encrypted extensions.
- Business software detection that blocks backup launch and is reflected in logs and UI.
- Windows XP inspired desktop interface for dashboard, jobs, add/edit job, execution, logs, real-time state, settings and about pages.
- Backup job creation and update from the GUI.
- Published GUI artifacts available in `publish/`.

## Delivered Screens

- Dashboard
- Backup jobs management
- Add / edit backup job
- Backup execution
- Daily logs review
- Real-time state
- General settings
- About

## Technical Notes

- Core backup logic remains in `EasySave.Core`.
- Logging remains handled through `EasyLog`.
- Settings, jobs and states remain file-based and portable.
- The published Windows executable is `publish/EasySave.exe`.

## Known Limits

- Pause / resume / stop controls are intentionally not implemented in v2.0.
- Job deletion is still not exposed as a real workflow in the GUI.
- GitHub release publication may require repository-side authentication tooling even when the Git tag exists.

## Validation

- `dotnet build .\EasySave.sln -m:1 --verbosity minimal`
- `dotnet test .\EasySave.sln -m:1 --verbosity minimal`
- `dotnet publish .\EasySave.GUI\EasySave.GUI.csproj -c Release -o .\publish --verbosity minimal`

Result:

- Build OK
- Tests OK (`16/16`)
- Publish OK

## Git Release Commands

```bash
git checkout feature/pre-release-fixes-ui-refresh
git pull
dotnet build EasySave.sln -m:1
dotnet test EasySave.sln -m:1
dotnet publish .\EasySave.GUI\EasySave.GUI.csproj -c Release -o .\publish
git add docs/RELEASE_NOTES_v2.0.md
git commit -m "docs(release): add EasySave v2.0 notes"
git tag -a v2.0 -m "Livrable 2 - EasySave v2.0"
git push origin feature/pre-release-fixes-ui-refresh
git push origin v2.0
```
