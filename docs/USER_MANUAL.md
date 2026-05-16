# EasySave - User Manual

This manual describes the current EasySave application as delivered in this repository.

EasySave provides:

- a graphical interface built with Avalonia;
- a console interface;
- complete and differential backups;
- JSON or XML logs;
- real-time state tracking;
- optional CryptoSoft encryption;
- business software blocking;
- support for French, English and Japanese.

## 1. Start the Application

### Graphical interface

For the published Windows version used by the team:

```text
publish\EasySave.exe
```

You can also launch the desktop shortcut if it points to `publish\EasySave.exe`.

### Console interface

From the project directory:

```bash
dotnet run --project EasySave.Console
```

Published console executable:

```text
publish-cli\EasySave.Cli.exe
```

## 2. Application Files and Folders

EasySave stores its runtime files next to the executable base directory.

Main paths:

- settings file: `config\settings.json`
- jobs file: `config\jobs.json`
- state file: `state\state.json`
- logs folder: `logs\`

For the published GUI, these files are therefore created under:

```text
publish\
```

Examples:

```text
publish\config\settings.json
publish\config\jobs.json
publish\state\state.json
publish\logs\
```

## 3. Supported Languages

The interface can be displayed in:

- French
- English
- Japanese

Changing the language in Settings updates the GUI texts after applying the change.

## 4. Main Screens in the GUI

### Dashboard

The dashboard gives a quick overview of the application:

- backup summary cards;
- global status information;
- shortcuts to the main workflow;
- current file paths for the state and settings files.

It is an overview screen. Backup creation and detailed actions are handled in the dedicated pages.

### Jobs List

This page is the main place to manage backup jobs.

Available actions:

- create a job;
- edit a job;
- delete a job;
- run one job;
- select multiple jobs;
- run multiple selected jobs;
- pause one job;
- stop one job;
- pause all running jobs;
- stop all running jobs.

Important behavior:

- a row action button runs the selected job directly;
- checkboxes are reserved for multi-selection;
- a running job cannot be edited;
- a running job cannot be deleted;
- during a multi-run sequence, manual conflicting actions are blocked.

### Real-time State

This page reads `state.json` and shows the current state of backup jobs.

Displayed information includes:

- job name;
- current status;
- total files;
- remaining files;
- progression percentage;
- current source file path;
- current destination file path.

If no current file is available, EasySave keeps at least the job source and target visible.

### Settings

This page controls application behavior.

Editable settings:

- language;
- log format;
- CryptoSoft path;
- encryption key;
- encrypted extensions;
- priority extensions;
- large file threshold in Ko;
- business software process names.

Read-only information:

- state file path;
- logs folder path;
- settings file path.

The **Apply** button saves changes immediately.

If the user changes values and tries to leave without saving, EasySave asks whether to:

- save;
- discard;
- stay on the page.

### About

This page presents product information:

- product name;
- version;
- stack;
- UI technology;
- architecture;
- logging technology;
- encryption engine;
- pricing and maintenance summary;
- copyright notice.

## 5. Create a Backup Job

To create a job in the GUI:

1. Open the Jobs List page.
2. Click the button to create a backup job.
3. Fill in the form.
4. Save the job.

Required fields:

- backup name;
- source path;
- target path;
- backup type.

Supported source types:

- a folder;
- a single file;
- multiple sources when supported by the selection flow.

Supported target types:

- local folder;
- external disk folder;
- mapped network drive;
- UNC path if Windows can access it.

### Name validation

The job name is refused if:

- it is empty;
- it contains only spaces;
- it contains invalid file-name characters such as `\ / : * ? " < > |`;
- it is longer than 100 characters;
- another job already uses the same name.

### Source and target validation

EasySave refuses a job if:

- the source does not exist;
- the target path is empty;
- the backup type is not selected;
- the target is the same as the source;
- the target is inside the source;
- the target contains the source.

If the target folder does not exist during job creation or update, EasySave tries to create it.

## 6. Backup Types

### Complete backup

A complete backup copies:

- all files;
- all subfolders;
- the full tree structure.

### Differential backup

A differential backup copies only files that are:

- missing from the target;
- changed by size;
- changed by last write time.

If there is no previous backup content in the target, a differential backup behaves like a first full copy for the missing files.

If a file was renamed in the source, the new name is treated as a new target file and is copied.

## 7. Run Backups

### Run a single job

From the Jobs List page, click the run button on the chosen job.

### Run multiple jobs

Use the checkboxes, then launch the multi-run action.

### Pause and stop

