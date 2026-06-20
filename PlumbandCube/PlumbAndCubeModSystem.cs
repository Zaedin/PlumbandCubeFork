using System;
using System.Text;
using System.Transactions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Common;

namespace PlumbAndCube;

public class PlumbAndCubeModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        
        api.RegisterItemClass("PlumbAndCube", typeof(PlumbAndCube));
        api.RegisterItemClass("AdminPlumbAndSquare", typeof(AdminPlumbAndSquare));
    }
}