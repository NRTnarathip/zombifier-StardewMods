using System;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Triggers;
using StardewValley.GameData.Objects;
using StardewValley.Tools;
using StardewValley.Delegates;
using StardewValley.Internal;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.Inventories;
using StardewValley.GameData.Machines;
using StardewValley.GameData;
using StardewValley.Projectiles;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Selph.StardewMods.ExtraMachineConfig;

using SObject = StardewValley.Object;

sealed class MachineHarmonyPatcher
{

    // Keys for the CustomData map
    internal static Regex RequirementIdKeyRegex =
      new Regex(@$"{ModEntry.UniqueId}\.RequirementId\.(\d+)");
    internal static Regex RequirementTagsKeyRegex =
      new Regex(@$"{ModEntry.UniqueId}\.RequirementTags\.(\d+)");
    internal static string RequirementCountKeyPrefix = $"{ModEntry.UniqueId}.RequirementCount";
    internal static string RequirementAddPriceMultiplierKeyPrefix = $"{ModEntry.UniqueId}.RequirementAddPriceMultiplier";
    internal static string RequirementInvalidMsgKey = $"{ModEntry.UniqueId}.RequirementInvalidMsg";
    internal static string RequirementNoDuplicateKeyPrefix = $"{ModEntry.UniqueId}.RequirementNoDuplicate";
    internal static string InheritPreserveIdKey = $"{ModEntry.UniqueId}.InheritPreserveId";
    internal static string CopyColorKey = $"{ModEntry.UniqueId}.CopyColor";
    internal static string RequiredCountMaxKey = $"{ModEntry.UniqueId}.RequiredCountMax";
    internal static string RequiredCountDivisibleByKey = $"{ModEntry.UniqueId}.RequiredCountDivisibleBy";
    internal static string ExtraOutputIdsKey = $"{ModEntry.UniqueId}.ExtraOutputIds";
    internal static string OverrideInputItemIdKey = $"{ModEntry.UniqueId}.OverrideInputItemId";
    internal static string UnflavoredDisplayNameOverrideKey = $"{ModEntry.UniqueId}.UnflavoredDisplayNameOverride";
    internal static string AutomaticProduceCountKey = $"{ModEntry.UniqueId}.AutomaticProduceCount";
    internal static string InputEdibilityMultiplierKey = $"{ModEntry.UniqueId}.InputEdibilityMultiplier";

    // Keys for machine CustomFields
    internal static string TriggerActionToRunWhenReady = $"{ModEntry.UniqueId}.TriggerActionToRunWhenReady";
    internal static string IsCustomCask = $"{ModEntry.UniqueId}.IsCustomCask";
    internal static string CaskWorksAnywhere = $"{ModEntry.UniqueId}.CaskWorksAnywhere";
    internal static string AllowMoreThanOneQualityIncrement = $"{ModEntry.UniqueId}.AllowMoreThanOneQualityIncrement";
    internal static string ReturnInput = $"{ModEntry.UniqueId}.ReturnInput";
    internal static string IsCustomSlimeIncubator = $"{ModEntry.UniqueId}.IsCustomSlimeIncubator";
    internal static string DrawLayerTexture = $"{ModEntry.UniqueId}.DrawLayerTexture";
    internal static string DrawLayerTextureIndex = $"{ModEntry.UniqueId}.DrawLayerTextureIndex";

    // ModData keys
    internal static string ExtraContextTagsKey = $"{ModEntry.UniqueId}.ExtraContextTags";
    internal static string ExtraPreserveIdKeyPrefix = $"{ModEntry.UniqueId}.ExtraPreserveId";
    internal static string ExtraColorKeyPrefix = $"{ModEntry.UniqueId}.ExtraColor";
    // on machines
    internal static string AutomaticProduceCountRemainingKey = $"{ModEntry.UniqueId}.AutomaticProduceCountRemaining";
    //internal static string TriggerActionToRunWhenReady = $"{ModEntry.UniqueId}.TriggerActionToRunWhenReady";

    // ModData value regexes
    internal static Regex DropInIdRegex = new Regex(@"DROP_IN_ID_(\d+)");
    internal static Regex DropInPreserveRegex = new Regex(@"DROP_IN_PRESERVE_(\d+)");
    internal static Regex InputExtraIdRegex = new Regex(@"INPUT_EXTRA_ID_(\d+)");

    // Display name macros
    internal static string ExtraPreservedDisplayNamePrefix = "%EXTRA_PRESERVED_DISPLAY_NAME";

    // CustomFields keys
    internal static string SlingshotDamage = $"{ModEntry.UniqueId}.SlingshotDamage";
    internal static string SlingshotExplosiveRadius = $"{ModEntry.UniqueId}.SlingshotExplosiveRadius";
    internal static string SlingshotExplosiveDamage = $"{ModEntry.UniqueId}.SlingshotExplosiveDamage";

    // The ID of the holder item to get around the `Object` only limitation.
    internal static string HolderId = $"{ModEntry.UniqueId}.Holder";
    internal static string HolderQualifiedId = $"(O){HolderId}";

    internal static bool enableGetOutputItemSideEffect = false;
    internal static bool enableOutputMachineSideEffect = false;

