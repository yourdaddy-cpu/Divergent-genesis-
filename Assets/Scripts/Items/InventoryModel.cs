using System;
using System.Collections.Generic;
using UnityEngine;

namespace DivergentGenesis.Items
{
    /// <summary>36 slots: 0-8 are the hotbar, 9-35 are the backpack.</summary>
    [Serializable]
    public class InventoryModel
    {
        public const int HotbarSize = 9;
        public const int MainSize = 27;
        public const int TotalSize = HotbarSize + MainSize;

        public ItemStack[] Slots = new ItemStack[TotalSize];
        public int Selected;

        public event Action Changed;

        public ItemStack SelectedStack
        {
            get { return Slots[Mathf.Clamp(Selected, 0, HotbarSize - 1)]; }
        }

        public void Notify() { if (Changed != null) Changed(); }

        // ------------------------------------------------------------- mutation
        /// <summary>Adds items, returns whatever did not fit.</summary>
        public int Add(ItemId id, int count)
        {
            if (id == ItemId.None || count <= 0) return 0;
            int max = ItemDef.Get(id).MaxStack;
            int remaining = count;

            // top up partial stacks first (hotbar first so tools stay reachable)
            for (int i = 0; i < TotalSize && remaining > 0; i++)
            {
                if (Slots[i].IsEmpty || Slots[i].Id != id || Slots[i].Count >= max) continue;
                int space = max - Slots[i].Count;
                int take = Mathf.Min(space, remaining);
                Slots[i].Count += take;
                remaining -= take;
            }

            for (int i = 0; i < TotalSize && remaining > 0; i++)
            {
                if (!Slots[i].IsEmpty) continue;
                int take = Mathf.Min(max, remaining);
                Slots[i] = new ItemStack(id, take);
                remaining -= take;
            }

            Notify();
            return remaining;
        }

        public bool Has(ItemId id, int count)
        {
            int total = 0;
            for (int i = 0; i < TotalSize; i++)
                if (Slots[i].Id == id) total += Slots[i].Count;
            return total >= count;
        }

        public int CountOf(ItemId id)
        {
            int total = 0;
            for (int i = 0; i < TotalSize; i++)
                if (Slots[i].Id == id) total += Slots[i].Count;
            return total;
        }

        public bool Remove(ItemId id, int count)
        {
            if (!Has(id, count)) return false;
            int remaining = count;
            for (int i = 0; i < TotalSize && remaining > 0; i++)
            {
                if (Slots[i].Id != id) continue;
                int take = Mathf.Min(Slots[i].Count, remaining);
                Slots[i].Count -= take;
                remaining -= take;
                if (Slots[i].Count <= 0) Slots[i] = ItemStack.Empty;
            }
            Notify();
            return true;
        }

        public void ConsumeSelected(int count = 1)
        {
            int i = Mathf.Clamp(Selected, 0, HotbarSize - 1);
            if (Slots[i].IsEmpty) return;
            Slots[i].Count -= count;
            if (Slots[i].Count <= 0) Slots[i] = ItemStack.Empty;
            Notify();
        }

        public void SetSlot(int index, ItemStack stack)
        {
            if (index < 0 || index >= TotalSize) return;
            Slots[index] = stack;
            Notify();
        }

        public void Swap(int a, int b)
        {
            if (a < 0 || b < 0 || a >= TotalSize || b >= TotalSize) return;
            var tmp = Slots[a]; Slots[a] = Slots[b]; Slots[b] = tmp;
            Notify();
        }

        public void Clear()
        {
            for (int i = 0; i < TotalSize; i++) Slots[i] = ItemStack.Empty;
            Selected = 0;
            Notify();
        }

