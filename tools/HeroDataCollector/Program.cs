using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

// 第七史诗官方战绩数据采集工具（传说分段）
// 数据源：https://epic7.onstove.com/zh-TW/gg/herorecord 背后的 gameApi
// 用法：dotnet run [--out <输出目录>]   默认输出到 src/TiezhuToolbox/Assets/HeroData/

const string ApiBase = "https://e7api.onstove.com/gameApi";
const string AvatarBase = "https://static-pubcomm.onstove.com/event/live/epic7/guide/images/hero";
const string SetIconBase = "https://static-pubcomm.onstove.com/event/live/epic7/guide/wearingStatus/images/sets";
const string GradeCode = "legend"; // 只采集传说分段，避免低分段污染

// API 属性 → 游戏内简体中文属性名
var statNames = new Dictionary<string, string>
{
    ["att"] = "攻击力",
    ["def"] = "防御力",
    ["max_hp"] = "生命值",
    ["speed"] = "速度",
    ["cri"] = "暴击率",
    ["cri_dmg"] = "暴击伤害",
    ["acc"] = "效果命中",
    ["res"] = "效果抗性",
};

// 套装代码 → 游戏内简体中文套装名（必须与 OCR 识别出的 SetName 完全一致）
var setNames = new Dictionary<string, string>
{
    ["set_att"] = "攻击套装",
    ["set_def"] = "防御套装",
    ["set_max_hp"] = "生命值套装",
    ["set_speed"] = "速度套装",
    ["set_cri"] = "暴击套装",
    ["set_cri_dmg"] = "破灭套装",
    ["set_acc"] = "命中套装",
    ["set_res"] = "抵抗套装",
    ["set_vampire"] = "吸血套装",
    ["set_counter"] = "反击套装",
    ["set_coop"] = "夹攻套装",
    ["set_immune"] = "免疫套装",
    ["set_rage"] = "愤怒套装",
    ["set_penetrate"] = "穿透套装",
    ["set_scar"] = "伤口套装",
    ["set_shield"] = "守护套装",
    ["set_torrent"] = "激流套装",
    ["set_revenant"] = "逆袭套装",
    ["set_riposte"] = "回击套装",
    ["set_opener"] = "开战套装",
    ["set_chase"] = "追击套装",
    ["set_weak"] = "弱化套装",
    ["set_might"] = "全力套装",
    // “憎恨套装”暂无角色使用、未抓到代码，待补
};

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

// 有用属性判定：直方图 0+1 号低桶人数占比低于该阈值，说明玩家普遍在堆此属性
const double UsefulStatLowBucketThreshold = 0.15;
// 有用属性判定：人数最多的桶位于该序号及以上（高区间），同样说明玩家普遍在堆此属性
const int UsefulStatPeakBucket = 4;
// 主流套装判定：使用率不低于该值才保留
const double MainstreamSetRateThreshold = 10.0;

var outputDir = GetOutputDir(args);
Directory.CreateDirectory(outputDir);
Directory.CreateDirectory(Path.Combine(outputDir, "heroes"));
Directory.CreateDirectory(Path.Combine(outputDir, "sets"));

using var http = new HttpClient();
http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
http.DefaultRequestHeaders.TryAddWithoutValidation("Caller-Id", "HeroDataCollector");
http.DefaultRequestHeaders.TryAddWithoutValidation("Caller-Detail", "HeroDataCollector");
http.Timeout = TimeSpan.FromSeconds(30);

var t2s = LoadT2STable();

// 1. 当前赛季
var seasonCode = await GetCurrentSeasonCode();
Console.WriteLine($"当前赛季：{seasonCode}");

// 2. 传说分段角色列表（含角色名表）
var (heroCodes, heroNames) = await GetPopularHeroes(seasonCode);
Console.WriteLine($"传说分段角色数：{heroCodes.Count}，名字表覆盖：{heroNames.Count}");

// 3. 逐角色拉详情，推导有用属性与主流套装
var heroes = new List<HeroEntry>();
var failed = new List<string>();
for (var i = 0; i < heroCodes.Count; i++)
{
    var code = heroCodes[i];
    var entry = await FetchHero(code, seasonCode);
    if (entry == null)
    {
        failed.Add(code);
        Console.WriteLine($"[{i + 1}/{heroCodes.Count}] {code} 获取失败，跳过");
    }
    else
    {
        entry.Name = heroNames.TryGetValue(code, out var n) ? ToSimplified(n) : code;
        heroes.Add(entry);
        Console.WriteLine($"[{i + 1}/{heroCodes.Count}] {entry.Name}({code}) 有用属性=[{string.Join(",", entry.UsefulStats)}] 主流套装={entry.SetCombos.Count} 组");
    }
    await Task.Delay(300); // 礼貌限速
}

