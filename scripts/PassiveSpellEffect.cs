using Godot;
using System.Collections.Generic;

namespace WizardSurvivors.scripts;

// Shared base for defensive/passive spells (issue #22). Unlike the offensive spells
// (MagicMissile/ArcaneExplosion/SpiritualWeapon), which fire at the nearest enemy from
// Player._PhysicsProcess, passive spells either:
//   1. React to the player taking damage (e.g. Frozen Bulwark, Stormguard Aura) - override
//      OnPlayerDamaged().
//   2. Pulse on a timer independent of enemy targeting (e.g. Aegis Ward, Guardian Vines,
//      Tidal Barrier, Thornmail Barrier, Venom Cloak) - override OnPulseTick() and set
//      UsesPulseTimer = true.
// Instances are expected to live as a child node of Player, following the same persistent-child
// pattern used by ArcaneExplosion/SpiritualWeapon (see Player.RefreshPersistentSpellInstance).
public partial class PassiveSpellEffect : Node2D
{
	[Export] public SpellData SpellData { get; set; }
	[Export] public int CurrentLevel { get; set; } = 1;

	// When true, OnPulseTick() is called every SpellData.GetCooldownAtLevel(CurrentLevel) seconds.
	[Export] public bool UsesPulseTimer { get; set; } = false;

	protected Player OwnerPlayer;
	private float pulseTimer = 0f;

	public override void _Ready()
	{
		OwnerPlayer = GetParent() as Player ?? GetOwner() as Player;
		if (OwnerPlayer != null)
		{
			OwnerPlayer.Connect(Player.SignalName.DamageTaken, new Callable(this, nameof(HandlePlayerDamaged)));
		}
	}

	public override void _ExitTree()
	{
		if (OwnerPlayer != null && IsInstanceValid(OwnerPlayer))
		{
			OwnerPlayer.Disconnect(Player.SignalName.DamageTaken, new Callable(this, nameof(HandlePlayerDamaged)));
		}
	}

	public override void _Process(double delta)
	{
		if (!UsesPulseTimer || SpellData == null)
			return;

		float interval = Mathf.Max(0.05f, SpellData.GetCooldownAtLevel(CurrentLevel));
		pulseTimer += (float)delta;
		if (pulseTimer >= interval)
		{
			pulseTimer = 0f;
			OnPulseTick();
		}
	}

	// Called by Player's DamageTaken signal. Kept private + a thin wrapper so subclasses only need
	// to override the parameterless-friendly OnPlayerDamaged(int).
	private void HandlePlayerDamaged(int amount)
	{
		OnPlayerDamaged(amount);
	}

	public virtual void SetSpellLevel(int level)
	{
		CurrentLevel = level;
	}

	// Override in reactive spells (Frozen Bulwark, Stormguard Aura, ...).
	protected virtual void OnPlayerDamaged(int amount) { }

	// Override in timer-driven aura spells (Aegis Ward, Guardian Vines, Tidal Barrier,
	// Thornmail Barrier, Venom Cloak, ...). Only called when UsesPulseTimer is true.
	protected virtual void OnPulseTick() { }

	// Override to contribute flat incoming-damage reduction (e.g. Stone Bulwark). Summed across
	// all equipped passive spells in Player.TakeDamage().
	public virtual int GetFlatDamageReduction() => 0;

	// Shared helper: returns enemies within `radius` of the owning player, for area-effect passives.
	protected List<Node2D> GetNearbyEnemies(float radius)
	{
		var result = new List<Node2D>();
		if (OwnerPlayer == null)
			return result;

		foreach (var node in OwnerPlayer.GetTree().GetNodesInGroup("enemies"))
		{
			if (node is Node2D enemy && IsInstanceValid(enemy))
			{
				if (OwnerPlayer.GlobalPosition.DistanceTo(enemy.GlobalPosition) <= radius)
					result.Add(enemy);
			}
		}
		return result;
	}
}
