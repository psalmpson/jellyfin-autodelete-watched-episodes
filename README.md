# Auto Delete Watched Episodes (Jellyfin 12+)

A Jellyfin plugin that deletes episodes automatically once they have been watched, and cleans up
series and seasons that are left without any episodes.

It is built for **Jellyfin 12.x** (built and verified against **Jellyfin 12.2.0**, `net10.0`).

## Features

- **Delete episodes after they are watched** – the delay is configurable (for example `24 Hours`,
  or `90 Minutes`, or `3 Days`).
- **Choose which libraries to watch** – only episodes from the libraries you tick are ever touched.
- **Removes series that have no episodes left**, optionally deleting the (now useless) series
  folder from disk, including leftover artwork, subtitles and `.nfo` files.
- **Removes seasons that have no episodes left** (useful when a series is only partly deleted).
- **Dry run mode** – logs exactly what it would delete without touching a single file. Dry run is
  **enabled by default**.
- **Works with all users** – the countdown is based on the most recent watch by *any* user, so a
  re-watch restarts the timer.
- **GUI** on the plugin configuration page (`Dashboard → Plugins → Auto Delete Watched Episodes`).
- A **"Save and run the task now"** button so you can test the dry run immediately.

## Requirements

- Jellyfin Server **12.0 or newer** (plugins built for 10.x will not load on 12.x and vice versa).
- To build it yourself: the **.NET 10 SDK**.

## Build

```bash
dotnet publish Jellyfin.Plugin.AutoDelete/Jellyfin.Plugin.AutoDelete.csproj -c Release -o ./publish
```

The `publish` folder then contains a single `Jellyfin.Plugin.AutoDelete.dll` (plus `.pdb` and
`.xml`, which are optional). No Jellyfin assemblies are copied, because the server provides them.

## Install

1. Create a folder for the plugin inside the server's plugin directory, for example:

   ```
   <jellyfin-data>/plugins/AutoDeleteWatchedEpisodes/
   ```

   On Linux this is usually `~/.local/share/jellyfin/plugins/`, on Windows
   `%LOCALAPPDATA%\jellyfin\plugins\`, and in Docker `/config/plugins/`.

2. Copy `Jellyfin.Plugin.AutoDelete.dll` (and `Jellyfin.Plugin.AutoDelete.pdb` if you want stack
   traces with line numbers) into that folder.

3. Restart Jellyfin.

4. Go to **Dashboard → Plugins → Auto Delete Watched Episodes** and configure it.

## Configuration

| Setting | What it does |
| --- | --- |
| **Enable automatic deletion** | Master switch. When off, the scheduled task does nothing. |
| **Dry run** | When on, nothing is deleted; the task only logs what it *would* do. On by default. |
| **Delete episodes after** | The grace period, with `Minutes` / `Hours` / `Days`. Default `24 Hours`. |
| **Libraries to watch** | Only episodes inside the ticked libraries are considered. No selection = nothing happens. |
| **Remove series once they have no episodes left** | Deletes the series item when its last episode is gone. |
| **Also delete the series folder from disk** | Recursively removes the series folder (artwork, subtitles, metadata) once it has no video files left. |
| **Remove seasons once they have no episodes left** | Deletes empty season folders, e.g. when only part of a series was deleted. |

The cleanup runs as a scheduled task called **"Delete watched episodes"**
(**Dashboard → Scheduled Tasks**), which runs every 15 minutes by default. You can change the
trigger there, disable it, or run it on demand.

## How it works

1. The task collects all episodes of the selected libraries.
2. For every episode it looks at the playback state of **every** user and takes the most recent
   `LastPlayedDate` of a fully watched (`Played`) episode.
3. Episodes whose last watch is older than the configured delay become candidates.
4. Candidates are deleted through Jellyfin's own library API, so files, database entries and
   Jellyfin's internal state all stay consistent.
5. Afterwards, every series and season that lost episodes is re-checked. If it has no episodes
   left (and no video files remaining on disk) it is removed as well.

### Safety

Deleting media is irreversible, so the plugin is deliberately conservative:

- **Dry run is on by default.**
- The dry run logs are written at *Information* level, e.g.
  `[Dry run] Would delete episode "Pilot" of series "Example" (watched 2026-10-17 20:13:05Z by alice, file: /media/tv/Example/Season 1/Example S01E01.mkv)`.
- If a folder cannot be read, the plugin does **not** delete it.
- A folder is never deleted if it still contains **any** video file, even if Jellyfin no longer
  has episodes for it (for example files that were not recognised by the scanner).
- A folder is never deleted if it *is* a library folder or a parent of one (protects flat library
  layouts where a series folder is the library root).
- Symbolic links are never deleted.
- Episodes whose file is missing on disk are skipped.

### Limitations

- "Watched" means Jellyfin has marked the episode as played (the user watched past the played
  threshold, or marked it played manually). Partially watched episodes are left alone.
- Deleting the last episode of a series also removes the series' collected metadata (posters,
  fanart, `.nfo`); if you re-add the series later, Jellyfin will fetch it again.
- There is no per-user "keep it for user X" behaviour: if any user watched an episode, it is
  eligible for deletion.

## Publishing a plugin repository (optional)

`build.yaml` contains the manifest metadata used by Jellyfin's plugin repository system. Update
`owner` (and `targetAbi` if you target a different server version) before submitting, then follow
the [plugin repository guide](https://jellyfin.org/docs/general/server/plugins/).

## Licence

GPL-3.0. Compiling a Jellyfin plugin links against the Jellyfin assemblies, which are licensed
under the GPL-3.0, so the resulting plugin is GPL-3.0 as well.
