# LANG-01 catalog tooling (ignored by Unity: the folder name ends with ~)

Regenerate `../GameTextCatalog.cs` after Core, Simulation, Inventory or Interaction text changes:

```
python -I extract_en.py ../../ en_catalog.json
python -I gen_catalog.py . ../GameTextCatalog.cs
```

- `extract_en.py`: English strings and format patterns from the gameplay sources (plus a few manual composite
  patterns and single words listed at its top).
- `es.json`, `fr.json`: reviewed translations keyed by the English text; `extra_es.json`, `extra_fr.json`: later
  additions merged over them. New English strings without a translation stay English at runtime.
- `gen_catalog.py`: rejects any translation whose placeholders or edge spaces differ, then writes the C# catalog.
