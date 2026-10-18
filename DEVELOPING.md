# Developing

## Build

```bash
dotnet publish Jellyfin.Plugin.WatchedDeleter/Jellyfin.Plugin.WatchedDeleter.csproj -c Release -o dist
```

The published folder contains a single `Jellyfin.Plugin.WatchedDeleter.dll` (plus `.pdb` and `.xml`,
which are optional). No Jellyfin assemblies are copied, because the server provides them.

## Releasing

`manifest.json` in the repository root is the manifest Jellyfin reads when the repository URL is
added, and every release carries the installable package.

1. Bump the version in `Directory.Build.props`.
2. Build (see above).
3. Zip `dist/*.dll`, `dist/*.pdb` and `dist/*.xml` **flat**, with the files at the archive root,
   because Jellyfin extracts the archive straight into the plugin folder:

   ```bash
   cd dist
   zip "../Jellyfin.Plugin.WatchedDeleter_<version>.zip" \
       Jellyfin.Plugin.WatchedDeleter.dll \
       Jellyfin.Plugin.WatchedDeleter.pdb \
       Jellyfin.Plugin.WatchedDeleter.xml
   ```

4. Create the release:

   ```bash
   gh release create "v<version>" "Jellyfin.Plugin.WatchedDeleter_<version>.zip"
   ```

5. Add a version entry to `manifest.json` with the new `version`, its `sourceUrl` and the **MD5**
   `checksum` of the zip. Jellyfin verifies the download with MD5, case-insensitively.
