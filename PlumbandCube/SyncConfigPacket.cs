using ProtoBuf;

namespace PlumbAndCube;
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class SyncConfigPacket
{
    [ProtoMember(1)]
    public int BlocksPerItem;
    
    [ProtoMember(2)]
    public int ReinforceArea;

    [ProtoMember(3)]
    public bool AdminReinforceAnyBlock;
}
