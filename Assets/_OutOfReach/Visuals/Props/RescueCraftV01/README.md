# RescueCraftV01: provenance

An original recovery craft for the OUT OF REACH ending, built procedurally at runtime by
`Scripts/Presentation/Rescue/RescueCraftV01.cs`. Its parts are lathed hull sections, boxes, and cones for the plumes.

- **No sources:** no downloaded or third-party mesh, texture or sound, and no copy of a real vehicle.
- **No insignia:** no agency insignia, flags, logos or livery.
- **Palette:** graphite hull, lighter panel bands, amber running and collar lights, cyan docking lights and cabin
  windows.
- **Parts:** docking collar with a soft-capture ring at the nose (+Z, the front at `CollarFrontZ` = 7.62 m), crew
  cabin, service module, engine bell, two wings, four reaction-control quads (forward braking nozzles and side
  nozzles) and a nose floodlight. It is about 13 m long and 15 m across the wings.
- **Materials:** `Materials/RC_*.mat` use the project's URP Lit and Unlit shaders. The rescue installer creates them
  when they are missing; a later run reuses them.
- **Visual only:** no collider and no physics.
