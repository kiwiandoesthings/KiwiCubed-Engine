namespace BaseMod;

using System.Drawing;
using System.Numerics;
using KiwiCubed.Api;
using Silk.NET.Input;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.IInventory;
using static KiwiCubed.Api.Util;

public class KiwiCubedMod : IMod {
	private AssetStringID mainMenuID = new AssetStringID("kiwicubed", "main");
	private AssetStringID settingsMenuID = new AssetStringID("kiwicubed", "settings");
	private AssetStringID pauseMenuID = new AssetStringID("kiwicubed", "pause");
	private AssetStringID inventoryScreenID = new AssetStringID("kiwicubed", "inventory");

	public override bool Initialize() {
		OVERRIDE_LOG_NAME("KiwiCubed initialization");

		INFO("Initializing KiwiCubed base mod...");

		IAssetManager assetManager = Systems.Get<IAssetManager>();
		BlockStone stone = new BlockStone();
		BlockDirt dirt = new BlockDirt();
		BlockGrass grass = new BlockGrass();
		BlockSand sand = new BlockSand();
		assetManager.RegisterBlock(stone);
		assetManager.RegisterBlock(dirt);
		assetManager.RegisterBlock(grass);
		assetManager.RegisterBlock(sand);

		AssetStringID itemStringID = new AssetStringID("kiwicubed", "dropped_item");
		assetManager.RegisterEntityType(itemStringID, typeof(EntityItem));

		AssetStringID plainsStringID = new AssetStringID("kiwicubed", "plains");
		AssetStringID desertStringID = new AssetStringID("kiwicubed", "desert");
		BiomeModel plainsBiome = new BiomeModel(0.4f, 0.2f, 0.5f, grass, dirt, stone);
		BiomeModel desertBiome = new BiomeModel(0.1f, 1.0f, -0.4f, sand, sand, stone);
		assetManager.RegisterBiomeModel(plainsStringID, plainsBiome);
		assetManager.RegisterBiomeModel(desertStringID, desertBiome);

		IUI ui = Systems.Get<IUI>();
		IVirtualWindow globalWindow = ui.GetGlobalWindow();

		ui.AddScreen(mainMenuID);

		List<TextureAtlasData> buttonAtlasDatas = new();
		buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_unselected")));
		buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_selected")));
		buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_activated")));
		MetaTexture buttonTexture = new MetaTexture(buttonAtlasDatas.ToArray(), new byte[] { 0, 0, 0, 0, 0, 0 });

		int windowCenterX = (int)globalWindow.GetWidth() / 2;
		int buttonWidth = 64 * 8;
		int buttonCenterX = windowCenterX - (buttonWidth / 2);
		Vector2 buttonSize = new Vector2(512, 128);

		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 200), buttonSize, () => {
			SingleplayerHandler.CreateWorld(5, 4);
		}, buttonTexture, "Create World"));
		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 400), buttonSize, () => { }, buttonTexture, "Settings"));
		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 600), buttonSize, () => {
			//something something MetaGameHandler???
		}, buttonTexture, "Exit Game"));

		ui.SetCurrentScreen(mainMenuID);

		ui.AddScreen(settingsMenuID);

		ui.AddScreen(pauseMenuID);
		ui.AddElementToScreen(pauseMenuID, new UIButton(new Vector2(buttonCenterX, 400), buttonSize, () => {
			TogglePause();
		}, buttonTexture, "Resume Game"));
		ui.AddElementToScreen(pauseMenuID, new UIButton(new Vector2(buttonCenterX, 600), buttonSize, () => {
			ui.SetCurrentScreen(settingsMenuID);
		}, buttonTexture, "Settings"));

		List<TextureAtlasData> inventoryAtlasDatas = new();
		inventoryAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/inventory_player")));
		inventoryAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/inventory_27")));
		inventoryAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/hotbar")));
		MetaTexture inventoryTextures = new MetaTexture(inventoryAtlasDatas.ToArray(), new byte[] { 0, 0, 0, 0, 0, 0 });

		int playerTopCenterX = windowCenterX - (76 * 8 / 2);
		int inventoryCenterX = windowCenterX - (96 * 8 / 2);

		int inventoryY = 500;
		int playerY = 500 - (32 * 8);
		int hotbarY = 500 + (32 * 8);

		ui.AddScreen(inventoryScreenID);
		ui.AddElementToScreen(inventoryScreenID, new UIImage(new Vector2(playerTopCenterX, playerY), new Vector2(608, 256), inventoryTextures, 0));
		ui.AddElementToScreen(inventoryScreenID, new UIImage(new Vector2(inventoryCenterX, inventoryY), new Vector2(768, 256), inventoryTextures, 1));
		ui.AddElementToScreen(inventoryScreenID, new UIImage(new Vector2(inventoryCenterX, hotbarY), new Vector2(768, 96), inventoryTextures, 2));
		ui.AddCustomDrawCommandToScreen(inventoryScreenID, (IUIScreen uiScreen) => {
			IInventory playerInventory = SingleplayerHandler.GetPlayer().GetEntityData().inventory;
			List<ValueTuple<AssetStringID, InventorySlot>> inventorySlots = playerInventory.GetAllSlots();
			for (int slotIndex = 0; slotIndex < 27; slotIndex++) {
				int slotX = slotIndex % 9;
				int slotY = slotIndex / 9;
				ValueTuple<AssetStringID, InventorySlot> slotPair = inventorySlots[slotIndex];
				InventorySlot slot = slotPair.Item2;
				if (!slot.HasItem()) {
					continue;
				}
				IItem item = assetManager.GetItem(slot.itemStringID);
				MetaTexture itemTexture = item.GetTexture();
				int slotXOffset = slotX * ((8 + 2) * 8);
				int slotYOffset = slotY * ((8 + 2) * 8);
				int slotInventoryX = inventoryCenterX + (4 * 8);
				int slotInventoryY = inventoryY + (2 * 8);
				int finalX = slotInventoryX + slotXOffset;
				int finalY = slotInventoryY + slotYOffset;
				UIImage.Render(ui, new Vector2(finalX, finalY), new Vector2(64, 64), itemTexture, 0);
				Vector2 textSize = Renderer.MeasureText(slot.itemCount.ToString());
				Renderer.DrawText(slot.itemCount.ToString(), new Vector2(finalX + (8 * 8) - textSize.X , finalY + (8 * 8)), new Vector2(1.0f), Color.Black);
			}
		});

		// later stop using in favor of controlhandler or something like that
		IInputHandler inputHandler = ui.GetInputHandler();
		inputHandler.RegisterKeyCallback(Key.Escape, (Key key) => {
			TogglePause();
		}, true);
		inputHandler.RegisterKeyCallback(Key.E, (Key key) => {
			ToggleInventory();
		}, true);

		INFO("Initialized KiwiCubed base mod");

		return true;
	}

	private void TogglePause() {
		if (!SingleplayerHandler.IsLoadedIntoSingleplayerWorld()) {
			return;
		}
		IUI ui = Systems.Get<IUI>();
		if (!ui.IsDisabled()) {
			ui.MoveScreenBack();
		} else {
			ui.SetCurrentScreen(pauseMenuID);
		}
	}

	private void ToggleInventory() {
		IUI ui = Systems.Get<IUI>();
		if (ui.IsDisabled()) {
			ui.SetCurrentScreen(inventoryScreenID);
		} else if (ui.GetCurrentScreenName() == inventoryScreenID) {
			ui.MoveScreenBack();
		}
	}

	public override void Unload() {
	}
}