    public static void ApplyPatches(Harmony harmony)
    {
        // Machine logic patches
        harmony.Patch(
            original: AccessTools.Method(
              typeof(MachineDataUtility),
              nameof(MachineDataUtility.GetOutputData),
              new Type[] { typeof(List<MachineItemOutput>), typeof(bool), typeof(Item),
          typeof(Farmer), typeof(GameLocation) }),
            prefix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.MachineDataUtility_GetOutputData_prefix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(MachineDataUtility),
              nameof(MachineDataUtility.GetOutputItem)),
            prefix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.MachineDataUtility_GetOutputItem_prefix)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.MachineDataUtility_GetOutputItem_postfix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(SObject),
              nameof(SObject.PlaceInMachine)),
            prefix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_PlaceInMachine_Prefix)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_PlaceInMachine_Postfix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(MachineDataUtility),
              nameof(MachineDataUtility.CanApplyOutput)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.MachineDataUtility_CanApplyOutput_Postfix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(SObject),
              nameof(SObject.OutputMachine)),
            prefix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_OutputMachine_Prefix)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_OutputMachine_Postfix)));

        // other patches 
        harmony.Patch(
            original: AccessTools.Method(typeof(Item),
              "_PopulateContextTags"),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Item_GetContextTags_postfix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer),
              nameof(Farmer.OnItemReceived)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Farmer_OnItemReceived_postfix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(Chest),
              nameof(Chest.addItem)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Chest_addItem_postfix)));

        harmony.Patch(
            original: AccessTools.Method(typeof(SObject), "loadDisplayName"),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_loadDisplayName_postfix)));

        // Slingshot patches
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Slingshot), nameof(Slingshot.canThisBeAttached), new Type[] { typeof(SObject), typeof(int) }),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Slingshot_canThisBeAttached_postfix)));

        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Slingshot), nameof(Slingshot.GetAmmoDamage)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Slingshot_GetAmmoDamage_postfix)));

        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Slingshot), nameof(Slingshot.GetAmmoCollisionBehavior)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Slingshot_GetAmmoCollisionBehavior_postfix)));

        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Slingshot), nameof(Slingshot.GetAmmoCollisionSound)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Slingshot_GetAmmoCollisionSound_postfix)));

        // Holder item patches (the draw patches are in SmokedItemHarmonyPatcher)
        harmony.Patch(
            original: AccessTools.DeclaredPropertyGetter(typeof(SObject),
              nameof(SObject.DisplayName)),
            prefix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_DisplayName_prefix)));

        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(SObject),
              nameof(SObject.getDescription)),
            prefix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_getDescription_prefix)));

        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Farmer),
              nameof(Farmer.GetItemReceiveBehavior)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Farmer_GetItemReceiveBehavior_postfix)));

        // Le color
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(ItemQueryResolver),
              nameof(ItemQueryResolver.ApplyItemFields),
              new Type[] { typeof(ISalable), typeof(int), typeof(int), typeof(int), typeof(string), typeof(string), typeof(string), typeof(int), typeof(bool), typeof(List<QuantityModifier>), typeof(QuantityModifier.QuantityModifierMode), typeof(List<QuantityModifier>), typeof(QuantityModifier.QuantityModifierMode), typeof(Dictionary<string, string>), typeof(ItemQueryContext), typeof(Item) }),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.ItemQueryResolver_ApplyItemFields_postfix)));

        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(SObject),
              nameof(SObject.onReadyForHarvest)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_onReadyForHarvest_Postfix)));

        // Cask stuff
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Cask),
              nameof(Cask.IsValidCaskLocation)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Cask_IsValidCaskLocation_Postfix)));

        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(SObject),
              nameof(SObject.placementAction)),
            prefix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_placementAction_Prefix)));

        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Cask),
              nameof(Cask.checkForMaturity)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Cask_checkForMaturity_Postfix)));

        // Return input object
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(SObject),
              nameof(SObject.performRemoveAction)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_performRemoveAction_Postfix)));

        // drawwwww
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(SObject),
              nameof(SObject.draw),
              new Type[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) }),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_draw_Postfix)));

        // buff trigger end
        harmony.Patch(
            original: AccessTools.DeclaredMethod(typeof(Buff),
              nameof(Buff.OnRemoved)),
            postfix: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.Buff_OnRemoved_Postfix)));


        // Transpilers go here
        // Allow non-Object input (by turning them into weeds if necessary to satisfy the non-machine code branches)
        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(SObject),
                  nameof(SObject.performObjectDropInAction)),
                transpiler: new HarmonyMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.SObject_performObjectDropInAction_transpiler)));
        }
        catch (Exception e)
        {
            ModEntry.StaticMonitor.Log("Error when patching performObjectDropInAction: " + e.Message, LogLevel.Error);
        }

    }

    // This patch:
    // * Checks for additional fuel requirements specified in the output rule's custom data, and
    // removes rules that cannot be satisfied
    private static void MachineDataUtility_GetOutputData_prefix(ref List<MachineItemOutput> outputs,
        bool useFirstValidOutput, Item inputItem, Farmer who,
        GameLocation location)
    {
        if (outputs == null || outputs.Count < 0)
        {
            return;
        }
        string? invalidMessage = null;
        IInventory inventory = SObject.autoLoadFrom ?? who.Items;
        List<MachineItemOutput> newOutputs = new List<MachineItemOutput>();
        foreach (MachineItemOutput output in outputs)
        {
            if (output.CustomData == null)
            {
                newOutputs.Add(output);
                continue;
            }
            if (ModEntry.ModApi.GetFuelsForThisRecipe(output, inputItem, inventory) is not null)
            {
                newOutputs.Add(output);
            }
            else
            {
                if (output.CustomData.TryGetValue(RequirementInvalidMsgKey, out var msg))
                {
                    invalidMessage ??= msg;
                }
            }
        }
        outputs = newOutputs;
        if (outputs.Count == 0 && invalidMessage != null && who.IsLocalPlayer &&
            SObject.autoLoadFrom == null)
        {
            Game1.showRedMessage(invalidMessage);
        }
    }

    // This patch:
    // * Generates a replacement input item if that is specified
    private static void MachineDataUtility_GetOutputItem_prefix(SObject machine,
        MachineItemOutput outputData, ref Item? inputItem,
        Farmer who, bool probe)
    {
        if (outputData?.CustomData?.TryGetValue(OverrideInputItemIdKey, out var overrideInput) ?? false)
        {
            string overrideInputId = overrideInput == "NEARBY_FLOWER_QUALIFIED_ID" ?
              ItemRegistry.QualifyItemId(MachineDataUtility.GetNearbyFlowerItemId(machine) ?? null) :
              overrideInput;
            if (overrideInputId != null)
            {
                ItemQueryContext context = new ItemQueryContext(machine.Location, who, Game1.random, "machine '" + machine.QualifiedItemId + "' > output rules - input item replaced with ExtraMachineConfig");
                if (context.CustomFields is null)
                {
                    context.CustomFields = new();
                }
                context.CustomFields["Tile"] = machine.TileLocation;
                context.CustomFields["Machine"] = machine;
                inputItem = ItemQueryResolver.TryResolveRandomItem(overrideInputId, context);
            }
            else
            {
                inputItem = null;
            }
        }
    }

    // This is a dirty, dirty hack to prevent an infinite loop where the machine byproducts get
    // their own global machine byproducts attached, leading to cascading calls of
    // GetOutputItems on top of GetOutputItems.
    // (Gods why did I code it like this?)
    public static bool addByproducts = true;

    // This patch:
    // * Checks for additional fuel requirements specified in the output rule's custom data, and
    // removes them from inventory
    // * Checks if preserve ID is set to inherit the input item's preserve ID, and applies it
    // * Checks if a colored item should be created and apply the changes
    // * Check if more input items should be consumed
    // * Produces extra outputs and put them in a chest saved in the output item's heldObject
    // * Applies the display name override if the output is unflavored
    // * Saves extra flavor and color info if specified
    // * Set the automatic produce count, if this comes from `PlaceInMachine` and it's not set.
    private static void MachineDataUtility_GetOutputItem_postfix(ref Item __result, SObject machine,
        MachineItemOutput outputData, Item inputItem,
        Farmer who, bool probe,
        ref int? overrideMinutesUntilReady)
    {
        Console.WriteLine("On MachineDataUtility_GetOutputItem_postfix");

        if (__result == null || outputData == null)
        {
            return;
        }

        var resultObject = __result as SObject;
        var inputObject = inputItem as SObject;

        // Generate the extra output items and save them in a chest saved in the output item's heldObject.
        var extraOutputs = ModEntry.ModApi.GetExtraOutputs(outputData, machine.GetMachineData());
        if (extraOutputs.Count > 0 && resultObject != null)
        {
            var chest = new Chest(false);
            resultObject.heldObject.Value = chest;
            GameStateQueryContext context = new GameStateQueryContext(machine.Location, who, resultObject, inputItem, Game1.random);
            ItemQueryContext itemContext = new ItemQueryContext(machine.Location, who, Game1.random, "machine '" + machine.QualifiedItemId + "' > output rules - extra output items from with ExtraMachineConfig");
            foreach (var extraOutputData in extraOutputs)
            {
                if (!GameStateQuery.CheckConditions(extraOutputData.Condition, context))
                {
                    continue;
                }
                addByproducts = false;
                var item = MachineDataUtility.GetOutputItem(machine, extraOutputData, inputItem, who, false, out var _);
                addByproducts = true;
                if (item != null)
                {
                    chest.addItem(item);
                }
            }
        }


        IInventory inventory = SObject.autoLoadFrom ?? who.Items;
        // Inherit preserve ID
        if ((outputData.PreserveId == "INHERIT" ||
              (outputData.CustomData != null &&
               outputData.CustomData.ContainsKey(InheritPreserveIdKey))) &&
            inputObject is not null &&
            inputObject.preservedParentSheetIndex.Value != "-1" &&
            resultObject != null)
        {
            resultObject.preservedParentSheetIndex.Value = inputObject.preservedParentSheetIndex.Value;
        }

        // Override display name if unflavored
        if (outputData.CustomData != null &&
            outputData.CustomData.TryGetValue(UnflavoredDisplayNameOverrideKey, out var unflavoredDislayNameOverride) &&
            resultObject != null &&
            (resultObject.preservedParentSheetIndex.Value == null ||
             resultObject.preservedParentSheetIndex.Value == "-1"))
        {
            resultObject.displayNameFormat = unflavoredDislayNameOverride;
        }


        if (outputData.CustomData == null)
        {
            return;
        }

        // Set the machine automatic production count
        // If a PlaceInMachine-called output rule, always refresh the count
        // otherwise only set it if it doesn't exist (to help migrate existing machines)
        // (but only if this function is called inside OutputMachine)
        if (!probe && enableOutputMachineSideEffect)
        {
            if (outputData.CustomData.TryGetValue(AutomaticProduceCountKey, out var str) &&
                Int32.TryParse(str, out int automaticProduceCount) &&
                (enableGetOutputItemSideEffect || !machine.modData.ContainsKey(AutomaticProduceCountRemainingKey)))
            {
                machine.modData[AutomaticProduceCountRemainingKey] = str;
            }
            if (!outputData.CustomData.ContainsKey(AutomaticProduceCountKey))
            {
                machine.modData.Remove(AutomaticProduceCountRemainingKey);
            }
            if (outputData.CustomData.TryGetValue(TriggerActionToRunWhenReady, out var action))
            {
                machine.modData[TriggerActionToRunWhenReady] = action;
            }
        }



        // PlaceInMachine-exclusive rules below
        if (inputItem is null) return;

        // Remove extra fuel (and add their prices if specified)
        IDictionary<string, Item> usedFuels = new Dictionary<string, Item>();
        foreach (var (fuelItem, entry) in Utils.GetFuelsForThisRecipe(outputData, inputItem, inventory) ?? [])
        {
            if (enableGetOutputItemSideEffect && !probe)
            {
                fuelItem.Stack -= entry.count;
                if (fuelItem.Stack == 0)
                {
                    inventory.RemoveButKeepEmptySlot(fuelItem);
                }
            }
            usedFuels[entry.fuelEntryId] = fuelItem;
            if (entry.priceMultiplier > 0 && resultObject is not null)
            {
                resultObject.Price += (int)(((fuelItem as SObject)?.Price ?? 0) * entry.priceMultiplier);
            }
        }


        int i = 1;
        // Record the extra fuels' ID/preserve ID if needed
        while (true)
        {
            string? val = Utils.getPreserveId(__result, i);
            if (val is not null)
            {
                // Fuel id -> flavor
                var dropInIdMatch = DropInIdRegex.Match(val);
                if (dropInIdMatch.Success)
                {
                    string idToCheck = dropInIdMatch.Groups[1].Value;
                    if (usedFuels.TryGetValue(idToCheck, out var fuelItem))
                    {
                        __result.modData[$"{ExtraPreserveIdKeyPrefix}.{i}"] = fuelItem.ItemId;
                        if (resultObject is not null)
                        {
                            resultObject.Name = resultObject.Name.Replace($"PRESERVE_ID_{idToCheck}", fuelItem.ItemId);
                        }
                    }
                }
                // Fuel flavor -> flavor
                var dropIdPreserveMatch = DropInPreserveRegex.Match(val);
                if (dropIdPreserveMatch.Success)
                {
                    string idToCheck = dropIdPreserveMatch.Groups[1].Value;
                    if (usedFuels.TryGetValue(idToCheck, out var fuelItem))
                    {
                        __result.modData[$"{ExtraPreserveIdKeyPrefix}.{i}"] = (fuelItem as SObject)?.preservedParentSheetIndex.Value ?? "";
                        if (resultObject is not null)
                        {
                            resultObject.Name = resultObject.Name.Replace($"PRESERVE_ID_{idToCheck}", (fuelItem as SObject)?.preservedParentSheetIndex.Value ?? "");
                        }
                    }
                }
                // Input item's extra flavors -> flavor
                var inputExtraPreserveIdMatch = InputExtraIdRegex.Match(val);
                if (inputExtraPreserveIdMatch.Success)
                {
                    string idToCheck = inputExtraPreserveIdMatch.Groups[1].Value;
                    if (inputItem.modData.TryGetValue($"{ExtraPreserveIdKeyPrefix}.{idToCheck}", out var preserveId))
                    {
                        __result.modData[$"{ExtraPreserveIdKeyPrefix}.{i}"] = preserveId;
                        if (resultObject is not null)
                        {
                            resultObject.Name = resultObject.Name.Replace($"PRESERVE_ID_{idToCheck}", preserveId);
                        }
                    }
                }
                // TODO: Fuel's extra flavors??? This is super niche I sincerely hope no mod authors plan on using this lmao
                i++;
            }
            else
            {
                break;
            }
        }


        // Record the extra fuels' color if needed
        i = 1;
        while (true)
        {
            if (__result.modData.TryGetValue($"{ExtraColorKeyPrefix}.{i}", out var val))
            {
                var dropInIdMatch = DropInIdRegex.Match(val);
                if (dropInIdMatch.Success)
                {
                    string idToCheck = dropInIdMatch.Groups[1].Value;
                    if (usedFuels.TryGetValue(idToCheck, out var fuelItem))
                    {
                        __result.modData[$"{ExtraColorKeyPrefix}.{i}"] = Utils.colorToString(TailoringMenu.GetDyeColor(fuelItem) ?? Color.White);
                    }
                }
                var dropIdPreserveMatch = DropInPreserveRegex.Match(val);
                if (dropIdPreserveMatch.Success)
                {
                    string idToCheck = dropIdPreserveMatch.Groups[1].Value;
                    if (usedFuels.TryGetValue(idToCheck, out var fuelItem))
                    {
                        var preservedIdItem = ItemRegistry.Create((fuelItem as SObject)?.preservedParentSheetIndex.Value);
                        __result.modData[$"{ExtraColorKeyPrefix}.{i}"] = Utils.colorToString(TailoringMenu.GetDyeColor(preservedIdItem) ?? Color.White);
                    }
                }
                var inputExtraPreserveIdMatch = InputExtraIdRegex.Match(val);
                if (inputExtraPreserveIdMatch.Success)
                {
                    string idToCheck = inputExtraPreserveIdMatch.Groups[1].Value;
                    if (inputItem.modData.TryGetValue($"{ExtraColorKeyPrefix}.{idToCheck}", out var color))
                    {
                        __result.modData[$"{ExtraColorKeyPrefix}.{i}"] = color;
                    }
                }
                i++;
            }
            else
            {
                break;
            }
        }

        //return; // ok

        // Color the item
        if ((outputData.CustomData.ContainsKey(CopyColorKey)) &&
            resultObject is not null)
        {
            StardewValley.Objects.ColoredObject newColoredObject;
            if (__result is StardewValley.Objects.ColoredObject coloredObject)
            {
                newColoredObject = coloredObject;
            }
            else
            {
                newColoredObject = new StardewValley.Objects.ColoredObject(
                    __result.ItemId,
                    __result.Stack,
                    Color.White
                    );
                ModEntry.Helper.Reflection.GetMethod(newColoredObject, "GetOneCopyFrom").Invoke(__result);
                newColoredObject.Stack = __result.Stack;
                newColoredObject.heldObject.Value = resultObject.heldObject.Value;
            }
            var color = TailoringMenu.GetDyeColor(inputItem);
            if (color != null)
            {
                newColoredObject.color.Value = (Color)color;
                __result = newColoredObject;
                resultObject = __result as SObject;
            }
        }
        // Consume extra input items and replace output stack count

        //return; // ok


        if (enableGetOutputItemSideEffect && !probe)
        {
            if (outputData.CustomData.ContainsKey(RequiredCountMaxKey) &&
                Int32.TryParse(outputData.CustomData[RequiredCountMaxKey], out int requiredCountMax) &&
                MachineDataUtility.TryGetMachineOutputRule(machine, machine.GetMachineData(), MachineOutputTrigger.ItemPlacedInMachine, inputItem, who, machine.Location, out var _, out var triggerRule, out var _, out var _))
            {
                int requiredCountMin = triggerRule.RequiredCount;
                if (requiredCountMax < requiredCountMin)
                {
                    ModEntry.StaticMonitor.Log($"Warning: RequiredCountMax value ({requiredCountMax}) smaller than TriggerCount ({requiredCountMin}) for rule {outputData.Id}, ignoring.", LogLevel.Warn);
                }
                else
                {
                    int baseOutputStack = Math.Min(inputItem.Stack, requiredCountMax);
                    if (outputData.CustomData.ContainsKey(RequiredCountDivisibleByKey) &&
                        Int32.TryParse(outputData.CustomData[RequiredCountDivisibleByKey], out int requiredCountDivisor))
                    {
                        baseOutputStack -= baseOutputStack % requiredCountDivisor;
                        baseOutputStack = Math.Max(triggerRule.RequiredCount, baseOutputStack);
                    }
                    __result.Stack = (int)Utility.ApplyQuantityModifiers(baseOutputStack, outputData.StackModifiers, outputData.StackModifierMode, machine.Location, who, __result, inputItem);

                    // original: Bug here
                    //SObject.ConsumeInventoryItem(who, inputItem, baseOutputStack - requiredCountMin);
                    // fixed android
                    var ConsumeInventoryItem_Method = AccessTools.Method(typeof(SObject), nameof(SObject.ConsumeInventoryItem));
                    ConsumeInventoryItem_Method.Invoke(null, [who, inputItem, baseOutputStack - requiredCountMin]);
                }
            }
        }

        if (resultObject is not null && inputObject is not null &&
            outputData.CustomData.TryGetValue(InputEdibilityMultiplierKey, out var inputEdiblityMultiplierStr) &&
            Double.TryParse(inputEdiblityMultiplierStr, out var inputEdibilityMultiplier))
        {
            resultObject.Edibility = (int)(inputObject.Edibility * inputEdibilityMultiplier);
        }
    }

    private static void Item_GetContextTags_postfix(Item __instance, ref HashSet<string> tags)
    {
        if (__instance.modData.TryGetValue(ExtraContextTagsKey, out string contextTags))
        {
            tags.UnionWith(contextTags.Split(","));
        }
        // Some extra helper tags
        //if (__instance is SObject obj && obj.preservedParentSheetIndex.Value is null) {
        //  tags.Add("no_preserve_parent_sheet_index");
        //}
        int i = 1;
        while (true)
        {
            var extraPreserveId = Utils.getPreserveId(__instance, i);
            if (extraPreserveId is not null)
            {
                tags.Add($"extra_preserve_sheet_index_{i}_{ItemContextTagManager.SanitizeContextTag(extraPreserveId)}");
                i++;
            }
            else
            {
                break;
            }
        }
    }

    private static void Farmer_OnItemReceived_postfix(Farmer __instance, Item item, int countAdded, Item mergedIntoStack, bool hideHudNotification = false)
    {
        if (item.QualifiedItemId == HolderQualifiedId)
        {
            __instance.removeItemFromInventory(item);
        }
        if (item is SObject obj && obj.heldObject.Value is Chest chest)
        {
            __instance.addItemsByMenuIfNecessary(new List<Item>(chest.Items));
            obj.heldObject.Value = null;
        }
    }

    public static void Chest_addItem_postfix(Chest __instance, Item __result, Item item)
    {
        if (item.QualifiedItemId == HolderQualifiedId)
        {
            __instance.Items.Remove(item);
        }
        if (__result != null || item is not SObject { heldObject.Value: Chest chest } obj) return;
        foreach (var extraItem in chest.Items)
        {
            var leftoverItem = __instance.addItem(extraItem);
            if (leftoverItem != null)
            {
                Game1.createItemDebris(leftoverItem, __instance.TileLocation * 64f, -1, __instance.Location);
            }
        }
        obj.heldObject.Value = null;
    }

    // These two patches ensure GetOutputItem only performs side effects (ie removing items from
    // inventory for additional fuels) if it's called within the context of PlaceInMachine,
    // allowing GetOutputItem to be callable anywhere without ill effects.
    public static void SObject_PlaceInMachine_Prefix(MachineData machineData, Item inputItem, bool probe, Farmer who, bool showMessages = true, bool playSounds = true)
    {
        if (!probe) enableGetOutputItemSideEffect = true;
    }

    public static void SObject_PlaceInMachine_Postfix(MachineData machineData, Item inputItem, bool probe, Farmer who, bool showMessages = true, bool playSounds = true)
    {
        enableGetOutputItemSideEffect = false;
    }

    // If a machine ran out of automatic producing count, return false.
    public static void MachineDataUtility_CanApplyOutput_Postfix(ref bool __result, SObject machine, MachineOutputRule rule, MachineOutputTrigger trigger, Item inputItem, Farmer who, GameLocation location, ref MachineOutputTriggerRule triggerRule, ref bool matchesExceptCount)
    {
        if (trigger == MachineOutputTrigger.OutputCollected &&
            machine.modData.TryGetValue(AutomaticProduceCountRemainingKey, out var str) &&
            Int32.TryParse(str, out var automaticProduceCountRemaining) &&
            automaticProduceCountRemaining <= 0)
        {
            __result = false;
        }
    }

    public static void SObject_OutputMachine_Prefix(SObject __instance, MachineData machine, MachineOutputRule outputRule, Item inputItem, Farmer who, GameLocation location, bool probe, bool heldObjectOnly = false)
    {
        if (!probe) enableOutputMachineSideEffect = true;
    }
    public static void SObject_OutputMachine_Postfix(bool __result, SObject __instance, MachineData machine, MachineOutputRule outputRule, Item inputItem, Farmer who, GameLocation location, bool probe, bool heldObjectOnly = false)
    {
        enableOutputMachineSideEffect = false;
        if (!probe && __result &&
            __instance.modData.TryGetValue(AutomaticProduceCountRemainingKey, out var str) &&
            Int32.TryParse(str, out var automaticProduceCountRemaining))
        {
            __instance.modData[AutomaticProduceCountRemainingKey] = (automaticProduceCountRemaining - 1).ToString();
        }
    }

    public static void SObject_loadDisplayName_postfix(ref string __result, SObject __instance)
    {
        int i = 1;
        while (true)
        {
            var val = Utils.getPreserveId(__instance, i);
            if (val is not null)
            {
                string preserveDisplayName = (ItemRegistry.GetData("(O)" + val)?.DisplayName ?? "");
                __result = __result.Replace($"{ExtraPreservedDisplayNamePrefix}_{i}", preserveDisplayName);
                i++;
            }
            else
            {
                break;
            }
        }
    }

    public static void Slingshot_canThisBeAttached_postfix(ref bool __result, SObject o, int slot)
    {
        if (!__result &&
            ItemRegistry.GetDataOrErrorItem(o.QualifiedItemId).RawData is ObjectData objectData &&
            (objectData.CustomFields?.ContainsKey(SlingshotDamage) ?? false))
        {
            __result = true;
        }
    }

    public static void Slingshot_GetAmmoDamage_postfix(ref int __result, SObject? ammunition)
    {
        if (ammunition is not null && ItemRegistry.GetDataOrErrorItem(ammunition.QualifiedItemId).RawData is ObjectData objectData &&
            (objectData.CustomFields?.TryGetValue(SlingshotDamage, out var slingshotDamageStr) ?? false) &&
            Int32.TryParse(slingshotDamageStr, out var slingshotDamage))
        {
            __result = slingshotDamage;
        }
    }

    public static void Slingshot_GetAmmoCollisionSound_postfix(ref string __result, SObject? ammunition)
    {
        if (ammunition is not null && ItemRegistry.GetDataOrErrorItem(ammunition.QualifiedItemId).RawData is ObjectData objectData &&
            (objectData.CustomFields?.ContainsKey(SlingshotDamage) ?? false))
        {
            __result = (objectData.CustomFields?.ContainsKey(SlingshotExplosiveRadius) ?? false) ?
              "explosion" :
              "hammer";
        }
    }

    public static void Slingshot_GetAmmoCollisionBehavior_postfix(ref BasicProjectile.onCollisionBehavior? __result, SObject? ammunition)
    {
        if (ammunition is not null && __result is null && ItemRegistry.GetDataOrErrorItem(ammunition.QualifiedItemId).RawData is ObjectData objectData &&
            (objectData.CustomFields?.TryGetValue(SlingshotExplosiveRadius, out var slingshotExplosiveRadiusStr) ?? false) &&
            (objectData.CustomFields?.TryGetValue(SlingshotExplosiveDamage, out var slingshotExplosiveDamageStr) ?? false) &&
            Int32.TryParse(slingshotExplosiveRadiusStr, out var slingshotExplosiveRadius) &&
            Int32.TryParse(slingshotExplosiveDamageStr, out var slingshotExplosiveDamage))
        {
            __result = (GameLocation location, int x, int y, Character who) =>
            {
                location.explode(new Vector2(x / 64, y / 64), slingshotExplosiveRadius, who as Farmer, damage_amount: slingshotExplosiveDamage);
            };
        }
    }

    public static bool SObject_DisplayName_prefix(SObject __instance, ref string __result)
    {
        var item = Utils.GetActualItemForHolder(__instance);
        if (item is not null)
        {
            __result = item.DisplayName;
            return false;
        }
        return true;
    }

    public static bool SObject_getDescription_prefix(SObject __instance, ref string __result)
    {
        var item = Utils.GetActualItemForHolder(__instance);
        if (item is not null)
        {
            __result = item.getDescription();
            return false;
        }
        return true;
    }

    public static void Farmer_GetItemReceiveBehavior_postfix(Item item, ref bool needsInventorySpace, ref bool showNotification)
    {
        if (item.QualifiedItemId == HolderQualifiedId)
        {
            showNotification = false;
            if (item is SObject obj &&
                (obj.heldObject.Value is null || (obj.heldObject.Value is Chest chest && chest.Items.Count == 0)))
            {
                needsInventorySpace = false;
            }
        }
    }
    public static void ItemQueryResolver_ApplyItemFields_postfix(ref ISalable __result, ISalable item, int minStackSize, int maxStackSize, int toolUpgradeLevel, string objectInternalName, string objectDisplayName, string objectColor, int quality, bool isRecipe, List<QuantityModifier> stackSizeModifiers, QuantityModifier.QuantityModifierMode stackSizeModifierMode, List<QuantityModifier> qualityModifiers, QuantityModifier.QuantityModifierMode qualityModifierMode, Dictionary<string, string> modData, ItemQueryContext context, Item? inputItem = null)
    {
        if (__result is Clothing clothing && !string.IsNullOrWhiteSpace(objectColor))
        {
            Color? color = Utility.StringToColor(objectColor);
            if (color is not null)
            {
                clothing.Dye((Color)color);
            }
        }
    }

    static Item MakeWeeds(SObject? obj)
    {
        return obj ?? ItemRegistry.Create("(O)0");
    }

    public static IEnumerable<CodeInstruction> SObject_performObjectDropInAction_transpiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher matcher = new(instructions);
        // Old: if (!(dropInItem is Object @object)) return false;
        // New: if (!(dropInItem is Object @object)) @object = MakeWeeds();
        matcher.MatchEndForward(
            new CodeMatch(OpCodes.Ldarg_1),
            new CodeMatch(OpCodes.Isinst, typeof(SObject)),
            new CodeMatch(OpCodes.Stloc_0))
          //        new CodeMatch(OpCodes.Ldloc_0),
          //        new CodeMatch(OpCodes.Brtrue_S),
          //        new CodeMatch(OpCodes.Ldc_I4_0),
          //        new CodeMatch(OpCodes.Ret))
          .ThrowIfNotMatch($"Could not find entry point for {nameof(SObject_performObjectDropInAction_transpiler)}")
          .Advance(1)
          .InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldloc_0),
            new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(MachineHarmonyPatcher), nameof(MachineHarmonyPatcher.MakeWeeds))),
            new CodeInstruction(OpCodes.Stloc_0)
              );
        return matcher.InstructionEnumeration();
    }

    static void SObject_onReadyForHarvest_Postfix(SObject __instance, ref MachineEffects ____machineAnimation, ref bool ____machineAnimationLoop, ref int ____machineAnimationIndex, ref int ____machineAnimationFrame, ref int ____machineAnimationInterval)
    {
        if (__instance.GetMachineData()?.CustomFields?.TryGetValue(TriggerActionToRunWhenReady, out var action) ?? false)
        {
            if (!TriggerActionManager.TryRunAction(action, out string error, out Exception ex))
            {
                ModEntry.StaticMonitor.Log($"Failed running action '{action}' after machine is ready: {error} (exception: {ex})",
                    LogLevel.Warn);
            }
        }
        if (__instance.modData.TryGetValue(TriggerActionToRunWhenReady, out var action2))
        {
            if (!TriggerActionManager.TryRunAction(action2, out string error, out Exception ex))
            {
                ModEntry.StaticMonitor.Log($"Failed running action '{action2}' after machine is ready: {error} (exception: {ex})",
                    LogLevel.Warn);
            }
            __instance.modData.Remove(TriggerActionToRunWhenReady);
        }
        // Slime incubator stuff
        if (__instance.GetMachineData()?.CustomFields?.ContainsKey(IsCustomSlimeIncubator) ?? false)
        {
            var location = __instance.Location;
            if (location.canSlimeHatchHere())
            {
                GreenSlime? greenSlime = null;
                Vector2 position = new Vector2((int)__instance.TileLocation.X, (int)__instance.TileLocation.Y + 1) * 64f;
                switch (__instance.heldObject.Value.QualifiedItemId)
                {
                    case "(O)680":
                        greenSlime = new GreenSlime(position, 0);
                        break;
                    case "(O)413":
                        greenSlime = new GreenSlime(position, 40);
                        break;
                    case "(O)437":
                        greenSlime = new GreenSlime(position, 80);
                        break;
                    case "(O)439":
                        greenSlime = new GreenSlime(position, 121);
                        break;
                    case "(O)857":
                        greenSlime = new GreenSlime(position, 121);
                        greenSlime.makeTigerSlime();
                        break;
                }
                if (greenSlime != null)
                {
                    Game1.showGlobalMessage(greenSlime.cute.Value ? Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12689") : Game1.content.LoadString("Strings\\StringsFromCSFiles:Object.cs.12691"));
                    Vector2 vector = Utility.recursiveFindOpenTileForCharacter(greenSlime, location, __instance.TileLocation + new Vector2(0f, 1f), 10, allowOffMap: false);
                    greenSlime.setTilePosition((int)vector.X, (int)vector.Y);
                    location.characters.Add(greenSlime);
                    __instance.ResetParentSheetIndex();
                    __instance.heldObject.Value = null;
                    __instance.MinutesUntilReady = -1;
                }
            }
            else
            {
                __instance.MinutesUntilReady = Utility.CalculateMinutesUntilMorning(Game1.timeOfDay);
                __instance.readyForHarvest.Value = false;
            }
        }
        // ready effects stuff
        if (ModEntry.extraMachineDataAssetHandler.data.TryGetValue(__instance.QualifiedItemId, out var extraMachineData))
        {
            foreach (ReadyEffects readyEffects in extraMachineData.ReadyEffects)
            {
                if (__instance.PlayMachineEffect(readyEffects, true))
                {
                    ____machineAnimation = readyEffects;
                    ____machineAnimationLoop = false;
                    ____machineAnimationIndex = 0;
                    ____machineAnimationFrame = -1;
                    ____machineAnimationInterval = 0;
                    if (readyEffects.IncrementMachineParentSheetIndex > 0)
                    {
                        __instance.ResetParentSheetIndex();
                        __instance.ParentSheetIndex += readyEffects.IncrementMachineParentSheetIndex;
                    }
                    break;
                }
            }
        }
    }

    static void Cask_IsValidCaskLocation_Postfix(Cask __instance, ref bool __result)
    {
        if (__instance.GetMachineData()?.CustomFields?.ContainsKey(CaskWorksAnywhere) ?? false)
        {
            __result = true;
        }
    }

    static bool SObject_placementAction_Prefix(SObject __instance, ref bool __result, GameLocation location, int x, int y, Farmer? who = null)
    {
        if (__instance.GetMachineData()?.CustomFields?.ContainsKey(IsCustomCask) ?? false)
        {
            Vector2 vector = new Vector2(x / 64, y / 64);
            var itemId = __instance.ItemId;
            var cask = new Cask(vector);
            cask.ItemId = itemId;
            if (Game1.bigCraftableData.TryGetValue(itemId, out var value))
            {
                cask.name = value.Name ?? ItemRegistry.GetDataOrErrorItem($"(BC){itemId}").InternalName;
                cask.Price = value.Price;
                cask.Type = "Crafting";
                cask.Category = -9;
                cask.setOutdoors.Value = value.CanBePlacedOutdoors;
                cask.setIndoors.Value = value.CanBePlacedIndoors;
                cask.Fragility = value.Fragility;
                cask.isLamp.Value = value.IsLamp;
            }
            cask.ResetParentSheetIndex();
            location.objects.Add(vector, cask);
            location.playSound("hammer");
            __result = true;
            return false;
        }
        return true;
    }

    public static void Cask_checkForMaturity_Postfix(Cask __instance)
    {
        if (__instance.GetMachineData()?.CustomFields?.ContainsKey(AllowMoreThanOneQualityIncrement) ?? false)
        {
            var produce = __instance.heldObject.Value;
            while (__instance.daysToMature.Value <= __instance.GetDaysForQuality(__instance.GetNextQuality(produce.Quality)))
            {
                __instance.heldObject.Value.Quality = __instance.GetNextQuality(produce.Quality);
                if (produce.Quality == 4)
                {
                    __instance.MinutesUntilReady = 1;
                    return;
                }
            }
        }
    }

    public static void SObject_performRemoveAction_Postfix(SObject __instance)
    {
        if (__instance.heldObject.Value is not null &&
            !__instance.readyForHarvest.Value &&
            (__instance.GetMachineData()?.CustomFields?.ContainsKey(ReturnInput) ?? false))
        {
            __instance.Location.debris.Add(new Debris(__instance.heldObject.Value, __instance.TileLocation * 64f + new Vector2(32f, 32f)));
        }
    }

    public static void SObject_draw_Postfix(SObject __instance, SpriteBatch spriteBatch, int x, int y, float alpha, int ____machineAnimationFrame, MachineEffects ____machineAnimation)
    {
        if (__instance.isTemporarilyInvisible || !__instance.bigCraftable.Value || __instance.heldObject.Value is null) return;
        if ((__instance.GetMachineData()?.CustomFields?.TryGetValue(DrawLayerTexture, out var drawLayerTexture) ?? false))
        {
            int drawLayerTextureIndex = 0;
            if (!(__instance.GetMachineData()?.CustomFields?.TryGetValue(DrawLayerTextureIndex, out var drawLayerTextureIndexStr) ?? false) ||
                !Int32.TryParse(drawLayerTextureIndexStr, out drawLayerTextureIndex))
            {
                drawLayerTextureIndex = 0;
            }
            var offset = __instance.ParentSheetIndex - ItemRegistry.GetDataOrErrorItem(__instance.QualifiedItemId).SpriteIndex;
            var color = TailoringMenu.GetDyeColor(__instance.heldObject.Value) ?? Color.White;
            int animationOffset = 0;
            if (__instance.showNextIndex.Value)
            {
                animationOffset = 1;
            }
            if (____machineAnimationFrame >= 0 && ____machineAnimation != null)
            {
                animationOffset = ____machineAnimationFrame;
            }
            Vector2 vector = __instance.getScale();
            vector *= 4f;
            Vector2 vector2 = Game1.GlobalToLocal(Game1.viewport, new Vector2(x * 64, y * 64 - 64));
            Rectangle destinationRectangle = new Microsoft.Xna.Framework.Rectangle((int)(vector2.X - vector.X / 2f) + ((__instance.shakeTimer > 0) ? Game1.random.Next(-1, 2) : 0), (int)(vector2.Y - vector.Y / 2f) + ((__instance.shakeTimer > 0) ? Game1.random.Next(-1, 2) : 0), (int)(64f + vector.X), (int)(128f + vector.Y / 2f));
            Texture2D texture = GetTexture(drawLayerTexture);
            float layer = Math.Max(0f, (float)((y + 1) * 64 - 24) / 10000f) + (float)x * 1E-05f + 1E-06f;
            spriteBatch.Draw(
                texture,
                destinationRectangle,
                SObject.getSourceRectForBigCraftable(texture, drawLayerTextureIndex + animationOffset + offset), color * alpha, 0f, Vector2.Zero, SpriteEffects.None, layer);
        }
    }

    static Dictionary<string, Texture2D> textures = new();
    static Texture2D GetTexture(string name)
    {
        if (textures.TryGetValue(name, out var texture))
        {
            return texture;
        }
        try
        {
            textures[name] = Game1.content.Load<Texture2D>(name);
            return textures[name];
        }
        catch (Exception e)
        {
            ModEntry.StaticMonitor.Log($"Error loading texture {name}: {e.ToString()}", LogLevel.Error);
            return Game1.bigCraftableSpriteSheet;
        }
    }

    public static void Buff_OnRemoved_Postfix(Buff __instance)
    {
        var targetItem = ItemRegistry.Create("(O)0");
        targetItem.modData[$"{ModEntry.UniqueId}_BuffName"] = __instance.source ?? "";
        targetItem.modData[$"{ModEntry.UniqueId}_BuffId"] = __instance.id ?? "";
        TriggerActionManager.Raise($"{ModEntry.UniqueId}_BuffRemoved", targetItem: targetItem);
    }
}
