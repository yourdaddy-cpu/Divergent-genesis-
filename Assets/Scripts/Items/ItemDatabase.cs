using UnityEngine;
using DivergentGenesis.World;

namespace DivergentGenesis.Items
{
    /// <summary>
    /// Item ids 1..63 deliberately mirror World.Blocks, so a block item and its
    /// block are the same number - one less thing to get wrong.
    /// </summary>
    public enum ItemId : ushort
    {
        None = 0,

        Stone = 1, Dirt = 2, Grass = 3, Sand = 4, Gravel = 5, Clay = 6,
        Log = 7, Leaves = 8, Water = 9, Cobblestone = 10, Planks = 11,
        CoalOre = 12, IronOre = 13, CopperOre = 14, AluminiumOre = 15, DiamondOre = 16,
        Snow = 17, Ice = 18, Glass = 19, Bricks = 20, Obsidian = 21, Bedrock = 22,
        Terracotta = 23, Cactus = 24, Ash = 25, CraftingTable = 26, Furnace = 27,
        Torch = 28, TallGrass = 29, FlowerRed = 30, FlowerYellow = 31,
        IronBlock = 32, CopperBlock = 33, AluminiumBlock = 34, DiamondBlock = 35,
        CoalBlock = 36, Sandstone = 37, Lava = 38, StoneBricks = 39, DeadBush = 40,
        Pumpkin = 41, Melon = 42, Wheat = 43,

        Stick = 64, Coal = 65, RawIron = 66, RawCopper = 67, RawAluminium = 68, RawDiamond = 69,
        IronIngot = 70, CopperIngot = 71, AluminiumIngot = 72, Diamond = 73,
        Apple = 74, RawMeat = 75, CookedMeat = 76, WheatSheaf = 77, Bread = 78, Bone = 79,

        WoodenSword = 80, StoneSword = 81, IronSword = 82, DiamondSword = 83,
        WoodenPickaxe = 84, StonePickaxe = 85, IronPickaxe = 86, DiamondPickaxe = 87,
        WoodenAxe = 88, StoneAxe = 89, IronAxe = 90, DiamondAxe = 91,
        WoodenShovel = 92, StoneShovel = 93, IronShovel = 94, DiamondShovel = 95,

        Bucket = 96, WaterBucket = 97, Bowl = 98, MelonSlice = 99, WheatSeeds = 100
    }

    public struct ItemDef
    {
        public ItemId Id;
        public string Name;
        public int MaxStack;
        public bool IsBlock;
        public byte BlockId;
        public Color32 Color;
        public ToolClass Tool;
        public int Tier;            // 0 wood, 1 stone, 2 iron, 3 diamond
        public float AttackDamage;
        public int Durability;
        public bool IsFood;
        public float HealAmount;
        public bool Smeltable;
        public ItemId SmeltResult;

        public bool IsTool { get { return Tool != ToolClass.None; } }

        public static ItemDef Get(ItemId id)
        {
            int i = (int)id;
            if (i < 0 || i >= Table.Length) return Table[0];
            return Table[i];
        }

        public static ItemDef Get(int id) { return Get((ItemId)id); }
        public static string NameOf(ItemId id) { return Get(id).Name; }

        public static readonly ItemDef[] Table = Build();

