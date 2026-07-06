namespace VanillaCubed;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using Arch.Core;
using VanillaCubed.Entities;
using VanillaCubed.UI;
using KiwiCubed.Api;
using Silk.NET.Input;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.IPlayer;

public class KiwiCubedMod : ModBase {
	private AssetStringID mainMenuID = new AssetStringID("kiwicubed", "main");
	private AssetStringID settingsMenuID = new AssetStringID("kiwicubed", "settings");
	private AssetStringID pauseMenuID = new AssetStringID("kiwicubed", "pause");
	private AssetStringID inventoryScreenID = new AssetStringID("kiwicubed", "inventory");

	private ComponentType[] commonPlayerComponents = { typeof(EntityPhysicalComponent), typeof(EntityPlayerComponent), typeof(EntityInventoryComponent) };
	private ushort playerInventorySlotsCount = 54;

	public override bool InitializeServer() {
		logger.INFO("Initializing KiwiCubed base mod...");

		IAssetManager assetManager = Meta.Get<IAssetManager>();

        AssetStringID playerStringID = new AssetStringID("kiwicubed", "player");
        EntityType playerType = new EntityType(playerStringID, commonPlayerComponents, (ArchWorld archWorld, ArchEntity archEntity) => {
			EntityPlayerComponent playerComponent = new EntityPlayerComponent();
			archWorld.Set<EntityPlayerComponent>(archEntity, playerComponent);
			bool applyGravity = playerComponent.gameMode == GameMode.SURVIVAL ? true : false;
			bool applyCollision = playerComponent.gameMode == GameMode.SURVIVAL ? true : false;
			archWorld.Set<EntityPhysicalComponent>(archEntity, new EntityPhysicalComponent { 
				applyGravity = applyGravity, 
				applyCollision = applyCollision
			});
			EntityInventoryComponent inventoryComponent = new EntityInventoryComponent(Inventory.CreateInventory(playerInventorySlotsCount));
            archWorld.Set<EntityInventoryComponent>(archEntity, inventoryComponent);
        });
        assetManager.RegisterEntityType(playerStringID, playerType);

        EntityType itemType = new EntityType(DroppedItemEntity.itemStringID, new ComponentType[] { typeof(EntityPhysicalComponent), typeof(DroppedItemEntity.EntityDroppedItemComponent) }, DroppedItemEntity.ItemEntitySetupServer);
		assetManager.RegisterEntityType(DroppedItemEntity.itemStringID, itemType);

		ComponentType[] baseBlockComponents = {
			typeof(BlockSolidComponent)
		};

		ArchWorld archWorld = assetManager.GetArchWorld();
		ArchEntity stoneEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID stoneStringID = new AssetStringID("kiwicubed", "stone");
        BlockDefinition stoneDefinition = new BlockDefinition(stoneStringID, stoneEntity);


        ArchEntity dirtEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID dirtStringID = new AssetStringID("kiwicubed", "dirt");
        BlockDefinition dirtDefinition = new BlockDefinition(dirtStringID, dirtEntity);

        ArchEntity grassEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID grassStringID = new AssetStringID("kiwicubed", "grass");
        BlockDefinition grassDefinition = new BlockDefinition(grassStringID, grassEntity);

        ArchEntity sandEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID sandStringID = new AssetStringID("kiwicubed", "sand");
        BlockDefinition sandDefinition = new BlockDefinition(sandStringID, sandEntity);

        ArchEntity iceEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID iceStringID = new AssetStringID("kiwicubed", "ice");
        BlockDefinition iceDefinition = new BlockDefinition(iceStringID, iceEntity);

        ArchEntity oakLogEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
		AssetStringID oakLogStringID = new AssetStringID("kiwicubed", "oak_log");
		BlockDefinition oakLogDefinition = new BlockDefinition(oakLogStringID, oakLogEntity);

        ArchEntity highEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID highStringID = new AssetStringID("kiwicubed", "high");
        BlockDefinition highDefinition = new BlockDefinition(highStringID, highEntity);

        ArchEntity lowEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID lowStringID = new AssetStringID("kiwicubed", "low");
        BlockDefinition lowDefinition = new BlockDefinition(lowStringID, lowEntity);

        ArchEntity dryEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID dryStringID = new AssetStringID("kiwicubed", "dry");
        BlockDefinition dryDefinition = new BlockDefinition(dryStringID, dryEntity);

        ArchEntity wetEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID wetStringID = new AssetStringID("kiwicubed", "wet");
        BlockDefinition wetDefinition = new BlockDefinition(wetStringID, wetEntity);

        ushort stoneID = assetManager.RegisterBlockDefinition(stoneDefinition);
		ushort dirtID = assetManager.RegisterBlockDefinition(dirtDefinition);
		ushort grassID = assetManager.RegisterBlockDefinition(grassDefinition);
		ushort sandID = assetManager.RegisterBlockDefinition(sandDefinition);
		ushort iceID = assetManager.RegisterBlockDefinition(iceDefinition);
		ushort oakLogID = assetManager.RegisterBlockDefinition(oakLogDefinition);
        ushort highID = assetManager.RegisterBlockDefinition(highDefinition);
        ushort lowID = assetManager.RegisterBlockDefinition(lowDefinition);
        ushort dryID = assetManager.RegisterBlockDefinition(dryDefinition);
        ushort wetID = assetManager.RegisterBlockDefinition(wetDefinition);

		IWorldServerHandler serverHandler = Meta.Get<IWorldServerHandler>();
		IEventManager eventManager = Meta.Get<IEventManager>();
		IEntityManager? entityManager = null;
		eventManager.SubscribeToEvent<WorldLoadEvent>((WorldLoadEvent eventData) => {
			entityManager = eventData.world.GetEntityManager();
		});
		eventManager.SubscribeToEvent<PlayerBlockInteractionEvent>((PlayerBlockInteractionEvent eventData) => {
			if (eventData.interactionType != BlockEventType.BLOCK_MINED) {
				return;
			}

			Vector3 entityPosition = eventData.blockPosition.ToVector3();
			entityPosition.X += 0.5f;
			entityPosition.Y += 0.15f;
			entityPosition.Z += 0.5f;
			ArchEntity entity = entityManager!.SpawnEntity(itemType, entityPosition, Quaternion.Identity);
		});

		AssetStringID plainsStringID = new AssetStringID("kiwicubed", "plains");
		AssetStringID desertStringID = new AssetStringID("kiwicubed", "desert");
		AssetStringID icyDesertStringID = new AssetStringID("kiwicubed", "icy_desert");
        AssetStringID highBiomeStringID = new AssetStringID("kiwicubed", "high");
        AssetStringID lowBiomeStringID = new AssetStringID("kiwicubed", "low");
        AssetStringID dryBiomeStringID = new AssetStringID("kiwicubed", "dry");
        AssetStringID wetBiomeStringID = new AssetStringID("kiwicubed", "wet");
        BiomeModel plainsBiome = new BiomeModel(0.5f, 0.5f, 0.5f, grassID, dirtID, stoneID);
		BiomeModel desertBiome = new BiomeModel(1.0f, 0.5f, 0.5f, sandID, sandID, stoneID);
		BiomeModel icyDesertBiome = new BiomeModel(0.0f, 0.5f, 0.5f, iceID, iceID, stoneID);
		//BiomeModel highBiome = new BiomeModel(0.5f, 0.5f, 1.0f, highID, highID, highID);
        //BiomeModel lowBiome = new BiomeModel(0.5f, 0.5f, 0.0f, lowID, lowID, lowID);
        //BiomeModel dryBiome = new BiomeModel(0.5f, 0.0f, 0.5f, dryID, dryID, dryID);
        //BiomeModel wetBiome = new BiomeModel(0.5f, 1.0f, 0.5f, wetID, wetID, wetID);
        assetManager.RegisterBiomeModel(plainsStringID, plainsBiome);
		assetManager.RegisterBiomeModel(desertStringID, desertBiome);
		assetManager.RegisterBiomeModel(icyDesertStringID, icyDesertBiome);
		//assetManager.RegisterBiomeModel(highBiomeStringID, highBiome);
		//assetManager.RegisterBiomeModel(lowBiomeStringID, lowBiome);
		//assetManager.RegisterBiomeModel(dryBiomeStringID, dryBiome);
		//assetManager.RegisterBiomeModel(wetBiomeStringID, wetBiome);

		logger.INFO("Initialized KiwiCubed base mod");

		return true;
	}

