namespace BaseMod;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using Arch.Core;
using KiwiCubed.Api;
using Silk.NET.Input;
using System.Drawing;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.IInventory;

public class KiwiCubedMod : IMod {
	private AssetStringID mainMenuID = new AssetStringID("kiwicubed", "main");
	private AssetStringID settingsMenuID = new AssetStringID("kiwicubed", "settings");
	private AssetStringID pauseMenuID = new AssetStringID("kiwicubed", "pause");
	private AssetStringID inventoryScreenID = new AssetStringID("kiwicubed", "inventory");

	public override bool InitializeServer() {
		OVERRIDE_LOG_NAME("KiwiCubed initialization");

		INFO("Initializing KiwiCubed base mod...");

		IAssetManager assetManager = Systems.Get<IAssetManager>();
		//BlockStone stone = new BlockStone();
		//BlockDirt dirt = new BlockDirt();
		//BlockGrass grass = new BlockGrass();
		//BlockSand sand = new BlockSand();
		//ushort stoneID = assetManager.RegisterBlock(stone);
		//ushort dirtID = assetManager.RegisterBlock(dirt);
		//ushort grassID = assetManager.RegisterBlock(grass);
		//ushort sandID = assetManager.RegisterBlock(sand);

		EntityType itemType = new EntityType(DroppedItemEntity.itemStringID, new ComponentType[] { typeof(EntityRenderableComponent), typeof(EntityPhysicalComponent), typeof(DroppedItemEntity.EntityDroppedItemComponent) }, DroppedItemEntity.ItemEntitySetup);
		assetManager.RegisterEntityType(DroppedItemEntity.itemStringID, itemType);

		ComponentType[] baseBlockComponents = {
			typeof(BlockRenderableComponent),
			typeof(BlockSolidComponent)
		};

		ArchWorld archWorld = assetManager.GetArchWorld();
		ArchEntity stoneDefinition = assetManager.CreateBlockDefinition(baseBlockComponents);
		AssetStringID stoneTextureStringID1 = new AssetStringID("kiwicubed", "texture/stone_1");
		AssetStringID stoneTextureStringID2 = new AssetStringID("kiwicubed", "texture/stone_2");
		AssetStringID stoneTextureStringID3 = new AssetStringID("kiwicubed", "texture/stone_3");
		AssetStringID stoneTextureStringID4 = new AssetStringID("kiwicubed", "texture/stone_4");
		TextureAtlasData[] stoneFaces = {
			assetManager.GetTextureAtlasData(stoneTextureStringID1)!,
			assetManager.GetTextureAtlasData(stoneTextureStringID2)!,
			assetManager.GetTextureAtlasData(stoneTextureStringID3)!,
			assetManager.GetTextureAtlasData(stoneTextureStringID4)!,
		};
		MetaTexture stoneMetaTexture = new MetaTexture(stoneFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 4, 1);
		archWorld.Set<BlockRenderableComponent>(stoneDefinition, new BlockRenderableComponent(stoneMetaTexture));

		ArchEntity dirtDefinition = assetManager.CreateBlockDefinition(baseBlockComponents);
		AssetStringID dirtTextureStringID = new AssetStringID("kiwicubed", "texture/dirt");
		TextureAtlasData[] dirtFaces = {
			assetManager.GetTextureAtlasData(dirtTextureStringID)!,
		};
		MetaTexture dirtMetaTexture = new MetaTexture(dirtFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
		archWorld.Set<BlockRenderableComponent>(dirtDefinition, new BlockRenderableComponent(dirtMetaTexture));

		ArchEntity grassDefinition = assetManager.CreateBlockDefinition(baseBlockComponents);
		AssetStringID grassTopTextureStringID = new AssetStringID("kiwicubed", "texture/grass_top");
		AssetStringID grassSideTextureStringID = new AssetStringID("kiwicubed", "texture/grass_side");
		TextureAtlasData[] grassFaces = {
			assetManager.GetTextureAtlasData(grassTopTextureStringID),
			assetManager.GetTextureAtlasData(grassSideTextureStringID),
			assetManager.GetTextureAtlasData(dirtTextureStringID)
		};
		MetaTexture grassMetaTexture = new MetaTexture(grassFaces, new byte[] { 1, 1, 1, 1, 0, 2 }, 1, 3);
		archWorld.Set<BlockRenderableComponent>(grassDefinition, new BlockRenderableComponent(grassMetaTexture));

		ArchEntity sandDefinition = assetManager.CreateBlockDefinition(baseBlockComponents);
		AssetStringID sandTextureStringID = new AssetStringID("kiwicubed", "texture/sand");
		TextureAtlasData[] sandFaces = {
			assetManager.GetTextureAtlasData(sandTextureStringID)!,
		};
		MetaTexture sandMetaTexture = new MetaTexture(sandFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
		archWorld.Set<BlockRenderableComponent>(sandDefinition, new BlockRenderableComponent(sandMetaTexture));

		AssetStringID stoneStringID = new AssetStringID("kiwicubed", "stone");
		ushort stoneID = assetManager.RegisterBlockDefinition(stoneStringID, stoneDefinition);
		AssetStringID dirtStringID = new AssetStringID("kiwicubed", "dirt");
		ushort dirtID = assetManager.RegisterBlockDefinition(dirtStringID, dirtDefinition);
		AssetStringID grassStringID = new AssetStringID("kiwicubed", "grass");
		ushort grassID = assetManager.RegisterBlockDefinition(grassStringID, grassDefinition);
		AssetStringID sandStringID = new AssetStringID("kiwicubed", "sand");
		ushort sandID = assetManager.RegisterBlockDefinition(sandStringID, sandDefinition);

		DroppedItemEntity.SetupEntity();
		ISingleplayerHandler singleplayerHandler = Systems.Get<ISingleplayerHandler>();
		IEventManager eventManager = Systems.Get<IEventManager>();
		IEntityManager entityManager = null;
		eventManager.SubscribeToEvent<WorldLoadEvent>((WorldLoadEvent eventData) => {
			entityManager = singleplayerHandler.GetWorld().GetEntityManager();
		});
		eventManager.SubscribeToEvent<PlayerBlockInteractionEvent>((PlayerBlockInteractionEvent eventData) => {
			if (eventData.interactionType != BlockInteractionType.BLOCK_MINED) {
				return;
			}

			Vector3 entityPosition = eventData.blockPosition.ToVector3();
			entityPosition.X += 0.5f;
			entityPosition.Y += 0.15f;
			entityPosition.Z += 0.5f;
			ArchEntity entity = entityManager!.SpawnEntity(itemType, entityPosition, Vector3.Zero);
			DroppedItemEntity.SetItemTexture(entityManager.GetArchWorld(), entity, eventData.blockStringID);
		});
		eventManager.SubscribeToEvent<WorldTickEvent>((WorldTickEvent eventData) => {
			QueryDescription query = new QueryDescription().WithAll<DroppedItemEntity.EntityDroppedItemComponent>();
			entityManager!.GetArchWorld().Query(query, (ref DroppedItemEntity.EntityDroppedItemComponent droppedItemComponent, ref EntityRenderableComponent renderableComponent) => {
				renderableComponent.orientationOffset.Y += 0.05f;
				renderableComponent.positionOffset.Y = (float)((Math.Sin((eventData.totalTicks) / 5.0f)) + 1.0f) * 0.08f;
			});
		});

		AssetStringID plainsStringID = new AssetStringID("kiwicubed", "plains");
		AssetStringID desertStringID = new AssetStringID("kiwicubed", "desert");
		BiomeModel plainsBiome = new BiomeModel(0.4f, 0.2f, 0.5f, grassID, dirtID, stoneID);
		BiomeModel desertBiome = new BiomeModel(0.1f, 1.0f, -0.4f, sandID, sandID, stoneID);
		assetManager.RegisterBiomeModel(plainsStringID, plainsBiome);
		assetManager.RegisterBiomeModel(desertStringID, desertBiome);

		//IUI ui = Systems.Get<IUI>();
		//IVirtualWindow globalWindow = ui.GetGlobalWindow();
		//
		//ui.AddScreen(mainMenuID);
		//
		//TextureAtlasData logoAtlasData = assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/kiwicubed_logo_89x18"));
		//MetaTexture logoTexture = new MetaTexture(new TextureAtlasData[] { logoAtlasData }, new byte[] { 0, 0, 0, 0, 0, 0 });
		//
		//List<TextureAtlasData> buttonAtlasDatas = new();
		//buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_unselected")));
		//buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_selected")));
		//buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_activated")));
		//MetaTexture buttonTexture = new MetaTexture(buttonAtlasDatas.ToArray(), new byte[] { 0, 0, 0, 0, 0, 0 });
		//
		//List<TextureAtlasData> sliderAtlasDatas = new();
		//sliderAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/slider_64x16")));
		//sliderAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/slider_bar_unselected")));
		//sliderAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/slider_bar_selected")));
		//MetaTexture sliderTexture = new MetaTexture(sliderAtlasDatas.ToArray(), new byte[] { 0, 0, 0, 0, 0, 0 });
		//
		//int windowCenterX = (int)globalWindow.GetWidth() / 2;
		//int buttonWidth = 64 * 8;
		//int buttonCenterX = windowCenterX - (buttonWidth / 2);
		//Vector2 buttonSize = new Vector2(512, 128);
		//
		//ui.AddElementToScreen(mainMenuID, new UIImage(new Vector2(windowCenterX - (89 * 4 / 2), 100), new Vector2(89 * 4, 18 * 4), logoTexture, 0));
		//ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 200), buttonSize, () => {
		//	singleplayerHandler.CreateWorld(5, 4);
		//}, buttonTexture, "Create World"));
		//ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX + 600, 200), buttonSize, () => {
		//	singleplayerHandler.LoadWorld("worldname");
		//}, buttonTexture, "Load World"));
		//ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 400), buttonSize, () => { }, buttonTexture, "Settings"));
		//ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 600), buttonSize, () => {
		//	Systems.Get<IMetaHandler>().CloseGame();
		//}, buttonTexture, "Exit Game"));
		//
		//ui.SetCurrentScreen(mainMenuID);
		//
		//ui.AddScreen(settingsMenuID);
		////ui.AddElementToScreen(settingsMenuID, new UISlider(new Vector2(buttonCenterX, 400), buttonSize,  sliderTexture, "FOV", () => { return SingleplayerHandler.GetWorld().GetPlayer().FOV; }, (float newValue) => { SingleplayerHandler.GetWorld().GetPlayer().FOV = newValue; }, 10, 170));
		//ui.AddElementToScreen(settingsMenuID, new UIButton(new Vector2(buttonCenterX, 600), buttonSize, () => {
		//	ui.MoveScreenBack();
		//}, buttonTexture, "Back"));
		//
		//ui.AddScreen(pauseMenuID);
		//ui.AddElementToScreen(pauseMenuID, new UIButton(new Vector2(buttonCenterX, 200), buttonSize, () => {
		//	TogglePause();
		//}, buttonTexture, "Resume Game"));
		//ui.AddElementToScreen(pauseMenuID, new UIButton(new Vector2(buttonCenterX, 400), buttonSize, () => {
		//	ui.SetCurrentScreen(settingsMenuID);
		//}, buttonTexture, "Settings"));
		//ui.AddElementToScreen(pauseMenuID, new UIButton(new Vector2(buttonCenterX, 600), buttonSize, () => {
		//	singleplayerHandler.SaveWorld();
		//	singleplayerHandler.ExitWorld();
		//	ui.SetCurrentScreen(mainMenuID);
		//}, buttonTexture, "Exit World"));
		//
		//List<TextureAtlasData> inventoryAtlasDatas = new();
		//inventoryAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/inventory_player")));
		//inventoryAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/inventory_27")));
		//inventoryAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/hotbar")));
		//MetaTexture inventoryTextures = new MetaTexture(inventoryAtlasDatas.ToArray(), new byte[] { 0, 0, 0, 0, 0, 0 });
		//
		//int playerTopCenterX = windowCenterX - (76 * 8 / 2);
		//int inventoryCenterX = windowCenterX - (96 * 8 / 2);
		//
		//int inventoryY = 500;
		//int playerY = 500 - (32 * 8);
		//int hotbarY = 500 + (32 * 8);
		//
		//ui.AddScreen(inventoryScreenID);
		//ui.AddElementToScreen(inventoryScreenID, new UIImage(new Vector2(playerTopCenterX, playerY), new Vector2(608, 256), inventoryTextures, 0));
		//ui.AddElementToScreen(inventoryScreenID, new UIImage(new Vector2(inventoryCenterX, inventoryY), new Vector2(768, 256), inventoryTextures, 1));
		//ui.AddElementToScreen(inventoryScreenID, new UIImage(new Vector2(inventoryCenterX, hotbarY), new Vector2(768, 96), inventoryTextures, 2));
		//ui.AddCustomDrawCommandToScreen(inventoryScreenID, (IUIScreen uiScreen) => {
		//	//IInventory playerInventory = SingleplayerHandler.GetWorld().GetPlayer().GetEntityData().inventory;
		//	//List<ValueTuple<AssetStringID, InventorySlot>> inventorySlots = playerInventory.GetAllSlots();
		//	//for (int slotIndex = 0; slotIndex < 27; slotIndex++) {
		//	//	int slotX = slotIndex % 9;
		//	//	int slotY = slotIndex / 9;
		//	//	ValueTuple<AssetStringID, InventorySlot> slotPair = inventorySlots[slotIndex];
		//	//	InventorySlot slot = slotPair.Item2;
		//	//	if (!slot.HasItem()) {
		//	//		continue;
		//	//	}
		//	//	IItem item = assetManager.GetItem(slot.itemStringID);
		//	//	MetaTexture itemTexture = item.GetTexture();
		//	//	int slotXOffset = slotX * ((8 + 2) * 8);
		//	//	int slotYOffset = slotY * ((8 + 2) * 8);
		//	//	int slotInventoryX = inventoryCenterX + (4 * 8);
		//	//	int slotInventoryY = inventoryY + (2 * 8);
		//	//	int finalX = slotInventoryX + slotXOffset;
		//	//	int finalY = slotInventoryY + slotYOffset;
		//	//	UIImage.Render(ui, new Vector2(finalX, finalY), new Vector2(64, 64), itemTexture, 0);
		//	//	Vector2 textSize = Renderer.MeasureText(slot.itemCount.ToString());
		//	//	Renderer.DrawText(slot.itemCount.ToString(), new Vector2(finalX + (8 * 8) - textSize.X , finalY + (8 * 8)), new Vector2(1.0f), Color.Black);
		//	//}
		//});
		//
		//// later stop using in favor of controlhandler or something like that
		//IInputHandler inputHandler = ui.GetInputHandler();
		//inputHandler.RegisterKeyCallback(Key.Escape, (Key key) => {
		//	TogglePause();
		//}, true);
		//inputHandler.RegisterKeyCallback(Key.E, (Key key) => {
		//	ToggleInventory();
		//}, true);

		INFO("Initialized KiwiCubed base mod");

		return true;
	}

	private void TogglePause() {
		ISingleplayerHandler singleplayerHandler = Systems.Get<ISingleplayerHandler>();
		if (!singleplayerHandler.IsLoadedIntoWorld()) {
			return;
		}
		IUI ui = Systems.Get<IUI>();
		if (!ui.IsDisabled()) {
			ui.MoveScreenBack();
		} else {
			ui.SetCurrentScreen(pauseMenuID);
			//SingleplayerHandler.SaveWorld();
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

	public override void UnloadServer() {
	}

	public override bool InitializeClient() {
		OVERRIDE_LOG_NAME("KiwiCubed initialization");

		INFO("Initializing KiwiCubed base mod...");

		IAssetManager assetManager = Systems.Get<IAssetManager>();
		//BlockStone stone = new BlockStone();
		//BlockDirt dirt = new BlockDirt();
		//BlockGrass grass = new BlockGrass();
		//BlockSand sand = new BlockSand();
		//ushort stoneID = assetManager.RegisterBlock(stone);
		//ushort dirtID = assetManager.RegisterBlock(dirt);
		//ushort grassID = assetManager.RegisterBlock(grass);
		//ushort sandID = assetManager.RegisterBlock(sand);

		EntityType itemType = new EntityType(DroppedItemEntity.itemStringID, new ComponentType[] { typeof(EntityRenderableComponent), typeof(EntityPhysicalComponent), typeof(DroppedItemEntity.EntityDroppedItemComponent) }, DroppedItemEntity.ItemEntitySetup);
		assetManager.RegisterEntityType(DroppedItemEntity.itemStringID, itemType);

		ComponentType[] baseBlockComponents = {
			typeof(BlockRenderableComponent)
		};

		ArchWorld archWorld = assetManager.GetArchWorld();
		ArchEntity stoneDefinition = assetManager.CreateBlockDefinition(baseBlockComponents);
		AssetStringID stoneTextureStringID1 = new AssetStringID("kiwicubed", "texture/stone_1");
		AssetStringID stoneTextureStringID2 = new AssetStringID("kiwicubed", "texture/stone_2");
		AssetStringID stoneTextureStringID3 = new AssetStringID("kiwicubed", "texture/stone_3");
		AssetStringID stoneTextureStringID4 = new AssetStringID("kiwicubed", "texture/stone_4");
		TextureAtlasData[] stoneFaces = {
			assetManager.GetTextureAtlasData(stoneTextureStringID1)!,
			assetManager.GetTextureAtlasData(stoneTextureStringID2)!,
			assetManager.GetTextureAtlasData(stoneTextureStringID3)!,
			assetManager.GetTextureAtlasData(stoneTextureStringID4)!,
		};
		MetaTexture stoneMetaTexture = new MetaTexture(stoneFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 4, 1);
		archWorld.Set<BlockRenderableComponent>(stoneDefinition, new BlockRenderableComponent(stoneMetaTexture));

		ArchEntity dirtDefinition = assetManager.CreateBlockDefinition(baseBlockComponents);
		AssetStringID dirtTextureStringID = new AssetStringID("kiwicubed", "texture/dirt");
		TextureAtlasData[] dirtFaces = {
			assetManager.GetTextureAtlasData(dirtTextureStringID)!,
		};
		MetaTexture dirtMetaTexture = new MetaTexture(dirtFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
		archWorld.Set<BlockRenderableComponent>(dirtDefinition, new BlockRenderableComponent(dirtMetaTexture));

		ArchEntity grassDefinition = assetManager.CreateBlockDefinition(baseBlockComponents);
		AssetStringID grassTopTextureStringID = new AssetStringID("kiwicubed", "texture/grass_top");
		AssetStringID grassSideTextureStringID = new AssetStringID("kiwicubed", "texture/grass_side");
		TextureAtlasData[] grassFaces = {
			assetManager.GetTextureAtlasData(grassTopTextureStringID),
			assetManager.GetTextureAtlasData(grassSideTextureStringID),
			assetManager.GetTextureAtlasData(dirtTextureStringID)
		};
		MetaTexture grassMetaTexture = new MetaTexture(grassFaces, new byte[] { 1, 1, 1, 1, 0, 2 }, 1, 3);
		archWorld.Set<BlockRenderableComponent>(grassDefinition, new BlockRenderableComponent(grassMetaTexture));

		ArchEntity sandDefinition = assetManager.CreateBlockDefinition(baseBlockComponents);
		AssetStringID sandTextureStringID = new AssetStringID("kiwicubed", "texture/sand");
		TextureAtlasData[] sandFaces = {
			assetManager.GetTextureAtlasData(sandTextureStringID)!,
		};
		MetaTexture sandMetaTexture = new MetaTexture(sandFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
		archWorld.Set<BlockRenderableComponent>(sandDefinition, new BlockRenderableComponent(sandMetaTexture));

		AssetStringID stoneStringID = new AssetStringID("kiwicubed", "stone");
		ushort stoneID = assetManager.RegisterBlockDefinition(stoneStringID, stoneDefinition);
		AssetStringID dirtStringID = new AssetStringID("kiwicubed", "dirt");
		ushort dirtID = assetManager.RegisterBlockDefinition(dirtStringID, dirtDefinition);
		AssetStringID grassStringID = new AssetStringID("kiwicubed", "grass");
		ushort grassID = assetManager.RegisterBlockDefinition(grassStringID, grassDefinition);
		AssetStringID sandStringID = new AssetStringID("kiwicubed", "sand");
		ushort sandID = assetManager.RegisterBlockDefinition(sandStringID, sandDefinition);

		DroppedItemEntity.SetupEntity();
		ISingleplayerHandler singleplayerHandler = Systems.Get<ISingleplayerHandler>();
		IEventManager eventManager = Systems.Get<IEventManager>();
		IEntityManager entityManager = null;
		eventManager.SubscribeToEvent<WorldLoadEvent>((WorldLoadEvent eventData) => {
			entityManager = singleplayerHandler.GetWorld().GetEntityManager();
		});
		eventManager.SubscribeToEvent<PlayerBlockInteractionEvent>((PlayerBlockInteractionEvent eventData) => {
			if (eventData.interactionType != BlockInteractionType.BLOCK_MINED) {
				return;
			}

			Vector3 entityPosition = eventData.blockPosition.ToVector3();
			entityPosition.X += 0.5f;
			entityPosition.Y += 0.15f;
			entityPosition.Z += 0.5f;
			ArchEntity entity = entityManager!.SpawnEntity(itemType, entityPosition, Vector3.Zero);
			DroppedItemEntity.SetItemTexture(entityManager.GetArchWorld(), entity, eventData.blockStringID);
		});
		eventManager.SubscribeToEvent<WorldTickEvent>((WorldTickEvent eventData) => {
			QueryDescription query = new QueryDescription().WithAll<DroppedItemEntity.EntityDroppedItemComponent>();
			entityManager!.GetArchWorld().Query(query, (ref DroppedItemEntity.EntityDroppedItemComponent droppedItemComponent, ref EntityRenderableComponent renderableComponent) => {
				renderableComponent.orientationOffset.Y += 0.05f;
				renderableComponent.positionOffset.Y = (float)((Math.Sin((eventData.totalTicks) / 5.0f)) + 1.0f) * 0.08f;
			});
		});

		AssetStringID plainsStringID = new AssetStringID("kiwicubed", "plains");
		AssetStringID desertStringID = new AssetStringID("kiwicubed", "desert");
		BiomeModel plainsBiome = new BiomeModel(0.4f, 0.2f, 0.5f, grassID, dirtID, stoneID);
		BiomeModel desertBiome = new BiomeModel(0.1f, 1.0f, -0.4f, sandID, sandID, stoneID);
		assetManager.RegisterBiomeModel(plainsStringID, plainsBiome);
		assetManager.RegisterBiomeModel(desertStringID, desertBiome);

		IUI ui = Systems.Get<IUI>();
		IVirtualWindow globalWindow = ui.GetGlobalWindow();
		
		ui.AddScreen(mainMenuID);
		
		TextureAtlasData logoAtlasData = assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/kiwicubed_logo_89x18"));
		MetaTexture logoTexture = new MetaTexture(new TextureAtlasData[] { logoAtlasData }, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
		
		List<TextureAtlasData> buttonAtlasDatas = new();
		buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_unselected")));
		buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_selected")));
		buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_activated")));
		MetaTexture buttonTexture = new MetaTexture(buttonAtlasDatas.ToArray(), new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
		
		List<TextureAtlasData> sliderAtlasDatas = new();
		sliderAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/slider_64x16")));
		sliderAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/slider_bar_unselected")));
		sliderAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/slider_bar_selected")));
		MetaTexture sliderTexture = new MetaTexture(sliderAtlasDatas.ToArray(), new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
		
		int windowCenterX = (int)globalWindow.GetWidth() / 2;
		int buttonWidth = 64 * 8;
		int buttonCenterX = windowCenterX - (buttonWidth / 2);
		Vector2 buttonSize = new Vector2(512, 128);
		
		ui.AddElementToScreen(mainMenuID, new UIImage(new Vector2(windowCenterX - (89 * 4 / 2), 100), new Vector2(89 * 4, 18 * 4), logoTexture, 0));
		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 200), buttonSize, () => {
			singleplayerHandler.CreateWorld(5, 4);
		}, buttonTexture, "Create World"));
		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX + 600, 200), buttonSize, () => {
			singleplayerHandler.LoadWorld("worldname");
		}, buttonTexture, "Load World"));
		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 400), buttonSize, () => { }, buttonTexture, "Settings"));
		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 600), buttonSize, () => {
			Systems.Get<IMetaHandler>().CloseGame();
		}, buttonTexture, "Exit Game"));
		
		ui.SetCurrentScreen(mainMenuID);
		
		ui.AddScreen(settingsMenuID);
		//ui.AddElementToScreen(settingsMenuID, new UISlider(new Vector2(buttonCenterX, 400), buttonSize,  sliderTexture, "FOV", () => { return SingleplayerHandler.GetWorld().GetPlayer().FOV; }, (float newValue) => { SingleplayerHandler.GetWorld().GetPlayer().FOV = newValue; }, 10, 170));
		ui.AddElementToScreen(settingsMenuID, new UIButton(new Vector2(buttonCenterX, 600), buttonSize, () => {
			ui.MoveScreenBack();
		}, buttonTexture, "Back"));
		
		ui.AddScreen(pauseMenuID);
		ui.AddElementToScreen(pauseMenuID, new UIButton(new Vector2(buttonCenterX, 200), buttonSize, () => {
			TogglePause();
		}, buttonTexture, "Resume Game"));
		ui.AddElementToScreen(pauseMenuID, new UIButton(new Vector2(buttonCenterX, 400), buttonSize, () => {
			ui.SetCurrentScreen(settingsMenuID);
		}, buttonTexture, "Settings"));
		ui.AddElementToScreen(pauseMenuID, new UIButton(new Vector2(buttonCenterX, 600), buttonSize, () => {
			singleplayerHandler.SaveWorld();
			singleplayerHandler.ExitWorld();
			ui.SetCurrentScreen(mainMenuID);
		}, buttonTexture, "Exit World"));
		//
		//List<TextureAtlasData> inventoryAtlasDatas = new();
		//inventoryAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/inventory_player")));
		//inventoryAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/inventory_27")));
		//inventoryAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/hotbar")));
		//MetaTexture inventoryTextures = new MetaTexture(inventoryAtlasDatas.ToArray(), new byte[] { 0, 0, 0, 0, 0, 0 });
		//
		//int playerTopCenterX = windowCenterX - (76 * 8 / 2);
		//int inventoryCenterX = windowCenterX - (96 * 8 / 2);
		//
		//int inventoryY = 500;
		//int playerY = 500 - (32 * 8);
		//int hotbarY = 500 + (32 * 8);
		//
		//ui.AddScreen(inventoryScreenID);
		//ui.AddElementToScreen(inventoryScreenID, new UIImage(new Vector2(playerTopCenterX, playerY), new Vector2(608, 256), inventoryTextures, 0));
		//ui.AddElementToScreen(inventoryScreenID, new UIImage(new Vector2(inventoryCenterX, inventoryY), new Vector2(768, 256), inventoryTextures, 1));
		//ui.AddElementToScreen(inventoryScreenID, new UIImage(new Vector2(inventoryCenterX, hotbarY), new Vector2(768, 96), inventoryTextures, 2));
		//ui.AddCustomDrawCommandToScreen(inventoryScreenID, (IUIScreen uiScreen) => {
		//	//IInventory playerInventory = SingleplayerHandler.GetWorld().GetPlayer().GetEntityData().inventory;
		//	//List<ValueTuple<AssetStringID, InventorySlot>> inventorySlots = playerInventory.GetAllSlots();
		//	//for (int slotIndex = 0; slotIndex < 27; slotIndex++) {
		//	//	int slotX = slotIndex % 9;
		//	//	int slotY = slotIndex / 9;
		//	//	ValueTuple<AssetStringID, InventorySlot> slotPair = inventorySlots[slotIndex];
		//	//	InventorySlot slot = slotPair.Item2;
		//	//	if (!slot.HasItem()) {
		//	//		continue;
		//	//	}
		//	//	IItem item = assetManager.GetItem(slot.itemStringID);
		//	//	MetaTexture itemTexture = item.GetTexture();
		//	//	int slotXOffset = slotX * ((8 + 2) * 8);
		//	//	int slotYOffset = slotY * ((8 + 2) * 8);
		//	//	int slotInventoryX = inventoryCenterX + (4 * 8);
		//	//	int slotInventoryY = inventoryY + (2 * 8);
		//	//	int finalX = slotInventoryX + slotXOffset;
		//	//	int finalY = slotInventoryY + slotYOffset;
		//	//	UIImage.Render(ui, new Vector2(finalX, finalY), new Vector2(64, 64), itemTexture, 0);
		//	//	Vector2 textSize = Renderer.MeasureText(slot.itemCount.ToString());
		//	//	Renderer.DrawText(slot.itemCount.ToString(), new Vector2(finalX + (8 * 8) - textSize.X , finalY + (8 * 8)), new Vector2(1.0f), Color.Black);
		//	//}
		//});
		//
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

	public override void UnloadClient() {
	}
}

