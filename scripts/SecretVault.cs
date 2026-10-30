using Godot;
using WizardSurvivors.scripts;

// THE SPELL VAULT (.ai/achievements-and-secrets.md 6.4): a secret class between the anchor and the
// keepsake, built so a spell is never a coin flip. What every vault shares, and so what a chapter's vault
// subclass does NOT have to write:
//
//   - In every run of its chapter, placed once at stage start from the seed, five to seven chunks out
//     (35 to 50 s of walking), at a bearing at least 90 degrees from the side event's and the keepsake's -
//     SecretPlacement.Vault, which the director reads. Its footprint is large (SecretCatalog.VaultFootprint),
//     so the ground round it is a place to fight.
//   - A far tell from two chunks out (FarTell: the decor gradient), and a large object readable from 400 px
//     on a phone in a crowd - the subclass's art.
//   - The ask may run to 20 seconds, twice an ordinary secret's, because it pays a spell; no hold past
//     five seconds still. The subclass's rule.
//   - It stands down with the boss, like every secret: Active goes false, and a vault reads it.
//   - Opening it (Open): the first time ever, its deed - SecretDirector.Found, which grants the spell on the
//     spot through AchievementManager.CompleteNow, so it joins the level-up pool from the next level-up.
//     Found once, it still works: in a later run the same beat pays a chest from the secret budget instead,
//     so it stays worth the walk (the side events' exhausted rule).
//
// Never put into the run: no spell in the loadout, no level, no offer (6.2) - only onto the save.
public abstract partial class SecretVault : SecretSite
{
	/// <summary>True once this vault has been opened in this run.</summary>
	public bool IsOpened => State?.Done == true;

	/// <summary>True if this save had already found it before the run - a later run, which pays a chest.</summary>
	protected bool FoundBefore { get; private set; }

	public override void _EnterTree()
	{
		base._EnterTree();
		FoundBefore = GetNodeOrNull<SaveManager>("/root/SaveManager")?.Data.HasFoundSecret(SecretId) == true;
	}

	/// <summary>
	/// The vault is open: marks it done for the run, and pays - its deed and spell the first time ever, a
	/// chest from the budget near <paramref name="rewardAt"/> after that. Call once, when the subclass's own
	/// beat (the shatter, the hart reaching its stand) has been seen.
	/// </summary>
	protected void Open(Vector2 rewardAt)
	{
		SecretDirector.SiteState state = State;
		if (state == null || Director == null)
			return;
		state.Done = true;
		bool later = FoundBefore || Director.FoundThisRun(SecretId);
		Director.Found(SecretId);
		if (later)
			Director.PayChest(rewardAt);
	}
}