        private static ItemDef[] Build()
        {
            var t = new ItemDef[192];
            t[0] = new ItemDef { Id = ItemId.None, Name = "-", MaxStack = 0 };

            // every placeable block is also an item
            var placeable = Blocks.AllPlaceable;
            for (int i = 0; i < placeable.Length; i++)
            {
                byte b = placeable[i];
                var bd = BlockDef.Def(b);
                bool smelt = b == Blocks.Cobblestone || b == Blocks.IronOre || b == Blocks.CopperOre ||
                             b == Blocks.AluminiumOre || b == Blocks.CoalOre;
                ItemId res = ItemId.None;
                if (b == Blocks.Cobblestone) res = ItemId.IronIngot;
                else if (b == Blocks.IronOre) res = ItemId.RawIron;
                else if (b == Blocks.CopperOre) res = ItemId.RawCopper;
                else if (b == Blocks.AluminiumOre) res = ItemId.RawAluminium;
                else if (b == Blocks.CoalOre) res = ItemId.Coal;

                t[b] = new ItemDef
                {
                    Id = (ItemId)b,
                    Name = bd.Name,
                    MaxStack = 64,
                    IsBlock = true,
                    BlockId = b,
                    Color = bd.Color,
                    Smeltable = smelt,
                    SmeltResult = res
                };
            }

            MakeMaterial(t, ItemId.Stick, "Stick", 64, new Color32(150, 116, 74, 255));
            MakeMaterial(t, ItemId.Coal, "Raw Coal", 64, new Color32(38, 38, 40, 255));
            MakeMaterial(t, ItemId.RawIron, "Raw Iron", 64, new Color32(200, 160, 132, 255));
            MakeMaterial(t, ItemId.RawCopper, "Raw Copper", 64, new Color32(206, 116, 66, 255));
            MakeMaterial(t, ItemId.RawAluminium, "Raw Aluminium", 64, new Color32(196, 198, 204, 255));
            MakeMaterial(t, ItemId.RawDiamond, "Raw Diamond", 64, new Color32(120, 214, 214, 255));
            MakeMaterial(t, ItemId.IronIngot, "Iron Ingot", 64, new Color32(214, 214, 218, 255));
            MakeMaterial(t, ItemId.CopperIngot, "Copper Ingot", 64, new Color32(214, 124, 72, 255));
            MakeMaterial(t, ItemId.AluminiumIngot, "Aluminium Ingot", 64, new Color32(206, 210, 216, 255));
            MakeMaterial(t, ItemId.Diamond, "Diamond", 64, new Color32(112, 226, 226, 255));
            MakeMaterial(t, ItemId.Bone, "Bone", 64, new Color32(232, 230, 216, 255));
            MakeMaterial(t, ItemId.Bucket, "Bucket", 16, new Color32(184, 188, 196, 255));
            MakeMaterial(t, ItemId.WaterBucket, "Water Bucket", 1, new Color32(64, 132, 196, 255));
            MakeMaterial(t, ItemId.Bowl, "Bowl", 16, new Color32(198, 196, 190, 255));
            MakeMaterial(t, ItemId.MelonSlice, "Melon Slice", 64, new Color32(122, 186, 96, 255));
            MakeMaterial(t, ItemId.WheatSeeds, "Wheat Seeds", 64, new Color32(150, 178, 92, 255));

            MakeFood(t, ItemId.Apple, "Apple", 64, new Color32(206, 62, 58, 255), 4f);
            MakeFood(t, ItemId.RawMeat, "Raw Meat", 64, new Color32(214, 130, 132, 255), 2f);
            MakeFood(t, ItemId.CookedMeat, "Cooked Meat", 64, new Color32(178, 112, 66, 255), 7f);
            MakeFood(t, ItemId.WheatSheaf, "Wheat", 64, new Color32(216, 190, 92, 255), 1f);
            MakeFood(t, ItemId.Bread, "Bread", 64, new Color32(206, 164, 92, 255), 6f);

            MakeTool(t, ItemId.WoodenSword, "Wooden Sword", ToolClass.Sword, 0, 4f, 60, new Color32(164, 130, 80, 255));
            MakeTool(t, ItemId.StoneSword, "Stone Sword", ToolClass.Sword, 1, 5.5f, 132, new Color32(140, 140, 142, 255));
            MakeTool(t, ItemId.IronSword, "Iron Sword", ToolClass.Sword, 2, 7.5f, 251, new Color32(216, 216, 220, 255));
            MakeTool(t, ItemId.DiamondSword, "Diamond Sword", ToolClass.Sword, 3, 9.5f, 1562, new Color32(112, 226, 226, 255));

            MakeTool(t, ItemId.WoodenPickaxe, "Wooden Pickaxe", ToolClass.Pickaxe, 0, 2.2f, 60, new Color32(164, 130, 80, 255));
            MakeTool(t, ItemId.StonePickaxe, "Stone Pickaxe", ToolClass.Pickaxe, 1, 3.4f, 132, new Color32(140, 140, 142, 255));
            MakeTool(t, ItemId.IronPickaxe, "Iron Pickaxe", ToolClass.Pickaxe, 2, 5f, 251, new Color32(216, 216, 220, 255));
            MakeTool(t, ItemId.DiamondPickaxe, "Diamond Pickaxe", ToolClass.Pickaxe, 3, 7f, 1562, new Color32(112, 226, 226, 255));

            MakeTool(t, ItemId.WoodenAxe, "Wooden Axe", ToolClass.Axe, 0, 3f, 60, new Color32(164, 130, 80, 255));
            MakeTool(t, ItemId.StoneAxe, "Stone Axe", ToolClass.Axe, 1, 4.2f, 132, new Color32(140, 140, 142, 255));
            MakeTool(t, ItemId.IronAxe, "Iron Axe", ToolClass.Axe, 2, 6f, 251, new Color32(216, 216, 220, 255));
            MakeTool(t, ItemId.DiamondAxe, "Diamond Axe", ToolClass.Axe, 3, 8.5f, 1562, new Color32(112, 226, 226, 255));

            MakeTool(t, ItemId.WoodenShovel, "Wooden Shovel", ToolClass.Shovel, 0, 2f, 60, new Color32(164, 130, 80, 255));
            MakeTool(t, ItemId.StoneShovel, "Stone Shovel", ToolClass.Shovel, 1, 3f, 132, new Color32(140, 140, 142, 255));
            MakeTool(t, ItemId.IronShovel, "Iron Shovel", ToolClass.Shovel, 2, 4.5f, 251, new Color32(216, 216, 220, 255));
            MakeTool(t, ItemId.DiamondShovel, "Diamond Shovel", ToolClass.Shovel, 3, 6.5f, 1562, new Color32(112, 226, 226, 255));

            return t;
        }