public class DroppedItemEntity {
	public readonly static AssetStringID itemStringID = new AssetStringID("kiwicubed", "dropped_item");
	private static GeneralMesh droppedItemMesh;

	public static void SetupEntity() {
		droppedItemMesh = Systems.Get<IAssetManager>().GetMesh(itemStringID.Prefix("model"));
	}

	public static void SetItemTexture(ArchWorld archWorld, ArchEntity archEntity, AssetStringID blockStringID) {
		EntityRenderableComponent renderableComponent = archWorld.Get<EntityRenderableComponent>(archEntity);
		Block block = Systems.Get<IAssetManager>().GetBlock(blockStringID);
		TextureAtlasData atlasData = block.GetMetaTexture().atlasDatas[0];
		List<float> newTextureCoordinates = new();
		float u0 = atlasData.xPosition;
		float u1 = (atlasData.xPosition + atlasData.xSize);
		float v0 = atlasData.yPosition;
		float v1 = (atlasData.yPosition + atlasData.ySize);
		newTextureCoordinates.Add(u0);
		newTextureCoordinates.Add(v1);
		newTextureCoordinates.Add(u1);
		newTextureCoordinates.Add(v1);
		newTextureCoordinates.Add(u1);
		newTextureCoordinates.Add(v0);
		newTextureCoordinates.Add(u0);
		newTextureCoordinates.Add(v0);
		newTextureCoordinates.Add(u0);
		newTextureCoordinates.Add(v1);
		newTextureCoordinates.Add(u1);
		newTextureCoordinates.Add(v1);
		newTextureCoordinates.Add(u1);
		newTextureCoordinates.Add(v0);
		newTextureCoordinates.Add(u0);
		newTextureCoordinates.Add(v0);
		renderableComponent.mesh.UpdateTextureCooordinates(newTextureCoordinates);
		Renderer.UpdateBuffers(renderableComponent.renderBuffers, renderableComponent.mesh);
	}

