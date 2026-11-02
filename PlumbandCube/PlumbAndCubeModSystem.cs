using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlumbAndCube;

public class PlumbAndCubeModSystem : ModSystem
{
    private ICoreAPI _api;
    private IServerNetworkChannel _serverChannel;

    public override double ExecuteOrder()
    {
        return 1;
    }

    public override void StartPre(ICoreAPI api)
    {
        ModConfig config;
        try
        {
            config = api.LoadModConfig<ModConfig>("PlumbAndCubeConfig.json");
            if (config == null)
            {
                api.Logger.Event("No config found. Generating default config.");
                config = new ModConfig();
                api.StoreModConfig(config, "PlumbAndCubeConfig.json");
            }
        }
        catch (Exception e)
        {
            api.Logger.Error($"[{Mod.Info.ModID}]: Failed to load config: {e}. Loading default config.");
            config = new ModConfig();
        }

        ModConfig.Instance = config;

        base.StartPre(api);
    }

    public override void Start(ICoreAPI api)
    {
        _api = api;
        base.Start(api);

        api.RegisterItemClass("PlumbAndCube", typeof(PlumbAndCube));
        api.RegisterItemClass("AdminPlumbAndSquare", typeof(AdminPlumbAndSquare));
        api.RegisterBlockBehaviorClass("AdminReinforcable", typeof(BlockBehaviorAdminReinforcable));
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server || !ModConfig.Instance.AdminReinforceAnyBlock) return;

        foreach (var block in api.World.Blocks)
        {
            if (block.Code == null || block.Id == 0 || block.HasBehavior<BlockBehaviorReinforcable>(true)) continue;

            var behavior = new BlockBehaviorAdminReinforcable(block);
            block.BlockBehaviors = block.BlockBehaviors.Append(behavior);
            block.CollectibleBehaviors = block.CollectibleBehaviors.Append(behavior);
        }
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        sapi.Event.PlayerJoin += OnPlayerJoin;
        _serverChannel = sapi.Network.RegisterChannel(Mod.Info.ModID)
            .RegisterMessageType<SyncConfigPacket>()
            .SetMessageHandler<SyncConfigPacket>((_, _) => { });
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        capi.Network.RegisterChannel(Mod.Info.ModID)
            .RegisterMessageType<SyncConfigPacket>()
            .SetMessageHandler<SyncConfigPacket>(packet =>
            {
                ModConfig.Instance.BlocksPerItem = packet.BlocksPerItem;
                Mod.Logger.Event($"Received BlocksPerItem of {packet.BlocksPerItem} from server");
                ModConfig.Instance.ReinforceArea = packet.ReinforceArea;
                Mod.Logger.Event($"Received ReinforceArea of {packet.ReinforceArea} from server");
                ModConfig.Instance.AdminReinforceAnyBlock = packet.AdminReinforceAnyBlock;
                Mod.Logger.Event($"Received AdminReinforceAnyBlock of {packet.AdminReinforceAnyBlock} from server");
            });
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        _serverChannel.SendPacket(new SyncConfigPacket
        {
            BlocksPerItem = ModConfig.Instance.BlocksPerItem,
            ReinforceArea = ModConfig.Instance.ReinforceArea,
            AdminReinforceAnyBlock = ModConfig.Instance.AdminReinforceAnyBlock
        }, player);
    }

    public override void Dispose()
    {
        if (_api is ICoreServerAPI sapi) sapi.Event.PlayerJoin -= OnPlayerJoin;
    }
}