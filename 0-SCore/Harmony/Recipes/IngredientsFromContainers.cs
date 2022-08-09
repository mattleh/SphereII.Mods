﻿using HarmonyLib;
using Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UAI;
using UnityEngine;

namespace SCore.Harmony.Recipes
{

    public class EnhancedRecipeLists
    {

        private static readonly string AdvFeatureClass = "AdvancedRecipes";
        private static readonly string Feature = "ReadFromContainers";
        public static List<TileEntity> GetTileEntities(EntityAlive player)
        {
            var distance = 30f;
            var strDistance = Configuration.GetPropertyValue(AdvFeatureClass, "Distance");
            if (!string.IsNullOrEmpty(strDistance))
                distance = StringParsers.ParseFloat(strDistance);

            var tileEntities = new List<TileEntity>();
            var _targetTypes = "Loot, SecureLoot, SecureLootSigned";
            var paths = SCoreUtils.ScanForTileEntities(player, _targetTypes, true);
            foreach (var path in paths)
            {
                var distanceToLeader = Vector3.Distance(player.position, path);
                if (distanceToLeader < distance)
                {
                    var tileEntity = player.world.GetTileEntity(0, new Vector3i(path));
                    if (tileEntity == null) continue;
                    switch (tileEntity.GetTileEntityType())
                    {
                        case TileEntityType.Loot:
                            var lootTileEntity = tileEntity as TileEntityLootContainer;
                            if (lootTileEntity == null) break;
                            tileEntities.Add(tileEntity);
                            break;

                        case TileEntityType.SecureLootSigned:
                        case TileEntityType.SecureLoot:

                            var secureTileEntity = tileEntity as TileEntitySecureLootContainer;
                            if (secureTileEntity == null) break;

                            PlatformUserIdentifierAbs internalLocalUserIdentifier = PlatformManager.InternalLocalUserIdentifier;
                            if (secureTileEntity.IsUserAllowed(internalLocalUserIdentifier) == false)
                                break;

                            tileEntities.Add(tileEntity);
                            break;

                        default:
                            break;
                    }

                }
            }
            return tileEntities;
        }
        public static List<ItemStack> SearchNearbyContainers(EntityAlive player)
        {
            var _items = new List<ItemStack>();
            var tileEntities = EnhancedRecipeLists.GetTileEntities(player);
            foreach (var tileEntity in tileEntities)
            {
                switch (tileEntity.GetTileEntityType())
                {
                    case TileEntityType.Loot:
                        var lootTileEntity = tileEntity as TileEntityLootContainer;
                        if (lootTileEntity == null) break;
                        _items.AddRange(lootTileEntity.GetItems());
                        break;
                    case TileEntityType.SecureLootSigned:
                    case TileEntityType.SecureLoot:

                        var secureTileEntity = tileEntity as TileEntitySecureLootContainer;
                        if (secureTileEntity == null) break;

                        PlatformUserIdentifierAbs internalLocalUserIdentifier = PlatformManager.InternalLocalUserIdentifier;
                        if (secureTileEntity.IsUserAllowed(internalLocalUserIdentifier) == false)
                            break;

                        _items.AddRange(secureTileEntity.GetItems());
                        break;

                    default:
                        break;
                }

            }
            return _items;
        }
        [HarmonyPatch(typeof(XUiC_RecipeList))]
        [HarmonyPatch("BuildRecipeInfosList")]
        public class BuildRecipeInfosList
        {
            public static bool Prefix(XUiC_RecipeList __instance, ref List<ItemStack> _items)
            {
                // Check if this feature is enabled.
                if (!Configuration.CheckFeatureStatus(AdvFeatureClass, Feature))
                    return true;
                var player = __instance.xui.playerUI.entityPlayer;

                _items.AddRange(EnhancedRecipeLists.SearchNearbyContainers(player));
                return true;
            }
        }

