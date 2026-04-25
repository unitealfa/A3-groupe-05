# EasySave v1.0 - User Manual

EasySave is a ProSoft console application used to create and run backup jobs. The interface is available in French and English.

## Start EasySave

From the project directory:

```bash
dotnet run --project EasySave.Console
```

After publication, launch the executable:

```bash
EasySave.exe
```

## Choose the Language

At interactive startup, choose:

- `1` for French.
- `2` for English.

The selected language is saved in:

```text
%LocalAppData%/ProSoft/EasySave/config/settings.json
```

## Create a Backup Job

Use menu option `1`. A backup job requires:

- a backup name;
- an existing source directory;
- a target directory;
- a type: `1` Complete or `2` Differential.

EasySave accepts local disks, external disks, network drives and UNC paths if the operating system can access them. The maximum number of jobs is 5.

## Run a Backup

Use menu option `3` to run one job by index, or menu option `4` to run all jobs sequentially.

Complete backup copies all files and subdirectories recursively while preserving the source tree.

Differential backup copies only files that are missing from the target or different by `LastWriteTimeUtc` or size.

## Run from CLI

```bash
EasySave.exe 1
EasySave.exe 1-3
EasySave.exe "1;3"
EasySave.exe all
```

With `dotnet run`:

```bash
dotnet run --project EasySave.Console -- 1
dotnet run --project EasySave.Console -- 1-3
dotnet run --project EasySave.Console -- '1;3'
dotnet run --project EasySave.Console -- all
```

## Understand Logs and State

The daily log file is written after each copied file:

```text
%LocalAppData%/ProSoft/EasySave/logs/yyyy-MM-dd.json
```

Each entry contains timestamp, backup name, full source path, full destination path, file size, transfer time, status and error message when needed.

The real-time state file is:

```text
%LocalAppData%/ProSoft/EasySave/state/state.json
```

It contains the current status, total files, remaining files, progression and current file paths for each backup job.
