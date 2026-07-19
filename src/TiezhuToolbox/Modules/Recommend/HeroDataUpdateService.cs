using System.Collections.Concurrent;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TiezhuToolbox.Modules.Recommend;

public record HeroDataUpdateProgress(string Stage, int Current, int Total, string Message);
public record HeroDataUpdateResult(HeroDataDocument Document, IReadOnlyList<string> Warnings);

/// <summary>STOVE 官方英雄元数据与传说分段统计采集服务，供主程序和命令行采集工具共用。</summary>
public sealed class HeroDataUpdateService : IDisposable
{
    private const string ApiBase = "https://e7api.onstove.com/gameApi";
    private const string MetadataUrl = "https://static-pubcomm.onstove.com/gameRecord/epic7/epic7_hero.json";
    private const string AvatarBase = "https://static-pubcomm.onstove.com/event/live/epic7/guide/images/hero";
    private const string SetIconBase = "https://static-pubcomm.onstove.com/event/live/epic7/guide/wearingStatus/images/sets";
    private const double MainstreamSetRateThreshold = 10.0;

    public static IReadOnlyDictionary<string, string> SetNames { get; } = new Dictionary<string, string>
    {
        ["set_att"] = "攻击套装", ["set_def"] = "防御套装", ["set_max_hp"] = "生命值套装",
        ["set_speed"] = "速度套装", ["set_cri"] = "暴击套装", ["set_cri_dmg"] = "破灭套装",
        ["set_acc"] = "命中套装", ["set_res"] = "抵抗套装", ["set_vampire"] = "吸血套装",
        ["set_counter"] = "反击套装", ["set_coop"] = "夹攻套装", ["set_immune"] = "免疫套装",
        ["set_rage"] = "愤怒套装", ["set_penetrate"] = "穿透套装", ["set_scar"] = "伤口套装",
        ["set_shield"] = "守护套装", ["set_torrent"] = "激流套装", ["set_revenant"] = "逆袭套装",
        ["set_riposte"] = "回击套装", ["set_opener"] = "开战套装", ["set_chase"] = "追击套装",
        ["set_weak"] = "弱化套装", ["set_might"] = "全力套装",
    };

    private readonly HttpClient _http;

