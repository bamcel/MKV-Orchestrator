# MKV Orchestrator v1_14

Built from the v1_13 dashboard scroll/outline baseline.

## Changes

- Dashboard panel title changed from **File Queue** to **File Info**.
- Dashboard Status column now reports template comparison state:
  - `Ready` = audio/subtitle track structure matches the standard/template file.
  - `Warning` = audio/subtitle track structure differs from the standard/template file, or media/track info could not be verified.
- Warning rows retain the established orange mismatch color so discrepancies are visible in the File Info grid.

## Notes

The first scanned file is still treated as the standard/template for comparison.


## v1_15
- Dashboard/DataGrid selection now uses row highlight only.
- Removed white focus/selection box outline around selected cells/elements.


## v1_17
- File Info grid uses row-only selection with no focused-cell white outline.
- Use checkbox now toggles with a single click using a template checkbox column.

## Cross-platform / Docker

MKVO targets .NET 8 and Avalonia desktop. It can be published for Windows and Linux, and includes Docker scaffolding for Linux GUI/container testing.

See `docs/CROSS_PLATFORM_AND_DOCKER.md` for Docker notes, tool resolution behavior, and platform limitations.

## v80 - Separate ad hoc scan cache

This build separates metadata caching into two SQLite databases:

- `metadata_cache.db` - watch-folder database used by configured watch folders, live watchers, library audit, and watch-folder cache builds.
- `metadata_cache_adhoc.db` - ad hoc scan database used when the scanned folder is outside all configured watch folders.

Dashboard scans now report whether they are using the watch-folder cache or the ad hoc cache. Settings includes separate cleanup buttons for the watch-folder cache and the ad hoc scan cache so temporary/outside-folder scans can be cleaned without touching the large watch-folder database.
