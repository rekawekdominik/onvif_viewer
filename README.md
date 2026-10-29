# onvif_viewer

Application for assistance during camera setup.

A small native Windows ONVIF camera viewer. Preview up to 10 cameras in one window and switch between named camera sets. ONVIF discovers each camera's stream; VLC plays its RTSP video. Includes automatic RTSP reconnect attempts, muted video, configurable grid columns, and fullscreen with **F11** (exit with **Esc**).

## Requirements

- 64-bit Windows with .NET Framework 4.5 or newer (included in Windows 10/11).
- **64-bit VLC 3**, installed from [VideoLAN](https://www.videolan.org/vlc/). The viewer embeds VLC playback; it does not open separate VLC windows. VLC 4 is not supported.
- Camera ONVIF service addresses, ONVIF enabled on each camera, and network access. Some cameras require a separate ONVIF user.

## Run

From PowerShell in the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\run.ps1
```

This compiles `bin/Kamery.exe`, creates `cameras.json` from the example if missing, and opens the viewer. Click **Edit config**, replace the example addresses, save, then click **Reload config**. Select a camera set from the dropdown. **Reconnect** restarts the current set. Invalid reloads leave the existing configuration running.

You can also build once and launch the executable directly:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
.\bin\Kamery.exe "C:\path\to\cameras.json"
```

Without a config argument, the executable uses `cameras.json` beside itself and creates it from the adjacent example when missing. Keep `cameras.example.json` with the executable when distributing it. The build uses Windows' .NET Framework compiler; no SDK or NuGet restore is needed.

## Configuration

See [cameras.example.json](cameras.example.json) for a complete editable JSON file. JSON uses double quotes and does not support comments or trailing commas.

| Field | Meaning |
| --- | --- |
| `cameras` | Camera definitions with unique `id`, display `name`, ONVIF `onvifUrl`, and optional `username` / `password`. |
| `cameras[].profileToken` | Optional exact ONVIF video profile token. Empty or omitted selects the first video profile returned by the camera. |
| `sets` | Named groups referencing camera IDs in display order, with 1–10 cameras per set. Cameras can belong to multiple sets. |
| `sets[].columns` | `0` for automatic layout, or 1–10 for fixed columns. |
| `defaultSet` | Set selected on startup; omit to use the first set. Reload preserves the selected set if it still exists. |
| `networkCachingMs` | Playback buffer, 100–30000 ms; default 500. Higher values may help unstable networks but add delay. |
| `reconnectSeconds` | Retry interval for streams that do not reach playing state, 3–300 seconds; default 10. |
| `rtspTcp` | Prefer TCP transport when true (default). Use false to allow VLC's default transport. |
| `vlcDirectory` | Optional VLC installation folder; empty uses `C:\Program Files\VideoLAN\VLC`. Relative paths resolve against the config folder. Restart after changing it. |

For authentication, add optional `username` and `password` fields to each camera:

```json
{
  "id": "front",
  "name": "Front door",
  "onvifUrl": "http://192.168.1.101/onvif/device_service",
  "profileToken": "",
  "username": "viewer",
  "password": "your-camera-password"
}
```

Set `onvifUrl` to the camera's device service endpoint, including its ONVIF port if different from 80, for example `http://192.168.1.101:8000/onvif/device_service`. The application calls `GetCapabilities`, `GetProfiles`, and `GetStreamUri` using the ONVIF Media v1 service (Profile S). Media2-only cameras and automatic network discovery are not implemented. The protocol flow follows the [ONVIF Media specification](https://www.onvif.org/specs/srv/media/ONVIF-Media-Service-Spec-v240.pdf).

Enter credentials as ordinary JSON strings; special URL characters such as `@`, `:`, and `%` are encoded automatically for playback. ONVIF requests use WS-Security UsernameToken PasswordDigest and HTTP challenge authentication. Keep the computer and camera clocks synchronized for digest authentication. A username with an empty or omitted password is supported. A nonempty password requires a username. Omit both fields or leave them empty for cameras without authentication. Click **Reload config** after saving changes.

To choose a substream, enter its exact ONVIF profile token in `profileToken` (obtain it from the camera's ONVIF tools/settings). On connection failure, a message appears in that camera's tile; correct the address, credentials, profile or clock and click **Reconnect**. ONVIF resolution runs in parallel outside the UI thread with bounded request timeouts. ONVIF discovery is retried on Reconnect, set switching, or Reload config; automatic playback retries reuse the resolved RTSP address.

Legacy camera entries with `url: "rtsp://..."` still work. If both fields are supplied, `onvifUrl` takes priority. Separate credentials override credentials embedded in legacy RTSP URLs.

Passwords are stored as plain text in the JSON file. Local `cameras.json` is ignored by Git; keep files containing credentials private. Titles and status labels do not display stream URLs or credentials.

Use camera substreams for 4–10 simultaneous previews to reduce decoding load and bandwidth. Reconnect detects playback errors and connection timeouts; a stream that stays in playing state with frozen frames may require the Reconnect button. Actual RTSP paths depend on your camera model.

## Development and validation

Source is in `src/`; configuration checks are in `tests/`. Use four-space C# indentation and keep playback resources alive until their native players stop. Native calls target the [LibVLC 3 API](https://videolan.videolan.me/vlc-3.0/group__libvlc__media__player.html).

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\test.ps1
```

Tests cover configuration parsing, invalid references, duplicate IDs, limits, credential encoding, and ONVIF endpoint/profile/stream resolution with simulated responses, without needing VLC. For playback validation, supply real camera endpoints, test authentication, profile selection, set switching and reload, disconnect/reconnect one camera, and close the viewer during playback. A build and automated tests alone do not verify camera compatibility or live playback.
>>>>>>> 83adb87 (Initial commit)
