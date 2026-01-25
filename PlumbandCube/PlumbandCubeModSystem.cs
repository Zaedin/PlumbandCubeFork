using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlumbandCube
{
    public class PlumbandCubeModSystem : ModSystem
    {


        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("PlumbandCube", typeof(PlumbandCube));
            api.RegisterItemClass("AdminPlumbAndSquare", typeof(Adminplumbandsquare));
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);
        }

    }
}
