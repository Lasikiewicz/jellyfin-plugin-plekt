# Plekt for Jellyfin

A Jellyfin server plugin that sends playback and explicit watched or unwatched events to Plekt.

## Build

The development build targets Jellyfin 12.2 and .NET 10.

```powershell
dotnet build Jellyfin.Plugin.Plekt.csproj --configuration Release
```

To create a distributable plugin archive with [JPRM](https://github.com/oddstr13/jellyfin-plugin-repository-manager), create the output folder first, then run:

```powershell
New-Item -ItemType Directory -Force artifacts | Out-Null
python -m jprm plugin build . --output artifacts --version 0.1.0.4 --dotnet-framework net10.0
python scripts/fix_jellyfin_image_meta.py artifacts/plekt_0.1.0.4.zip
```

The archive includes the full Plekt wordmark on a transparent background. Run the helper after JPRM so the installed archive's `meta.json` uses Jellyfin's `imagePath` field.

A release archive and catalog manifest are not published yet.

## Configure

After installing the plugin and restarting Jellyfin, open **Dashboard > Plugins > Plekt**. Enter:

- **Plekt webhook address**: paste the complete address from **Plekt > Settings > Media Servers > Webhooks**. Jellyfin must be able to reach the host in that address. For example, `localhost` refers to Jellyfin's own network namespace, which may differ from Plekt when they run in separate containers.

The plugin posts directly to the supplied address, including its existing token query. It does not require the Jellyfin Webhook plugin or a new Plekt server endpoint. Existing split URL/secret settings continue to work until a complete webhook address is entered.

Delivery is best-effort with a short timeout. The plugin does not persist or retry failed events. Plekt's scheduled Jellyfin sync remains the catch-up path.

## License

The plugin is licensed under GPL-3.0-or-later. See [LICENSE](LICENSE).
