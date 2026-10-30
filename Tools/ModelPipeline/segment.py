"""Find the separate CAD bodies (connected triangle islands) in each primitive of the decoded
GLB and assign every island to a named part using part_map.json.

Usage: python segment.py <work_dir> <part_map.json>
Reads p{i}_pos.bin / p{i}_idx.bin / prims.json written by decode.mjs and writes
tri_part_p{i}.bin (uint16 part index per triangle), parts.json and islands.json.

Island ids are deterministic for a given source file: islands are numbered per primitive in
the order scipy's connected_components labels welded vertices. part_map.json refers to them.
"""
import json, sys
import numpy as np
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components

work, map_path = sys.argv[1], sys.argv[2]
prims = json.load(open(f'{work}/prims.json'))
islands, labels = [], {}
for p in prims:
    i = p['i']
    pos = np.fromfile(f'{work}/p{i}_pos.bin', dtype=np.float32).reshape(-1, 3)
    idx = np.fromfile(f'{work}/p{i}_idx.bin', dtype=np.uint32).reshape(-1, 3)
    # CAD exports split vertices along hard edges; weld by position (0.01 mm) before labelling.
    _, weld = np.unique(np.round(pos / 0.01).astype(np.int64), axis=0, return_inverse=True)
    tri = weld.ravel()[idx]
    n = tri.max() + 1
    rows = np.concatenate([tri[:, 0], tri[:, 1], tri[:, 2]])
    cols = np.concatenate([tri[:, 1], tri[:, 2], tri[:, 0]])
    count, lab = connected_components(coo_matrix((np.ones(len(rows)), (rows, cols)), shape=(n, n)), directed=False)
    tl = lab[tri[:, 0]]
    labels[i] = (len(islands), tl)
    for comp in range(count):
        v = pos[idx[tl == comp].ravel()]
        islands.append(dict(prim=i, comp=comp, tris=int((tl == comp).sum()),
                            mn=v.min(0).round(1).tolist(), mx=v.max(0).round(1).tolist()))
json.dump(islands, open(f'{work}/islands.json', 'w'))

part_map = json.load(open(map_path))
owner = {}
for part, ids in part_map.items():
    for k in ids:
        if k in owner:
            sys.exit(f'island {k} is listed in both {owner[k]} and {part}')
        owner[k] = part
mn = np.array([q['mn'] for q in islands], float)
mx = np.array([q['mx'] for q in islands], float)
diag = np.linalg.norm(mx - mn, axis=1)
final = {}
for k in range(len(islands)):
    if k in owner:
        final[k] = owner[k]
        continue
    # Nearest explicitly mapped island by bounding-box gap; ties (touching bodies) go to the
    # island listed first in part_map.json.
    gap, j = None, None
    for m in owner:
        g = np.linalg.norm(np.maximum(0, np.maximum(mn[m] - mx[k], mn[k] - mx[m])))
        if gap is None or g < gap:
            gap, j = g, m
    inside = np.all(mn[k] >= mn[j] - .5) and np.all(mx[k] <= mx[j] + .5)
    if inside and diag[k] < 60:
        final[k] = owner[j]                      # surface detail of that part
    elif diag[k] < 30:
        final[k] = owner[j] + '__fasteners'      # screws, nuts and washers that hold it
    else:
        final[k] = owner[j]
        print(f'review: island {k} ({diag[k]:.0f} mm) merged into {owner[j]}')

parts = sorted(set(final.values()))
index = {p: n for n, p in enumerate(parts)}
for p in prims:
    first, tl = labels[p['i']]
    lut = np.array([index[final[first + c]] for c in range(tl.max() + 1)], dtype=np.uint16)
    lut[tl].tofile(f"{work}/tri_part_p{p['i']}.bin")
json.dump(parts, open(f'{work}/parts.json', 'w'))
print(len(islands), 'islands ->', len(parts), 'part nodes')