        public void MoveOrMerge(int from, int to)
        {
            if (from < 0 || to < 0 || from >= TotalSize || to >= TotalSize || from == to) return;
            if (Slots[from].IsEmpty) return;

            if (!Slots[to].IsEmpty && Slots[to].Id == Slots[from].Id)
            {
                int max = Slots[to].MaxStack;
                int space = max - Slots[to].Count;
                int take = Mathf.Min(space, Slots[from].Count);
                if (take > 0)
                {
                    Slots[to].Count += take;
                    Slots[from].Count -= take;
                    if (Slots[from].Count <= 0) Slots[from] = ItemStack.Empty;
                    Notify();
                    return;
                }
            }
            Swap(from, to);
        }

        // ------------------------------------------------------- starting kit
        public void GiveStartingKit()
        {
            Clear();
            Add(ItemId.WoodenPickaxe, 1);
            Add(ItemId.WoodenAxe, 1);
            Add(ItemId.WoodenSword, 1);
            Add(ItemId.Torch, 16);
            Add(ItemId.Bread, 6);
            Add(ItemId.CraftingTable, 1);
            Selected = 0;
            Notify();
        }
    }

    /// <summary>One crafting recipe.</summary>
    public sealed class Recipe
    {
        public string Name;
        public ItemId Result;
        public int ResultCount = 1;
        public string[] Pattern;          // rows of chars, ' ' = empty
        public bool NeedsTable;           // requires a CraftingTable nearby

        public int Width { get { return Pattern != null && Pattern.Length > 0 ? Pattern[0].Length : 0; } }
        public int Height { get { return Pattern != null ? Pattern.Length : 0; } }
    }

    /// <summary>Shaped crafting plus a pile of smelting recipes.</summary>
    public static class Recipes
    {
        public static readonly List<Recipe> All = Build();

        private static Recipe R(string name, ItemId result, int count, bool table, params string[] pattern)
        {
            return new Recipe { Name = name, Result = result, ResultCount = count, Pattern = pattern, NeedsTable = table };
        }

        private static List<Recipe> Build()
        {
            var list = new List<Recipe>
            {
                // --- wood chain ----------------------------------------------------
                R("Oak Planks", ItemId.Planks, 4, false, "L"),
                R("Stick", ItemId.Stick, 4, false, "P", "P"),
                R("Crafting Table", ItemId.CraftingTable, 1, false, "PP", "PP"),
                R("Furnace", ItemId.Furnace, 1, false, "CCC", "C C", "CCC"),
                R("Torch", ItemId.Torch, 4, false, "C", "S"),

                // --- stone tools ----------------------------------------------------
                R("Wooden Sword", ItemId.WoodenSword, 1, false, "P", "P", "S"),
                R("Wooden Pickaxe", ItemId.WoodenPickaxe, 1, false, "PPP", " S ", " S "),
                R("Wooden Axe", ItemId.WoodenAxe, 1, false, "PP", "PS", " S"),
                R("Wooden Shovel", ItemId.WoodenShovel, 1, false, "P", "S", "S"),
                R("Stone Sword", ItemId.StoneSword, 1, true, "P", "P", "C"),
                R("Stone Pickaxe", ItemId.StonePickaxe, 1, true, "CCC", " S ", " S "),
                R("Stone Axe", ItemId.StoneAxe, 1, true, "CC", "CS", " S"),
                R("Stone Shovel", ItemId.StoneShovel, 1, true, "C", "S", "S"),

                // --- iron tools -----------------------------------------------------
                R("Iron Sword", ItemId.IronSword, 1, true, "P", "P", "I"),
                R("Iron Pickaxe", ItemId.IronPickaxe, 1, true, "III", " S ", " S "),
                R("Iron Axe", ItemId.IronAxe, 1, true, "II", "IS", " S"),
                R("Iron Shovel", ItemId.IronShovel, 1, true, "I", "S", "S"),

                // --- diamond tools --------------------------------------------------
                R("Diamond Sword", ItemId.DiamondSword, 1, true, "P", "P", "D"),
                R("Diamond Pickaxe", ItemId.DiamondPickaxe, 1, true, "DDD", " S ", " S "),
                R("Diamond Axe", ItemId.DiamondAxe, 1, true, "DD", "DS", " S"),
                R("Diamond Shovel", ItemId.DiamondShovel, 1, true, "D", "S", "S"),

                // --- blocks ----------------------------------------------------------
                R("Stone Bricks", ItemId.StoneBricks, 4, true, "SS", "SS"),
                R("Bricks", ItemId.Bricks, 1, true, "SS", "SS"),
                R("Iron Block", ItemId.IronBlock, 1, true, "III", "III", "III"),
                R("Copper Block", ItemId.CopperBlock, 1, true, "CCC", "CCC", "CCC"),
                R("Aluminium Block", ItemId.AluminiumBlock, 1, true, "AAA", "AAA", "AAA"),
                R("Diamond Block", ItemId.DiamondBlock, 1, true, "DDD", "DDD", "DDD"),
                R("Coal Block", ItemId.CoalBlock, 1, true, "CCC", "CCC", "CCC"),
                R("Glass", ItemId.Glass, 1, true, "SSS", "SSS", "SSS"),
                R("Sandstone", ItemId.Sandstone, 1, true, "DDD", "DDD", "DDD"),
                R("Bucket", ItemId.Bucket, 1, true, "I I", " I "),
                R("Bowl", ItemId.Bowl, 4, true, "P P", " P "),
                R("Bread", ItemId.Bread, 1, true, "WWW")
            };
            return list;
        }

