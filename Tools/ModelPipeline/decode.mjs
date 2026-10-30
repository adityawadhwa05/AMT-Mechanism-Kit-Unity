import { NodeIO } from '@gltf-transform/core';
import { ALL_EXTENSIONS } from '@gltf-transform/extensions';
import draco3d from 'draco3dgltf';
import fs from 'fs';
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({
  'draco3d.decoder': await draco3d.createDecoderModule(),
});
const doc = await io.read(process.argv[2]);
const root = doc.getRoot();
for (const n of root.listNodes()) console.log('node', n.getName(), n.getTranslation(), n.getRotation(), n.getScale(), !!n.getMesh());
const out = [];
root.listMeshes().forEach((m) => m.listPrimitives().forEach((p, i) => {
  const pos = p.getAttribute('POSITION').getArray();
  const idx = p.getIndices().getArray();
  const name = p.getMaterial()?.getName();
  const col = p.getMaterial()?.getBaseColorFactor();
  fs.writeFileSync(`${process.argv[3]}/p${i}_pos.bin`, Buffer.from(new Float32Array(pos).buffer));
  fs.writeFileSync(`${process.argv[3]}/p${i}_idx.bin`, Buffer.from(new Uint32Array(idx).buffer));
  out.push({ i, name, col, verts: pos.length / 3, tris: idx.length / 3 });
}));
fs.writeFileSync(`${process.argv[3]}/prims.json`, JSON.stringify(out, null, 1));
console.log(JSON.stringify(out));