        //[HarmonyPatch(typeof(XUiM_PlayerInventory))]
        //[HarmonyPatch("GetAllItemStacks")]
        //public class GetAllItemStacks
        //{
        //    public static void Postfix(ref List<ItemStack> __result, EntityPlayerLocal ___localPlayer)
        //    {
        //        // Check if this feature is enabled.
        //        if (!Configuration.CheckFeatureStatus(AdvFeatureClass, Feature))
        //            return;
        //        __result.AddRange(EnhancedRecipeLists.SearchNearbyContainers(___localPlayer));
        //    }
        //}

        //[HarmonyPatch(typeof(XUiM_PlayerInventory))]
        //[HarmonyPatch("GetItemCount")]
        //[HarmonyPatch(new[] { typeof(ItemValue) })]
        //public class GetItemCount
        //{
        //    public static void Postfix(ref int __result, EntityPlayerLocal ___localPlayer, ItemValue _itemValue)
        //    {
        //        // Check if this feature is enabled.
        //        if (!Configuration.CheckFeatureStatus(AdvFeatureClass, Feature))
        //            return;

        //        foreach (var item in EnhancedRecipeLists.SearchNearbyContainers(___localPlayer))
        //        {
        //            if (item == null) continue;
        //            if (item.IsEmpty()) continue;
        //            if (item.itemValue == null) continue;
        //            if ((!item.itemValue.HasModSlots || !item.itemValue.HasMods()) && item.itemValue.type == _itemValue.type)
        //                __result += item.count;

        //        }

        //    }
        //}

        // replaces getitemcount
        [HarmonyPatch(typeof(XUiC_IngredientEntry))]
        [HarmonyPatch("GetBindingValue")]
        public class GetBindingValue
        {
            public static bool Prefix(XUiC_IngredientEntry __instance, ref bool __result, ref string value, string bindingName, CachedStringFormatter<int> ___needcountFormatter, CachedStringFormatter<int> ___havecountFormatter, bool ___materialBased, ItemStack ___ingredient, string ___material, XUiC_RecipeCraftCount ___craftCountControl, CachedStringFormatterXuiRgbaColor ___itemicontintcolorFormatter)
            {
                if (!Configuration.CheckFeatureStatus(AdvFeatureClass, Feature))
                    return true;
                bool flag = ___ingredient != null;
                switch (bindingName)
                {
                    case "haveneedcount":
                        {
                            string text = (flag ? ___needcountFormatter.Format(___ingredient.count * ___craftCountControl.Count) : "");
                            int value1 = 0;
                            XUiC_WorkstationMaterialInputGrid childByType = __instance.WindowGroup.Controller.GetChildByType<XUiC_WorkstationMaterialInputGrid>();
                            if (childByType != null)
                            {
                                if (___materialBased)
                                {
                                    value = (flag ? (___havecountFormatter.Format(childByType.GetWeight(___material)) + "/" + text) : "");
                                }
                                else
                                {
                                    value = (flag ? (___havecountFormatter.Format(__instance.xui.PlayerInventory.GetItemCount(___ingredient.itemValue)) + "/" + text) : "");
                                }
                            }
                            else
                            {
                                XUiC_WorkstationInputGrid childByType2 = __instance.WindowGroup.Controller.GetChildByType<XUiC_WorkstationInputGrid>();
                                if (childByType2 != null)
                                {
                                    value = (flag ? (___havecountFormatter.Format(childByType2.GetItemCount(___ingredient.itemValue)) + "/" + text) : "");
                                }
                                else
                                {
                                    value = (flag ? (___havecountFormatter.Format(__instance.xui.PlayerInventory.GetItemCount(___ingredient.itemValue)) + "/" + text) : "");
                                    if (flag)
                                    {
                                        value1 = __instance.xui.PlayerInventory.GetItemCount(___ingredient.itemValue);
                                        ItemStack[] array = SearchNearbyContainers(__instance.xui.playerUI.entityPlayer).ToArray();
                                        for (int k = 0; k < array.Length; k++)
                                        {
                                            if (array[k] != null && array[k].itemValue.type != 0 && ___ingredient.itemValue.type == array[k].itemValue.type)
                                            {
                                                value1 += array[k].count;
                                            }
                                        }
                                        value = (flag ? (___havecountFormatter.Format(value1) + "/" + text) : "");
                                    }
                                }
                            }
                            __result = true;
                            return false;
                        }
                    default:
                        return true;
                }
            }
        }

