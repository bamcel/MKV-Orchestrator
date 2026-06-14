# Cross-Platform and Docker Notes

## Supported runtime targets

MKV Orchestrator is a .NET 8 Avalonia desktop application. The project is intended to run on:

- Windows x64
- Linux x64
- macOS, with additional testing required

## External tools

MKVO resolves tools in this order:

1. User-configured MKVToolNix directory in Settings
2. Operating-system PATH
3. Common install locations for the current platform

Required/recommended tools:

- `mkvmerge`
- `mkvpropedit`
- `mkvextract`
- `mkvinfo`
- `ffprobe` from FFmpeg for optional video metadata fallback

## Path handling

The app should use `Path.Combine`, `Path.GetDirectoryName`, and runtime path normalization rather than hardcoded separators. UNC paths remain supported on Windows.

## Safe replacement behavior

Remux operations write a temporary output file beside the original MKV. Replacement uses `File.Replace` when supported and falls back to a move-based replacement strategy for Linux/macOS/container-mounted volumes that reject `File.Replace`.

## Docker

Docker support is primarily useful for Linux desktop environments or build/test packaging. Because MKVO is a GUI app, runtime containers require a display server such as X11 or Wayland.

Build:

```bash
docker compose build
```

Run on a Linux host using X11:

```bash
xhost +local:docker
docker compose up
```

Media folders should be mounted into the container, for example:

```yaml
volumes:
  - /mnt/user/media:/media
```

Inside MKVO, select `/media/...` paths rather than host-only paths.

## Docker limitations

- Windows GUI display from Docker is not included.
- macOS GUI display from Docker requires additional XQuartz setup.
- File watchers may behave differently on bind mounts depending on host filesystem and Docker backend.
- Hardware acceleration/display permissions may require additional device mounts.
