import { NodeIO, Document } from '@gltf-transform/core';
import { ALL_EXTENSIONS, KHRDracoMeshCompression } from '@gltf-transform/extensions';
import { prune, dedup } from '@gltf-transform/functions';
import draco3d from 'draco3dgltf';
import fs from 'fs';
const [src, dir, out] = process.argv.slice(2);
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({
  'draco3d.decoder': await draco3d.createDecoderModule(),
  'draco3d.encoder': await draco3d.createEncoderModule(),
});
const srcDoc = await io.read(src);
const parts = JSON.parse(fs.readFileSync(`${dir}/parts.json`));
const doc = new Document(); const buf = doc.createBuffer();
const scene = doc.createScene('AMT Mechanism Kit');
doc.getRoot().setDefaultScene(scene); // glTFast instantiates only the default scene
// Source is millimetres with -Z up. Rotate +90deg about X so -Z becomes +Y, scale to metres.
const s = Math.SQRT1_2;
const root = doc.createNode('AMT_MechanismKit').setRotation([s, 0, 0, s]).setScale([0.001, 0.001, 0.001]);
scene.addChild(root);
const srcPrims = srcDoc.getRoot().listMeshes()[0].listPrimitives();
const mats = srcPrims.map((p) => {
  const m = p.getMaterial();
  return doc.createMaterial(m.getName()).setBaseColorFactor(m.getBaseColorFactor())
    .setMetallicFactor(0.15).setRoughnessFactor(0.55).setDoubleSided(true);
});
const data = srcPrims.map((p, i) => ({
  pos: p.getAttribute('POSITION').getArray(), nrm: p.getAttribute('NORMAL').getArray(),
  idx: p.getIndices().getArray(),
  owner: new Uint16Array(fs.readFileSync(`${dir}/tri_part_p${i}.bin`).buffer.slice(0)),
}));
const report = [];
parts.forEach((name, pi) => {
  // bounds of this part across all primitives
  let mn = [1e9, 1e9, 1e9], mx = [-1e9, -1e9, -1e9];
  data.forEach((d) => { for (let t = 0; t < d.owner.length; t++) if (d.owner[t] === pi) for (let c = 0; c < 3; c++) { const v = d.idx[t * 3 + c]; for (let a = 0; a < 3; a++) { const x = d.pos[v * 3 + a]; if (x < mn[a]) mn[a] = x; if (x > mx[a]) mx[a] = x; } } });
  const ctr = mn.map((m, a) => (m + mx[a]) / 2);
  const mesh = doc.createMesh(name); let tris = 0;
  data.forEach((d, i) => {
    const remap = new Map(); const P = [], N = [], I = [];
    for (let t = 0; t < d.owner.length; t++) {
      if (d.owner[t] !== pi) continue;
      for (let c = 0; c < 3; c++) {
        const v = d.idx[t * 3 + c]; let nv = remap.get(v);
        if (nv === undefined) { nv = remap.size; remap.set(v, nv); for (let a = 0; a < 3; a++) { P.push(d.pos[v * 3 + a] - ctr[a]); N.push(d.nrm[v * 3 + a]); } }
        I.push(nv);
      }
    }
    if (!I.length) return; tris += I.length / 3;
    const prim = doc.createPrimitive().setMaterial(mats[i])
      .setAttribute('POSITION', doc.createAccessor().setType('VEC3').setArray(new Float32Array(P)).setBuffer(buf))
      .setAttribute('NORMAL', doc.createAccessor().setType('VEC3').setArray(new Float32Array(N)).setBuffer(buf))
      .setIndices(doc.createAccessor().setType('SCALAR').setArray(remap.size > 65535 ? new Uint32Array(I) : new Uint16Array(I)).setBuffer(buf));
    mesh.addPrimitive(prim);
  });
  root.addChild(doc.createNode(name).setTranslation(ctr).setMesh(mesh));
  report.push({ id: name, tris, centerMm: ctr.map((x) => +x.toFixed(2)), sizeMm: mn.map((m, a) => +(mx[a] - m).toFixed(2)) });
});
doc.createExtension(KHRDracoMeshCompression).setRequired(true).setEncoderOptions({
  method: KHRDracoMeshCompression.EncoderMethod.EDGEBREAKER,
  encodeSpeed: 5, decodeSpeed: 5,
  quantizationBits: { POSITION: 16, NORMAL: 10 },
});
doc.getRoot().getAsset().generator = 'AMT part segmentation (Tools/ModelPipeline) via glTF-Transform';
await doc.transform(dedup(), prune());
await io.write(out, doc);
fs.writeFileSync(`${dir}/parts_report.json`, JSON.stringify(report, null, 1));
console.log('wrote', out, fs.statSync(out).size, 'bytes,', report.reduce((a, r) => a + r.tris, 0), 'tris');
