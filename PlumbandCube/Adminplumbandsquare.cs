using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace PlumbandCube
{
    internal class Adminplumbandsquare : Item
    {
        private WorldInteraction[] _interactions;
        private List<LoadedTexture> _symbols;

        private const int ADMIN_REINFORCE_STRENGTH = 99999;

        public override void OnLoaded(ICoreAPI capi)
        {
            if (api.Side != EnumAppSide.Client) return;

            _interactions = ObjectCacheUtil.GetOrCreate(api, "plumbAndSquareInteractions", () => {
                var stacks = (from obj in api.World.Collectibles where obj.Attributes?["reinforcementStrength"].AsInt() > 0 select new ItemStack(obj)).ToList();

                return new WorldInteraction[] {
                    new() {
                        ActionLangCode = "heldhelp-reinforceblock",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacks.ToArray()
                    },
                    new() {
                        ActionLangCode = "heldhelp-removereinforcement",
                        MouseButton = EnumMouseButton.Left,
                        Itemstacks = stacks.ToArray()
                    }
                };
            });

            _symbols = [GenTexture(1, 1)];
        }

        public override void OnUnloaded(ICoreAPI capi)
        {
            base.OnUnloaded(capi);
            if (capi is not ICoreClientAPI || _symbols == null) return;
            foreach (var texture in _symbols) texture.Dispose();
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (handling == EnumHandHandling.PreventDefault) return;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            if (blockSel == null) return;
            
            var bre = byEntity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();

            var player = (byEntity as EntityPlayer)?.Player;
            if (player == null) return;

            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative) { (player as IServerPlayer)?.SendIngameError("admin_nocreative", "You are not allowed to use this tool!"); return; }

            if (slot.Itemstack != null)
            {
                var toolMode = slot.Itemstack.Attributes.GetInt("toolMode");
                var groupUid = 0;
                var groups = player.GetGroups();

                // Reinforce to group
                if (toolMode > 0 && toolMode - 1 < groups.Length) groupUid = groups[toolMode - 1].GroupUid;

                // Not reinforceable
                if (!api.World.BlockAccessor.GetBlock(blockSel.Position).HasBehavior<BlockBehaviorReinforcable>())
                {
                    (player as IServerPlayer)?.SendIngameError("notreinforcable", "This block can not be reinforced!");
                    return;
                }
                bre.ClearReinforcement(blockSel.Position);

                // Admin reinforcement Strength
                var didStrengthen = groupUid > 0 ? bre.StrengthenBlock(blockSel.Position, player, ADMIN_REINFORCE_STRENGTH, groupUid) : bre.StrengthenBlock(blockSel.Position, player, ADMIN_REINFORCE_STRENGTH);

                if (!didStrengthen)
                {
                    (player as IServerPlayer)?.SendIngameError("alreadyreinforced", "Cannot reinforce block, it's already reinforced!");
                    return;
                }
            }

            var pos = blockSel.Position;
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/reinforce"), pos.X, pos.Y, pos.Z, null);

            handling = EnumHandHandling.PreventDefaultAction;
            if (byEntity.World.Side == EnumAppSide.Client) (((EntityPlayer) byEntity).Player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        }

        public override void OnHeldAttackStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandHandling handling)
        {
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                handling = EnumHandHandling.PreventDefaultAction;
                return;
            }

            if (blockSel == null) return;

            var modBre = byEntity.Api.ModLoader.GetModSystem<ModSystemBlockReinforcement>();
            if ((byEntity as EntityPlayer)?.Player is not IServerPlayer player) { return; }

            if (player.WorldData.CurrentGameMode != EnumGameMode.Creative) { player.SendIngameError("admin_nocreative", "You are not allowed to use this tool!"); return; }

            var bre = modBre.GetReinforcment(blockSel.Position);
            if (bre == null) return;

            if (bre.Locked)
            {
                var stack = new ItemStack(byEntity.World.GetItem(new AssetLocation(bre.LockedByItemCode)));
                if (!player.InventoryManager.TryGiveItemstack(stack, true))
                {
                    byEntity.World.SpawnItemEntity(stack, byEntity.Pos.XYZ);
                }
            }
            modBre.ClearReinforcement(blockSel.Position);

            var pos = blockSel.Position;
            byEntity.World.PlaySoundAt(new AssetLocation("sounds/tool/reinforce"), pos.X, pos.Y, pos.Z, null);

            handling = EnumHandHandling.PreventDefaultAction;
        }

        public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
        {
            slot.Itemstack?.Attributes.SetInt("toolMode", toolMode);
        }

        public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
        {
            return Math.Min(1 + byPlayer.GetGroups().Length - 1, slot.Itemstack.Attributes.GetInt("toolMode"));
        }

        public override SkillItem[] GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
        {
            var groups = forPlayer.GetGroups();
            var modes = new SkillItem[1 + groups.Length];
            var capi = api as ICoreClientAPI;
            var seed = 1;
            var texture = FetchOrCreateTexture(seed);
            modes[0] = new SkillItem() { Code = new AssetLocation("self"), Name = Lang.Get("Reinforce for yourself") }.WithIcon(capi, texture);
            for (var i = 0; i < groups.Length; i++)
            {
                texture = FetchOrCreateTexture(++seed);
                modes[i + 1] = new SkillItem() { Code = new AssetLocation("group"), Name = Lang.Get("Reinforce for group " + groups[i].GroupName) }.WithIcon(capi, texture);
            }

            return modes;
        }

        private LoadedTexture FetchOrCreateTexture(int seed)
        {
            if (_symbols.Count >= seed) return _symbols[seed - 1];

            var newTexture = GenTexture(seed, seed);
            _symbols.Add(newTexture);
            return newTexture;
        }

        private LoadedTexture GenTexture(int seed, int addLines)
        {
            var capi = api as ICoreClientAPI;
            return capi.Gui.Icons.GenTexture(48, 48, (ctx, surface) => { capi.Gui.Icons.DrawRandomSymbol(ctx, 0, 0, 48, GuiStyle.MacroIconColor, 2, seed, addLines); });
        }

        public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
        {
            return _interactions.Append(base.GetHeldInteractionHelp(inSlot));
        }
    }
}
