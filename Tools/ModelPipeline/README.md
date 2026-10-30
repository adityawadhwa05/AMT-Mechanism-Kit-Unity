# Model pipeline — recover named parts from the supplied GLB

`Assets/StreamingAssets/Model/MechanismKit.glb` (the supplied model, kept unchanged) is a single
Draco mesh with eight material groups. Those groups are not parts, but the mesh still contains
**276 disconnected CAD bodies**. This pipeline finds them, assigns each to a named part, and
writes `MechanismKit_Parts.glb` with one node per part. Geometry, normals and colours are copied
exactly (197,004 triangles in, 197,004 out); only the vertex order and pivots change.

The root node also converts the source frame (millimetres, **−Z up**, matching the Vuforia
`upVector="0 0 -1"`) to Unity's metres and +Y up.

## Run

Requires Node 18+ and Python 3.10+ with `numpy` and `scipy`.

```bash
cd Tools/ModelPipeline
npm install
mkdir -p work
node decode.mjs ../../Assets/StreamingAssets/Model/MechanismKit.glb work
python segment.py work part_map.json
node build_parts.mjs ../../Assets/StreamingAssets/Model/MechanismKit.glb work ../../Assets/StreamingAssets/Model/MechanismKit_Parts.glb
```

## Editing the part map

`part_map.json` maps a part id to the island numbers that form it. Bodies that are not listed
are assigned automatically: small bodies inside a mapped body become part of it; small bodies
outside it become `<part>__fasteners` (screws, nuts, washers); larger bodies are merged into the
nearest part and printed for review. Part ids must match `id` values in
`Assets/Resources/Data/components.json`, which holds the names and descriptions shown in the app.

`parts_report.json` lists each generated node with its triangle count, centre and size in mm.

## How the parts were identified

Bodies were rendered individually and identified from their shape, size and position, and
checked against the kit's published architecture (servo-driven indexing table with RFID,
distance-sensor, vision-camera and solenoid stations). Identifications that depend on shape alone
are marked `"verification": "geometry"` in `components.json`, and need confirmation against the
physical kit or the manufacturer's CAD.
