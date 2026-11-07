# ProBuilder 5.2.4 — Project Reference

**Project version:** Unity 2022.3.62f3c1 · ProBuilder 5.2.4 · **Last verified:** 2026-11-01

---

## Status in this project: installed, unused

- `com.unity.probuilder 5.2.4` is a direct dependency.
- **No `ProBuilderMesh` component exists in any scene or prefab**, and no script
  references the ProBuilder API.
- All geometry is either imported third-party models (`Assets/Art/`) or primitives
  assembled by editor scripts (`MetroSceneDev`, `FactorySceneDev`).

## What it is good for here

ProBuilder is an in-editor modelling tool. It is genuinely useful for the kind of
blockout this project does — corridor shells, door frames, room volumes — because
the alternative in this codebase is writing `GameObject.CreatePrimitive` chains in
`MetroSceneDev`/`FactorySceneDev` and then hand-placing them.

If you use it, prefer the **ProBuilder window** workflow (create shape → edit
faces/edges → export) over scripting the API. The scripting surface is large and
undocumented in places.

## Gotchas

- **ProBuilder meshes are `MeshFilter` + `MeshRenderer` with a `ProBuilderMesh`
  component.** They serialize the mesh *inside the scene/prefab*, not as a
  separate `.asset`. A big ProBuilder room bloats the scene file. For anything
  reused, use ProBuilder's "Export" to a real mesh asset.
- **The default ProBuilder material is `ProBuilder/Default`** which is a magenta
  placeholder in some pipelines. Always assign a project material.
- **ProBuilder does not generate colliders automatically.** Use
  `ProBuilderMesh.CreateMeshCollider`-style tooling or add colliders manually;
  forgetting this produces walk-through walls.
- **Do not use ProBuilder at runtime.** It is an editor tool; the `ProBuilderMesh`
  component has no runtime cost but also no runtime utility here.