public class BlockStone : Block {
	public BlockStone() {
		stringID = new AssetStringID("kiwicubed", "stone");
		AssetStringID textureStringID1 = new AssetStringID("kiwicubed", "texture/stone_1");
		AssetStringID textureStringID2 = new AssetStringID("kiwicubed", "texture/stone_2");
		AssetStringID textureStringID3 = new AssetStringID("kiwicubed", "texture/stone_3");
		AssetStringID textureStringID4 = new AssetStringID("kiwicubed", "texture/stone_4");
		TextureAtlasData[] faces = {
			assetManager.GetTextureAtlasData(textureStringID1)!,
			assetManager.GetTextureAtlasData(textureStringID2)!,
			assetManager.GetTextureAtlasData(textureStringID3)!,
			assetManager.GetTextureAtlasData(textureStringID4)!,
		};
		metaTexture = new MetaTexture(faces, new byte[] { 0, 0, 0, 0, 0, 0 });
	}

	// Stop using in favor of a better established variants system
	public override GeneralMesh GetMesh(Span<bool> neighborsMask, FullBlockPosition fullPosition, List<float> vertices, List<ushort> indices) {
		IntVector3 blockPosition = fullPosition.blockPosition;
		IntVector3 chunkPosition = fullPosition.chunkPosition;
		IntVector3 blockOffset = chunkPosition * chunkSize;
		int index = Random.Shared.Next(metaTexture.atlasDatas.Length);
		TextureAtlasData atlasData = metaTexture.atlasDatas[index];
		for (int face = 0; face < 6; ++face) {
			if (neighborsMask[face] == true) {
				ushort vertexOffset = (ushort)((int)face * 20);
				int baseIndex = vertices.Count / 5;

				for (int i = vertexOffset; i < vertexOffset + 20; i += 5) {
					vertices.Add((Block.vertices[i + 0]) + (blockPosition.X + blockOffset.X));
					vertices.Add((Block.vertices[i + 1]) + (blockPosition.Y + blockOffset.Y));
					vertices.Add((Block.vertices[i + 2]) + (blockPosition.Z + blockOffset.Z));

					float u0 = atlasData.xPosition;
					float u1 = (atlasData.xPosition + atlasData.xSize);
					float v0 = atlasData.yPosition;
					float v1 = (atlasData.yPosition + atlasData.ySize);

					switch ((i - vertexOffset) / 5 % 4) {
						case 0: {
							vertices.Add(u0);
							vertices.Add(v1);
							break;
						}
						case 1: {
							vertices.Add(u1);
							vertices.Add(v1);
							break;
						}
						case 2: {
							vertices.Add(u1);
							vertices.Add(v0);
							break;
						}
						case 3: {
							vertices.Add(u0);
							vertices.Add(v0);
							break;
						}
					}
				}

				for (int i = 0; i < 6; ++i) {
					indices.Add((ushort)(baseIndex + Block.indices[i]));
				}
			}
		}

		return new GeneralMesh(vertices, indices);
	}
}

