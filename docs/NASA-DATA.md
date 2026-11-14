# NASA data register

The canonical source, units, assumptions, provenance, and integration checklist are in [../NASA-DATA.md](../NASA-DATA.md).

The committed NASA/JPL Horizons dataset is in [data/horizons/](data/horizons/). Verify it offline with:

```powershell
python tools/nasa/horizons_snapshot.py
```

Run that command from the repository root. A verified reference snapshot does not mean the data is already wired into gameplay; see the canonical register for current integration status.