	private void TogglePause() {
        IWorldClientHandler clientHandler = Meta.Get<IWorldClientHandler>();
        if (!clientHandler.IsLoadedIntoWorld()) {
			return;
		}
		IUI ui = Meta.Get<IUI>();
		if (!ui.IsDisabled()) {
			ui.MoveScreenBack();
		} else {
			ui.SetCurrentScreen(pauseMenuID);
            //clientHandler.SaveWorld();
        }
    }

	private void ToggleInventory() {
		IUI ui = Meta.Get<IUI>();
		if (ui.IsDisabled()) {
			ui.SetCurrentScreen(inventoryScreenID);
		} else if (ui.GetCurrentScreenName() == inventoryScreenID) {
			ui.MoveScreenBack();
		}
	}

	public override void UnloadServer() {
	}

	public override bool InitializeClient() {
		logger.INFO("Initializing KiwiCubed base mod...");

		ModInstaller modInstaller = new ModInstaller();

        IAssetManager assetManager = Meta.Get<IAssetManager>();

		AssetStringID playerModelStringID = new AssetStringID("kiwicubed", "model/player");
		GeneralMesh playerModel = assetManager.GetMesh(playerModelStringID);

        AssetStringID playerStringID = new AssetStringID("kiwicubed", "player");
        EntityType playerType = new EntityType(playerStringID, commonPlayerComponents.With([typeof(EntityRenderableComponent), typeof(EntityPlayerClientComponent)]), (ArchWorld archWorld, ArchEntity archEntity) => {
            archWorld.Set<EntityRenderableComponent>(archEntity, new EntityRenderableComponent(true, playerModel));
            EntityPlayerComponent playerComponent = new EntityPlayerComponent();
            archWorld.Set<EntityPlayerComponent>(archEntity, playerComponent);
            bool applyGravity = playerComponent.gameMode == GameMode.SURVIVAL ? true : false;
            bool applyCollision = playerComponent.gameMode == GameMode.SURVIVAL ? true : false;
            archWorld.Set<EntityPhysicalComponent>(archEntity, new EntityPhysicalComponent {
                applyGravity = applyGravity,
                applyCollision = applyCollision
            });
			archWorld.Set<EntityPlayerClientComponent>(archEntity, new EntityPlayerClientComponent());
            EntityInventoryComponent inventoryComponent = new EntityInventoryComponent(Inventory.CreateInventory(playerInventorySlotsCount));
            archWorld.Set<EntityInventoryComponent>(archEntity, inventoryComponent);
        });
        assetManager.RegisterEntityType(playerStringID, playerType);

        EntityType itemType = new EntityType(DroppedItemEntity.itemStringID, new ComponentType[] { typeof(EntityRenderableComponent), typeof(EntityPhysicalComponent), typeof(DroppedItemEntity.EntityDroppedItemComponent) }, DroppedItemEntity.ItemEntitySetupClient);
		assetManager.RegisterEntityType(DroppedItemEntity.itemStringID, itemType);

		ComponentType[] baseBlockComponents = {
			typeof(BlockRenderableComponent),
			typeof(BlockSolidComponent)
		};

        ArchWorld archWorld = assetManager.GetArchWorld();
        ArchEntity stoneEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID stoneTextureStringID1 = new AssetStringID("kiwicubed", "texture/stone_1");
        AssetStringID stoneTextureStringID2 = new AssetStringID("kiwicubed", "texture/stone_2");
        AssetStringID stoneTextureStringID3 = new AssetStringID("kiwicubed", "texture/stone_3");
        AssetStringID stoneTextureStringID4 = new AssetStringID("kiwicubed", "texture/stone_4");
        TextureAtlasData[] stoneFaces = {
            assetManager.GetTextureAtlasData(stoneTextureStringID1),
            assetManager.GetTextureAtlasData(stoneTextureStringID2),
            assetManager.GetTextureAtlasData(stoneTextureStringID3),
            assetManager.GetTextureAtlasData(stoneTextureStringID4),
        };
        MetaTexture stoneMetaTexture = new MetaTexture(stoneFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 4, 1);
        archWorld.Set<BlockRenderableComponent>(stoneEntity, new BlockRenderableComponent(stoneMetaTexture));
        AssetStringID stoneStringID = new AssetStringID("kiwicubed", "stone");
        BlockDefinition stoneDefinition = new BlockDefinition(stoneStringID, stoneEntity);

        ArchEntity dirtEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID dirtTextureStringID = new AssetStringID("kiwicubed", "texture/dirt");
        TextureAtlasData[] dirtFaces = {
            assetManager.GetTextureAtlasData(dirtTextureStringID),
        };
        MetaTexture dirtMetaTexture = new MetaTexture(dirtFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
        archWorld.Set<BlockRenderableComponent>(dirtEntity, new BlockRenderableComponent(dirtMetaTexture));
        AssetStringID dirtStringID = new AssetStringID("kiwicubed", "dirt");
        BlockDefinition dirtDefinition = new BlockDefinition(dirtStringID, dirtEntity);

        ArchEntity grassEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID grassTopTextureStringID = new AssetStringID("kiwicubed", "texture/grass_top");
        AssetStringID grassSideTextureStringID = new AssetStringID("kiwicubed", "texture/grass_side");
        TextureAtlasData[] grassFaces = {
            assetManager.GetTextureAtlasData(grassTopTextureStringID),
            assetManager.GetTextureAtlasData(grassSideTextureStringID),
            assetManager.GetTextureAtlasData(dirtTextureStringID)
        };
        MetaTexture grassMetaTexture = new MetaTexture(grassFaces, new byte[] { 1, 1, 1, 1, 0, 2 }, 1, 3);
        archWorld.Set<BlockRenderableComponent>(grassEntity, new BlockRenderableComponent(grassMetaTexture));
        AssetStringID grassStringID = new AssetStringID("kiwicubed", "grass");
        BlockDefinition grassDefinition = new BlockDefinition(grassStringID, grassEntity);

        ArchEntity sandEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID sandTextureStringID = new AssetStringID("kiwicubed", "texture/sand");
        TextureAtlasData[] sandFaces = {
            assetManager.GetTextureAtlasData(sandTextureStringID),
        };
        MetaTexture sandMetaTexture = new MetaTexture(sandFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
        archWorld.Set<BlockRenderableComponent>(sandEntity, new BlockRenderableComponent(sandMetaTexture));
        AssetStringID sandStringID = new AssetStringID("kiwicubed", "sand");
        BlockDefinition sandDefinition = new BlockDefinition(sandStringID, sandEntity);

        ArchEntity iceEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID iceTextureStringID = new AssetStringID("kiwicubed", "texture/ice");
        TextureAtlasData[] iceFaces = {
            assetManager.GetTextureAtlasData(iceTextureStringID),
        };
        MetaTexture iceMetaTexture = new MetaTexture(iceFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
        archWorld.Set<BlockRenderableComponent>(iceEntity, new BlockRenderableComponent(iceMetaTexture));
        AssetStringID iceStringID = new AssetStringID("kiwicubed", "ice");
        BlockDefinition iceDefinition = new BlockDefinition(iceStringID, iceEntity);

        ArchEntity oakLogEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID oakLogTopTextureStringID = new AssetStringID("kiwicubed", "texture/oak_log_top");
        AssetStringID oakLogSideTextureStringID = new AssetStringID("kiwicubed", "texture/oak_log_side");
        TextureAtlasData[] oakLogFaces = {
            assetManager.GetTextureAtlasData(oakLogTopTextureStringID),
            assetManager.GetTextureAtlasData(oakLogSideTextureStringID),
        };
        MetaTexture oakLogMetaTexture = new MetaTexture(oakLogFaces, new byte[] { 1, 1, 1, 1, 0, 0 }, 1, 1);
        archWorld.Set<BlockRenderableComponent>(oakLogEntity, new BlockRenderableComponent(oakLogMetaTexture));
        AssetStringID oakLogStringID = new AssetStringID("kiwicubed", "oak_log");
        BlockDefinition oakLogDefinition = new BlockDefinition(oakLogStringID, oakLogEntity);

        ArchEntity highEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID highTextureStringID = new AssetStringID("kiwicubed", "texture/high");
        TextureAtlasData[] highFaces = { 
			assetManager.GetTextureAtlasData(highTextureStringID) 
		};
        MetaTexture highMetaTexture = new MetaTexture(highFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
        archWorld.Set<BlockRenderableComponent>(highEntity, new BlockRenderableComponent(highMetaTexture));
        AssetStringID highStringID = new AssetStringID("kiwicubed", "high");
        BlockDefinition highDefinition = new BlockDefinition(highStringID, highEntity);

        ArchEntity lowEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID lowTextureStringID = new AssetStringID("kiwicubed", "texture/low");
        TextureAtlasData[] lowFaces = { 
			assetManager.GetTextureAtlasData(lowTextureStringID)
		};
        MetaTexture lowMetaTexture = new MetaTexture(lowFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
        archWorld.Set<BlockRenderableComponent>(lowEntity, new BlockRenderableComponent(lowMetaTexture));
        AssetStringID lowStringID = new AssetStringID("kiwicubed", "low");
        BlockDefinition lowDefinition = new BlockDefinition(lowStringID, lowEntity);

        ArchEntity dryEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID dryTextureStringID = new AssetStringID("kiwicubed", "texture/dry");
        TextureAtlasData[] dryFaces = { 
			assetManager.GetTextureAtlasData(dryTextureStringID)
		};
        MetaTexture dryMetaTexture = new MetaTexture(dryFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
        archWorld.Set<BlockRenderableComponent>(dryEntity, new BlockRenderableComponent(dryMetaTexture));
        AssetStringID dryStringID = new AssetStringID("kiwicubed", "dry");
        BlockDefinition dryDefinition = new BlockDefinition(dryStringID, dryEntity);

        ArchEntity wetEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID wetTextureStringID = new AssetStringID("kiwicubed", "texture/wet");
        TextureAtlasData[] wetFaces = { 
			assetManager.GetTextureAtlasData(wetTextureStringID)
		};
        MetaTexture wetMetaTexture = new MetaTexture(wetFaces, new byte[] { 0, 0, 0, 0, 0, 0 }, 1, 1);
        archWorld.Set<BlockRenderableComponent>(wetEntity, new BlockRenderableComponent(wetMetaTexture));
        AssetStringID wetStringID = new AssetStringID("kiwicubed", "wet");
        BlockDefinition wetDefinition = new BlockDefinition(wetStringID, wetEntity);

        ushort stoneID = assetManager.RegisterBlockDefinition(stoneDefinition);
        ushort dirtID = assetManager.RegisterBlockDefinition(dirtDefinition);
        ushort grassID = assetManager.RegisterBlockDefinition(grassDefinition);
        ushort sandID = assetManager.RegisterBlockDefinition(sandDefinition);
        ushort iceID = assetManager.RegisterBlockDefinition(iceDefinition);
		ushort oakLogID = assetManager.RegisterBlockDefinition(oakLogDefinition);
        ushort highID = assetManager.RegisterBlockDefinition(highDefinition);
        ushort lowID = assetManager.RegisterBlockDefinition(lowDefinition);
        ushort dryID = assetManager.RegisterBlockDefinition(dryDefinition);
        ushort wetID = assetManager.RegisterBlockDefinition(wetDefinition);

        DroppedItemEntity.SetupEntityVisuals();
		IEventManager eventManager = Meta.Get<IEventManager>();
		IEntityManager? entityManager = null;
		eventManager.SubscribeToEvent<WorldLoadEvent>((WorldLoadEvent eventData) => {
			entityManager = eventData.world.GetEntityManager();
		});

        AssetStringID plainsStringID = new AssetStringID("kiwicubed", "plains");
        AssetStringID desertStringID = new AssetStringID("kiwicubed", "desert");
        AssetStringID icyDesertStringID = new AssetStringID("kiwicubed", "icy_desert");
        BiomeModel plainsBiome = new BiomeModel(0.4f, 0.2f, 0.5f, grassID, dirtID, stoneID);
        BiomeModel desertBiome = new BiomeModel(0.1f, 1.0f, -0.4f, sandID, sandID, stoneID);
        BiomeModel icyDesertBiome = new BiomeModel(0.9f, 0.8f, 0.5f, iceID, iceID, stoneID);
        assetManager.RegisterBiomeModel(plainsStringID, plainsBiome);
        assetManager.RegisterBiomeModel(desertStringID, desertBiome);
        assetManager.RegisterBiomeModel(icyDesertStringID, icyDesertBiome);

        IUI ui = Meta.Get<IUI>();
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
			Meta.Get<IClientServerInterface>().InitializeServerConnection("localhost");
			ui.DisableUI();
		}, buttonTexture, "Connect to Server"));
		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX + 600, 200), buttonSize, () => {
			IReadOnlyList<string>? modFiles = modInstaller.SelectZippedMods();
			if (modFiles != null) {
				modInstaller.InstallZippedMods(modFiles);
			}
		}, buttonTexture, "Install Mods"));
		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 400), buttonSize, () => { }, buttonTexture, "Settings"));
		ui.AddElementToScreen(mainMenuID, new UIButton(new Vector2(buttonCenterX, 600), buttonSize, () => {
			Meta.CloseGame();
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
			//singleplayerHandler.SaveWorld();
			//singleplayerHandler.ExitWorld();
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

		logger.INFO("Initialized KiwiCubed base mod");

		return true;
	}

	public override void UnloadClient() {
	}
}