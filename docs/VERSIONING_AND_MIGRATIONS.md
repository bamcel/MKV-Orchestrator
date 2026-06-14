# Versioning and Migrations

## Settings schema

`AppSettings.SettingsSchemaVersion` tracks the JSON settings schema.

Current version:

```text
1
```

The settings loader calls a migration hook before returning settings. Future schema changes should add migration steps in `AppSettingsService.Migrate`.

## Metadata cache schema

`MetadataCacheDatabase.CurrentCacheSchemaVersion` tracks the SQLite cache schema.

Current version:

```text
1
```

The cache database stores the version in:

```sql
cache_metadata(key, value)
```

Future cache changes should add migration logic in `MetadataCacheDatabase.EnsureSchema`.

If a cache is detected from a newer app version, MKVO clears cached media rows rather than risking incompatible reads. This is safe because the metadata cache is rebuildable.