public class BlockDirt : Block {
	public BlockDirt() {
		stringID = new AssetStringID("kiwicubed", "dirt");
		TextureAtlasData[] faces = {
			assetManager.GetTextureAtlasData(stringID.Prefix("texture"))
		};
		metaTexture = new MetaTexture(faces, new byte[] { 0, 0, 0, 0, 0, 0 });
	}
}

public class BlockGrass : Block {
	public BlockGrass() {
		stringID = new AssetStringID("kiwicubed", "grass");
		TextureAtlasData[] faces = {
			assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/grass_top")),
			assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/grass_side")),
			assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/dirt")),
		};
		metaTexture = new MetaTexture(faces, new byte[] { 1, 1, 1, 1, 0, 2 });
	}
}

public class BlockSand : Block {
	public BlockSand() {
		stringID = new AssetStringID("kiwicubed", "sand");
		TextureAtlasData[] faces = {
			assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/sand"))
		};
		metaTexture = new MetaTexture(faces, new byte[] { 0, 0, 0, 0, 0, 0 });
	}
}

public class EntityItem : Entity {
	public EntityItem(uint AUID, Vector3 position) : base(AUID, position, Vector3.Zero) {

	}
}

public class UIButton : IUIElement {
	private Action? triggerFunction;
	private MetaTexture image;
	private string label;
	private int frame;

	public UIButton(Vector2 position, Vector2 size, Action? triggerFunction, MetaTexture image, string label) : base(position, size) {
		this.triggerFunction = triggerFunction;
		this.image = image;
		this.label = label;
		frame = 0;
	}

	public void Trigger() {
		if (triggerFunction != null) {
			triggerFunction();
		}
	}

	public override void OnClick() {
		if (!GetHovered()) {
			return;
		}

		Trigger();
	}

	public override void OnEnter() {
		Trigger();
	}

