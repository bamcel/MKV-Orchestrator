# MKV Orchestrator Internal Architecture

## Direction

MKVO now uses a service-oriented internal architecture with a canonical `MediaFile` model. UI row models such as `MkvFileItem` remain for Avalonia binding, but scan/cache/watch/rename/remux flows should exchange `MediaFile` through service contracts.

## Shared Core Layers

- `../MKVOrchestrator.Core/Models/MediaFile.cs` - canonical media identity, technical metadata, tracks, attachments, provider match state.
- `../MKVOrchestrator.Core/Services/Media/MediaFileMapper.cs` - single projection boundary between `MediaFile` and `MkvFileItem`.
- `../MKVOrchestrator.Core/Services/Library/MediaServiceContracts.cs` - interfaces for scanner, cache, and media library orchestration.
- `../MKVOrchestrator.Core/Services/Library/MediaLibraryService.cs` - coordinates scanning, cache validation, and watch-folder cache builds.
- `../MKVOrchestrator.Core/Services/Media/MkvScannerServiceAdapter.cs` - exposes the existing mkvmerge scanner through the canonical model.
- `../MKVOrchestrator.Core/Services/Cache/MetadataCacheServiceAdapter.cs` - exposes SQLite cache through the cache service interface.
- `../MKVOrchestrator.Core/Services/Pipeline/ScanPipeline.cs` - dashboard scan coordinator; now consumes `IMediaLibraryService`.
- `../MKVOrchestrator.Core/Services/State/AppStateService.cs` - shared UI state only; business logic should remain in services.

## Design Rules

1. New features should depend on service interfaces, not directly on ViewModels.
2. New scan/cache/watch code should use `MediaFile` first and project to UI only at the ViewModel boundary.
3. Cache validity should remain path + size + modified-time + track-payload based.
4. Watch-folder rebuilds should be incremental by default.
5. Platform-specific handling must stay inside platform/process/path services.

## Current Compatibility Bridge

The existing UI still binds to `MkvFileItem`. `MediaFileMapper` keeps this compatible while allowing the internal service layer to move toward a single canonical media object.

## v2.29 architecture cleanup

- `MkvPropEditCommandBuilder` is now the single assembly point for mkvpropedit command arguments covering container title, video/track names, track languages, default flags, and forced subtitle flags.
- `MkvTrackSelector` centralizes selector semantics so mkvmerge numeric IDs and mkvpropedit type-specific selectors (`track:v1`, `track:a1`, `track:s1`) do not get mixed.
- `MediaLibraryService.ScanFileAsync` is the preferred single-file scan/cache hydration boundary for dashboard refreshes, watcher updates, and library-audit handoff imports.
- `GlobalOperationStatusService` provides one status model for scan, cache build, library audit, mkvmerge, and mkvpropedit progress text.
