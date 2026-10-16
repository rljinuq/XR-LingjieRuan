# Headset Migration and Record Preservation

Prepared: 2026-10-17. Target repository: `rljinuq/XR-LingjieRuan`.

## Scope

- Publish the existing independent `HideAR_Week9` project as the active Quest 3 / Quest 3S implementation, including its current bilingual source comments and guides.
- Keep `Week3_Activity1` and all existing non-code files at their original paths with identical contents. This includes the mobile test plans/results, explanatory documents, images, assets, settings and licenses.
- Remove only `Prototype1/**/*.cs` files that are not test sources. Existing test-source files and all `.meta` files are retained conservatively. No directories are deleted wholesale.
- Preserve the entire old root README text within the updated repository overview. `Prototype1/README.md` and other original documents are not edited.
- Treat `Prototype1` as legacy records/resources, not a runnable project after its old C# sources are retired. Open the complete `HideAR_Week9` Unity project instead.
- Preserve Git history and submit a pull request first. This migration does not merge itself into `main`.

## Local Project Safety

All migration changes are prepared in a separate checkout. Neither local Unity project is modified, and uncommitted Prototype1 changes from the other development conversation are not included. No Unity scene setup, gameplay change, APK rebuild or headset installation is performed here.

## Preservation Checks

The migration check compares the staged tree against its `origin/main` parent:

1. Every deletion must be an explicitly selected old non-test C# source file under `Prototype1`.
2. Every other existing file, except the root README, must have the same path, Git blob and file mode as before. This protects all old records, not just a guessed list of documentation extensions.
3. The original root README must appear verbatim inside the new overview.
4. Every exported Week9 file must match the local source copy. Official settings under `Assets/CompositionLayers/UserSettings` and C# `.meta` references must be included.
5. Package JSON must parse, Mac launchers must pass `zsh -n`, and generated caches, signing keys and APKs must not be staged.

These are repository/export checks, not a new Unity gameplay test run. The historical 27-test run and hardware-validation limits are recorded in `HideAR_Week9/VERIFICATION.md`; the migration does not convert them into new test or participant results.

The pull request's Files changed view provides the exact source-file removal list for review before merging.

## Checked Snapshot

The staged export passed the preservation checks against base commit `5aaffc14caa32991f4145533be28f099d960b146`:

- 2,289 original files retain identical Git blobs and modes, including all 151 Week3 files and all original records outside the root overview.
- The original root README text remains verbatim within the updated overview.
- Deletions are limited to 292 old non-test C# source files in Prototype1. Its three Test-prefixed C# sources are retained as well.
- All 1,874 exported Week9 files match the local source copy. No Library/Logs caches, APKs or signing keys are staged.
- Package JSON and Mac launcher syntax checks pass. No new Unity gameplay or participant test was run for the migration.

The previous source and documents remain available in [the pre-migration commit](https://github.com/rljinuq/XR-LingjieRuan/tree/5aaffc14caa32991f4145533be28f099d960b146).
