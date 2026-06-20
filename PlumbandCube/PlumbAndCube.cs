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
            if (itemSlot?.Itemstack == null || slot?.Itemstack == null) return;
            if (itemSlot.StackSize <= 0) return;

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
                (player as IServerPlayer)?.SendIngameError("notreinforcable", "This block can not be reinforced!");
                return;
            }

            BlockPos min;
            BlockPos max;
            switch (blockSel.Face.Axis)
            {
                case EnumAxis.X:
                    min = blockSel.Position.AddCopy(0, -2, -2);
                    max = blockSel.Position.AddCopy(0, 2, 2);
                    break;
                case EnumAxis.Y:
                    min = blockSel.Position.AddCopy(-2, 0, -2);
                    max = blockSel.Position.AddCopy(2, 0, 2);
                    break;
                case EnumAxis.Z:
                    min = blockSel.Position.AddCopy(-2, -2, 0);
                    max = blockSel.Position.AddCopy(2, 2, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var reinforceCounter = 0;
            var tempPos = new BlockPos(blockSel.Position.dimension);
            
            void Reinforce() {
                for (var x = min.X; x <= max.X; x++)
                {
                    for (var y = min.Y; y <= max.Y; y++)
                    {
                        for (var z = min.Z; z <= max.Z; z++)
                        {
                            if (itemSlot.StackSize == 0) 
                            {
                                itemSlot = modSystem.FindResourceForReinforcing(player);
                                if (itemSlot?.Itemstack == null) return;
                                strength = itemSlot.Itemstack.ItemAttributes["reinforcementStrength"].AsInt();
                            }
                            tempPos.Set(x, y, z);
                            if (modSystem.IsReinforced(tempPos))
                            {
                                (player as IServerPlayer)?.SendIngameError("alreadyreinforced",
                                    "Cannot reinforce block, it's already reinforced!");
                                continue;
                            }

                            var reinforced = reinforcingForGroup
                                ? modSystem.StrengthenBlock(tempPos, player, strength, num)
                                : modSystem.StrengthenBlock(tempPos, player, strength);
                            if (!reinforced) continue;

                            if (reinforceCounter == 0)
                            {
                                itemSlot.TakeOut(1);
                                itemSlot.MarkDirty();
                                reinforceCounter++;
                            }

                            if (reinforceCounter == ModConfig.Instance.BlocksPerItem) reinforceCounter = 0;
                        }
                    }
                }
            }

            Reinforce();

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
        switch (blockSel.Face.Axis)
        {
            case EnumAxis.X:
                min = blockSel.Position.AddCopy(0, -2, -2);
                max = blockSel.Position.AddCopy(0, 2, 2);
                break;
            case EnumAxis.Y:
                min = blockSel.Position.AddCopy(-2, 0, -2);
                max = blockSel.Position.AddCopy(2, 0, 2);
                break;
            case EnumAxis.Z:
                min = blockSel.Position.AddCopy(-2, -2, 0);
                max = blockSel.Position.AddCopy(2, 2, 0);
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

            var reinforcment = modSystem.GetReinforcment(tempPos);

            if (!modSystem.TryRemoveReinforcement(tempPos, serverPlayer, ref errorCode))
            {
                serverPlayer.SendIngameError("cantremove",
                    errorCode == "notownblock"
                        ? "Cannot remove reinforcement. This block does not belong to you"
                        : "Cannot remove reinforcement. It's not reinforced");
            }
            else if (reinforcment.Locked)
            {
                var itemstack = new ItemStack(byEntity.World.GetItem(new AssetLocation(reinforcment.LockedByItemCode)));
                if (!serverPlayer.InventoryManager.TryGiveItemstack(itemstack, true))
                    byEntity.World.SpawnItemEntity(itemstack, byEntity.Pos.XYZ);
            }
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