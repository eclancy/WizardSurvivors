using Godot;

public partial class ChestReward : PickupBase
{
	[Export] public string ItemId { get; set; } = ChestItemCatalog.RelicKey;
	[Export] public Texture2D ChestTexture { get; set; }

	public override void _Ready()
	{
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
			var sprite = new Sprite2D { Texture = texture, Position = Vector2.Zero, Scale = new Vector2(1.15f, 1.15f) };
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
				Shape = new CircleShape2D { Radius = 14f }
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
			}
		}
		QueueFree();
	}
}
