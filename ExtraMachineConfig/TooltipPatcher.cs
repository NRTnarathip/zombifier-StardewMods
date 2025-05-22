using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;
using StardewValley.Menus;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Selph.StardewMods.ExtraMachineConfig;

using SObject = StardewValley.Object;

sealed class TooltipPatcher
{
    public static void ApplyPatches(Harmony harmony)
    {
        try
        {
            harmony.Patch(
                original: AccessTools.DeclaredMethod(typeof(BuffEffects),
                  nameof(BuffEffects.ToLegacyAttributeFormat)),
                postfix: new HarmonyMethod(typeof(TooltipPatcher), nameof(TooltipPatcher.BuffEffects_ToLegacyAttributeFormat_Postfix)));

            //harmony.Patch(
            //    original: AccessTools.DeclaredMethod(typeof(IClickableMenu),
            //      nameof(IClickableMenu.drawToolTip)),
            //    transpiler: new HarmonyMethod(typeof(TooltipPatcher), nameof(TooltipPatcher.IClickableMenu_drawToolTip_Transpiler)));

            // High priority to run before SpaceCore's own transpilers...
            // android fixed, just temp
            //  harmony.Patch(
            //      original: AccessTools.DeclaredMethod(typeof(IClickableMenu),
            //        nameof(IClickableMenu.drawHoverText),
            //        new Type[] {
            //typeof(SpriteBatch),
            //typeof(StringBuilder),
            //typeof(SpriteFont),
            //typeof(int),
            //typeof(int),
            //typeof(int),
            //typeof(string),
            //typeof(int),
            //typeof(string[]),
            //typeof(Item),
            //typeof(int),
            //typeof(string),
            //typeof(int),
            //typeof(int),
            //typeof(int),
            //typeof(float),
            //typeof(CraftingRecipe),
            //typeof(IList<Item>),
            //typeof(Texture2D),
            //typeof(Microsoft.Xna.Framework.Rectangle?),
            //typeof(Color?),
            //typeof(Color?),
            //typeof(float),
            //typeof(int),
            //typeof(int),

            //// fixed: int stackNumber = -1
            //typeof(int),

            //        }),
            //      transpiler: new HarmonyMethod(AccessTools.Method(typeof(TooltipPatcher), nameof(TooltipPatcher.IClickableMenu_drawHoverText_Transpiler)), Priority.High));
        }
        catch (Exception e)
        {
            ModEntry.StaticMonitor.Log($"Failed patching in extra tooltip/buff icons: {e.ToString()}", LogLevel.Error);
        }
    }

    // Makes combat level shows up. Not that it does anything...
    static void BuffEffects_ToLegacyAttributeFormat_Postfix(BuffEffects __instance, ref string[] __result)
    {
        __result[3] = ((int)__instance.CombatLevel.Value).ToString();
    }

    static float[]? GetExtraBuffs(Item? hoveredItem)
    {
        if (hoveredItem is null || !Game1.objectData.TryGetValue(hoveredItem.ItemId, out var objectData)) return null;
        BuffEffects buffEffects = new BuffEffects();
        foreach (Buff item in SObject.TryCreateBuffsFromData(objectData, hoveredItem.Name, hoveredItem.DisplayName, 1f, hoveredItem.ModifyItemBuffs))
        {
            buffEffects.Add(item.effects);
        }
        return new[] {
      buffEffects.AttackMultiplier.Value,
      buffEffects.Immunity.Value,
      buffEffects.KnockbackMultiplier.Value,
      buffEffects.WeaponSpeedMultiplier.Value,
      buffEffects.CriticalChanceMultiplier.Value,
      buffEffects.CriticalPowerMultiplier.Value,
      buffEffects.WeaponPrecisionMultiplier.Value,
    };
    }

    static int GetAdjustedHeight(Item? hoveredItem, int height)
    {
        var extraBuffs = GetExtraBuffs(hoveredItem);
        if (extraBuffs is null) return height;
        foreach (float value in extraBuffs)
        {
            if (value != 0f)
            {
                height += 39;
            }
        }
        return height;
    }

    static int GetAdjustedWidth(Item? hoveredItem, int width, SpriteFont font)
    {
        var extraBuffs = GetExtraBuffs(hoveredItem);
        if (extraBuffs is null) return width;
        for (int i = 0; i <= 6; i++)
        {
            float value = extraBuffs[i];
            if (value != 0f)
            {
                width = (int)Math.Max(width, font.MeasureString(Game1.content.LoadString($"{ModEntry.BuffStringsAssetName}:ItemHoverBuff{i}", 9999)).X + (float)92);
            }
        }
        return width;
    }

