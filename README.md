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
python -m jprm plugin build . --output artifacts --version 0.1.0.0 --dotnet-framework net10.0
```

A release archive and catalog manifest are not published yet.

## Configure

After installing the plugin and restarting Jellyfin, open **Dashboard > Plugins > Plekt**. Enter:

- **Plekt URL**: the base URL Jellyfin can reach, such as `http://plekt:5056`.
- **Webhook secret**: copy it from **Plekt > Settings > Media Servers > Webhooks**.

The plugin posts to Plekt's existing `/api/webhook?token=...` endpoint. It does not require the Jellyfin Webhook plugin or a new Plekt server endpoint.

Delivery is best-effort with a short timeout. The plugin does not persist or retry failed events. Plekt's scheduled Jellyfin sync remains the catch-up path.

## License

The plugin is licensed under GPL-3.0-or-later. See [LICENSE](LICENSE).