// 4. 下载头像与套装图标
var usedSets = heroes.SelectMany(h => h.SetCombos).SelectMany(c => c.Sets).Distinct().OrderBy(s => s).ToList();
var unknownSets = usedSets.Where(s => !setNames.ContainsKey(s)).ToList();
foreach (var s in unknownSets)
    Console.WriteLine($"警告：未知套装代码 {s}，请在 setNames 映射中补全");

await DownloadImages(heroes.Select(h => h.Code), usedSets);

// 5. 写出 heroes.json（camelCase、带缩进、UTF-8 无 BOM，方便 diff）
var doc = new HeroDataDoc
{
    SeasonCode = seasonCode,
    GradeCode = GradeCode,
    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
    Sets = usedSets.Where(setNames.ContainsKey)
        .Select(s => new SetEntry { Code = s, Name = setNames[s] }).ToList(),
    Heroes = heroes,
};
var json = JsonSerializer.Serialize(doc, jsonOptions);
await File.WriteAllTextAsync(Path.Combine(outputDir, "heroes.json"), json, new UTF8Encoding(false));

Console.WriteLine($"完成：成功 {heroes.Count} 个，失败 {failed.Count} 个{(failed.Count > 0 ? $"（{string.Join(",", failed)}）" : "")}");
Console.WriteLine($"输出目录：{outputDir}");
return failed.Count > 0 ? 1 : 0;

// ---------- 以下为实现 ----------

static string GetOutputDir(string[] args)
{
    var idx = Array.IndexOf(args, "--out");
    if (idx >= 0 && idx + 1 < args.Length)
        return Path.GetFullPath(args[idx + 1]);

    // 从输出目录向上找仓库根（含 src/TiezhuToolbox/TiezhuToolbox.csproj）
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "src", "TiezhuToolbox", "TiezhuToolbox.csproj")))
            return Path.Combine(dir.FullName, "src", "TiezhuToolbox", "Assets", "HeroData");
        dir = dir.Parent;
    }
    throw new InvalidOperationException("未找到仓库根目录，请用 --out 指定输出目录");
}

async Task<string> GetCurrentSeasonCode()
{
    using var doc = await PostApi("getSeasonList", $"lang=zh-TW");
    var seasons = doc.RootElement.GetProperty("value").GetProperty("result_body");
    return seasons[0].GetProperty("season_code").GetString()!;
}

async Task<(List<string> Codes, Dictionary<string, string> Names)> GetPopularHeroes(string seasonCode)
{
    using var doc = await PostApi("getPopularHero", $"lang=zh-TW&season_code={seasonCode}&grade_code={GradeCode}");
    var body = doc.RootElement.GetProperty("value").GetProperty("result_body");
    var codes = new List<string>();
    var names = new Dictionary<string, string>();
    foreach (var h in body.EnumerateArray())
    {
        codes.Add(h.GetProperty("hero_code").GetString()!);
        if (h.TryGetProperty("hero_names", out var ns))
            foreach (var p in ns.EnumerateObject())
                names[p.Name] = p.Value.GetString()!;
    }
    return (codes, names);
}

async Task<HeroEntry?> FetchHero(string heroCode, string seasonCode)
{
    for (var attempt = 0; attempt < 3; attempt++)
    {
        try
        {
            using var doc = await PostApi("getHeroAnalysis",
                $"lang=zh-TW&hero_code={heroCode}&season_code={seasonCode}&grade_code={GradeCode}");
            var body = doc.RootElement.GetProperty("value").GetProperty("result_body");
            if (body.ValueKind != JsonValueKind.Object
                || !body.TryGetProperty("abillity", out var abillity)
                || abillity.ValueKind != JsonValueKind.Object)
                return new HeroEntry { Code = heroCode }; // 无数据角色：保留空信息

            var useful = new List<string>();
            foreach (var p in abillity.EnumerateObject())
            {
                if (!statNames.TryGetValue(p.Name, out var cn)) continue;
                if (p.Value.ValueKind != JsonValueKind.String) continue;
                var buckets = (p.Value.GetString() ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => double.TryParse(s, out var v) ? v : 0).ToArray();
                var total = buckets.Sum();
                if (total <= 0) continue;

                // 有用属性判定（直方图左移=没人堆，右移/高峰值=玩家普遍在堆）：
                // 0+1 号低桶占比低于阈值，或人数最多的桶位于高区间（≥4 号桶）
                var lowRatio = (buckets.ElementAtOrDefault(0) + buckets.ElementAtOrDefault(1)) / total;
                var peakBucket = Array.IndexOf(buckets, buckets.Max());
                if (lowRatio < UsefulStatLowBucketThreshold || peakBucket >= UsefulStatPeakBucket)
                    useful.Add(cn);
            }

            var combos = new List<SetCombo>();
            if (body.TryGetProperty("equip", out var equip) && equip.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in equip.EnumerateArray())
                {
                    var rate = e.GetProperty("rate").GetDouble();
                    if (rate < MainstreamSetRateThreshold) continue;
                    combos.Add(new SetCombo
                    {
                        Sets = e.GetProperty("equip_list").EnumerateArray().Select(x => x.GetString()!).ToList(),
                        Rate = rate,
                        WinRate = e.GetProperty("win_rate").GetDouble(),
                    });
                }
            }

            return new HeroEntry { Code = heroCode, UsefulStats = useful, SetCombos = combos };
        }
        catch (ApiException ex) when (ex.IsNoData)
        {
            return new HeroEntry { Code = heroCode }; // 该角色传说分段无统计数据，保留空信息
        }
        catch (Exception ex)
        {
            if (attempt >= 2)
            {
                Console.WriteLine($"  {heroCode} 第 {attempt + 1} 次请求异常：{ex.Message}，放弃");
                return null;
            }
            Console.WriteLine($"  {heroCode} 第 {attempt + 1} 次请求异常：{ex.Message}，重试...");
            await Task.Delay(1500);
        }
    }
    return null;
}