        // replaces GetAllItemStacks
        [HarmonyPatch(typeof(XUiC_RecipeCraftCount))]
        [HarmonyPatch("calcMaxCraftable")]
        public class calcMaxCraftable
        {
            public static int Postfix(int __result, Recipe ___recipe, XUiC_RecipeCraftCount __instance)
            {
                if (!Configuration.CheckFeatureStatus(AdvFeatureClass, Feature))
                    return __result;


                EntityPlayerLocal player = __instance.xui.playerUI.entityPlayer;
                //ItemStack[] array = __instance.xui.PlayerInventory.GetAllItemStacks().ToArray();
                ItemStack[] array1 = SearchNearbyContainers(player).ToArray();
                ItemStack[] array2 = player.bag.GetSlots();
                ItemStack[] array = array1.Union(array2).ToArray();
                var recipe = ___recipe;
                if (recipe == null)
                {
                    return 1;
                }

                for (int i = 0; i < recipe.ingredients.Count; i++)
                {
                    ItemStack itemStack = recipe.ingredients[i];
                    if (itemStack != null && itemStack.itemValue.HasQuality)
                    {
                        return 1;
                    }
                }
                int num = int.MaxValue;
                int craftingTier = (int)EffectManager.GetValue(PassiveEffects.CraftingTier, null, 1f, __instance.xui.playerUI.entityPlayer, recipe, recipe.tags);
                for (int j = 0; j < recipe.ingredients.Count; j++)
                {
                    ItemStack itemStack2 = recipe.ingredients[j];
                    if (itemStack2 == null || itemStack2.itemValue.type == 0)
                    {
                        continue;
                    }

                    int num2 = itemStack2.count;
                    float num3 = ((!recipe.UseIngredientModifier) ? ((float)num2) : ((float)(int)EffectManager.GetValue(PassiveEffects.CraftingIngredientCount, null, num2, __instance.xui.playerUI.entityPlayer, recipe, FastTags.Parse(itemStack2.itemValue.ItemClass.GetItemName()), calcEquipment: true, calcHoldingItem: true, calcProgression: true, calcBuffs: true, craftingTier)));
                    int num4 = 0;
                    for (int k = 0; k < array.Length; k++)
                    {
                        if (array[k] != null && array[k].itemValue.type != 0 && itemStack2.itemValue.type == array[k].itemValue.type)
                        {
                            num4 += array[k].count;
                        }
                    }
                    int num5 = Mathf.CeilToInt((float)num4 / num3);
                    if (Mathf.FloorToInt(num3 * (float)num5) > num4)
                    {
                        num5--;
                    }

                    num = Mathf.Min(num5, num);
                    if (num == 0)
                    {
                        break;
                    }
                }
                return Mathf.Clamp(num, 1, 10000);
            }
        }

