# glTFast 6.14.1 — Project Reference

**Project version:** Unity 2022.3.62f3c1 · `com.unity.cloud.gltfast` 6.14.1 · **Last verified:** 2026-11-01

---

## Status in this project: installed and used by one editor tool

- `com.unity.cloud.gltfast 6.14.1` is a direct dependency.
- It is the import path for `.glb` / `.gltf` assets. Several props and world
  packages arrive as GLB.
- **`Assets/Editor/GlbTextureCompressor.cs` exists because of a real problem with
  it** — read that file's header comment before touching GLB assets.

## The known problem: GLB textures bypass `TextureImporter`

When glTFast imports a `.glb`, **embedded textures do not go through
`TextureImporter`**. There is no import setting to change, no max size, no
compression. They land as uncompressed `ARGB32`.

Measured 2026-10-27 in this project: three prop GLBs carried **17 × 2048×2048
textures, each 42.7 MB, 725 MB total** — about 40 % of the project's 1737 MB
`Texture2D` memory.

`GlbTextureCompressor` fixes it by:

1. Blitting each embedded `Texture2D` out (GLB textures are not readable, so
   `EncodeToPNG` cannot be called directly);
2. writing an external PNG next to the GLB under `CompressedTextures/`;
3. assigning a `TextureImporter` on the PNG — `maxSize 1024`, compressed (normal
   maps as `NormalMap` type → BC5);
4. copying the GLB material to `Assets/Material/GLBCompressed/` with the external
   PNGs bound;
5. repointing every renderer in scenes/prefabs that referenced the original GLB
   material.

Result: 2048² ARGB32 (42.7 MB) → 1024² BC3/BC5 (~5.3 MB), an 8× reduction per
texture.

**So: after importing any new GLB, check texture memory.** If the asset is
large, run that tool rather than assuming import settings handled it.

## Import gotchas

- **glTFast has its own runtime loader and an editor import path.** Dragging a
  `.glb` into the Project window uses the editor path (creates a prefab). Loading
  from a URL/stream at runtime uses `GltfImport` — different code, different
  settings.
- **Material generation**: glTFast creates materials on import. The project
  deliberately duplicates and repoints them (`GLBCompressed`) rather than editing
  the generated ones, because regeneration would overwrite edits.
- **Multi-file glTF** (`.gltf` + `.bin` + textures) must be imported as a set;
  a bare `.gltf` without its sidecars imports with missing textures.
- **Coordinate system**: glTF is Y-up right-handed; Unity is Y-up left-handed.
  glTFast handles the conversion, but hand-authored transforms on imported roots
  can double-apply it. Check `localScale`/rotation if a model comes in mirrored.