async Task DownloadImages(IEnumerable<string> heroCodes, IEnumerable<string> setCodes)
{
    var heroDir = Path.Combine(outputDir, "heroes");
    var setDir = Path.Combine(outputDir, "sets");
    var count = 0;
    foreach (var code in heroCodes)
    {
        var path = Path.Combine(heroDir, $"{code}.png");
        if (File.Exists(path) && new FileInfo(path).Length > 0) continue;
        await Download($"{AvatarBase}/{code}_s.png", path);
        if (++count % 50 == 0) Console.WriteLine($"  头像下载进度 {count}");
        await Task.Delay(80);
    }
    foreach (var code in setCodes)
    {
        var path = Path.Combine(setDir, $"{code}.png");
        if (File.Exists(path) && new FileInfo(path).Length > 0) continue;
        await Download($"{SetIconBase}/{code}.png", path);
        await Task.Delay(80);
    }
}

async Task Download(string url, string path)
{
    try
    {
        var bytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(path, bytes);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  下载失败 {url}：{ex.Message}");
    }
}

// gameApi 均为 POST + query string 传参
async Task<JsonDocument> PostApi(string api, string query)
{
    using var resp = await http.PostAsync($"{ApiBase}/{api}?{query}", new StringContent(""));
    resp.EnsureSuccessStatusCode();
    var stream = await resp.Content.ReadAsStreamAsync();
    var doc = await JsonDocument.ParseAsync(stream);
    if (doc.RootElement.GetProperty("code").GetInt32() != 0)
        throw new ApiException(doc.RootElement.GetProperty("message").GetString() ?? "UNKNOWN");
    return doc;
}


// 读取 OpenCC TSCharacters.txt，构建 繁→简 单字映射（多候选取第一个）
static Dictionary<char, char> LoadT2STable()
{
    var path = Path.Combine(AppContext.BaseDirectory, "assets", "TSCharacters.txt");
    var table = new Dictionary<char, char>();
    foreach (var line in File.ReadLines(path))
    {
        if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line)) continue;
        var parts = line.Split('\t', StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || parts[0].Length != 1) continue;
        var simp = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (simp?.Length == 1)
            table[parts[0][0]] = simp[0];
    }
    Console.WriteLine($"繁简映射表：{table.Count} 字");
    return table;
}

string ToSimplified(string text)
{
    var sb = new StringBuilder(text.Length);
    foreach (var ch in text)
        sb.Append(t2s.TryGetValue(ch, out var s) ? s : ch);
    return sb.ToString();
}

class HeroDataDoc
{
    [JsonPropertyName("seasonCode")] public string SeasonCode { get; set; } = "";
    [JsonPropertyName("gradeCode")] public string GradeCode { get; set; } = "";
    [JsonPropertyName("updatedAt")] public string UpdatedAt { get; set; } = "";
    [JsonPropertyName("sets")] public List<SetEntry> Sets { get; set; } = new();
    [JsonPropertyName("heroes")] public List<HeroEntry> Heroes { get; set; } = new();
}

class SetEntry
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

class HeroEntry
{
    [JsonPropertyName("code")] public string Code { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("usefulStats")] public List<string> UsefulStats { get; set; } = new();
    [JsonPropertyName("setCombos")] public List<SetCombo> SetCombos { get; set; } = new();
}

class SetCombo
{
    [JsonPropertyName("sets")] public List<string> Sets { get; set; } = new();
    [JsonPropertyName("rate")] public double Rate { get; set; }
    [JsonPropertyName("winRate")] public double WinRate { get; set; }
}

// API 业务错误；ERROR_COMMOM_EVENTPERIOD_REPONSE 表示该角色在当前赛季/分段无统计数据
class ApiException(string message) : Exception(message)
{
    public bool IsNoData => Message.Contains("EVENTPERIOD");
}
