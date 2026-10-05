using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Central character roster (issue #29): single source of truth for both CharacterSelection.cs
// (issue #1's dynamic refactor) and Player.cs, replacing CharacterSelection's previous hardcoded
// stub list that didn't correspond to any real CharacterData resource.
public static class CharacterRoster
{
	// Test Wizard stays first for development flows (it is the only entry that can pick its own
	// starting loadout); the four after it are the playable roster, one per starting element.
	//
	// CharacterData.tres - the Arcane "Apprentice Wizard" - is deliberately no longer listed. It
	// started with Magic Missile, which carries Arcane and Lightning weight, so it was the one
	// character whose opening spell pulled toward two elements at once. The file is still on disk
	// if it is ever wanted back.
	private static readonly string[] ResourcePaths = new[]
	{
		"res://CharacterData_TestWizard.tres",
		"res://CharacterData_Pyromancer.tres",
		"res://CharacterData_Frostweaver.tres",
		"res://CharacterData_Stormcaller.tres",
		"res://CharacterData_Geomancer.tres",
		// Rescued in side events (.ai/side-events.md). Listed here like everyone else; whether the
		// player can pick them is UnlockCatalog's question, not the roster's.
		"res://CharacterData_Tituba.tres",
		"res://CharacterData_Vainamoinen.tres",
	};

	public static List<CharacterData> GetAll()
	{
		var result = new List<CharacterData>();
		foreach (var path in ResourcePaths)
		{
			// Plain load (default CacheMode.Reuse) - matches every other resource load in this
			// codebase (SpellData etc. in Player.cs). CacheMode.Replace was tried here previously
			// as a speculative fix for a suspected stale-cache bug, but it turned out to be the
			// actual cause of a real bug: on the FIRST load of a resource in a fresh process,
			// Replace mode returns a blank/default-constructed instance instead of the
			// deserialized .tres data (confirmed via runtime diagnostic logging - every exported
			// property came back as the C# class's bare default, e.g. Id="", Name="",
			// StartingSpellResource=null). Reuse mode works correctly and is what the rest of the
			// project already relies on.
			var data = ResourceLoader.Load<CharacterData>(path);
			if (data != null)
				result.Add(data);
		}
		return result;
	}

	public static CharacterData GetByIndex(int index)
	{
		var all = GetAll();
		if (all.Count == 0)
			return null;
		if (index < 0 || index >= all.Count)
			return all[0];
		return all[index];
	}
}