	public static void ItemEntitySetup(ArchWorld archWorld, ArchEntity archEntity) {
		archWorld.Set<EntityRenderableComponent>(archEntity, new EntityRenderableComponent(true, droppedItemMesh));
		archWorld.Set<EntityPhysicalComponent>(archEntity, new EntityPhysicalComponent());
		archWorld.Set<EntityDroppedItemComponent>(archEntity, new EntityDroppedItemComponent());
	}

	public struct EntityDroppedItemComponent { }
}

//public class BlockStone : Block {
//	public BlockStone() {
//		stringID = new AssetStringID("kiwicubed", "stone");
//		totalVariants = 4;
//		uniqueFaces = 1;
//		AssetStringID textureStringID1 = new AssetStringID("kiwicubed", "texture/stone_1");
//		AssetStringID textureStringID2 = new AssetStringID("kiwicubed", "texture/stone_2");
//		AssetStringID textureStringID3 = new AssetStringID("kiwicubed", "texture/stone_3");
//		AssetStringID textureStringID4 = new AssetStringID("kiwicubed", "texture/stone_4");
//		TextureAtlasData[] faces = {
//			assetManager.GetTextureAtlasData(textureStringID1)!,
//			assetManager.GetTextureAtlasData(textureStringID2)!,
//			assetManager.GetTextureAtlasData(textureStringID3)!,
//			assetManager.GetTextureAtlasData(textureStringID4)!,
//		};
//		metaTexture = new MetaTexture(faces, new byte[] { 0, 0, 0, 0, 0, 0 });
//	}
//}
//
//public class BlockDirt : Block {
//	public BlockDirt() {
//		stringID = new AssetStringID("kiwicubed", "dirt");
//		TextureAtlasData[] faces = {
//			assetManager.GetTextureAtlasData(stringID.Prefix("texture"))
//		};
//		metaTexture = new MetaTexture(faces, new byte[] { 0, 0, 0, 0, 0, 0 });
//	}
//}
//
//public class BlockGrass : Block {
//	public BlockGrass() {
//		stringID = new AssetStringID("kiwicubed", "grass");
//		TextureAtlasData[] faces = {
//			assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/grass_top")),
//			assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/grass_side")),
//			assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/dirt")),
//		};
//		metaTexture = new MetaTexture(faces, new byte[] { 1, 1, 1, 1, 0, 2 });
//	}
//}
//
//public class BlockSand : Block {
//	public BlockSand() {
//		stringID = new AssetStringID("kiwicubed", "sand");
//		TextureAtlasData[] faces = {
//			assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/sand"))
//		};
//		metaTexture = new MetaTexture(faces, new byte[] { 0, 0, 0, 0, 0, 0 });
//	}
//}

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

	public override void OnClickDown() {
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

		IRenderBuffers renderBuffers = ui.GetRenderBuffers();

		Renderer.UpdateBuffers(renderBuffers, vertices, indices);

		Matrix4x4 modelMatrix = Matrix4x4.CreateScale(new Vector3(size.X, size.Y, 1.0f)) * Matrix4x4.CreateTranslation(new Vector3(position.X, position.Y, 0.0f));
		Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(0, ui.GetGlobalWindow().GetWidth(), ui.GetGlobalWindow().GetHeight(), 0, -1.0f, 1.0f);
		ui.GetUIShader().SetMatrix4("modelMatrix", modelMatrix);
		ui.GetUIShader().SetMatrix4("projectionMatrix", projection);

		Renderer.DrawElements(renderBuffers, indices.Count);

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

		IRenderBuffers renderBuffers = ui.GetRenderBuffers();

		Renderer.UpdateBuffers(renderBuffers, vertices, indices);

		Matrix4x4 modelMatrix = Matrix4x4.CreateScale(new Vector3(size.X, size.Y, 1.0f)) * Matrix4x4.CreateTranslation(new Vector3(position.X, position.Y, 0.0f));
		Matrix4x4 projection = Matrix4x4.CreateOrthographicOffCenter(0, ui.GetGlobalWindow().GetWidth(), ui.GetGlobalWindow().GetHeight(), 0, -1.0f, 1.0f);
		ui.GetUIShader().SetMatrix4("modelMatrix", modelMatrix);
		ui.GetUIShader().SetMatrix4("projectionMatrix", projection);

		Renderer.DrawElements(renderBuffers, indices.Count);
	}
}

