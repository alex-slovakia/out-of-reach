# Earth–Mars reference snapshot

Provider: NASA/JPL Solar System Dynamics, Horizons. Dataset ID: `jpl-horizons-earth-mars-2026-v1`.

Read [the scientific register](../../../NASA-DATA.md) before using these values. The raw JSON contains the unmodified API response. The CSV is an offline, deterministic transformation; the manifest records the exact request and SHA-256 hashes.

There are 32 daily UTC epochs from 2026-11-14 08:00 through 2026-12-15 08:00, inclusive. Distances are simultaneous geometric Earth-center to Mars-center ranges, not a spacecraft trajectory. The two-way delay column is twice the frozen-epoch one-way value; it excludes processing, moving-endpoint and link-availability effects.

```powershell
python tools/nasa/horizons_snapshot.py
```

Execute from the repository root. The default command does not access the network. Use `--fetch` only for an intentional reviewed refresh; NASA may update its ephemerides, so a later response need not have the same raw hash.
