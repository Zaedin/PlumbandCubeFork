using ProtoBuf;

namespace PlumbAndCube;
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SyncConfigPacket
{
    public int BlocksPerItem;
    public int ReinforceArea;
}