        private static void MakeMaterial(ItemDef[] t, ItemId id, string name, int stack, Color32 c)
        {
            t[(int)id] = new ItemDef
            {
                Id = id, Name = name, MaxStack = stack, Color = c,
                Tool = ToolClass.None, Tier = 0, AttackDamage = 0f, Durability = 0, IsFood = false, HealAmount = 0f
            };
        }

        private static void MakeFood(ItemDef[] t, ItemId id, string name, int stack, Color32 c, float heal)
        {
            t[(int)id] = new ItemDef
            {
                Id = id, Name = name, MaxStack = stack, Color = c,
                Tool = ToolClass.None, Tier = 0, AttackDamage = 0f, Durability = 0, IsFood = true, HealAmount = heal
            };
        }

        private static void MakeTool(ItemDef[] t, ItemId id, string name, ToolClass tool, int tier,
                                 float damage, int durability, Color32 c)
        {
            t[(int)id] = new ItemDef
            {
                Id = id, Name = name, MaxStack = 1, Color = c,
                Tool = tool, Tier = tier, AttackDamage = damage, Durability = durability,
                IsFood = false, HealAmount = 0f
            };
        }
    }

    /// <summary>A quantity of one item.</summary>
    [System.Serializable]
    public struct ItemStack
    {
        public ItemId Id;
        public int Count;

        public ItemStack(ItemId id, int count) { Id = id; Count = count; }

        public static ItemStack Empty { get { return new ItemStack { Id = ItemId.None, Count = 0 }; } }
        public bool IsEmpty { get { return Id == ItemId.None || Count <= 0; } }
        public int MaxStack { get { return ItemDef.Get(Id).MaxStack; } }
        public ItemDef Def { get { return ItemDef.Get(Id); } }
        public Color32 Color { get { return ItemDef.Get(Id).Color; } }

        public static ItemStack Of(ItemId id, int count)
        {
            if (count <= 0) return Empty;
            return new ItemStack { Id = id, Count = count };
        }
    }
}