public class UISlider : IUIElement {
	private MetaTexture texture;
	private string label;
	private Func<float> getValue;
	private Action<float> setValue;
    private int lowerBound;
	private int upperBound;
	private float clickStartX;
	private float clickStartValue;
	private IInputHandler inputHandler;

	public UISlider(Vector2 position, Vector2 size, MetaTexture texture, string label, Func<float> getValue, Action<float> setValue, int lowerBound, int upperBound) : base(position, size) {
		this.texture = texture;
		this.label = label + ": ";
		this.getValue = getValue;
		this.setValue = setValue;
		this.lowerBound = lowerBound;
		this.upperBound = upperBound;
		clickStartX = -1;
		clickStartValue = 0;
		inputHandler = Systems.Get<IInputHandler>();
    }

    public override void Render() {
		int frame = 1;
		IUI ui = parentScreen.GetUI();
		if ((GetHovered())) {
			if (inputHandler.GetMouseButtonState(MouseButton.Left)) {
                frame = 2;
			}
		}

        float boundWidth = upperBound - lowerBound;
		float modPerPixel = boundWidth / (size.X - 32.0f);

        if (clickStartX != -1) {
			int currentMouseX = (int)inputHandler.GetMousePosition().X;
			float newValue = clickStartValue + (currentMouseX - clickStartX) * modPerPixel;
			if (newValue < lowerBound) {
				newValue = lowerBound;
			} else if (newValue > upperBound) {
				newValue = upperBound;
			}
            setValue(newValue);
		}

		int value = (int)getValue();

        UIImage.Render(parentScreen.GetUI(), position, size, texture, 0);

        float currentOffset = (value - lowerBound) / boundWidth * (size.X - 32.0f);
        UIImage.Render(ui, new Vector2(position.X + currentOffset, position.Y), new Vector2(32, 128), texture, frame);

        Vector2 textDimensions = Renderer.MeasureText(label + value.ToString()) * 2;
        Renderer.DrawText(label + value.ToString(), new Vector2((position.X + size.X / 2) - (textDimensions.X / 2), (position.Y + size.Y / 2) + 24), new Vector2(2.0f), Color.FromArgb(255, 150, 150, 150));
    }

    public override void OnClickDown() {
        clickStartX = (int)inputHandler.GetMousePosition().X;
        clickStartValue = getValue();
    }

    public override void OnClickUp() {
        clickStartX = -1;
    }
}