        [HarmonyPatch(typeof(XUiM_PlayerInventory))]
        [HarmonyPatch("HasItems")]
        public class HasItems
        {
            public static bool Postfix(bool __result, IList<ItemStack> _itemStacks, EntityPlayerLocal ___localPlayer, int _multiplier)
            {
                // Check if this feature is enabled.
                if (!Configuration.CheckFeatureStatus(AdvFeatureClass, Feature))
                    return __result;

                if (__result == true) return __result;

                var totalCount = 0;
                var tileEntities = EnhancedRecipeLists.GetTileEntities(___localPlayer);

                foreach (var itemStack in _itemStacks)
                {
                    int num = itemStack.count * _multiplier;
                    // check player inventory
                    var slots = ___localPlayer.bag.GetSlots();
                    totalCount = totalCount + slots
                        .Where(x => x.itemValue.ItemClass == itemStack.itemValue.ItemClass)
                        .Sum(y => y.count);
                    // check container
                    foreach (var tileEntity in tileEntities)
                    {
                        var lootTileEntity = tileEntity as TileEntityLootContainer;
                        totalCount = totalCount + lootTileEntity.items
                            .Where(x => x.itemValue.ItemClass == itemStack.itemValue.ItemClass)
                            .Sum(y => y.count);
                        if (totalCount >= num) return true;
                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(XUiM_PlayerInventory))]
        [HarmonyPatch("RemoveItems")]
        public class RemoveItems
        {
            public static bool Prefix(IList<ItemStack> _itemStacks, EntityPlayerLocal ___localPlayer, int _multiplier)
            {
                Log.Out("Remove Items()");
                // Check if this feature is enabled.
                if (!Configuration.CheckFeatureStatus(AdvFeatureClass, Feature))
                    return true;


                var tileEntities = EnhancedRecipeLists.GetTileEntities(___localPlayer);
                Log.Out($"Tile Entities: {tileEntities.Count}.  Item Stacks: {_itemStacks.Count}");
                foreach (var itemStack in _itemStacks)
                {
                    // counter quantity needed from item
                    int q = itemStack.count * _multiplier;
                    //check player inventory for materials and reduce counter
                    var slots = ___localPlayer.bag.GetSlots();
                    q = q - slots
                        .Where(x => x.itemValue.ItemClass == itemStack.itemValue.ItemClass)
                        .Sum(y => y.count);

                    // check storage boxes
                    foreach (var tileEntity in tileEntities)
                    {
                        if (q <= 0) break;
                        var lootTileEntity = tileEntity as TileEntityLootContainer;
                        if (lootTileEntity == null) continue;

                        // If there's no items in this container, skip.
                        if (!lootTileEntity.HasItem(itemStack.itemValue)) continue;

                        int num = itemStack.count * _multiplier;
                        if (lootTileEntity == null) break;
                        for (int y = 0; y < lootTileEntity.items.Length; y++)
                        {
                            var item = lootTileEntity.items[y];
                            if (item.IsEmpty()) continue;
                            if (item.itemValue.ItemClass == itemStack.itemValue.ItemClass)
                            {
                                // If we can completely satisfy the result, let's do that.
                                if (item.count >= q)
                                {
                                    item.count -= q;
                                    q = 0;
                                }
                                else
                                {
                                    // Otherwise, let's just count down until we meet the requirement.
                                    while (q >= 0)
                                    {
                                        item.count--;
                                        q--;
                                        if (item.count <= 0)
                                            break;
                                    }
                                }

                                //Update the slot on the container, and do the Setmodified(), so that the dedis can get updated.
                                if (item.count < 1)
                                    lootTileEntity.UpdateSlot(y, ItemStack.Empty.Clone());
                                else
                                    lootTileEntity.UpdateSlot(y, item);
                                lootTileEntity.SetModified();
                            }
                        }



                        //foreach (var item in lootTileEntity.items.Where(x => x.itemValue.ItemClass == itemStack.itemValue.ItemClass))
                        //{
                        //    if ( item.count == q)
                        //    {
                        //        lootTileEntity.RemoveItem(item.itemValue);
                        //        q = 0;
                        //        break;
                        //    }

                        //    //if more or equal items available remove needed items
                        //    if (item.count >= q)
                        //    {
                        //        item.count = item.count - q;
                        //        q = 0;
                        //    }
                        //    // if less items available substact items and reduce counter until no items are left in the stack
                        //    else if (item.count < q)
                        //    {
                        //        while (item.count != 0)
                        //        {
                        //            item.count--;
                        //            q--;
                        //        }
                        //    }
                        //    else
                        //    {
                        //        return true;
                        //    }
                        //}
                    }
                }
                return true;
            }
        }
    }
}