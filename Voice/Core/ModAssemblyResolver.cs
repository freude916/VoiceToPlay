using System.Reflection;

namespace VoiceToPlay.Voice.Core;

/// <summary>
///     Mod 程序集解析器。在运行时从 mod 目录加载依赖程序集。
///     游戏默认的程序集解析不会查找 mod 目录，需要手动挂载。
/// </summary>
internal static class ModAssemblyResolver
{
    private static readonly object SyncLock = new();
    private static bool _initialized;
    private static string? _modDirectory;
    private static readonly HashSet<string> LoadedAssemblies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Mod 目录路径。DLL 和资源文件都在这里。
    /// </summary>
    public static string ModDirectory => _modDirectory ?? ResolveModDirectory();

    /// <summary>
    ///     初始化程序集解析器。在 MainFile.Initialize() 中调用一次。
    /// </summary>
    public static void Initialize()
    {
        lock (SyncLock)
        {
            if (_initialized) return;
            _initialized = true;

            _modDirectory = ResolveModDirectory();
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            MainFile.Logger.Info($"ModAssemblyResolver initialized. ModDirectory='{_modDirectory}'");
        }
    }

    /// <summary>
    ///     确保指定程序集已加载。返回是否成功。
    /// </summary>
    public static bool EnsureLoaded(string assemblyName)
    {
        lock (SyncLock)
        {
            if (LoadedAssemblies.Contains(assemblyName))
                return true;

            var dllPath = Path.Combine(ModDirectory, $"{assemblyName}.dll");
            if (!File.Exists(dllPath))
            {
                MainFile.Logger.Warn($"Assembly not found at '{dllPath}'");
                return false;
            }

            try
            {
                Assembly.LoadFrom(dllPath);
                LoadedAssemblies.Add(assemblyName);
                MainFile.Logger.Info($"Loaded assembly from mod directory: {assemblyName}");
                return true;
            }
            catch (Exception ex)
            {
                MainFile.Logger.Error($"Failed to load assembly '{assemblyName}': {ex}");
                return false;
            }
        }
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);
        var simpleName = assemblyName.Name;
        if (string.IsNullOrEmpty(simpleName)) return null;

        var modDir = ModDirectory;
        var dllPath = Path.Combine(modDir, $"{simpleName}.dll");

        MainFile.Logger.Info(
            $"AssemblyResolve: attempting '{simpleName}' from '{dllPath}' (exists={File.Exists(dllPath)})");

        if (!File.Exists(dllPath)) return null;

        try
        {
            var assembly = Assembly.LoadFrom(dllPath);
            LoadedAssemblies.Add(simpleName);
            MainFile.Logger.Info($"AssemblyResolve: loaded '{simpleName}' from mod directory");
            return assembly;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"AssemblyResolve failed for '{simpleName}': {ex}");
            return null;
        }
    }

    private static string ResolveModDirectory()
    {
        if (_modDirectory != null) return _modDirectory;

        var modName = Assembly.GetExecutingAssembly().GetName().Name ?? "VoiceToPlay";

        // 尝试多种路径候选
        var candidates = new List<string>();

        // 1. 进程目录下的 mods/VoiceToPlay (Steam 游戏)
        var processDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (!string.IsNullOrEmpty(processDir)) candidates.Add(Path.Combine(processDir, "mods", modName));

        // 2. BaseDirectory 下的 mods/VoiceToPlay
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "mods", modName));

        // 3. BaseDirectory 本身（开发时或 data 目录）
        candidates.Add(AppContext.BaseDirectory);

        // 4. 进程目录本身
        if (!string.IsNullOrEmpty(processDir)) candidates.Add(processDir);

        // 5. 当前工作目录
        candidates.Add(Environment.CurrentDirectory);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate)) continue;
            var dir = Path.GetFullPath(candidate);
            if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, $"{modName}.dll")))
            {
                MainFile.Logger.Info($"ModAssemblyResolver found mod directory: '{dir}'");
                return dir;
            }
        }

        // 回退到 BaseDirectory
        MainFile.Logger.Warn(
            $"ModAssemblyResolver could not find mod directory. Candidates checked: {string.Join(", ", candidates)}");
        return AppContext.BaseDirectory;
    }
}