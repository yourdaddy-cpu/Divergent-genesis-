using System.Collections.Generic;
using UnityEngine;

namespace DivergentGenesis.Items
{
    public enum RecipeCategory : byte
    {
        Basics = 0,
        Tools = 1,
        Building = 2,
        Comfort = 3,
        Food = 4,
        Node = 5,
        Ritual = 6
    }

    /// <summary>One ingredient of a recipe, resolved to a real item and count.</summary>
    public struct Ingredient
    {
        public ItemId Item;
        public int Count;
    }

    /// <summary>
    /// Turns the char-grid recipes into something a player can read and tap.
    ///
    /// The grid is still the source of truth (that is what the crafting panel
    /// matches against), but this resolves each pattern into plain item counts so
    /// the recipe book can say "3 Planks + 2 Sticks" and craft it in one tap.
    /// </summary>
    public static class RecipeBook
    {
        private static readonly Dictionary<Recipe, List<Ingredient>> Cache =
            new Dictionary<Recipe, List<Ingredient>>();

        public static List<Ingredient> Ingredients(Recipe r)
        {
            List<Ingredient> list;
            if (Cache.TryGetValue(r, out list)) return list;

            list = new List<Ingredient>(6);
            var counts = new Dictionary<ItemId, int>();

            if (r.Pattern != null)
            {
                for (int row = 0; row < r.Pattern.Length; row++)
                {
                    string line = r.Pattern[row];
                    if (line == null) continue;
                    for (int col = 0; col < line.Length; col++)
                    {
                        char c = line[col];
                        if (c == ' ') continue;
                        ItemId id = Recipes.CharToItem(c);
                        if (id == ItemId.None) continue;
                        int have;
                        counts.TryGetValue(id, out have);
                        counts[id] = have + 1;
                    }
                }
            }

            foreach (var kv in counts) list.Add(new Ingredient { Item = kv.Key, Count = kv.Value });
            Cache[r] = list;
            return list;
        }

        public static RecipeCategory CategoryOf(Recipe r)
        {
            var def = ItemDef.Get(r.Result);

            if (r.Result == ItemId.RitualAltar || r.Result == ItemId.RitualPedestal ||
                r.Result == ItemId.RitualSigil || r.Result == ItemId.NodePortal)
                return RecipeCategory.Ritual;

            if (def.IsFood) return RecipeCategory.Food;
            if (def.IsTool) return RecipeCategory.Tools;

            if (r.Result == ItemId.Bed || r.Result == ItemId.Campfire || r.Result == ItemId.Lamp ||
                r.Result == ItemId.Chest || r.Result == ItemId.Bookshelf || r.Result == ItemId.Door ||
                r.Result == ItemId.CraftingTable || r.Result == ItemId.Furnace || r.Result == ItemId.Anvil)
                return RecipeCategory.Comfort;

            if (r.Result == ItemId.NodeShard || r.Result == ItemId.CuteEssence ||
                r.Result == ItemId.CandyCane || r.Result == ItemId.Marshmallow ||
                r.Result == ItemId.Gumdrop)
                return RecipeCategory.Node;

            if (def.IsBlock) return RecipeCategory.Building;
            return RecipeCategory.Basics;
        }

        public static bool CanCraft(Recipe r, InventoryModel inv)
        {
            if (r == null || inv == null) return false;
            var ing = Ingredients(r);
            for (int i = 0; i < ing.Count; i++)
                if (!inv.Has(ing[i].Item, ing[i].Count)) return false;
            return true;
        }

        /// <summary>Crafts straight from the book, consuming the ingredients it listed.</summary>
        public static bool Craft(Recipe r, InventoryModel inv)
        {
            if (!CanCraft(r, inv)) return false;

            var ing = Ingredients(r);
            for (int i = 0; i < ing.Count; i++) inv.Remove(ing[i].Item, ing[i].Count);

            int leftover = inv.Add(r.Result, r.ResultCount);
            if (leftover > 0) DropManagerQueue(r.Result, leftover);
            inv.Notify();
            return true;
        }

        /// <summary>ItemIds of everything the player is short of, for the "missing" line.</summary>
        public static string MissingText(Recipe r, InventoryModel inv)
        {
            var ing = Ingredients(r);
            string text = "";
            for (int i = 0; i < ing.Count; i++)
            {
                if (inv.Has(ing[i].Item, ing[i].Count)) continue;
                if (text.Length > 0) text += ", ";
                text += IngredientText(ing[i], inv);
            }
            return text;
        }

        public static string RequirementText(Recipe r)
        {
            var ing = Ingredients(r);
            string text = "";
            for (int i = 0; i < ing.Count; i++)
            {
                if (text.Length > 0) text += "  ";
                text += IngredientText(ing[i], null);
            }
            return text;
        }

        private static string IngredientText(Ingredient ing, InventoryModel inv)
        {
            string name = ItemDef.Get(ing.Item).Name;
            if (inv == null) return ing.Count + " " + name;
            int have = inv.CountOf(ing.Item);
            return ing.Count + " " + name + " (" + Mathf.Min(have, ing.Count) + "/" + ing.Count + ")";
        }

        /// <summary>Overflow goes to the player if there is one, otherwise nowhere.</summary>
        private static void DropManagerQueue(ItemId id, int count)
        {
            var world = World.ChunkManager.Instance;
            if (world == null) return;

            var player = Player.PlayerController.Instance;
            Vector3 at = player != null ? player.transform.position + Vector3.up : new Vector3(0f, 80f, 0f);
            Player.DropManager.Spawn(world, at, id, count);
        }
    }
}