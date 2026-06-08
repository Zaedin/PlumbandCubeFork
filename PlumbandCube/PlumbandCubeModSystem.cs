using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
    }
}
