namespace TiezhuToolbox.Modules.Account;

public sealed class HeroStatCalculator(StaticGameData gameData)
{
    public HeroPanelResult Calculate(
        AccountHero hero,
        HeroPreference? preference,
        IEnumerable<AccountGear> equipment)
    {
        if (!gameData.TryGetHero(hero.Code, out var catalog))
            return new HeroPanelResult(default, default, 0, 0, [], $"静态目录未覆盖英雄代码 {hero.Code}");

        var warning = hero.UsedFallbackProgression ? "扫描缺少养成字段，按六星满觉计算" : null;
        HeroStats baseStats;
        if (hero.Stars == 5 && hero.Level <= 50 && hero.Awaken >= 5)
            baseStats = catalog.Level50FiveStar;
        else
        {
            baseStats = catalog.Level60SixStar;
            if (!hero.UsedFallbackProgression && (hero.Level != 60 || hero.Stars != 6 || hero.Awaken != 6))
                warning = "当前静态目录仅精确覆盖五星/六星满觉，已按六星满觉计算";
        }

        if (preference?.MaxSpecialtyTree == true && catalog.SpecialtyTreeDataAvailable)
            baseStats += catalog.SpecialtyTreeBonus;

        var flat = default(HeroStats);
        var attackPercent = 0D;
        var healthPercent = 0D;
        var defensePercent = 0D;
        var speedPercent = 0D;
        var gear = equipment.ToArray();
        foreach (var stat in gear.SelectMany(item => item.Substats.Prepend(item.Main)))
            AddStat(stat.Type, stat.Value, ref flat, ref attackPercent, ref healthPercent, ref defensePercent);

        var activeSets = new List<string>();
        foreach (var group in gear.GroupBy(item => item.Set, StringComparer.OrdinalIgnoreCase))
        {
            var required = EquipmentSetCatalog.RequiredPieces(group.Key);
            var activations = group.Count() / required;
            if (activations <= 0)
                continue;
            for (var index = 0; index < activations; index++)
                activeSets.Add(group.Key);
            if (!EquipmentSetCatalog.All.TryGetValue(group.Key, out var definition))
                continue;
            flat += Scale(definition.Bonus, activations);
            attackPercent += definition.AttackPercent * activations;
            healthPercent += definition.HealthPercent * activations;
            defensePercent += definition.DefensePercent * activations;
            speedPercent += definition.SpeedPercent * activations;
        }

        ApplyImprint(catalog, preference?.ImprintGrade, ref flat, ref attackPercent, ref healthPercent, ref defensePercent);
        ApplyExclusiveEquipment(catalog, preference?.ExclusiveEquipmentIndex ?? -1, ref flat,
            ref attackPercent, ref healthPercent, ref defensePercent);

        var artifactCode = preference?.ArtifactCode ?? hero.ArtifactCode;
        if (!string.IsNullOrWhiteSpace(artifactCode))
        {
            if (gameData.TryGetArtifact(artifactCode, out var artifact))
                flat += artifact.GetStats(preference?.ArtifactLevel ?? hero.ArtifactLevel ?? 30);
            else
                warning = AppendWarning(warning, $"静态目录未覆盖神器代码 {artifactCode}");
        }

        var raw = new HeroStats(
            Math.Floor(baseStats.Attack * (1 + attackPercent / 100) + flat.Attack),
            Math.Floor(baseStats.Health * (1 + healthPercent / 100) + flat.Health),
            Math.Floor(baseStats.Defense * (1 + defensePercent / 100) + flat.Defense),
            Math.Floor(baseStats.Speed * (1 + speedPercent / 100) + flat.Speed),
            baseStats.CriticalChance + flat.CriticalChance,
            baseStats.CriticalDamage + flat.CriticalDamage,
            baseStats.Effectiveness + flat.Effectiveness,
            baseStats.Resistance + flat.Resistance);
        return new HeroPanelResult(
            baseStats,
            raw,
            Math.Max(0, raw.CriticalChance - 100),
            Math.Max(0, raw.CriticalDamage - 350),
            activeSets,
            warning);
    }

    public static double CalculateWeightedScore(
        HeroStats baseStats,
        HeroStats finalStats,
        HeroStats weights)
    {
        var sum = weights.Attack + weights.Health + weights.Defense + weights.Speed + weights.CriticalChance
                  + weights.CriticalDamage + weights.Effectiveness + weights.Resistance;
        if (sum <= 0)
            return 0;
        var attack = baseStats.Attack <= 0 ? 0 : (finalStats.Attack - baseStats.Attack) / baseStats.Attack * 100;
        var health = baseStats.Health <= 0 ? 0 : (finalStats.Health - baseStats.Health) / baseStats.Health * 100;
        var defense = baseStats.Defense <= 0 ? 0 : (finalStats.Defense - baseStats.Defense) / baseStats.Defense * 100;
        var speed = (finalStats.Speed - baseStats.Speed) * 2;
        var critical = (Math.Min(100, finalStats.CriticalChance) - baseStats.CriticalChance) * 1.5;
        var criticalDamage = (Math.Min(350, finalStats.CriticalDamage) - baseStats.CriticalDamage) * 1.125;
        var effectiveness = finalStats.Effectiveness - baseStats.Effectiveness;
        var resistance = finalStats.Resistance - baseStats.Resistance;
        return (attack * weights.Attack + health * weights.Health + defense * weights.Defense
                + speed * weights.Speed + critical * weights.CriticalChance
                + criticalDamage * weights.CriticalDamage + effectiveness * weights.Effectiveness
                + resistance * weights.Resistance) / sum;
    }

