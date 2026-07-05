public sealed class LevelUpOption
{
	public string SpellId { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public int NextLevel { get; set; } = 1;
	public bool IsNewUnlock { get; set; } = true;

	public string GetButtonText()
	{
		string prefix = IsNewUnlock ? "New" : "Upgrade";
		return $"{DisplayName}  [{prefix} Lv {NextLevel}]";
	}
}