    static int GetXFor(int k)
    {
        // the farming icon is 0
        return k switch
        {
            // attack multiplier
            0 => 11,
            // immunity
            1 => 14,
            // knockback multiplier
            2 => 6,
            // weapon speed multiplier
            3 => 12,
            // critical chance multiplier
            4 => 15,
            // critical power multiplier
            5 => 15,
            // weapon precision multiplier
            6 => 3,
            7 => 3,
            _ => 0,
        } * 10;
    }

    static void DrawExtraBuffs(SpriteBatch b, SpriteFont font, Item hoveredItem, int x, ref int y)
    {
        var extraBuffs = GetExtraBuffs(hoveredItem);
        if (extraBuffs is null) return;
        for (int k = 0; k <= 6; k++)
        {
            if (extraBuffs[k] == 0f) continue;
            Utility.drawWithShadow(
                b,
                Game1.mouseCursors,
                new Vector2(x + 16 + 4, y + 16),
                new Microsoft.Xna.Framework.Rectangle(10 + GetXFor(k), 428, 10, 10),
                Color.White,
                0f,
                Vector2.Zero,
                3f,
                flipped: false,
                0.95f);
            string text =
              k == 1 ?
              (((Convert.ToDouble(extraBuffs[k]) > 0.0) ? "+" : "") + extraBuffs[k]) :
              (((Convert.ToDouble(extraBuffs[k]) > 0.0) ? "+" : "") + extraBuffs[k] * 100 + "%");
            text = Game1.content.LoadString($"{ModEntry.BuffStringsAssetName}:ItemHoverBuff{k}", text);
            Utility.drawTextWithShadow(b, text, font, new Vector2(x + 16 + 34 + 4, y + 16), Game1.textColor);
            y += 39;
        }
    }

