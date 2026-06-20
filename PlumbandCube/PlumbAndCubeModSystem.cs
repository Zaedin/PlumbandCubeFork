using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PlumbAndCube;

public class PlumbAndCubeModSystem : ModSystem
{
    private ICoreAPI _api;
    private IServerNetworkChannel _serverChannel;

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
            });
    }

    private void OnPlayerJoin(IServerPlayer player)
    {
        _serverChannel.SendPacket(new SyncConfigPacket
        {
            BlocksPerItem = ModConfig.Instance.BlocksPerItem,
            ReinforceArea = ModConfig.Instance.ReinforceArea
        }, player);
    }

    public override void Dispose()
    {
        if (_api is ICoreServerAPI sapi) sapi.Event.PlayerJoin -= OnPlayerJoin;
    }
}