    private static HeroStats Scale(HeroStats value, int multiplier) => new(
        value.Attack * multiplier, value.Health * multiplier, value.Defense * multiplier,
        value.Speed * multiplier, value.CriticalChance * multiplier, value.CriticalDamage * multiplier,
        value.Effectiveness * multiplier, value.Resistance * multiplier);

    private static string AppendWarning(string? current, string value) =>
        string.IsNullOrWhiteSpace(current) ? value : current + "；" + value;

    private static void AddStat(GearStatType type, double value, ref HeroStats flat,
        ref double attackPercent, ref double healthPercent, ref double defensePercent)
    {
        switch (type)
        {
            case GearStatType.Attack: flat += new HeroStats(value, 0, 0, 0, 0, 0, 0, 0); break;
            case GearStatType.AttackPercent: attackPercent += value; break;
            case GearStatType.Health: flat += new HeroStats(0, value, 0, 0, 0, 0, 0, 0); break;
            case GearStatType.HealthPercent: healthPercent += value; break;
            case GearStatType.Defense: flat += new HeroStats(0, 0, value, 0, 0, 0, 0, 0); break;
            case GearStatType.DefensePercent: defensePercent += value; break;
            case GearStatType.Speed: flat += new HeroStats(0, 0, 0, value, 0, 0, 0, 0); break;
            case GearStatType.CriticalHitChancePercent: flat += new HeroStats(0, 0, 0, 0, value, 0, 0, 0); break;
            case GearStatType.CriticalHitDamagePercent: flat += new HeroStats(0, 0, 0, 0, 0, value, 0, 0); break;
            case GearStatType.EffectivenessPercent: flat += new HeroStats(0, 0, 0, 0, 0, 0, value, 0); break;
            case GearStatType.EffectResistancePercent: flat += new HeroStats(0, 0, 0, 0, 0, 0, 0, value); break;
        }
    }

    private static void ApplyImprint(HeroCatalogEntry catalog, string? grade, ref HeroStats flat,
        ref double attackPercent, ref double healthPercent, ref double defensePercent)
    {
        if (grade == null || catalog.ImprintType == null || !catalog.ImprintGrades.TryGetValue(grade, out var value))
            return;
        ApplyExternalStat(catalog.ImprintType, value, ref flat, ref attackPercent, ref healthPercent, ref defensePercent);
    }

    private static void ApplyExclusiveEquipment(HeroCatalogEntry catalog, int index, ref HeroStats flat,
        ref double attackPercent, ref double healthPercent, ref double defensePercent)
    {
        if (index < 0 || index >= catalog.ExclusiveEquipment.Count)
            return;
        var value = catalog.ExclusiveEquipment[index];
        ApplyExternalStat(value.Type, value.Value, ref flat, ref attackPercent, ref healthPercent, ref defensePercent);
    }

    private static void ApplyExternalStat(string type, double value, ref HeroStats flat,
        ref double attackPercent, ref double healthPercent, ref double defensePercent)
    {
        var normalized = value is > -1 and < 1 && type is not "speed" and not "att" and not "max_hp" and not "def"
            ? value * 100
            : value;
        switch (type)
        {
            case "att": flat += new HeroStats(normalized, 0, 0, 0, 0, 0, 0, 0); break;
            case "att_rate": attackPercent += normalized; break;
            case "max_hp": flat += new HeroStats(0, normalized, 0, 0, 0, 0, 0, 0); break;
            case "max_hp_rate": healthPercent += normalized; break;
            case "def": flat += new HeroStats(0, 0, normalized, 0, 0, 0, 0, 0); break;
            case "def_rate": defensePercent += normalized; break;
            case "speed": flat += new HeroStats(0, 0, 0, normalized, 0, 0, 0, 0); break;
            case "cri": flat += new HeroStats(0, 0, 0, 0, normalized, 0, 0, 0); break;
            case "cri_dmg": flat += new HeroStats(0, 0, 0, 0, 0, normalized, 0, 0); break;
            case "acc": flat += new HeroStats(0, 0, 0, 0, 0, 0, normalized, 0); break;
            case "res": flat += new HeroStats(0, 0, 0, 0, 0, 0, 0, normalized); break;
        }
    }
}
