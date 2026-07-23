using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiezhuToolbox;

/// <summary>软件设置。新增字段必须提供兼容旧文件的默认值。</summary>
public class AppSettings
{
    public const int CurrentVersion = 6;

    public int Version { get; set; } = CurrentVersion;
    public decimal LeftThreshold { get; set; } = 24;
    public decimal RightThreshold { get; set; } = 24;
    public decimal Level88Threshold { get; set; } = 28;
    public string RecognitionHotKey { get; set; } = "F2";
    public bool ContinuousRecognition { get; set; }
    public decimal RecognitionIntervalSeconds { get; set; } = 0.1M;
    public string AdbAddress { get; set; } = "127.0.0.1:16384";
    public int AutoEnhanceMaxEquipment { get; set; } = 50;
    public string AutoEnhanceDisposalMethod { get; set; } = "出售";
    // 保留旧 JSON 字段名，兼容已经保存的 settings.json。
    [JsonPropertyName("MinimumHeroMatchScore")]
    public decimal MinimumDemandMatchScore { get; set; } = 70;
    public bool AutoEnhanceStopOnValuableEquipment { get; set; } = true;
    public bool HeroicOnlyGambleSpeed { get; set; }

    public static AppSettings CreateDefault() => new();

    internal void Normalize()
    {
        Version = CurrentVersion;
        LeftThreshold = Math.Clamp(LeftThreshold, 0, 200);
        RightThreshold = Math.Clamp(RightThreshold, 0, 200);
        Level88Threshold = Math.Clamp(Level88Threshold, 0, 200);
        RecognitionIntervalSeconds = Math.Clamp(RecognitionIntervalSeconds, 0.1M, 60M);
        AutoEnhanceMaxEquipment = Math.Clamp(AutoEnhanceMaxEquipment, 1, 999);
        MinimumDemandMatchScore = Math.Clamp(MinimumDemandMatchScore, 0, 100);
        if (AutoEnhanceDisposalMethod is not ("出售" or "分解"))
            AutoEnhanceDisposalMethod = "出售";
        if (!Enum.TryParse<Keys>(RecognitionHotKey, out var key) || key is < Keys.F1 or > Keys.F12)
            RecognitionHotKey = "F2";
        if (string.IsNullOrWhiteSpace(AdbAddress))
            AdbAddress = "127.0.0.1:16384";
    }
}

internal static class AppSettingsStore
{
    public static AppSettings Load()
    {
        if (!File.Exists(AppPaths.SettingsPath))
            return AppSettings.CreateDefault();

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(
                File.ReadAllText(AppPaths.SettingsPath), AppPaths.JsonOptions) ?? AppSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            AppPaths.PreserveBrokenFile(AppPaths.SettingsPath);
            return AppSettings.CreateDefault();
        }
    }

    public static void Save(AppSettings settings)
    {
        settings.Normalize();
        AppPaths.WriteJsonAtomic(AppPaths.SettingsPath, settings);
    }
}
