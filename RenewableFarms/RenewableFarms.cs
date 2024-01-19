using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

public class RenewableFarms : Mod
{
    Harmony harmony;
    public void Start()
    {
        ExtentionMethods.modified = new List<PickupItem>();
        ExtentionMethods.yields = new Dictionary<Item_Base, SO_ItemYield>();
        Patch_RestoreYield.yields = new List<YieldHandler>();
        harmony = new Harmony("com.aidanamite.RenewableFarms");
        harmony.PatchAll();
        foreach (var p in FindObjectsOfType<Plant>())
            if (p.FullyGrown() && p.pickupComponent != null)
                p.pickupComponent.Modify();
        foreach (var p in FindObjectsOfType<HarvestableTree>())
            if (p.GetComponent<LandmarkItem_PickupItem>() != null)
                p.GetComponent<PickupItem>().Modify();
        Log("Mod has been loaded!");
    }

    public void OnModUnload()
    {
        harmony.UnpatchAll(harmony.Id);
        Log("Mod has been unloaded!");
    }

    public override void WorldEvent_WorldLoaded()
    {
        var b = true;
        foreach (var y in Patch_RestoreYield.yields)
            if (y.Yield.Count == y.yieldAsset.yieldAssets.Count)
                b = false;
        if (b)
            foreach (var y in Patch_RestoreYield.yields)
                y.AddLast();
    }

    [ConsoleCommand(name: "patchPlants", docs: "Due to the changes made by the Renewable Farms mod, you will want to run this command to fix any plants that were made before the mod was installed")]
    public static string MyCommand(string[] args)
    {
        foreach (var p in FindObjectsOfType<Plant>())
            if (p.FullyGrown() && p.pickupComponent != null)
                p.pickupComponent.yieldHandler.AddLast();
        foreach (var p in FindObjectsOfType<HarvestableTree>())
            if (p.GetComponent<LandmarkItem_PickupItem>() != null)
                p.GetComponent<PickupItem>().yieldHandler.AddLast();
        return "Plants modified";
    }
}

static class ExtentionMethods
{
    public static List<PickupItem> modified;
    public static Dictionary<Item_Base, SO_ItemYield> yields;
    public static void Modify(this PickupItem pickup)
    {
        if (!modified.AddUniqueOnly(pickup))
            return;
        Item_Base seedItem = pickup.GetSeed(out bool isChance);
        if (!isChance || pickup.yieldHandler == null || pickup.yieldHandler.Yield == null)
            return;
        pickup.yieldHandler.Yield.Add(new Cost(seedItem, 1));
        if (!yields.ContainsKey(seedItem))
        {
            var newYield = ScriptableObject.CreateInstance<SO_ItemYield>();
            var newYields = new List<Cost>(pickup.yieldHandler.yieldAsset.yieldAssets);
            newYields.Add(new Cost(seedItem, 1));
            newYield.yieldAssets = newYields;
            yields.Add(seedItem, newYield);
            pickup.yieldHandler.yieldAsset = newYield;
        } else
            pickup.yieldHandler.yieldAsset = yields[seedItem];
        if (pickup.GetComponent<HarvestableTree>() == null)
        {
            var amount = Traverse.Create(pickup.dropper).Field("amountOfItems").GetValue<Interval_Int>();
            if (amount.minValue > 0)
                amount.minValue--;
            if (amount.maxValue > 0)
                amount.maxValue--;
        }
        else
        {
            var rand = Traverse.Create(pickup.dropper).Field("randomDropperAsset").GetValue<SO_RandomDropper>().randomizer;
            foreach (var drop in rand.items)
                if (drop.obj != null && (Item_Base)drop.obj == seedItem)
                    drop.weight /= 2;
        }
    }

    public static Item_Base GetSeed(this PickupItem pickup, out bool isChance)
    {
        isChance = false;
        Item_Base seed = null;
        var plant = pickup.GetComponent<Plant>();
        if (plant != null)
            seed = plant.item;
        if (pickup.yieldHandler != null)
            foreach (var yield in pickup.yieldHandler.yieldAsset.yieldAssets)
                if ((seed != null) ? (yield.item == seed) : yield.item.UniqueName.Contains("Seed", System.StringComparison.OrdinalIgnoreCase))
                    return yield.item;
        if (pickup.itemInstance != null && pickup.itemInstance.Valid)
            if ((seed != null) ? (pickup.itemInstance.baseItem == seed) : pickup.itemInstance.UniqueName.Contains("Seed", System.StringComparison.OrdinalIgnoreCase))
                return pickup.itemInstance.baseItem;
        if (pickup.dropper != null)
            foreach (var drop in Traverse.Create(pickup.dropper).Field("randomDropperAsset").GetValue<SO_RandomDropper>().randomizer.items)
                if (drop.obj != null && ((seed != null) ? ((Item_Base)drop.obj == seed) : ((Item_Base)drop.obj).UniqueName.Contains("Seed", System.StringComparison.OrdinalIgnoreCase)))
                {
                    isChance = true;
                    return (Item_Base)drop.obj;
                }
        return seed;
    }

    public static void AddLast(this YieldHandler yield)
    {
        if (yield.Yield.Count < yield.yieldAsset.yieldAssets.Count)
            yield.Yield.Add(yield.yieldAsset.yieldAssets[yield.yieldAsset.yieldAssets.Count - 1]);
    }
}

[HarmonyPatch(typeof(Plant), "Complete")]
class Patch_NewPlant
{
    private static void Prefix(Plant __instance)
    {
        if (__instance.pickupComponent != null)
            __instance.pickupComponent.Modify();
    }
}

[HarmonyPatch(typeof(YieldHandler), "RestoreYield")]
class Patch_RestoreYield
{
    public static List<YieldHandler> yields;
    private static void Prefix(YieldHandler __instance)
    {
        var plant = __instance.GetComponent<Plant>();
        var tree = __instance.GetComponent<HarvestableTree>();
        var landmark = __instance.GetComponent<LandmarkItem_PickupItem>();
        var pickup = __instance.GetComponent<PickupItem>();
        if (pickup != null && (plant != null || (tree != null && landmark != null)))
            yields.Add(__instance);
    }
}

[HarmonyPatch(typeof(LandmarkItem), "AssignIndexAndTrack")]
class Patch_NewTree
{
    private static void Postfix(LandmarkItem __instance)
    {
        if (__instance is LandmarkItem_PickupItem && __instance.GetComponent<HarvestableTree>() != null)
            __instance.GetComponent<PickupItem>().Modify();
    }
}