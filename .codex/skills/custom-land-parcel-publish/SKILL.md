---
name: custom-land-parcel-publish
description: "Publish or prepare a release for the CustomLandParcel Cities: Skylines II mod in this repository. Use when the user asks to publish, release, tag, push, update Paradox Mods/PDX metadata, create a new mod version, or verify the CustomLandParcel release flow."
---

# Custom Land Parcel Publish

## Core Rules

- Treat publishing as a live operation. Do not publish, tag, or push unless the user explicitly asks.
- If the user says to test locally first, stop before publish/tag/push and only build/deploy locally.
- Do not run TDD unless the user asks; use the repository's build and manual-game-test evidence instead.
- Do not build/deploy while `Cities2.exe` is running. It locks `CustomLandParcel_win_x86_64.dll` in the local Mods directory and causes the final copy step to fail.
- Never claim success until a fresh verification command has exited 0.

## Repository Context

- Project root: `D:\code\csl2\CustomLandParcel`
- PDX publish config: `Properties\PublishConfiguration.xml`
- New version profile: `Properties\PublishProfiles\PublishNewVersion.pubxml`
- Metadata-only update profile: `Properties\PublishProfiles\UpdatePublishedConfiguration.pubxml`
- Local deployed mod: `C:\Users\charlie\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CustomLandParcel`
- Existing PDX mod id: `150138`
- GitHub remote normally points to `Charlie-can/CustomLandParcel`.

## Decide The Publish Type

Use `PublishNewVersion` when code, UI, asset, or behavior changes are included.

Use `UpdatePublishedConfiguration` only when changing PDX metadata such as links, description, tags, thumbnail, or access level without shipping new code.

For a new version:

1. Update `<ModVersion Value="...">` in `Properties\PublishConfiguration.xml`.
2. Update `<ChangeLog Value="...">` in the same file.
3. Prefer tags named `v<ModVersion>`, for example `v0.1.5-beta`.

## Preflight

Run these checks first:

```powershell
git status --short
git log --oneline --decorate -5
Get-Process | Where-Object { $_.ProcessName -like '*Cities*' } | Select-Object ProcessName,Id,Path
```

If `Cities2` is running, ask the user to close the game before building for deployment or publishing.

Inspect the pending diff before committing:

```powershell
git diff --stat
git diff -- Properties\PublishConfiguration.xml
```

If unrelated user changes are present, do not revert them. Either leave them uncommitted or ask how to handle them if they block release.

## Local Verification

Run the full Release build:

```powershell
dotnet build "D:\code\csl2\CustomLandParcel\CustomLandParcel.csproj" -c Release
```

The build must report `0 warning` and `0 error`, and must copy output to the local Mods directory. If it fails with access denied for `CustomLandParcel_win_x86_64.dll`, the game is still running or the file is locked; close the game and rerun the same command.

For frontend-only investigation, this narrower check is also useful, but it does not replace the Release build:

```powershell
npm run build
```

Run it from `GameUI`.

## Commit, Tag, And Push

After verification, stage only release-relevant files. Example:

```powershell
git add Properties\PublishConfiguration.xml GameUI\src Systems
git commit -m "Release 0.1.5-beta"
git tag v0.1.5-beta
git push origin master
git push origin v0.1.5-beta
```

Adjust the staged paths and version to match the actual release.

If the user asks to publish but not tag or push, clarify before continuing because this repository's release practice expects a commit and tag for code releases.

## Publish To Paradox Mods

For a new version:

```powershell
dotnet publish "D:\code\csl2\CustomLandParcel\CustomLandParcel.csproj" -c Release /p:PublishProfile=PublishNewVersion
```

For metadata-only updates:

```powershell
dotnet publish "D:\code\csl2\CustomLandParcel\CustomLandParcel.csproj" -c Release /p:PublishProfile=UpdatePublishedConfiguration
```

Read the publisher output. Confirm the command exited 0 before saying it published.

## Post-Publish

Check whether the local test mod still exists:

```powershell
$localMod = "C:\Users\charlie\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CustomLandParcel"
Test-Path $localMod
```

If the user wants to test only the online subscription version, remove the local test mod after confirming `Cities2.exe` is not running:

```powershell
Get-Process | Where-Object { $_.ProcessName -like '*Cities*' } | Select-Object ProcessName,Id,Path
$localMod = "C:\Users\charlie\AppData\LocalLow\Colossal Order\Cities Skylines II\Mods\CustomLandParcel"
if (Test-Path $localMod) {
    Remove-Item -LiteralPath $localMod -Recurse -Force
}
```

Do not delete the local mod when the user still wants local testing or when the game is running. Report clearly whether the local folder remains or was removed.

Report:

- version published
- commit hash
- tag name
- whether `git push` and tag push succeeded
- whether PDX publish command exited 0
- whether the local test mod folder remains or was removed

If any step fails, report the exact failing command and the important error line. Do not continue with later publish steps after a failed verification, commit, tag, push, or PDX publish command unless the failure is understood and fixed.
