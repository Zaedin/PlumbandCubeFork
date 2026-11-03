using System;
using System.Collections.Generic;
using System.Linq;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlumbAndCube;

public class PlumbAndCube : Item
{
    private WorldInteraction[] _interactions;
    private List<LoadedTexture> _symbols;

    public override void OnLoaded(ICoreAPI cApi)
    {
        if (cApi.Side != EnumAppSide.Client) return;

        _ = cApi;
        _interactions = ObjectCacheUtil.GetOrCreate(cApi, "plumbAndSquareInteractions", delegate
        {
            var list = (from collectible in cApi.World.Collectibles
                let attributes = collectible.Attributes
                where attributes != null && attributes["reinforcementStrength"].AsInt() > 0
                select new ItemStack(collectible)).ToList();

            return new WorldInteraction[]
            {
                new()
                {
                    ActionLangCode = "heldhelp-reinforceblock",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = list.ToArray()
                },
                new()
                {
                    ActionLangCode = "heldhelp-removereinforcement",
                    MouseButton = EnumMouseButton.Left,
                    Itemstacks = list.ToArray()
                }
            };
        });
        _symbols = [GenTexture(1, 1)];
    }

    public override void OnUnloaded(ICoreAPI cApi)
    {
        base.OnUnloaded(cApi);
        if (cApi is not ICoreClientAPI || _symbols == null) return;

        foreach (var symbol in _symbols) symbol.Dispose();
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

        if (handling == EnumHandHandling.PreventDefault) return;

        if (byEntity.World.Side == EnumAppSide.Client)
        {
            handling = EnumHandHandling.PreventDefaultAction;
        }
        else
        {
            if (blockSel == null) return;

            var modSystem = byEntity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
            var player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            var itemSlot = modSystem.FindResourceForReinforcing(player);
            if (itemSlot?.Itemstack == null || slot?.Itemstack == null || itemSlot.StackSize <= 0)
            {
                (player as IServerPlayer)?.SendIngameError("noreinforceitem");
                return;
            }

            var strength = itemSlot.Itemstack.ItemAttributes["reinforcementStrength"].AsInt();
            var @int = slot.Itemstack.Attributes.GetInt("toolMode");
            var num = 0;
            var reinforcingForGroup = false;
            var groups = player.GetGroups();
            if (@int > 0 && @int - 1 < groups.Length)
            {
                reinforcingForGroup = true;
                num = groups[@int - 1].GroupUid;
            }

            if (!api.World.BlockAccessor.GetBlock(blockSel.Position).HasBehavior<BlockBehaviorReinforcable>())
            {
                (player as IServerPlayer)?.SendIngameError("notreinforcable");
                return;
            }

            // remove 1 from reinforceArea to count with center block
            var reinforceArea = Math.Clamp(ModConfig.Instance.ReinforceArea, 1, 7);
            var tempPos = new BlockPos(blockSel.Position.dimension);

            int uCenter, vCenter;
            EnumAxis lockedAxis;

            switch (blockSel.Face.Axis)
            {
                case EnumAxis.X:
                    lockedAxis = EnumAxis.X;
                    uCenter = blockSel.Position.Y;
                    vCenter = blockSel.Position.Z;
                    break;
                case EnumAxis.Y:
                    lockedAxis = EnumAxis.Y;
                    uCenter = blockSel.Position.X;
                    vCenter = blockSel.Position.Z;
                    break;
                case EnumAxis.Z:
                    lockedAxis = EnumAxis.Z;
                    uCenter = blockSel.Position.X;
                    vCenter = blockSel.Position.Y;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var u = uCenter;
            var v = vCenter;
            var directions = new[,] { { 1, 0 }, { 0, 1 }, { -1, 0 }, { 0, -1 } };
            var dirIndex = 0;
            var stepLength = 1;
            var sideLength = reinforceArea * 2 - 1;
            var totalBlocks = sideLength * sideLength;
            var visitedCount = 0;

            void ProcessCoordinates(int currentU, int currentV, int reinforceStrength)
            {
                if (Math.Abs(currentU - uCenter) > reinforceArea ||
                    Math.Abs(currentV - vCenter) > reinforceArea) return;

                switch (lockedAxis)
                {
                    case EnumAxis.X: // x locked
                        tempPos.Set(blockSel.Position.X, currentU, currentV);
                        break;
                    case EnumAxis.Y: // y locked
                        tempPos.Set(currentU, blockSel.Position.Y, currentV);
                        break;
                    case EnumAxis.Z: // z locked
                        tempPos.Set(currentU, currentV, blockSel.Position.Z);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (modSystem.IsReinforced(tempPos))
                {
                    (player as IServerPlayer)?.SendIngameError("alreadyreinforced");
                    return;
                }

                var reinforced = reinforcingForGroup
                    ? modSystem.StrengthenBlock(tempPos, player, reinforceStrength, num)
                    : modSystem.StrengthenBlock(tempPos, player, reinforceStrength);
                if (!reinforced) return;

                visitedCount++;
            }

            // process center block first
            var reinforceCounter = 0;
            itemSlot.TakeOut(1);
            itemSlot.MarkDirty();
            reinforceCounter++;
            ProcessCoordinates(u, v, strength);

            while (visitedCount < totalBlocks)
            {
                for (var i = 0; i < 2; i++)
                {
                    var du = directions[dirIndex, 0];
                    var dv = directions[dirIndex, 1];

                    for (var j = 0; j < stepLength; j++)
                    {
                        u += du;
                        v += dv;

                        if (reinforceCounter >= ModConfig.Instance.BlocksPerItem)
                        {
                            if (itemSlot.StackSize == 0)
                            {
                                itemSlot = modSystem.FindResourceForReinforcing(player);
                                if (itemSlot?.Itemstack == null)
                                {
                                    (player as IServerPlayer)?.SendIngameError("noreinforceitem");
                                    return;
                                }

                                strength = itemSlot.Itemstack.ItemAttributes["reinforcementStrength"].AsInt();
                            }

                            itemSlot.TakeOut(1);
                            itemSlot.MarkDirty();
                            reinforceCounter = 0;
                        }

                        ProcessCoordinates(u, v, strength);
                        reinforceCounter++;

                        if (visitedCount >= totalBlocks) return;
                    }

                    dirIndex = (dirIndex + 1) % 4;
                }

                stepLength++;
            }

            var position = blockSel.Position;
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/reinforce"), position.X, position.Y, position.Z);
            handling = EnumHandHandling.PreventDefaultAction;
            if (byEntity.World.Side == EnumAppSide.Client)
                (((EntityPlayer)byEntity).Player as IClientPlayer)?.TriggerFpAnimation(
                    EnumHandInteract.HeldItemInteract);
        }
    }

    public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, ref EnumHandHandling handling)
    {
        if (byEntity.World.Side == EnumAppSide.Client)
        {
            handling = EnumHandHandling.PreventDefaultAction;
            return;
        }

        if (blockSel == null) return;

        var modSystem = byEntity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
        if ((byEntity as EntityPlayer)?.Player is not IServerPlayer serverPlayer) return;

        var errorCode = "";

        BlockPos min;
        BlockPos max;

        var reinforceArea = Math.Clamp(ModConfig.Instance.ReinforceArea - 1, 0, 7);
        switch (blockSel.Face.Axis)
        {
            case EnumAxis.X:
                min = blockSel.Position.AddCopy(0, -reinforceArea, -reinforceArea);
                max = blockSel.Position.AddCopy(0, reinforceArea, reinforceArea);
                break;
            case EnumAxis.Y:
                min = blockSel.Position.AddCopy(-reinforceArea, 0, -reinforceArea);
                max = blockSel.Position.AddCopy(reinforceArea, 0, reinforceArea);
                break;
            case EnumAxis.Z:
                min = blockSel.Position.AddCopy(-reinforceArea, -reinforceArea, 0);
                max = blockSel.Position.AddCopy(reinforceArea, reinforceArea, 0);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var tempPos = new BlockPos(blockSel.Position.dimension);
        for (var x = min.X; x <= max.X; x++)
        for (var y = min.Y; y <= max.Y; y++)
        for (var z = min.Z; z <= max.Z; z++)
        {
            tempPos.Set(x, y, z);
            var reinforcement = modSystem.GetReinforcment(tempPos);
            if (reinforcement == null) continue;
            if (!modSystem.TryRemoveReinforcement(tempPos, serverPlayer, ref errorCode)) continue;
            if (!reinforcement.Locked) continue;
            var itemStack = new ItemStack(byEntity.World.GetItem(new AssetLocation(reinforcement.LockedByItemCode)));
            if (!serverPlayer.InventoryManager.TryGiveItemstack(itemStack, true))
                byEntity.World.SpawnItemEntity(itemStack, byEntity.Pos.XYZ);
        }

        if (errorCode != "")
        {
            serverPlayer.SendIngameError(errorCode);
        }

        var position = blockSel.Position;
        byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/reinforce"), position.X, position.Y, position.Z);

        handling = EnumHandHandling.PreventDefaultAction;
    }

    public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
    {
        slot.Itemstack?.Attributes.SetInt("toolMode", toolMode);
    }

    public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
    {
        return slot.Itemstack != null
            ? Math.Min(1 + byPlayer.GetGroups().Length - 1, slot.Itemstack.Attributes.GetInt("toolMode"))
            : 0;
    }

    public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
    {
        var groups = forPlayer.GetGroups();
        var array = new SkillItem[1 + groups.Length];
        var capi = api as ICoreClientAPI;
        var num = 1;
        var texture = FetchOrCreateTexture(num);
        array[0] = new SkillItem
        {
            Code = new AssetLocation("self"),
            Name = Lang.Get("Reinforce for yourself")
        }.WithIcon(capi, texture);
        for (var i = 0; i < groups.Length; i++)
        {
            texture = FetchOrCreateTexture(++num);
            array[i + 1] = new SkillItem
            {
                Code = new AssetLocation("group"),
                Name = Lang.Get("Reinforce for group " + groups[i].GroupName)
            }.WithIcon(capi, texture);
        }

        return array;
    }

    private LoadedTexture FetchOrCreateTexture(int seed)
    {
        if (_symbols.Count >= seed) return _symbols[seed - 1];

        var loadedTexture = GenTexture(seed, seed);
        _symbols.Add(loadedTexture);
        return loadedTexture;
    }

    private LoadedTexture GenTexture(int seed, int addLines)
    {
        var cApi = api as ICoreClientAPI;
        return cApi?.Gui.Icons.GenTexture(48, 48,
            delegate(Context ctx, ImageSurface _)
            {
                cApi.Gui.Icons.DrawRandomSymbol(ctx, 0.0, 0.0, 48.0, GuiStyle.MacroIconColor, 2.0, seed, addLines);
            });
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return _interactions.Append(base.GetHeldInteractionHelp(inSlot));
    }
}