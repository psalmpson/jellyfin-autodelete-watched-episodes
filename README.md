# Jellyfin Watched Deleter

A Jellyfin 12 plugin that deletes episodes automatically once they have been watched, and cleans up
the series and seasons that are left behind without any episodes.

## What it does

- Deletes an episode a configurable delay after it was last watched (for example 24 hours).
- Only ever touches the libraries you pick, nothing else on the server.
- Removes series that have no episodes left, and deletes the now-empty series folder from disk
  (leftover artwork, subtitles and metadata included).
- Removes seasons that have no episodes left.
- Has a dry run mode, so you can see exactly what it *would* delete before it deletes anything.

## How it works

The cleanup runs as a scheduled task (**Dashboard → Scheduled Tasks → Delete watched episodes**),
every 15 minutes by default:

1. Every episode in the selected libraries is checked.
2. The most recent watch by **any** user starts the countdown, so re-watching an episode restarts the
   timer.
3. Once an episode was last watched longer ago than the configured delay, it is deleted through
   Jellyfin's own library API, so the database and the files stay in sync.
4. Any series or season that just lost its last episode is removed as well.

Deleting media is irreversible, so it is deliberately careful:

- **Dry run is enabled by default.** Leave it on for a few days and read the log before turning it
  off.
- A folder is never deleted while it still contains a video file.
- Library folders themselves, and their parent folders, are never deleted.
- Symbolic links are never deleted.
- Episodes whose file is missing are skipped.

## Install

Requires Jellyfin 12 or newer.

1. In Jellyfin, open **Dashboard → Plugins → Repositories** and add a repository.
2. Give it any name you like and paste this URL:

   ```
   https://raw.githubusercontent.com/psalmpson/jellyfin-watched-deleter/main/manifest.json
   ```

3. Save, then open the **Catalog** tab, find **Jellyfin Watched Deleter** and click **Install**.
4. Restart Jellyfin when it asks you to.
5. Open **Dashboard → Plugins → Jellyfin Watched Deleter**, tick the libraries to watch, leave
   **Dry run** on at first, and save. The **Save and run the task now** button runs the cleanup
   immediately, so you can check the log without waiting.

Licensed under [GPL-3.0](LICENSE).
