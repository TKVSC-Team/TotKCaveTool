using System.Diagnostics;
using System.Globalization;
using TotkCave.Building;
using TotkCave.Exporting;
using TotkCave.Models;
using TotkCave.PageSource;
using TotkCave.Utils;
using TotkCave.Validation;

namespace TotkCaveTool;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            PrintUsage();
            return 0;
        }

        string command = args[0].ToLowerInvariant();
        string[] cmdArgs = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "info" => RunInfo(cmdArgs),
                "export" => RunExport(cmdArgs),
                "batch" => RunBatch(cmdArgs),
                "depths" => RunDepths(cmdArgs),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("TotkCaveTool (.NET 10.0) - Tears of the Kingdom Cave & Quad Toolkit");
        Console.WriteLine("Usage:");
        Console.WriteLine("  TotkCaveTool info <cave_dir>");
        Console.WriteLine("  TotkCaveTool export <cave_dir> [-o <out.obj>] [--materials] [--mc <path>] [--lod <N>] [--clean <N>] [-j <threads>]");
        Console.WriteLine("  TotkCaveTool batch <root_dir> [-o <out_dir>] [--materials] [--mc <path>] [--lod <N>] [-j <threads>]");
        Console.WriteLine("  TotkCaveTool depths <crbin_file> [-o <out.obj>] [--lod <N>] [-j <threads>]");
        Console.WriteLine();
    }

    private static int UnknownCommand(string command)
    {
        Console.WriteLine($"Unknown command '{command}'. Run with --help for usage.");
        return 1;
    }

    private static int RunInfo(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: TotkCaveTool info <cave_dir>");
            return 1;
        }

        string caveDir = args[0];
        string crbinPath = Path.Combine(caveDir, "C.crbin");
        if (!File.Exists(crbinPath) && File.Exists(caveDir))
        {
            crbinPath = caveDir;
            caveDir = Path.GetDirectoryName(caveDir) ?? ".";
        }

        if (!File.Exists(crbinPath))
        {
            Console.WriteLine($"Error: C.crbin not found in '{caveDir}'.");
            return 1;
        }

        string? mcTool = GetArgValue(args, "--mc");
        CrBin cr = CrBin.FromFile(crbinPath);
        CavePageSource pages = new(cr, caveDir, mcTool: mcTool);

        Console.WriteLine($"Cave ID         : 0x{cr.CaveId:x8} ({cr.CaveId})");
        Console.WriteLine($"Path            : {cr.Path}");
        Console.WriteLine($"Chunk Dir       : {cr.ChunkDirPath}");
        Console.WriteLine($"Base Position   : ({cr.BasePos.X:F3}, {cr.BasePos.Y:F3}, {cr.BasePos.Z:F3})");
        Console.WriteLine($"Min Sidelength  : {cr.MinSidelength:F3}");
        Console.WriteLine($"Subdivisions    : {cr.NumSubdivisions} (LOD levels: 0..{cr.NumSubdivisions})");
        Console.WriteLine($"AABB Min        : ({cr.Aabb.Min.X:F2}, {cr.Aabb.Min.Y:F2}, {cr.Aabb.Min.Z:F2})");
        Console.WriteLine($"AABB Max        : ({cr.Aabb.Max.X:F2}, {cr.Aabb.Max.Y:F2}, {cr.Aabb.Max.Z:F2})");
        Console.WriteLine($"Nodes           : {cr.Nodes.Count}");
        Console.WriteLine($"Streams         : {cr.Streams.Count}");
        Console.WriteLine($"Materials       : {cr.Materials.Count}");
        Console.WriteLine($"Page Files      : {cr.PageFiles.Count}");

        return 0;
    }

    private static int RunExport(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: TotkCaveTool export <cave_dir> [-o <out.obj>] [--materials] [--mc <path>] [--lod <N>] [--clean <N>] [-j <threads>]");
            return 1;
        }

        string caveDir = args[0];
        string crbinPath = Path.Combine(caveDir, "C.crbin");
        if (!File.Exists(crbinPath) && File.Exists(caveDir))
        {
            crbinPath = caveDir;
            caveDir = Path.GetDirectoryName(caveDir) ?? ".";
        }

        string outObj = GetArgValue(args, "-o") ?? $"{Path.GetFileName(caveDir)}.obj";
        bool withMaterials = args.Contains("--materials");
        string? mcTool = GetArgValue(args, "--mc");
        int? lod = GetIntArg(args, "--lod");
        float clean = GetFloatArg(args, "--clean") ?? 0.0f;
        int threads = GetThreadsArg(args);

        CrBin cr = CrBin.FromFile(crbinPath);
        CavePageSource pages = new(cr, caveDir, mcTool: mcTool);

        Console.WriteLine($"[Building] {Path.GetFileName(caveDir)} (LOD {lod ?? cr.NumSubdivisions})...");
        Stopwatch sw = Stopwatch.StartNew();

        Action<int, int> progress = (done, total) => RenderProgressBar("Cave", done, total);
        CaveMesh mesh = MeshBuilder.BuildMesh(cr, pages, lod, weld: true, clean: clean, maxDegreeOfParallelism: threads, progressCallback: progress);
        sw.Stop();
        Console.WriteLine();

        Console.WriteLine($"Built mesh in {sw.ElapsedMilliseconds} ms: {mesh.Vertices.Count:N0} vertices, {mesh.Faces.Count:N0} faces");

        ObjExportOptions options = new(
            IncludeColors: true,
            IncludeNormals: true,
            IncludeGroups: withMaterials,
            IncludeMaterials: withMaterials,
            HeaderComment: $"Cave: {Path.GetFileName(caveDir)}, LOD: {lod ?? cr.NumSubdivisions}"
        );

        ObjExporter.WriteObj(mesh, outObj, options);
        Console.WriteLine($"Wrote: {outObj}{(withMaterials ? " (+ .mtl)" : "")}");

        return 0;
    }

    private static int RunBatch(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: TotkCaveTool batch <root_dir> [-o <out_dir>] [--materials] [--mc <path>] [--lod <N>] [-j <threads>]");
            return 1;
        }

        string rootDir = args[0];
        string outDir = GetArgValue(args, "-o") ?? "cave_objs";
        bool withMaterials = args.Contains("--materials");
        string? mcTool = GetArgValue(args, "--mc");
        int? lod = GetIntArg(args, "--lod");
        int threads = GetThreadsArg(args);

        var caves = CaveFinder.FindCaves(rootDir).ToList();
        if (caves.Count == 0)
        {
            Console.WriteLine($"No caves (containing C.crbin) found under '{rootDir}'.");
            return 1;
        }

        Console.WriteLine($"Found {caves.Count} caves under '{rootDir}'. Exporting to '{outDir}'...");
        Directory.CreateDirectory(outDir);

        int maxThreads = threads > 0 ? threads : Environment.ProcessorCount;
        ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = maxThreads };

        int exported = 0;
        Parallel.ForEach(caves, parallelOptions, cave =>
        {
            var (name, dirPath) = cave;
            try
            {
                string crbinPath = Path.Combine(dirPath, "C.crbin");
                CrBin cr = CrBin.FromFile(crbinPath);
                CavePageSource pages = new(cr, dirPath, mcTool: mcTool);

                CaveMesh mesh = MeshBuilder.BuildMesh(cr, pages, lod: lod);
                string outFile = Path.Combine(outDir, $"{name}.obj");

                ObjExporter.WriteObj(mesh, outFile, new ObjExportOptions(
                    IncludeColors: true,
                    IncludeNormals: true,
                    IncludeGroups: withMaterials,
                    IncludeMaterials: withMaterials,
                    HeaderComment: $"Batch exported {name} (LOD: {lod ?? cr.NumSubdivisions})"
                ));

                int currentCount = Interlocked.Increment(ref exported);
                RenderProgressBar("Batch", currentCount, caves.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[FAILED] {name}: {ex.Message}");
            }
        });

        Console.WriteLine();
        Console.WriteLine($"Batch completed: {exported}/{caves.Count} successfully exported.");
        return 0;
    }

    private static int RunDepths(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: TotkCaveTool depths <crbin_file> [-o <out.obj>] [--lod <N>] [-j <threads> | --threads <N>]");
            return 1;
        }

        string crbinPath = args[0];
        string outObj = GetArgValue(args, "-o") ?? "depths.obj";
        int? lod = GetIntArg(args, "--lod");
        int threads = GetThreadsArg(args);

        QuadResource res = new(crbinPath);
        QuadPageSource pages = new(res);

        int targetLod = lod ?? res.MaxLod;
        int activeThreads = threads > 0 ? threads : Environment.ProcessorCount;

        Console.WriteLine($"[Depths Resource] Nodes: {res.NodeCount:N0}, Far LODs: {res.NumFarLodLevels}, Normal LODs: {res.NumNormalLodLevels} (Max LOD: {res.MaxLod})");
        Console.WriteLine($"[Streaming Depths Quad Mesh] {Path.GetFileName(crbinPath)} (Target LOD: {targetLod}, Worker Threads: {activeThreads})...");
        Console.Out.Flush();

        Stopwatch sw = Stopwatch.StartNew();

        Action<int, int, int> progress = (done, verts, total) => RenderProgressBar("Depths", done, total);

        var (totalVerts, totalFaces, totalNodes) = QuadMeshBuilder.ExportObjStreaming(res, pages, outObj, lod: targetLod, weld: true, maxDegreeOfParallelism: activeThreads, progressCallback: progress);
        sw.Stop();
        Console.WriteLine();

        FileInfo objInfo = new(outObj);
        double sizeMB = objInfo.Exists ? objInfo.Length / (1024.0 * 1024.0) : 0.0;
        Console.WriteLine($"Wrote Depths OBJ ({totalVerts:N0} verts, {totalFaces:N0} faces, {sizeMB:F1} MB) in {sw.ElapsedMilliseconds / 1000.0:F2}s -> {outObj}");
        return 0;
    }

    private static void RenderProgressBar(string label, int done, int total)
    {
        if (total <= 0) return;
        float percent = (float)done / total * 100.0f;
        int barWidth = 30;
        int filled = Math.Min(barWidth, (int)(barWidth * done / total));
        string bar = new string('█', filled) + new string('░', barWidth - filled);
        Console.Write(string.Create(CultureInfo.InvariantCulture, $"\r[{label}] [{bar}] {percent,5:F1}% ({done:N0}/{total:N0} nodes)"));
        Console.Out.Flush();
    }

    private static string? GetArgValue(string[] args, string flag)
    {
        int idx = Array.IndexOf(args, flag);
        return (idx >= 0 && idx + 1 < args.Length) ? args[idx + 1] : null;
    }

    private static int? GetIntArg(string[] args, string flag)
    {
        string? val = GetArgValue(args, flag);
        return val != null && int.TryParse(val, out int res) ? res : null;
    }

    private static float? GetFloatArg(string[] args, string flag)
    {
        string? val = GetArgValue(args, flag);
        return val != null && float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float res) ? res : null;
    }

    private static int GetThreadsArg(string[] args)
    {
        int? jVal = GetIntArg(args, "-j");
        if (jVal.HasValue) return jVal.Value;
        int? tVal = GetIntArg(args, "--threads");
        if (tVal.HasValue) return tVal.Value;
        return -1;
    }
}
