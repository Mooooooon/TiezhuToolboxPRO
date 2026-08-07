namespace TiezhuToolbox.Modules.Account;

public sealed class AccountWorkspace
{
    private readonly AccountRepository _repository = new();

    public AccountWorkspace()
    {
        GameData = new StaticGameData();
        Calculator = new HeroStatCalculator(GameData);
        Snapshot = _repository.LoadSnapshot();
        Preferences = _repository.LoadPreferences();
        if (Snapshot != null)
            Preferences = _repository.MergePreferences(Snapshot, Preferences);
    }

    public AccountSnapshotV1? Snapshot { get; private set; }
    public HeroPreferenceDocument Preferences { get; private set; }
    public StaticGameData GameData { get; }
    public HeroStatCalculator Calculator { get; }
    public event EventHandler? Changed;

    public AccountSnapshotV1 Import(string text, string source, bool allowMissingOwners = false)
    {
        var snapshot = AccountImportService.Parse(text, source, allowMissingOwners);
        var preferences = _repository.MergePreferences(snapshot, Preferences);
        // 先完整构造并校验，最后才替换磁盘和内存状态。
        _repository.SaveSnapshot(snapshot);
        _repository.SavePreferences(preferences);
        Snapshot = snapshot;
        Preferences = preferences;
        Changed?.Invoke(this, EventArgs.Empty);
        return snapshot;
    }

    public HeroPreference? GetPreference(string heroId) =>
        Preferences.Active.FirstOrDefault(value => value.HeroId == heroId);

    public HeroPanelResult GetPanel(AccountHero hero, IEnumerable<AccountGear>? gear = null) =>
        Calculator.Calculate(hero, GetPreference(hero.Id), gear ?? GetEquipped(hero.Id));

    public IReadOnlyList<AccountGear> GetEquipped(string heroId) => Snapshot?.Items
        .Where(value => value.EquippedHeroId == heroId).ToArray() ?? [];

    public void MovePriority(string heroId, int priority)
    {
        AccountRepository.MovePriority(Preferences.Active, heroId, priority);
        SavePreferences();
    }

    public void UpdatePreference(HeroPreference preference)
    {
        var index = Preferences.Active.FindIndex(value => value.HeroId == preference.HeroId);
        if (index < 0)
            return;
        Preferences.Active[index] = preference;
        SavePreferences();
    }

    private void SavePreferences()
    {
        _repository.SavePreferences(Preferences);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
