# EasySave v1.0 - Support Sheet

## Default Software Location

EasySave can run from any installation directory that contains the published executable and its DLL dependencies. In development, run it from the repository root with `dotnet run --project EasySave.Console`.

## Minimum Configuration

- .NET 8 runtime for execution.
- .NET 8 SDK for development, build and tests.
- Read permission on source directories.
- Write permission on target directories and `%LocalAppData%/ProSoft/EasySave`.

## Data Locations

```text
Jobs configuration:
%LocalAppData%/ProSoft/EasySave/config/jobs.json

Language settings:
%LocalAppData%/ProSoft/EasySave/config/settings.json

Daily logs:
%LocalAppData%/ProSoft/EasySave/logs/yyyy-MM-dd.json

Real-time state:
%LocalAppData%/ProSoft/EasySave/state/state.json
```

All JSON files are indented and readable in Notepad.

## Frequent Issues

`Source directory does not exist`
Check that the source path exists and is accessible by the current user.

`The maximum number of backup jobs is five`
EasySave v1.0 supports up to 5 configured backup jobs.

`One or more requested backup jobs do not exist`
The CLI argument references an index that has not been configured yet.

`Invalid range format. Expected example: 1-3`
Use `1-3` for a range, `1;3` for a list, `1` for a single job, or `all`.

Files are not copied to a network path
Check network availability, credentials, and write permission on the target share.

Logs or state are missing
Check write permission in `%LocalAppData%/ProSoft/EasySave`.

## Useful Commands

```bash
dotnet restore EasySave.sln
dotnet build EasySave.sln -m:1
dotnet test EasySave.sln -m:1
dotnet run --project EasySave.Console
```
