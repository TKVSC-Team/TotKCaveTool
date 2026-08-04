# TotkCaveTool (CLI Application)

`TotkCaveTool` is a standalone command-line application built on top of the [`TotkCave`](../TotkCave) library for inspecting, converting, and batch exporting *Tears of the Kingdom* cave (`cave017`) and Depths terrain (`.quad`) streaming files.

---

## Installation & Build

Requires the **.NET 10.0 SDK**.

```bash
# Build Release executable
dotnet build TotkCaveTool.sln -c Release
```

---

## Commands

### 1. `info` — Inspect Cave Metadata
Displays cave ID, world matrix base origin, cell sidelength, subdivision LOD depth, bounding box (AABB), and node/stream counts.

```bash
dotnet run -- info <cave_dir>
```

**Example:**
```bash
dotnet run -- info Cave_Akkala_0000
```

---

### 2. `export` — Export Single Cave to OBJ
Reconstructs the 3D surface mesh for a cave folder and writes Wavefront `.obj` (and optional `.mtl`) files.

```bash
dotnet run -- export <cave_dir> [-o <out.obj>] [--materials] [--mc <path>] [--lod <N>] [--clean <N>] [-j <threads>]
```

**Options:**
- `-o <out.obj>`: Specify custom output OBJ file path (default: `<cave_name>.obj`).
- `--materials`: Generates companion `.mtl` material file with per-material face groups (`usemtl mat_X_layerY`) and triplanar UV projection coordinates.
- `--mc <path>`: Specify path to `mc_decompress.exe` / `mc_test_fixed.exe` binary.
- `--lod <N>`: Target Level-of-Detail (default: finest LOD, `NumSubdivisions`).
- `--clean <N>`: Filters out triangles with edges exceeding `N` meters (default: `0.0`, disabled).
- `-j <threads>` or `--threads <threads>`: Maximum parallel worker threads.

---

### 3. `batch` — Batch Export All Caves in Directory
Recursively scans a directory tree for folders containing `C.crbin` files and exports them to `.obj` format in parallel.

```bash
dotnet run -- batch <root_dir> [-o <out_dir>] [--materials] [--mc <path>] [--lod <N>] [-j <threads>]
```

**Options:**
- `-o <out_dir>`: Output directory for exported `.obj` files (default: `cave_objs`).
- `--materials`: Export `.mtl` files and triplanar UVs.
- `--mc <path>`: Path to MeshCodec executable.
- `--lod <N>`: Target Level-of-Detail for all caves.
- `-j <threads>` or `--threads <threads>`: Maximum parallel worker threads (default: CPU logical cores).

**Example:**
```bash
dotnet run -- batch /path/to/romfs/cave017 -o exported_caves --materials -j 8
```

---

### 4. `depths` — Export Depths Quad Mesh
Reconstructs Depths quad terrain geometry from a Depths `C.crbin` resource file, supporting custom LOD levels and multithreaded quad node decoding.

```bash
dotnet run -- depths <crbin_file> [-o <out.obj>] [--lod <N>] [-j <threads>]
```

**Options:**
- `-o <out.obj>`: Output OBJ file path (default: `depths.obj`).
- `--lod <N>`: Target Level-of-Detail (default: finest LOD, `res.MaxLod`).
- `-j <threads>` or `--threads <threads>`: Number of parallel worker threads for decoding quad nodes.

**Example:**
```bash
dotnet run -- depths /path/to/MinusField/Full/C.crbin -o depths_lod3.obj --lod 3 -j 16
```
