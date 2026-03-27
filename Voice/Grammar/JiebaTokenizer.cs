using JiebaNet.Segmenter;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Voice.Grammar;

/// <summary>
///     jieba 分词器。用于扩展语音词表。
///     使用延迟初始化，在首次分词时才尝试加载 jieba。
/// </summary>
internal sealed class JiebaTokenizer
{
    private static readonly string[] ResourceFileNames =
    [
        "dict.txt",
        "idf.txt",
        "stopwords.txt",
        "char_state_tab.json",
        "prob_emit.json",
        "prob_trans.json"
    ];

    private static readonly object SyncLock = new();
    private static bool _configAttempted;
    private static bool _initializationAttempted;
    private static bool _loggedUnavailable;
    private static string? _configuredResourceDirectory;
    private static JiebaSegmenter? _segmenter;

    /// <summary>
    ///     预热分词器。可选调用，提前初始化。
    /// </summary>
    public void Warmup()
    {
        _ = TryEnsureSegmenter(out _);
    }

    /// <summary>
    ///     将文本分词，返回词集合。
    ///     如果 jieba 不可用，返回空集合。
    /// </summary>
    public IReadOnlyCollection<string> Tokenize(string text)
    {
        if (text.Length == 0 || !TryEnsureSegmenter(out var segmenter))
            return Array.Empty<string>();

        try
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in segmenter.Cut(text, false, false))
            {
                var normalized = VoiceText.Normalize(token);
                if (normalized.Length > 0)
                    result.Add(normalized);
            }

            return result;
        }
        catch (Exception ex)
        {
            lock (SyncLock)
            {
                _segmenter = null;
                if (!_loggedUnavailable)
                {
                    _loggedUnavailable = true;
                    MainFile.Logger.Error($"Jieba segmentation failed. Fallback to whole-word grammar. {ex}");
                }
            }

            return Array.Empty<string>();
        }
    }

    private static bool TryEnsureSegmenter(out JiebaSegmenter segmenter)
    {
        lock (SyncLock)
        {
            if (_segmenter != null)
            {
                segmenter = _segmenter;
                return true;
            }

            if (_initializationAttempted)
            {
                segmenter = null!;
                return false;
            }

            _initializationAttempted = true;
            try
            {
                // 确保 JiebaNet.Segmenter 程序集已加载
                if (!ModAssemblyResolver.EnsureLoaded("JiebaNet.Segmenter"))
                {
                    if (!_loggedUnavailable)
                    {
                        _loggedUnavailable = true;
                        MainFile.Logger.Error(
                            $"JiebaNet.Segmenter assembly not found or failed to load from '{ModAssemblyResolver.ModDirectory}'.");
                    }

                    segmenter = null!;
                    return false;
                }

                ConfigureResourceDirectory();
                _segmenter = new JiebaSegmenter();
                var baseDirForLog = _configuredResourceDirectory ?? "(package default)";
                MainFile.Logger.Info($"Jieba segmenter initialized. baseDir='{baseDirForLog}'");
                segmenter = _segmenter;
                return true;
            }
            catch (Exception ex)
            {
                if (!_loggedUnavailable)
                {
                    _loggedUnavailable = true;
                    MainFile.Logger.Error(
                        $"Failed to initialize Jieba segmenter. Fallback to whole-word grammar. {ex}");
                }

                segmenter = null!;
                return false;
            }
        }
    }

    private static void ConfigureResourceDirectory()
    {
        if (_configAttempted) return;

        _configAttempted = true;
        var resourceDirectory = ResolveResourceDirectory();
        if (resourceDirectory == null)
        {
            MainFile.Logger.Warn("Jieba resources directory not found. Will use package default relative path.");
            return;
        }

        ConfigManager.ConfigFileBaseDir = resourceDirectory;
        _configuredResourceDirectory = resourceDirectory;
        MainFile.Logger.Info($"Jieba resource base dir set to '{resourceDirectory}'.");
    }

    private static string? ResolveResourceDirectory()
    {
        foreach (var candidate in EnumerateResourceDirectoryCandidates())
            if (DirectoryHasRequiredResources(candidate))
                return candidate;

        return null;
    }

    private static IEnumerable<string> EnumerateResourceDirectoryCandidates()
    {
        var modDirectory = ModAssemblyResolver.ModDirectory;
        if (!string.IsNullOrWhiteSpace(modDirectory))
            yield return Path.Combine(modDirectory, "Resources");

        yield return Path.Combine(AppContext.BaseDirectory, "Resources");
        yield return Path.Combine(AppContext.BaseDirectory, "..", "Resources");
        yield return Path.Combine(Environment.CurrentDirectory, "Resources");
    }

    private static bool DirectoryHasRequiredResources(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return false;

        // 至少要有 dict.txt
        return File.Exists(Path.Combine(directoryPath, "dict.txt"));
    }
}