using Godot;

public partial class ChestReward : PickupBase
{
	[Export] public string ItemId { get; set; } = ChestItemCatalog.RelicKey;
	[Export] public Texture2D ChestTexture { get; set; }

	// A chest is a deliberate reward, not a consumable to hoover up: it holds its spawn position
	// and waits for the player to walk into it, rather than flying to them like XP and health.
	public override void _Ready()
	{
		MagnetAttracted = false;
		base._Ready();
		SetupVisual();
		SetupCollision();
		if (string.IsNullOrWhiteSpace(ItemId))
			ItemId = ChestItemCatalog.GetRandomItem(new RandomNumberGenerator());
	}

	private void SetupVisual()
	{
		var root = GetNodeOrNull<Node2D>("PlaceholderShape");
		if (root == null)
		{
			root = new Node2D { Name = "PlaceholderShape" };
			AddChild(root);
		}

		if (root.GetChildCount() == 0)
		{
			Texture2D texture = ChestTexture ?? ResourceLoader.Load<Texture2D>("res://assets/organized/effects/fx-2d-pixel-dungeon-asset-pack-items-and-trap-animation-chest-1.png");
			// 16x16 source art. 3.0 -> ~48px on screen, clearly readable next to the 72px player
			// sprite without hiding it. Collision below is sized to match.
			var sprite = new Sprite2D { Texture = texture, Position = Vector2.Zero, Scale = new Vector2(3.0f, 3.0f) };
			root.AddChild(sprite);
		}
	}

	private void SetupCollision()
	{
		var collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		if (collision == null)
		{
			collision = new CollisionShape2D
			{
				Name = "CollisionShape2D",
				Shape = new CircleShape2D { Radius = 22f }
			};
			AddChild(collision);
		}
		Monitoring = true;
		Monitorable = true;
		CollisionMask = 1;
	}

	protected override void OnPickedUp(Node playerNode)
	{
		if (playerNode is Player player)
		{
			var game = GetTree().CurrentScene as Node2DGame;
			if (game != null)
			{
				game.OpenChestSelectionMenu();
			}
			else
			{
				if (!string.IsNullOrWhiteSpace(ItemId))
					player.AddChestItem(ItemId);
				player.Heal(4);
				// Mirrors Node2DGame.OnChestItemSelected: this is the no-menu fallback path, and
				// Vaultguard should pay out on either.
				player.OnChestOpened();
			}
		}
		QueueFree();
	}
}
