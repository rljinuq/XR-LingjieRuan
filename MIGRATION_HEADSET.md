# Headset Migration and Record Preservation

Prepared: 2026-10-17. Target repository: `rljinuq/XR-LingjieRuan`.

## Scope

- Publish the existing independent `HideAR_Week9` project as the active Quest 3 / Quest 3S implementation, including its current bilingual source comments and guides.
- Publish only the four current model choices and their necessary dependencies. Unused candidate packs, duplicate formats and previews remain local and are excluded from Git, not physically deleted.
- Keep `Week3_Activity1` and all existing non-code files at their original paths with identical contents. This includes the mobile test plans/results, explanatory documents, images, assets, settings and licenses.
- Remove only `Prototype1/**/*.cs` files that are not test sources. Existing test-source files and all `.meta` files are retained conservatively. No directories are deleted wholesale.
- Preserve the entire old root README text within the updated repository overview. `Prototype1/README.md` and other original documents are not edited.
- Treat `Prototype1` as legacy records/resources, not a runnable project after its old C# sources are retired. Open the complete `HideAR_Week9` Unity project instead.
- Preserve Git history and submit a pull request first. This migration does not merge itself into `main`.

## Local Project Safety

Migration commits are prepared in a separate checkout. The local Prototype1 project and its other conversation's uncommitted changes are untouched. In the local Week9 project, only `.gitignore`, README and the appended model-source publishing note are updated so the local pending-file list also excludes unused assets. No local models, gameplay code or scenes are changed or deleted. No Unity scene setup, APK rebuild or headset installation is performed here.

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
- All 179 exported Week9 files match the local source copy. No Library/Logs caches, APKs or signing keys are staged.
- Package JSON and Mac launcher syntax checks pass. No new Unity gameplay or participant test was run for the migration.

The previous source and documents remain available in [the pre-migration commit](https://github.com/rljinuq/XR-LingjieRuan/tree/5aaffc14caa32991f4145533be28f099d960b146).

## Asset Footprint Correction

The initial PR included 1,874 Week9 files because it also contained complete unused candidate packs. At the user's request, the published tree now has **179 Week9 files**: 1,695 optional source assets/duplicates/previews were withdrawn from the PR's additions using `git rm --cached`, without deleting the local copies. `.gitignore` is retained and now allowlists the current three pet FBXs, shared texture, license/source record and required metadata; the dinosaur and all four placement prefabs remain included.

All 1,743 snapshotted local candidate files still exist. The 1,742 files other than the model-source note have identical SHA-256 hashes, and that note retains all its original text with an appended publishing explanation. The existing APK is unchanged. Checks of retained serialized GUID references found no references to excluded local assets, including all 36 checked references into the candidate folder. Gameplay code and retained scene/model/rendering resources are unchanged from the first PR commit.

Relative to main, the final PR currently changes 473 paths: 179 new Week9 files, this new migration note, the root README update and the same 292 old non-test C# deletions. There are no test-record or old explanatory-document deletions. This is an ordinary follow-up commit, not a force-push or history rewrite; the PR remains unmerged for review.