	public override void Render() {
		IUI ui = parentScreen.GetUI();
		if ((GetHovered())) {
			if (ui.GetInputHandler().GetMouseButtonState(MouseButton.Left)) {
				frame = 2;
			} else {
				frame = 1;
			}
		} else if (tabSelected) {
			frame = 1;
		} else {
			frame = 0;
		}

		TextureAtlasData atlasData = image.atlasDatas[(int)frame];

		ITexture uiAtlas = ui.GetUIAtlas();

		uiAtlas.SetActive();
		uiAtlas.Bind();

		List<float> vertices = [
		    // Positions      // Texture Coordinates
		    0.0f, 0.0f, atlasData.xPosition, atlasData.yPosition,
			1.0f, 0.0f, atlasData.xPosition + atlasData.xSize, atlasData.yPosition,
			1.0f, 1.0f, atlasData.xPosition + atlasData.xSize, atlasData.yPosition + atlasData.ySize,
			0.0f, 1.0f, atlasData.xPosition, atlasData.yPosition + atlasData.ySize
		];

		List<ushort> indices = [
			0, 1, 2,
			2, 3, 0,
		];

		ui.GetUIShader().Bind();

		IRenderBuffer renderBuffer = ui.GetRenderBuffer();

		Renderer.UpdateBuffers(renderBuffer, vertices, indices);

		Matrix4x4 modelMatrix = Matrix4x4.CreateScale(new Vector3(size.X, size.Y, 1.0f)) * Matrix4x4.CreateTranslation(new Vector3(position.X, position.Y, 0.0f));
		Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(0, ui.GetGlobalWindow().GetWidth(), ui.GetGlobalWindow().GetHeight(), 0, -1.0f, 1.0f);
		ui.GetUIShader().SetMatrix4("modelMatrix", modelMatrix);
		ui.GetUIShader().SetMatrix4("projectionMatrix", projection);

		Renderer.DrawElements(renderBuffer, indices.Count);

		if (label != "") {
			Vector2 textDimensions = Renderer.MeasureText(label) * 2;
			Renderer.DrawText(label, new Vector2((position.X + size.X / 2) - (textDimensions.X / 2), (position.Y + size.Y / 2) + 24), new Vector2(2.0f), Color.FromArgb(255, 150, 150, 150));
		}
	}
}

public class UIImage : IUIElement {
	private MetaTexture image;
	private int frameIndex;

	public UIImage(Vector2 position, Vector2 size, MetaTexture image, int frameIndex = 0) : base(position, size) {
		this.image = image;
		this.frameIndex = frameIndex;
	}

	public override void Render() {
		Render(parentScreen.GetUI(), position, size, image, frameIndex);
	}

	public static void Render(IUI ui, Vector2 position, Vector2 size, MetaTexture image, int frameIndex) {
		TextureAtlasData atlasData = image.atlasDatas[frameIndex];

		ITexture uiAtlas = ui.GetUIAtlas();

		uiAtlas.SetActive();
		uiAtlas.Bind();

		List<float> vertices = [
		    // Positions      // Texture Coordinates
		    0.0f, 0.0f, atlasData.xPosition, atlasData.yPosition,
			1.0f, 0.0f, atlasData.xPosition + atlasData.xSize, atlasData.yPosition,
			1.0f, 1.0f, atlasData.xPosition + atlasData.xSize, atlasData.yPosition + atlasData.ySize,
			0.0f, 1.0f, atlasData.xPosition, atlasData.yPosition + atlasData.ySize
		];

		List<ushort> indices = [
			0, 1, 2,
			2, 3, 0,
		];

		ui.GetUIShader().Bind();

		IRenderBuffer renderBuffer = ui.GetRenderBuffer();

		Renderer.UpdateBuffers(renderBuffer, vertices, indices);

		Matrix4x4 modelMatrix = Matrix4x4.CreateScale(new Vector3(size.X, size.Y, 1.0f)) * Matrix4x4.CreateTranslation(new Vector3(position.X, position.Y, 0.0f));
		Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(0, ui.GetGlobalWindow().GetWidth(), ui.GetGlobalWindow().GetHeight(), 0, -1.0f, 1.0f);
		ui.GetUIShader().SetMatrix4("modelMatrix", modelMatrix);
		ui.GetUIShader().SetMatrix4("projectionMatrix", projection);

		Renderer.DrawElements(renderBuffer, indices.Count);
	}
}