        /// <summary>Grid characters: L=log P=planks C=stone S=stick I=iron A=aluminium D=diamond W=wheat.</summary>
        public static ItemId CharToItem(char c)
        {
            switch (c)
            {
                case 'L': return ItemId.Log;
                case 'P': return ItemId.Planks;
                case 'C': return ItemId.Cobblestone;
                case 'S': return ItemId.Stick;
                case 'I': return ItemId.IronIngot;
                case 'A': return ItemId.AluminiumIngot;
                case 'D': return ItemId.Diamond;
                case 'W': return ItemId.WheatSheaf;
                default: return ItemId.None;
            }
        }

        public static bool TryMatch(Recipe r, ItemStack[] grid, out ItemStack result)
        {
            result = ItemStack.Empty;
            if (r.Pattern == null) return false;

            int gh = grid.Length == 9 ? 3 : 2;
            int gw = gh;
            int w = r.Width, h = r.Height;
            if (w > gw || h > gh) return false;

            // the pattern must fit somewhere in the grid, and nothing else may be filled
            for (int oy = 0; oy <= gh - h; oy++)
            for (int ox = 0; ox <= gw - w; ox++)
            {
                bool ok = true;
                bool anyFilled = false;

                for (int y = 0; y < gh && ok; y++)
                for (int x = 0; x < gw && ok; x++)
                {
                    var cell = grid[y * gw + x];
                    bool inPattern = y >= oy && y < oy + h && x >= ox && x < ox + w;
                    char ch = inPattern ? r.Pattern[y - oy][x - ox] : ' ';

                    if (ch == ' ')
                    {
                        if (!cell.IsEmpty) ok = false;
                        continue;
                    }

                    ItemId want = CharToItem(ch);
                    if (cell.IsEmpty || cell.Id != want) { ok = false; break; }
                    anyFilled = true;
                }

                if (ok && anyFilled)
                {
                    result = new ItemStack(r.Result, r.ResultCount);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Every recipe the given grid can currently produce.</summary>
        public static List<Recipe> Available(ItemStack[] grid, bool hasTable)
        {
            var found = new List<Recipe>();
            ItemStack tmp;
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].NeedsTable && !hasTable) continue;
                if (TryMatch(All[i], grid, out tmp)) found.Add(All[i]);
            }
            return found;
        }

        public static ItemId Smelt(ItemId input)
        {
            var d = ItemDef.Get(input);
            return d.Smeltable ? d.SmeltResult : ItemId.None;
        }
    }
}
