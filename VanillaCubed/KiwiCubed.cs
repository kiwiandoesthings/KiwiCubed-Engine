namespace VanillaCubed;

using ArchWorld = Arch.Core.World;
using ArchEntity = Arch.Core.Entity;
using Arch.Core;
using Arch.Core.Extensions;
using VanillaCubed.Entities;
using VanillaCubed.UI;
using KiwiCubed.Api;
using Silk.NET.Input;
using System.Numerics;

using static KiwiCubed.Api.AssetDefinitions;
using static KiwiCubed.Api.Globals;
using static KiwiCubed.Api.IInventory;
using static KiwiCubed.Api.IPlayer;

public class KiwiCubedMod : ModBase {
	private AssetStringID mainMenuID = new AssetStringID("kiwicubed", "main");
	private AssetStringID settingsMenuID = new AssetStringID("kiwicubed", "settings");
	private AssetStringID pauseMenuID = new AssetStringID("kiwicubed", "pause");
	private AssetStringID inventoryScreenID = new AssetStringID("kiwicubed", "inventory");

	private ComponentType[] commonPlayerComponents = [typeof(EntityPhysicalComponent), typeof(EntityPlayerComponent), typeof(EntityInventoryComponent)];
	private ushort playerInventorySlotsCount = 54;

	public override bool InitializeServer() {
		logger.INFO("Initializing KiwiCubed base mod...");

		IAssetManager assetManager = Meta.Get<IAssetManager>();

        AssetStringID playerStringID = new AssetStringID("kiwicubed", "player");
        EntityType playerType = new EntityType(playerStringID, commonPlayerComponents, (ArchWorld archWorld, ArchEntity archEntity) => {
			EntityPlayerComponent playerComponent = new EntityPlayerComponent();
			archWorld.Set(archEntity, playerComponent);
			bool applyGravity = playerComponent.gameMode == GameMode.SURVIVAL;
			bool applyCollision = playerComponent.gameMode == GameMode.SURVIVAL;
			archWorld.Set(archEntity, new EntityPhysicalComponent { 
				applyGravity = applyGravity, 
				applyCollision = applyCollision
			});
            EntityInventoryComponent inventoryComponent = new EntityInventoryComponent(Inventory.CreateInventory(playerInventorySlotsCount));
            archWorld.Set(archEntity, inventoryComponent);
        });
        assetManager.RegisterEntityType(playerStringID, playerType);

        EntityType itemType = new EntityType(DroppedItemEntity.itemStringID, [typeof(EntityPhysicalComponent), typeof(DroppedItemEntity.EntityDroppedItemComponent)], DroppedItemEntity.ItemEntitySetupServer);
		assetManager.RegisterEntityType(DroppedItemEntity.itemStringID, itemType);

		ComponentType[] baseBlockComponents = [
			typeof(BlockSolidComponent)
		];

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
		eventManager.SubscribeToEvent((WorldLoadEvent eventData) => {
			entityManager = eventData.world.GetEntityManager();
		});
		eventManager.SubscribeToEvent((PlayerBlockInteractionEvent eventData) => {
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
        assetManager.RegisterBiomeModel(plainsStringID, plainsBiome);
		assetManager.RegisterBiomeModel(desertStringID, desertBiome);
		assetManager.RegisterBiomeModel(icyDesertStringID, icyDesertBiome);

		logger.INFO("Initialized KiwiCubed base mod");

		return true;
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
            archWorld.Set(archEntity, new EntityRenderableComponent(true, playerModel));
            EntityPlayerComponent playerComponent = new EntityPlayerComponent();
            archWorld.Set(archEntity, playerComponent);
            bool applyGravity = playerComponent.gameMode == GameMode.SURVIVAL;
            bool applyCollision = playerComponent.gameMode == GameMode.SURVIVAL;
            archWorld.Set(archEntity, new EntityPhysicalComponent {
                applyGravity = applyGravity,
                applyCollision = applyCollision
            });
			archWorld.Set(archEntity, new EntityPlayerClientComponent());
            EntityInventoryComponent inventoryComponent = new EntityInventoryComponent(Inventory.CreateInventory(playerInventorySlotsCount));
            archWorld.Set(archEntity, inventoryComponent);
        });
        assetManager.RegisterEntityType(playerStringID, playerType);

        EntityType itemType = new EntityType(DroppedItemEntity.itemStringID, [typeof(EntityRenderableComponent), typeof(EntityPhysicalComponent), typeof(DroppedItemEntity.EntityDroppedItemComponent)], DroppedItemEntity.ItemEntitySetupClient);
		assetManager.RegisterEntityType(DroppedItemEntity.itemStringID, itemType);

		ComponentType[] baseBlockComponents = [
			typeof(BlockRenderableComponent),
			typeof(BlockSolidComponent)
		];

        ArchWorld archWorld = assetManager.GetArchWorld();
        ArchEntity stoneEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID stoneTextureStringID1 = new AssetStringID("kiwicubed", "texture/stone_1");
        AssetStringID stoneTextureStringID2 = new AssetStringID("kiwicubed", "texture/stone_2");
        AssetStringID stoneTextureStringID3 = new AssetStringID("kiwicubed", "texture/stone_3");
        AssetStringID stoneTextureStringID4 = new AssetStringID("kiwicubed", "texture/stone_4");
        TextureAtlasData[] stoneFaces = [
            assetManager.GetTextureAtlasData(stoneTextureStringID1),
            assetManager.GetTextureAtlasData(stoneTextureStringID2),
            assetManager.GetTextureAtlasData(stoneTextureStringID3),
            assetManager.GetTextureAtlasData(stoneTextureStringID4),
        ];
        BlockTexture stoneMetaTexture = new BlockTexture(stoneFaces, [0, 0, 0, 0, 0, 0], 4, 1);
        archWorld.Set(stoneEntity, new BlockRenderableComponent(stoneMetaTexture));
        AssetStringID stoneStringID = new AssetStringID("kiwicubed", "stone");
        BlockDefinition stoneDefinition = new BlockDefinition(stoneStringID, stoneEntity);

        ArchEntity dirtEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID dirtTextureStringID = new AssetStringID("kiwicubed", "texture/dirt");
        TextureAtlasData[] dirtFaces = [
            assetManager.GetTextureAtlasData(dirtTextureStringID),
        ];
        BlockTexture dirtMetaTexture = new BlockTexture(dirtFaces, [0, 0, 0, 0, 0, 0], 1, 1);
        archWorld.Set(dirtEntity, new BlockRenderableComponent(dirtMetaTexture));
        AssetStringID dirtStringID = new AssetStringID("kiwicubed", "dirt");
        BlockDefinition dirtDefinition = new BlockDefinition(dirtStringID, dirtEntity);

        ArchEntity grassEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID grassTopTextureStringID = new AssetStringID("kiwicubed", "texture/grass_top");
        AssetStringID grassSideTextureStringID = new AssetStringID("kiwicubed", "texture/grass_side");
        TextureAtlasData[] grassFaces = [
            assetManager.GetTextureAtlasData(grassTopTextureStringID),
            assetManager.GetTextureAtlasData(grassSideTextureStringID),
            assetManager.GetTextureAtlasData(dirtTextureStringID)
        ];
        BlockTexture grassMetaTexture = new BlockTexture(grassFaces, [1, 1, 1, 1, 0, 2], 1, 3);
        archWorld.Set(grassEntity, new BlockRenderableComponent(grassMetaTexture));
        AssetStringID grassStringID = new AssetStringID("kiwicubed", "grass");
        BlockDefinition grassDefinition = new BlockDefinition(grassStringID, grassEntity);

        ArchEntity sandEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID sandTextureStringID = new AssetStringID("kiwicubed", "texture/sand");
        TextureAtlasData[] sandFaces = [
            assetManager.GetTextureAtlasData(sandTextureStringID),
        ];
        BlockTexture sandMetaTexture = new BlockTexture(sandFaces, [0, 0, 0, 0, 0, 0], 1, 1);
        archWorld.Set(sandEntity, new BlockRenderableComponent(sandMetaTexture));
        AssetStringID sandStringID = new AssetStringID("kiwicubed", "sand");
        BlockDefinition sandDefinition = new BlockDefinition(sandStringID, sandEntity);

        ArchEntity iceEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID iceTextureStringID = new AssetStringID("kiwicubed", "texture/ice");
        TextureAtlasData[] iceFaces = [
            assetManager.GetTextureAtlasData(iceTextureStringID),
        ];
        BlockTexture iceMetaTexture = new BlockTexture(iceFaces, [0, 0, 0, 0, 0, 0], 1, 1);
        archWorld.Set(iceEntity, new BlockRenderableComponent(iceMetaTexture));
        AssetStringID iceStringID = new AssetStringID("kiwicubed", "ice");
        BlockDefinition iceDefinition = new BlockDefinition(iceStringID, iceEntity);

        ArchEntity oakLogEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID oakLogTopTextureStringID = new AssetStringID("kiwicubed", "texture/oak_log_top");
        AssetStringID oakLogSideTextureStringID = new AssetStringID("kiwicubed", "texture/oak_log_side");
        TextureAtlasData[] oakLogFaces = [
            assetManager.GetTextureAtlasData(oakLogTopTextureStringID),
            assetManager.GetTextureAtlasData(oakLogSideTextureStringID),
        ];
        BlockTexture oakLogMetaTexture = new BlockTexture(oakLogFaces, [1, 1, 1, 1, 0, 0], 1, 1);
        archWorld.Set(oakLogEntity, new BlockRenderableComponent(oakLogMetaTexture));
        AssetStringID oakLogStringID = new AssetStringID("kiwicubed", "oak_log");
        BlockDefinition oakLogDefinition = new BlockDefinition(oakLogStringID, oakLogEntity);

        ArchEntity highEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID highTextureStringID = new AssetStringID("kiwicubed", "texture/high");
        TextureAtlasData[] highFaces = [
            assetManager.GetTextureAtlasData(highTextureStringID)
        ];
        BlockTexture highMetaTexture = new BlockTexture(highFaces, [0, 0, 0, 0, 0, 0], 1, 1);
        archWorld.Set(highEntity, new BlockRenderableComponent(highMetaTexture));
        AssetStringID highStringID = new AssetStringID("kiwicubed", "high");
        BlockDefinition highDefinition = new BlockDefinition(highStringID, highEntity);

        ArchEntity lowEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID lowTextureStringID = new AssetStringID("kiwicubed", "texture/low");
        TextureAtlasData[] lowFaces = [
            assetManager.GetTextureAtlasData(lowTextureStringID)
		];
        BlockTexture lowMetaTexture = new BlockTexture(lowFaces, [0, 0, 0, 0, 0, 0], 1, 1);
        archWorld.Set(lowEntity, new BlockRenderableComponent(lowMetaTexture));
        AssetStringID lowStringID = new AssetStringID("kiwicubed", "low");
        BlockDefinition lowDefinition = new BlockDefinition(lowStringID, lowEntity);

        ArchEntity dryEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID dryTextureStringID = new AssetStringID("kiwicubed", "texture/dry");
        TextureAtlasData[] dryFaces = [ 
			assetManager.GetTextureAtlasData(dryTextureStringID)
		];
        BlockTexture dryMetaTexture = new BlockTexture(dryFaces, [0, 0, 0, 0, 0, 0], 1, 1);
        archWorld.Set(dryEntity, new BlockRenderableComponent(dryMetaTexture));
        AssetStringID dryStringID = new AssetStringID("kiwicubed", "dry");
        BlockDefinition dryDefinition = new BlockDefinition(dryStringID, dryEntity);

        ArchEntity wetEntity = assetManager.CreateAssetDefinitionEntity(baseBlockComponents);
        AssetStringID wetTextureStringID = new AssetStringID("kiwicubed", "texture/wet");
        TextureAtlasData[] wetFaces = [ 
			assetManager.GetTextureAtlasData(wetTextureStringID)
		];
        BlockTexture wetMetaTexture = new BlockTexture(wetFaces, [0, 0, 0, 0, 0, 0], 1, 1);
        archWorld.Set(wetEntity, new BlockRenderableComponent(wetMetaTexture));
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

        AssetStringID tempItemID = new AssetStringID("kiwicubed", "temp");
        TextureAtlasData tempItemTexture = assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/thenameisstrider"));
        ArchEntity tempItemEntity = assetManager.CreateAssetDefinitionEntity([]);
        ItemDefinition tempItemDefinition = new ItemDefinition(tempItemID, tempItemTexture, "Temp", tempItemEntity);
        assetManager.RegisterItem(tempItemID, tempItemDefinition);

        DroppedItemEntity.SetupEntityVisuals();
		IEventManager eventManager = Meta.Get<IEventManager>();
		IEntityManager? entityManager = null;
		eventManager.SubscribeToEvent((WorldLoadEvent eventData) => {
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
		
		TextureAtlasData logoAtlasData = assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/kiwicubed_logo_89x18"));
        MetaTexture logoTexture = new MetaTexture([logoAtlasData]);
		
		List<TextureAtlasData> buttonAtlasDatas = [];
		buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_unselected")));
		buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_selected")));
		buttonAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/button_64x16_activated")));
        MetaTexture buttonTexture = new MetaTexture([.. buttonAtlasDatas]);
		
		List<TextureAtlasData> sliderAtlasDatas = [];
		sliderAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/slider_64x16")));
		sliderAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/slider_bar_unselected")));
		sliderAtlasDatas.Add(assetManager.GetTextureAtlasData(new AssetStringID("kiwicubed", "texture/slider_bar_selected")));
		MetaTexture sliderTexture = new MetaTexture([.. sliderAtlasDatas]);
		
		int windowCenterX = (int)globalWindow.GetWidth() / 2;
		int buttonWidth = 64 * 8;
		int buttonCenterX = windowCenterX - (buttonWidth / 2);
		Vector2 buttonSize = new Vector2(512, 128);

		ui.AddScreen(mainMenuID);
		UIContainer mainMenuContainer = new UIContainer(globalWindow.GetSize(), 16);
        ui.AddElementToScreen(mainMenuID, mainMenuContainer);
		ui.AddElementToElement(mainMenuContainer, new UIImage(new Vector2(89 * 4, 18 * 4), logoTexture, 0));
		ui.AddElementToElement(mainMenuContainer, new UIButton(buttonSize, () => {
			Meta.Get<IClientServerInterface>().InitializeServerConnection("localhost");
			ui.DisableUI();
			isIntegratedGame = true;
		}, buttonTexture, "Connect to Server"));
		ui.AddElementToElement(mainMenuContainer, new UIButton(buttonSize, () => {
			IReadOnlyList<string>? modFiles = modInstaller.SelectZippedMods();
			if (modFiles != null) {
				modInstaller.InstallZippedMods(modFiles);
			}
		}, buttonTexture, "Install Mods"));
		ui.AddElementToElement(mainMenuContainer, new UIButton(buttonSize, () => { }, buttonTexture, "Settings"));
		ui.AddElementToElement(mainMenuContainer, new UIButton(buttonSize, () => {
			Meta.CloseGame();
		}, buttonTexture, "Exit Game"));
		ui.SetCurrentScreen(mainMenuID);
		
		ui.AddScreen(settingsMenuID);
        UIContainer settingsContainer = new UIContainer(globalWindow.GetSize(), 16);
        ui.AddElementToScreen(settingsMenuID, settingsContainer);
        ui.AddElementToElement(settingsContainer, new UISlider(buttonSize,  sliderTexture, "FOV", () => { return 0; }, (float newValue) => { }, 10, 170));
        ui.AddElementToElement(settingsContainer, new UIButton(buttonSize, () => {
			ui.MoveScreenBack();
		}, buttonTexture, "Back"));
		
		ui.AddScreen(pauseMenuID);
        UIContainer pauseMenuContainer = new UIContainer(globalWindow.GetSize(), 16);
        ui.AddElementToScreen(pauseMenuID, pauseMenuContainer);
        ui.AddElementToElement(pauseMenuContainer, new UIButton(buttonSize, () => {
			TogglePause();
		}, buttonTexture, "Resume Game"));
		ui.AddElementToElement(pauseMenuContainer, new UIButton(buttonSize, () => {
			ui.SetCurrentScreen(settingsMenuID);
		}, buttonTexture, "Settings"));
		ui.AddElementToElement(pauseMenuContainer, new UIButton(buttonSize, () => {
			Meta.Get<IWorldClientHandler>().ExitWorld();
		}, buttonTexture, "Exit World"));

        ui.AddScreen(inventoryScreenID);

        // later stop using in favor of controlhandler or something like that
        IInputHandler inputHandler = ui.GetInputHandler();
		inputHandler.RegisterKeyCallback(Key.Escape, (Key key) => {
			TogglePause();
		}, true);
		inputHandler.RegisterKeyCallback(Key.E, (Key key) => {
			ToggleInventory();
		}, true);

        inputHandler.RegisterKeyCallback(Key.J, (Key key) => {
            IWorldClientHandler client = Meta.Get<IWorldClientHandler>();
            IWorldClient world = client.GetWorld();
            ArchEntity player = world.GetClientPlayer();
            EntityInventoryComponent inv = player.Get<EntityInventoryComponent>();
            IInventory inventory = inv.inventory;
            inventory.AddItem(new ItemStack(tempItemID, 1));
        }, true);

		logger.INFO("Initialized KiwiCubed base mod");

		return true;
	}

	public override void UnloadClient() {
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
            InventoryMenu menu = new InventoryMenu(ui, inventoryScreenID);
            UIInventorySlot[] slots = new UIInventorySlot[27];
            ItemStack[] inventoryStacks = Meta.Get<IWorldClientHandler>().GetWorld().GetClientPlayer().Get<EntityInventoryComponent>().inventory.GetAllStacks();
            int startX = 4 * 8;
            int startY = 2 * 8;
            int padding = 2 * 8;
            int itemSize = 64;
            for (int iterator = 0; iterator < slots.Length; iterator++) {
                int row = (iterator / 9);
                int column = (iterator % 9);
                UIInventorySlot slot = new UIInventorySlot(inventoryStacks[iterator], new Vector2(startX + (column * itemSize) + (column * padding), startY + (row * itemSize) + (row * padding)));
                ui.AddElementToElement(menu.GetInventoryUI(), slot);
                slots[iterator] = slot;
            }
            ui.ArrangeScreen();
        } else if (ui.GetCurrentScreenName() == inventoryScreenID) {
            ui.MoveScreenBack();
        }
    }
}