    public HeroDataUpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TiezhuToolbox/1.0");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Caller-Id", "TiezhuToolbox");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Caller-Detail", "HeroDataUpdater");
    }

    public async Task<HeroDataUpdateResult> CollectAsync(
        HeroDataDocument? existing,
        IProgress<HeroDataUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new("metadata", 0, 1, "正在读取官方英雄列表"));
        var metadata = await GetMetadataAsync(cancellationToken);
        var seasonCode = await GetCurrentSeasonCodeAsync(cancellationToken);
        var popularCodes = await GetPopularHeroCodesAsync(seasonCode, cancellationToken);
        var existingByCode = (existing?.Heroes ?? new()).ToDictionary(h => h.Code, StringComparer.Ordinal);
        var results = new ConcurrentDictionary<string, HeroInfo>(StringComparer.Ordinal);
        var warnings = new ConcurrentBag<string>();
        var targets = metadata.Where(h => popularCodes.Contains(h.Code)).ToList();
        var completed = 0;

        await Parallel.ForEachAsync(targets,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
            async (hero, token) =>
            {
                HeroInfo? analyzed = null;
                Exception? lastError = null;
                for (var attempt = 1; attempt <= 3 && analyzed == null; attempt++)
                {
                    try
                    {
                        analyzed = await FetchHeroAnalysisAsync(hero, seasonCode, token);
                    }
                    catch (Exception ex) when (attempt < 3 && ex is not OperationCanceledException)
                    {
                        lastError = ex;
                        await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        lastError = ex;
                    }
                }

                if (analyzed == null)
                {
                    if (existingByCode.TryGetValue(hero.Code, out var old))
                        analyzed = MergeMetadata(hero, old);
                    else
                        analyzed = hero;
                    warnings.Add($"{hero.Name}：{lastError?.Message ?? "无统计数据"}");
                }

                results[hero.Code] = analyzed;
                var current = Interlocked.Increment(ref completed);
                progress?.Report(new("analysis", current, targets.Count, $"正在采集 {hero.Name}"));
            });

        // 没有传说分段数据的官方英雄仍进入配置列表，默认配置为空。
        foreach (var hero in metadata)
            results.TryAdd(hero.Code, hero);

        var document = new HeroDataDocument
        {
            SeasonCode = seasonCode,
            GradeCode = "legend",
            UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Sets = SetNames.Select(kv => new HeroSetInfo { Code = kv.Key, Name = kv.Value })
                .OrderBy(s => s.Code).ToList(),
            Heroes = metadata.Select(h => results[h.Code]).ToList(),
        };
        return new HeroDataUpdateResult(document, warnings.OrderBy(x => x).ToList());
    }

    public async Task<HeroDataUpdateResult> WritePackageAsync(
        string outputDirectory,
        HeroDataDocument? existing,
        IProgress<HeroDataUpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = await CollectAsync(existing, progress, cancellationToken);
        Directory.CreateDirectory(outputDirectory);
        var heroDirectory = Path.Combine(outputDirectory, "heroes");
        var setDirectory = Path.Combine(outputDirectory, "sets");
        Directory.CreateDirectory(heroDirectory);
        Directory.CreateDirectory(setDirectory);

        var downloads = result.Document.Heroes.Select(h => (Url: $"{AvatarBase}/{h.Code}_s.png", Path: Path.Combine(heroDirectory, h.Code + ".png")))
            .Concat(SetNames.Keys.Select(code => (Url: $"{SetIconBase}/{code}.png", Path: Path.Combine(setDirectory, code + ".png"))))
            .ToList();
        var downloaded = 0;
        await Parallel.ForEachAsync(downloads,
            new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = cancellationToken },
            async (item, token) =>
            {
                if (!File.Exists(item.Path) || new FileInfo(item.Path).Length == 0)
                {
                    try
                    {
                        var bytes = await _http.GetByteArrayAsync(item.Url, token);
                        await File.WriteAllBytesAsync(item.Path, bytes, token);
                    }
                    catch (Exception) when (!token.IsCancellationRequested)
                    {
                        // 图标不是核心数据，失败时由界面使用旧缓存或占位显示。
                    }
                }
                var current = Interlocked.Increment(ref downloaded);
                progress?.Report(new("images", current, downloads.Count, "正在更新头像与套装图标"));
            });

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        var json = JsonSerializer.Serialize(result.Document, jsonOptions);
        var path = Path.Combine(outputDirectory, "heroes.json");
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, json, new UTF8Encoding(false), cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);

        if (result.Warnings.Count > 0)
            progress?.Report(new("warning", result.Warnings.Count, result.Warnings.Count, $"完成，{result.Warnings.Count} 个英雄沿用旧数据或为空"));
        return result;
    }

    private async Task<List<HeroInfo>> GetMetadataAsync(CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(MetadataUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("zh-CN", out var heroes))
            throw new InvalidOperationException("官方英雄元数据缺少简体中文列表");

        return heroes.EnumerateArray()
            .Select(h => new HeroInfo
            {
                Code = GetString(h, "code"),
                Name = GetString(h, "name"),
                Attribute = GetString(h, "attribute_cd"),
                Job = GetString(h, "job_cd"),
                Grade = int.TryParse(GetString(h, "grade"), out var grade) ? grade : 0,
            })
            .Where(h => !string.IsNullOrWhiteSpace(h.Code) && h.Code is not ("c0001" or "c1005"))
            .ToList();
    }

    private async Task<string> GetCurrentSeasonCodeAsync(CancellationToken cancellationToken)
    {
        using var doc = await PostApiAsync("getSeasonList", "lang=zh-CN", cancellationToken);
        var body = doc.RootElement.GetProperty("value").GetProperty("result_body");
        return body[0].GetProperty("season_code").GetString()
            ?? throw new InvalidOperationException("官方接口未返回赛季代码");
    }

    private async Task<HashSet<string>> GetPopularHeroCodesAsync(string seasonCode, CancellationToken cancellationToken)
    {
        using var doc = await PostApiAsync("getPopularHero",
            $"lang=zh-CN&season_code={Uri.EscapeDataString(seasonCode)}&grade_code=legend", cancellationToken);
        var body = doc.RootElement.GetProperty("value").GetProperty("result_body");
        return body.EnumerateArray().Select(h => GetString(h, "hero_code"))
            .Where(code => !string.IsNullOrWhiteSpace(code) && !code.StartsWith('m'))
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<HeroInfo> FetchHeroAnalysisAsync(HeroInfo metadata, string seasonCode, CancellationToken cancellationToken)
    {
        using var doc = await PostApiAsync("getHeroAnalysis",
            $"lang=zh-CN&hero_code={Uri.EscapeDataString(metadata.Code)}&season_code={Uri.EscapeDataString(seasonCode)}&grade_code=legend",
            cancellationToken);
        var body = doc.RootElement.GetProperty("value").GetProperty("result_body");
        var hero = MergeMetadata(metadata, null);
        hero.HasLegendData = body.ValueKind == JsonValueKind.Object;
        if (!hero.HasLegendData)
            return hero;

        if (body.TryGetProperty("abillity", out var ability) && ability.ValueKind == JsonValueKind.Object)
        {
            var histograms = new Dictionary<string, double[]>(StringComparer.Ordinal);
            foreach (var property in ability.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                    continue;
                histograms[property.Name] = (property.Value.GetString() ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => double.TryParse(s, out var value) ? value : 0).ToArray();
            }
            hero.UsefulStats.AddRange(HeroUsefulStatAnalyzer.InferUsefulStats(histograms));
        }

        if (body.TryGetProperty("equip", out var equip) && equip.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in equip.EnumerateArray())
            {
                var rate = item.TryGetProperty("rate", out var rateValue) ? rateValue.GetDouble() : 0;
                if (rate < MainstreamSetRateThreshold)
                    continue;
                hero.SetCombos.Add(new HeroSetCombo
                {
                    Sets = item.GetProperty("equip_list").EnumerateArray()
                        .Select(x => x.GetString() ?? string.Empty).Where(SetNames.ContainsKey).ToList(),
                    Rate = rate,
                    WinRate = item.TryGetProperty("win_rate", out var winRate) ? winRate.GetDouble() : 0,
                });
            }
        }
        return hero;
    }

    private async Task<JsonDocument> PostApiAsync(string api, string query, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsync($"{ApiBase}/{api}?{query}", new StringContent(string.Empty), cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.GetProperty("code").GetInt32() != 0)
        {
            var message = document.RootElement.TryGetProperty("message", out var value) ? value.GetString() : "UNKNOWN";
            document.Dispose();
            throw new InvalidOperationException(message);
        }
        return document;
    }

    private static HeroInfo MergeMetadata(HeroInfo metadata, HeroInfo? stats) => new()
    {
        Code = metadata.Code,
        Name = metadata.Name,
        Attribute = metadata.Attribute,
        Job = metadata.Job,
        Grade = metadata.Grade,
        HasLegendData = stats != null && (stats.HasLegendData || stats.UsefulStats.Count > 0 || stats.SetCombos.Count > 0),
        UsefulStats = stats?.UsefulStats.ToList() ?? new List<string>(),
        SetCombos = stats?.SetCombos.Select(c => new HeroSetCombo
        {
            Sets = c.Sets.ToList(), Rate = c.Rate, WinRate = c.WinRate,
        }).ToList() ?? new List<HeroSetCombo>(),
    };

    private static string GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    public void Dispose() => _http.Dispose();
}