EasySave supports:

- pause one job;
- stop one job;
- pause all jobs;
- stop all jobs.

When the application closes, running jobs are stopped cleanly, like a user stop action.

## 8. Runtime Validation and Errors

At execution start, EasySave checks the selected job again.

If the source no longer exists:

- the execution is refused;
- the user gets a clear error message with the missing source path.

If the target no longer exists:

- the execution is refused;
- the user gets a clear error message with the missing target path.

If one job is invalid during a multi-run:

- that job goes to error;
- valid jobs can continue according to the current execution rules.

If the source is empty:

- the backup finishes successfully;
- `0` files are copied.

If a source file disappears or the target disk is removed during the copy:

- the job goes to error;
- the failure is logged;
- execution stops cleanly.

## 9. Logs

EasySave writes one log entry per copied file.

Supported formats:

- JSON
- XML

The log format is controlled from Settings.

Log folder:

```text
logs\
```

Typical daily file names:

```text
logs\2026-05-16.json
logs\2026-05-16.xml
```

A log entry contains:

- date and time;
- backup name;
- source file path;
- destination file path;
- file size;
- transfer time;
- encryption time;
- status;
- error message if needed.

Behavior:

- no encryption: encryption time is `0`;
- encryption failure: encryption time is negative;
- transfer failure: transfer time is negative.

## 10. Real-time State File

EasySave updates:

```text
state\state.json
```

The file contains one state entry per job, with:

- name;
- state;
- current source file path;
- current destination file path;
- total files to copy;
- total size;
- remaining files;
- remaining size;
- progression.

Possible states include:

- Active
- Paused
- Stopped
- Finished
- Error

The state file is written in a safe way to reduce corruption risk during concurrent updates.

## 11. Encryption with CryptoSoft

Encryption is optional.

To enable it, the user must configure:

- a valid CryptoSoft path;
- a non-empty encryption key;
- encrypted extensions.

If no CryptoSoft path is configured:

- encryption is disabled;
- EasySave shows a clear message in Settings.

### Encrypted extensions

Examples:

- `.txt`
- `.pdf`
- `.txt;.pdf`
- `txt`
- `.TXT`
- `.txt ; .pdf`
- `*`

Behavior:

- extension matching is case-insensitive;
- extensions without a dot are normalized automatically;
- surrounding spaces are ignored;
- empty entries are ignored;
- `*` means every copied file is eligible for encryption.

## 12. Priority Extensions

Priority extensions affect execution order across parallel jobs.

Examples:

- `.sql`
- `.bak`
- `.sql;.bak`
- `*`

Behavior:

- if priority files are pending, non-priority files wait;
- between two priority files, EasySave follows the actual execution order;
- a file can be both priority and encrypted;
- in that case it is processed with priority and then encrypted.

## 13. Large File Threshold

The large file threshold is configured in Ko.

Rules:

- the field is required;
- it must be a whole number;
- it cannot be negative;
- minimum accepted value: `1`.

Behavior:

- a file strictly larger than the threshold is treated as a large file;
- only one large file transfer is allowed at a time across active jobs;
- smaller files may still run in parallel if priority rules allow it.

## 14. Business Software Blocking

This setting blocks backup execution when configured business processes are detected.

Examples:

- `notepad`
- `calc`
- `win32calc`
- `notepad;calc`

Behavior:

- spaces are ignored;
- matching is case-insensitive;
- aliases are supported for common tools such as calculator and notepad;
- if detected, EasySave pauses or blocks execution according to the current flow;
- the status is reflected in the GUI and the logs.

## 15. Console Usage

The console interface supports:

- interactive menu mode;
- CLI execution by indexes or ranges.

Examples:

```bash
EasySave.Cli.exe 1
EasySave.Cli.exe 1-3
EasySave.Cli.exe "1;3"
EasySave.Cli.exe all
```

Interactive language selection supports:

- French
- English
- Japanese

## 16. Practical Notes

- The published GUI version used in this repository is `publish\EasySave.exe`.
- Settings changes only take effect after clicking **Apply**.
- The About page texts, dashboard texts and real-time state labels follow the selected language.
- Published resources are stored in `publish\Resources\`.

## 17. Summary

EasySave allows the user to:

- create backup jobs;
- edit backup jobs;
- delete backup jobs;
- run one or more backups;
- pause and stop backups;
- monitor backups in real time;
- generate logs;
- configure encryption;
- define priority file extensions;
- limit concurrency for large files;
- block execution when business software is detected;
- switch the interface language between French, English and Japanese.
