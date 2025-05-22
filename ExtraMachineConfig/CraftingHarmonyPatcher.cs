using System;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Internal;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Inventories;
using StardewValley.GameData.Machines;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using Leclair.Stardew.BetterCrafting;

namespace Selph.StardewMods.ExtraMachineConfig;

using static HarmonyLib.Code;
using SObject = StardewValley.Object;

sealed class CraftingHarmonyPatcher
{
    public static void ApplyPatches(Harmony harmony)
    {
        harmony.Patch(
            original: AccessTools.Method(typeof(CraftingPage), "clickCraftingRecipe"),
            transpiler: new HarmonyMethod(typeof(CraftingHarmonyPatcher), nameof(CraftingHarmonyPatcher.CraftingPage_clickCraftingRecipe_Transpiler)));
    }

    static IInventory cloneInventory(IInventory inventory)
    {
        IInventory result = new Inventory();
        foreach (Item item in inventory)
        {
            if (item is null)
            {
                result.Add(item);
            }
            else
            {
                var copy = item.getOne();
                copy.Stack = item.Stack;
                result.Add(copy);
            }
        }
        return result;
    }

    static Item ApplyChanges_Android(Item item, List<IInventory?>? materialContainers)
    {
        var craftingPage = (Game1.activeClickableMenu as GameMenu)
            .pages.Single(p => p.GetType() == typeof(CraftingPage)) as CraftingPage;
        return ApplyChanges(craftingPage.hoverRecipe, item, materialContainers);
    }

    static Item ApplyChanges(CraftingRecipe craftingRecipe, Item item, List<IInventory?>? materialContainers)
    {
        Console.WriteLine($"On ApplyChanges: {craftingRecipe}, {item?.Name}");
        Console.WriteLine($"recipe name: {craftingRecipe.name}");
        Console.WriteLine($"item: {item}");
        Console.WriteLine($"item name: {item.Name}");
        Console.WriteLine($"item category: {item.category}");
        Console.WriteLine($"recipe display name: {craftingRecipe.DisplayName}");
        Console.WriteLine($"recipe desc: {craftingRecipe.description}");
        Console.WriteLine(" material containers: " + materialContainers?.Count);

        if (!ModEntry.extraCraftingConfigAssetHandler.data.TryGetValue(craftingRecipe.name, out var craftingConfig))
        {
            return item;
        }
        var oldPlayerInventory = cloneInventory(Game1.player.Items);
        try
        {
            // Get the ingredients that was used
            // Lord help me...
            var newMaterialContainers = materialContainers?.Select(inventory => inventory is not null ? cloneInventory(inventory) : null).ToList();

            craftingRecipe.consumeIngredients(newMaterialContainers);

            List<IInventory?> oldInventories = new();
            oldInventories.Add(oldPlayerInventory);
            if (materialContainers is not null)
            {
                oldInventories.AddRange(materialContainers);
            }

            List<IInventory?> newInventories = new();
            newInventories.Add(Game1.player.Items);
            if (newMaterialContainers is not null)
            {
                newInventories.AddRange(newMaterialContainers);
            }

            var ingredients = getUsedIngredients(oldInventories, newInventories);
            return Utils.applyCraftingChanges(item, ingredients, craftingConfig);
        }
        finally
        {
            Game1.player.Items.OverwriteWith(oldPlayerInventory);
        }
    }

    static List<Item> getUsedIngredients(List<IInventory?> oldInventories, List<IInventory?> newInventories)
    {
        List<Item> result = new();
        if (oldInventories.Count != newInventories.Count)
        {
            ModEntry.StaticMonitor.Log("Inventories count not matching?", LogLevel.Warn);
            return new();
        }
        for (int i = 0; i < oldInventories.Count; i++)
        {
            if (oldInventories[i] is null || newInventories[i] is null)
            {
                continue;
            }
            if (oldInventories[i]!.Count != newInventories[i]!.Count)
            {
                ModEntry.StaticMonitor.Log("Inventory size not matching?", LogLevel.Warn);
                return new();
            }
            for (int j = 0; j < oldInventories[i]!.Count; j++)
            {
                if (oldInventories[i]![j] == null)
                {
                    continue;
                }
                if (newInventories[i]![j] == null || oldInventories[i]![j].Stack != newInventories[i]![j].Stack)
                {
                    var itemToAdd = oldInventories[i]![j].getOne();
                    itemToAdd.Stack = oldInventories[i]![j].Stack - (newInventories[i]![j]?.Stack ?? 0);
                    result.Add(itemToAdd);
                }
            }
        }
        return result;
    }

    static IEnumerable<CodeInstruction> CraftingPage_clickCraftingRecipe_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher matcher = new(instructions);
        var createItemType = AccessTools.DeclaredMethod(typeof(CraftingRecipe), nameof(CraftingRecipe.createItem));
        // Matched code: Item item = recipe.createItem();
        // Inserted afterwards: item = CraftingHarmonyPatcher.ApplyChanges(recipe, item, this._materialContainers);

        // Seek Android ILSpy
        // Item crafted = hoverRecipe.createItem();
        //IL_0042: ldloc.0
        //IL_0043: ldarg.0
        //IL_0044: ldfld class StardewValley.CraftingRecipe StardewValley.Menus.CraftingPage::hoverRecipe
        //IL_0049: callvirt instance class StardewValley.Item StardewValley.CraftingRecipe::createItem()
        //IL_004e: stfld class StardewValley.Item StardewValley.Menus.CraftingPage/'<>c__DisplayClass80_0'::crafted

        matcher.MatchStartForward(
               new CodeMatch(OpCodes.Ldarg_0),
               new CodeMatch(OpCodes.Ldfld),
               new CodeMatch(OpCodes.Callvirt, createItemType),
               new CodeMatch(OpCodes.Stfld)
               )
             .ThrowIfNotMatch($"Could not find entry point for {nameof(CraftingPage_clickCraftingRecipe_Transpiler)}");
        matcher.Advance(1);
        var recipeVar = matcher.Operand;
        matcher.Advance(2);
        var itemVar = matcher.Operand;
        matcher
          .Advance(1)
          .InsertAndAdvance(
              new CodeInstruction(OpCodes.Ldloc_0),
              new CodeInstruction(OpCodes.Ldloc_0),
              new CodeInstruction(OpCodes.Ldfld, itemVar),
              new CodeInstruction(OpCodes.Ldarg_0),
              new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(CraftingPage), nameof(CraftingPage._materialContainers))),
              new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(CraftingHarmonyPatcher), nameof(CraftingHarmonyPatcher.ApplyChanges_Android))),
              new CodeInstruction(OpCodes.Stfld, itemVar)
              );
        return matcher.InstructionEnumeration();
    }
}
