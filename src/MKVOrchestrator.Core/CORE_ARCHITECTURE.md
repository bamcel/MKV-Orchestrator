# MKVOrchestrator.Core

`MKVOrchestrator.Core` is the shared engine layer for MKVO. The Windows desktop app references this project now, and future web/CLI/Docker projects should reference the same project instead of duplicating scan, metadata, cache, remux, or planning logic.

## Shared responsibility

- MKVToolNix process execution and command construction
- MKV scanning and metadata parsing
- Metadata cache and cache adapters
- Media library scan orchestration
- Remux planning and execution services
- PropEdit planning and execution services
- Rename metadata provider services
- Audit, pipeline, queue, and operation status services
- Shared models used by existing app workflows

## UI responsibility stays outside Core

The desktop UI remains in `MKVOrchestrator.App`. Future browser UI should live in a separate project such as `MKVOrchestrator.Web` and reference this Core project.

## UI-neutral status projection

Shared row models expose UI-neutral `VisualState` values for normal/warning state. Front ends are responsible for mapping those values to colors, badges, console output, JSON fields, or any other presentation-specific treatment.

## Shared worker configuration

`WorkerSettings` lives in `MKVOrchestrator.Core.Models` so every future front end uses the same concurrency limits.

Default profile:

```json
{
  "maxScanWorkers": 4,
  "maxEditWorkers": 2,
  "maxRemuxWorkers": 1
}
```

Current implementation:

- scan workers are honored by the shared scan pipeline and `MkvScannerService`.
- mkvpropedit workers are honored by the Windows app execution path while using the shared Core setting model.
- remux remains intentionally single-worker by default for Unraid/network-share safety.

Future web/Docker entry points should read the same settings from `/config/settings.json` and pass them into Core rather than introducing separate worker limits.