    // Adjust height and width for our extra buffs, and draw them
    public static IEnumerable<CodeInstruction> IClickableMenu_drawHoverText_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher matcher = new(instructions);
        // For the height and width adjustment, find where "buffIconsToDisplay != null" is being called the first two times, and insert code after it
        matcher.MatchEndForward(
          new CodeMatch(OpCodes.Ldarg_S, (byte)8),
          new CodeMatch(OpCodes.Brfalse_S))
        .ThrowIfNotMatch($"Could not find entry point for height adjustment portion of {nameof(IClickableMenu_drawHoverText_Transpiler)}")
        .Advance(1)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldarg_S, (byte)9),
          new CodeInstruction(OpCodes.Ldloc_2),
          new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TooltipPatcher), nameof(GetAdjustedHeight))),
          new CodeInstruction(OpCodes.Stloc_2)
        );

        matcher.MatchEndForward(
          new CodeMatch(OpCodes.Ldarg_S, (byte)8),
          new CodeMatch(OpCodes.Brfalse_S))
        .ThrowIfNotMatch($"Could not find entry point for width adjustment portion of {nameof(IClickableMenu_drawHoverText_Transpiler)}")
        .Advance(1)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldarg_S, (byte)9),
          new CodeInstruction(OpCodes.Ldloc_1),
          new CodeInstruction(OpCodes.Ldarg_2),
          new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TooltipPatcher), nameof(GetAdjustedWidth))),
          new CodeInstruction(OpCodes.Stloc_1)
        );

        // Now we draw
        // Find where (k == 12) is being checked (aka we're drawing the buff duration), and insert code before the duration draw
        matcher
        .End()
        .MatchStartBackwards(
          new CodeMatch(OpCodes.Ldstr, "Strings\\UI:ItemHover_Buff"))
        .MatchStartBackwards(
          new CodeMatch(OpCodes.Ldloc_S),
          new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)12),
          new CodeMatch(OpCodes.Bne_Un))
        .ThrowIfNotMatch($"Could not find entry point for drawing portion of {nameof(IClickableMenu_drawHoverText_Transpiler)}");
        // We do this to not break Glow Buff's transpiler, weh
        var instructionsToDuplicate = matcher.Instructions(3);
        matcher
        .InsertAndAdvance(instructionsToDuplicate)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldarg_0),
          new CodeInstruction(OpCodes.Ldarg_2),
          new CodeInstruction(OpCodes.Ldarg_S, (byte)9),
          // x and ref y
          new CodeInstruction(OpCodes.Ldloc_S, (byte)5),
          new CodeInstruction(OpCodes.Ldloca_S, (byte)6),
          new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TooltipPatcher), nameof(DrawExtraBuffs)))
        );

        return matcher.InstructionEnumeration();
    }

    // Pushes the time to the end
    public static IEnumerable<CodeInstruction> IClickableMenu_drawToolTip_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher matcher = new(instructions);
        // Old: array[12] = " " + Utility.getMinutesSecondsStringFromMilliseconds(num);
        // Old: array[19] = " " + Utility.getMinutesSecondsStringFromMilliseconds(num);
        matcher.MatchStartForward(
          new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Utility), nameof(Utility.getMinutesSecondsStringFromMilliseconds))))
        .MatchStartBackwards(
          new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)12))
        .ThrowIfNotMatch($"Could not find entry point for {nameof(IClickableMenu_drawToolTip_Transpiler)}")
        .RemoveInstruction()
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)19));
        return matcher.InstructionEnumeration();
    }

    //public static IEnumerable<CodeInstruction> IClickableMenu_drawHoverText_Transpiler_Old(IEnumerable<CodeInstruction> instructions) {
    //  // Make the "find longest tooltip" iterate to 19 instead of just 12
    //  CodeMatcher matcher = new(instructions);
    //  // Old: j <= 12
    //  // Old: j <= 19
    //  matcher.MatchStartForward(
    //    new CodeMatch(OpCodes.Ldstr, "Strings\\UI:ItemHover_Buff"))
    //  .MatchStartBackwards(
    //    // This should be 11: https://forums.stardewvalley.net/threads/food-tooltip-wider-than-expected-because-of-index-bug.41365/
    //    new CodeMatch(OpCodes.Ldloc_S),
    //    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)12),
    //    new CodeMatch(OpCodes.Bgt_S)
    //    )
    //  .ThrowIfNotMatch($"Could not find entry point for 1st portion of {nameof(IClickableMenu_drawHoverText_Transpiler)}")
    //  .Advance(1)
    //  .RemoveInstruction()
    //  .InsertAndAdvance(
    //    new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)18));

    //  // Change the reference to the duration in buffsIconsToDisplay from 12 to 19
    //  // Old: k == 12
    //  // New: k == 19
    //  matcher.MatchStartForward(
    //    new CodeMatch(OpCodes.Ldstr, "Strings\\UI:ItemHover_Buff"))
    //  .Advance(1)
    //  .MatchStartForward(
    //    new CodeMatch(OpCodes.Ldstr, "Strings\\UI:ItemHover_Buff"))
    //  .MatchStartBackwards(
    //    new CodeMatch(OpCodes.Ldloc_S),
    //    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)12),
    //    new CodeMatch(OpCodes.Bne_Un))
    //  .ThrowIfNotMatch($"Could not find entry point for 2nd portion of {nameof(IClickableMenu_drawHoverText_Transpiler)}");
    //  var addressOfK = matcher.Operand;
    //  matcher
    //  .Advance(1)
    //  .RemoveInstruction()
    //  .InsertAndAdvance(
    //    new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)19));

    //  // Change the source rect dynamically based on what k is, because cursors doesn't have enough icons for the new buffs
    //  matcher.MatchStartForward(
    //    new CodeMatch(OpCodes.Ldstr, "Strings\\UI:ItemHover_Buff"))
    //  .MatchStartBackwards(
    //    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Utility), nameof(Utility.drawWithShadow))))
    //  .MatchEndBackwards(
    //    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)10),
    //    new CodeMatch(OpCodes.Ldloc_S),
    //    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)10),
    //    new CodeMatch(OpCodes.Mul),
    //    new CodeMatch(OpCodes.Add))
    //  .ThrowIfNotMatch($"Could not find entry point for 3rd portion of {nameof(IClickableMenu_drawHoverText_Transpiler)}")
    //  .Advance(1)
    //  .InsertAndAdvance(
    //    new CodeInstruction(OpCodes.Ldloc_S, addressOfK),
    //    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TooltipPatcher), nameof(GetXAdjustment))),
    //    new CodeInstruction(OpCodes.Add)
    //  );

    //  // Extend ItemHover_Buffs string draw from 11 to 18
    //  // Old: k <= 11
    //  // New: k <= 18
    //  matcher.MatchStartForward(
    //    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)11))
    //  .ThrowIfNotMatch($"Could not find entry point for 4th portion of {nameof(IClickableMenu_drawHoverText_Transpiler)}")
    //  .RemoveInstruction()
    //  .InsertAndAdvance(
    //    new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)18));

    //  return matcher.InstructionEnumeration();
    //}

    //static int GetXAdjustment(int k) {
    //  if (k < 12) return 0;
    //  // the farming icon is 0
    //  return (-k + k switch {
    //    // attack multiplier
    //    12 => 11,
    //    // immunity
    //    13 => 14,
    //    // knockback multiplier
    //    14 => 6,
    //    // weapon speed multiplier
    //    15 => 12,
    //    // critical chance multiplier
    //    16 => 15,
    //    // critical power multiplier
    //    17 => 15,
    //    // weapon precision multiplier
    //    18 => 3,
    //    _ => 0,
    //  }) * 10;
    //}
}
