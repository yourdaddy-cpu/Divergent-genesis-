using UnityEngine;

namespace DivergentGenesis.World
{
    /// <summary>Block ids. Byte sized so a whole 32x32x160 chunk is 160 KB.</summary>
    public static class Blocks
    {
        public const byte Air = 0;
        public const byte Stone = 1;
        public const byte Dirt = 2;
        public const byte Grass = 3;
        public const byte Sand = 4;
        public const byte Gravel = 5;
        public const byte Clay = 6;
        public const byte Log = 7;
        public const byte Leaves = 8;
        public const byte Water = 9;          // == WorldConfig.WaterBlockId
        public const byte Cobblestone = 10;
        public const byte Planks = 11;
        public const byte CoalOre = 12;
        public const byte IronOre = 13;
        public const byte CopperOre = 14;
        public const byte AluminiumOre = 15;
        public const byte DiamondOre = 16;
        public const byte Snow = 17;
        public const byte Ice = 18;
        public const byte Glass = 19;
        public const byte Bricks = 20;
        public const byte Obsidian = 21;
        public const byte Bedrock = 22;
        public const byte Terracotta = 23;
        public const byte Cactus = 24;
        public const byte Ash = 25;
        public const byte CraftingTable = 26;
        public const byte Furnace = 27;
        public const byte Torch = 28;
        public const byte TallGrass = 29;
        public const byte FlowerRed = 30;
        public const byte FlowerYellow = 31;
        public const byte IronBlock = 32;
        public const byte CopperBlock = 33;
        public const byte AluminiumBlock = 34;
        public const byte DiamondBlock = 35;
        public const byte CoalBlock = 36;
        public const byte Sandstone = 37;
        public const byte Lava = 38;
        public const byte StoneBricks = 39;
        public const byte DeadBush = 40;
        public const byte Pumpkin = 41;
        public const byte Melon = 42;
        public const byte Wheat = 43;

        // --- furniture, building kit and Node dimension blocks ----------------
        public const byte Campfire = 44;
        public const byte Bed = 45;
        public const byte Door = 46;
        public const byte PlasterWall = 47;
        public const byte RoofTile = 48;
        public const byte Lamp = 49;
        public const byte Anvil = 50;
        public const byte Chest = 51;
        public const byte Bookshelf = 52;
        public const byte Ladder = 53;
        public const byte Fence = 54;
        public const byte RitualAltar = 55;
        public const byte RitualPedestal = 56;
        public const byte NodePortal = 57;
        public const byte CandyDirt = 58;
        public const byte FrostingGrass = 59;
        public const byte GumdropLog = 60;
        public const byte GumdropLeaves = 61;
        public const byte CottonBlock = 62;
        public const byte SherbetStone = 63;

        public const byte Count = 64;

        // Built on first use, not in a static field initialiser: Table itself is a
        // static field declared further down, and field initialisers run in
        // declaration order - touching Table from here would read null.
        private static byte[] _placeableCache;

        /// <summary>Every block the player is allowed to hold in their inventory.</summary>
        public static byte[] AllPlaceable
        {
            get
            {
                if (_placeableCache == null) _placeableCache = BuildPlaceableList();
                return _placeableCache;
            }
        }

        private static byte[] BuildPlaceableList()
        {
            // Only ids that actually have a BlockDef. Ids are a byte range, not
            // every value in it is a block - letting a hole through here would put
            // blank entries in the block picker and a zeroed def in the mesher.
            var list = new System.Collections.Generic.List<byte>(48);
            for (byte i = 1; i < Count; i++)
            {
                if (i == Bedrock || i == Water || i == Lava) continue;
                if (!BlockDef.IsDefined(i)) continue;
                list.Add(i);
            }
            return list.ToArray();
        }
    }

    public enum ToolClass : byte { None = 0, Pickaxe = 1, Axe = 2, Shovel = 3, Sword = 4 }

    public enum BlockRender : byte { Cube = 0, Cross = 1, Liquid = 2 }

    public struct BlockDef
    {
        public byte Id;
        public string Name;
        public Color32 Color;          // base tint used by the terrain shader
        public Color32 TopColor;       // grass/snow tops get their own tint
        public float Hardness;         // seconds to break bare handed
        public ToolClass PreferredTool;
        public bool RequiresTool;      // stone family needs a pickaxe
        public bool Solid;             // collider
        public bool Opaque;            // hides neighbouring faces
        public BlockRender Render;
        public byte DropItem;          // Items.Id, 0 = drops itself
        public byte DropCountMin;
        public byte DropCountMax;
        public int LightEmission;      // 0..15
        public bool Transparent;       // render queue / blend

        public static BlockDef Def(byte id)
        {
            if (id >= Table.Length) return Table[Blocks.Stone];
            return Table[id];
        }

        /// <summary>True when a BlockDef was registered for this id (i.e. it is a real block).</summary>
        public static bool IsDefined(byte id) { return id < Table.Length && Table[id].Name != null; }

        public static bool IsSolid(byte id) { return Table[id].Solid; }
        public static bool IsOpaque(byte id) { return Table[id].Opaque; }
        public static bool IsCross(byte id) { return Table[id].Render == BlockRender.Cross; }
        public static bool IsLiquid(byte id) { return Table[id].Render == BlockRender.Liquid; }

        public static readonly BlockDef[] Table = Build();

        private static BlockDef[] Build()
        {
            var t = new BlockDef[Blocks.Count];

            Add(t, Blocks.Air, "Air", 0, 0, 0, 0, 0, 0, 0f, ToolClass.None, false, false, false, BlockRender.Cross, 0, 0, 0, 0, false);

            Add(t, Blocks.Stone, "Stone", 128, 130, 134, 132, 130, 132, 1.5f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Dirt, "Dirt", 120, 92, 62, 120, 92, 62, 0.5f, ToolClass.Shovel, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Grass, "Grass Block", 128, 96, 64, 255, 255, 255, 0.6f, ToolClass.Shovel, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Sand, "Sand", 216, 206, 154, 222, 212, 162, 0.5f, ToolClass.Shovel, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Gravel, "Gravel", 138, 134, 130, 142, 138, 132, 0.6f, ToolClass.Shovel, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Clay, "Clay", 158, 160, 168, 162, 164, 172, 0.6f, ToolClass.Shovel, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Log, "Oak Log", 106, 78, 48, 116, 86, 54, 2.0f, ToolClass.Axe, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Leaves, "Leaves", 60, 112, 48, 60, 112, 48, 0.2f, ToolClass.None, false, true, false, BlockRender.Cube, 0, 0, 0, 0, true);
            Add(t, Blocks.Water, "Water", 44, 96, 160, 60, 118, 190, 500f, ToolClass.None, false, false, false, BlockRender.Liquid, 0, 0, 0, 0, true);
            Add(t, Blocks.Cobblestone, "Cobblestone", 120, 120, 120, 124, 124, 124, 2.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Planks, "Oak Planks", 164, 130, 80, 172, 138, 88, 2.0f, ToolClass.Axe, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.CoalOre, "Raw Coal Ore", 128, 130, 134, 128, 130, 134, 3.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 1, 2, 0, false);
            Add(t, Blocks.IronOre, "Raw Iron Ore", 156, 134, 118, 156, 134, 118, 3.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 1, 2, 0, false);
            Add(t, Blocks.CopperOre, "Raw Copper Ore", 190, 130, 92, 190, 130, 92, 3.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 1, 3, 0, false);
            Add(t, Blocks.AluminiumOre, "Raw Aluminium Ore", 178, 176, 168, 178, 176, 168, 3.2f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 1, 3, 0, false);
            Add(t, Blocks.DiamondOre, "Raw Diamond Ore", 120, 190, 190, 120, 190, 190, 3.6f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 1, 2, 0, false);
            Add(t, Blocks.Snow, "Snow Block", 238, 244, 250, 244, 248, 252, 0.35f, ToolClass.Shovel, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Ice, "Ice", 168, 208, 236, 178, 216, 242, 0.6f, ToolClass.None, false, true, false, BlockRender.Cube, 0, 0, 0, 0, true);
            Add(t, Blocks.Glass, "Glass", 210, 232, 240, 214, 236, 244, 0.3f, ToolClass.None, false, true, false, BlockRender.Cube, 0, 0, 0, 0, true);
            Add(t, Blocks.Bricks, "Bricks", 156, 82, 68, 160, 86, 72, 2.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Obsidian, "Obsidian", 22, 18, 30, 28, 22, 38, 9.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Bedrock, "Bedrock", 40, 40, 42, 44, 44, 46, 9999f, ToolClass.None, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Terracotta, "Terracotta", 178, 108, 62, 184, 114, 68, 1.4f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Cactus, "Cactus", 60, 122, 62, 60, 122, 62, 0.5f, ToolClass.None, false, true, false, BlockRender.Cube, 0, 0, 0, 0, true);
            Add(t, Blocks.Ash, "Ash", 104, 100, 96, 110, 106, 100, 0.5f, ToolClass.Shovel, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.CraftingTable, "Crafting Table", 150, 112, 68, 158, 120, 74, 2.0f, ToolClass.Axe, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Furnace, "Furnace", 116, 116, 120, 122, 122, 126, 2.5f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Torch, "Torch", 255, 214, 130, 255, 214, 130, 0.1f, ToolClass.None, false, false, false, BlockRender.Cross, 0, 0, 0, 14, false);
            Add(t, Blocks.TallGrass, "Tall Grass", 118, 168, 78, 126, 176, 86, 0.05f, ToolClass.None, false, false, false, BlockRender.Cross, 0, 0, 0, 0, true);
            Add(t, Blocks.FlowerRed, "Poppy", 200, 60, 56, 208, 66, 60, 0.05f, ToolClass.None, false, false, false, BlockRender.Cross, 0, 0, 0, 0, true);
            Add(t, Blocks.FlowerYellow, "Dandelion", 232, 200, 70, 240, 210, 80, 0.05f, ToolClass.None, false, false, false, BlockRender.Cross, 0, 0, 0, 0, true);
            Add(t, Blocks.IronBlock, "Block of Iron", 214, 214, 218, 220, 220, 224, 3.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.CopperBlock, "Block of Copper", 198, 118, 66, 206, 126, 72, 3.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.AluminiumBlock, "Block of Aluminium", 202, 204, 208, 208, 210, 214, 3.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.DiamondBlock, "Block of Diamond", 108, 220, 220, 120, 232, 232, 4.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.CoalBlock, "Block of Coal", 34, 34, 36, 40, 40, 42, 3.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Sandstone, "Sandstone", 224, 210, 160, 230, 216, 168, 1.2f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Lava, "Lava", 220, 90, 20, 250, 130, 30, 500f, ToolClass.None, false, false, false, BlockRender.Liquid, 0, 0, 0, 15, true);
            Add(t, Blocks.StoneBricks, "Stone Bricks", 124, 124, 122, 128, 128, 126, 2.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.DeadBush, "Dead Bush", 124, 100, 56, 130, 106, 60, 0.05f, ToolClass.None, false, false, false, BlockRender.Cross, 0, 0, 0, 0, true);
            Add(t, Blocks.Pumpkin, "Pumpkin", 208, 132, 40, 216, 140, 46, 1.0f, ToolClass.Axe, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Melon, "Melon", 96, 150, 62, 104, 160, 70, 1.0f, ToolClass.Axe, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Wheat, "Wheat", 210, 180, 80, 216, 188, 88, 0.05f, ToolClass.None, false, false, false, BlockRender.Cross, 0, 0, 0, 0, true);

            // ---- furniture and building kit -----------------------------------
            Add(t, Blocks.Campfire, "Campfire", 148, 96, 52, 168, 108, 58, 1.0f, ToolClass.Axe, false, false, false, BlockRender.Cross, 0, 0, 0, 15, false);
            Add(t, Blocks.Bed, "Bed", 176, 52, 58, 190, 62, 68, 0.6f, ToolClass.None, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Door, "Wooden Door", 158, 118, 70, 166, 126, 78, 1.0f, ToolClass.Axe, false, false, false, BlockRender.Cube, 0, 0, 0, 0, true);
            Add(t, Blocks.PlasterWall, "Plaster Wall", 226, 219, 202, 234, 228, 212, 1.2f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.RoofTile, "Roof Tile", 158, 74, 60, 172, 84, 68, 1.3f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Lamp, "Lamp", 255, 226, 158, 255, 232, 168, 0.5f, ToolClass.None, false, true, true, BlockRender.Cube, 0, 0, 0, 15, false);
            Add(t, Blocks.Anvil, "Anvil", 76, 78, 84, 84, 86, 92, 3.2f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Chest, "Chest", 150, 110, 62, 160, 118, 68, 1.6f, ToolClass.Axe, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Bookshelf, "Bookshelf", 138, 104, 62, 146, 112, 68, 1.6f, ToolClass.Axe, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.Ladder, "Ladder", 170, 132, 78, 178, 140, 84, 0.5f, ToolClass.Axe, false, false, false, BlockRender.Cross, 0, 0, 0, 0, true);
            Add(t, Blocks.Fence, "Fence", 168, 132, 80, 176, 140, 86, 1.0f, ToolClass.Axe, false, true, false, BlockRender.Cube, 0, 0, 0, 0, true);

            // ---- the ritual ---------------------------------------------------
            Add(t, Blocks.RitualAltar, "Ritual Altar", 58, 42, 88, 74, 54, 108, 4.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 8, false);
            Add(t, Blocks.RitualPedestal, "Ritual Pedestal", 92, 86, 104, 104, 98, 118, 3.0f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 4, false);
            Add(t, Blocks.NodePortal, "Node Rift", 128, 240, 224, 168, 255, 236, 9999f, ToolClass.None, false, false, false, BlockRender.Cross, 0, 0, 0, 15, true);

            // ---- the Node dimension --------------------------------------------
            Add(t, Blocks.CandyDirt, "Marshmallow Soil", 244, 214, 224, 248, 222, 232, 0.5f, ToolClass.Shovel, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.FrostingGrass, "Frosted Grass", 246, 206, 226, 255, 238, 250, 0.6f, ToolClass.Shovel, false, true, true, BlockRender.Cube, 0, 0, 0, 2, false);
            Add(t, Blocks.GumdropLog, "Gumdrop Trunk", 196, 148, 176, 210, 160, 188, 2.0f, ToolClass.Axe, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.GumdropLeaves, "Gumdrop Leaves", 172, 236, 216, 186, 246, 226, 0.2f, ToolClass.None, false, true, false, BlockRender.Cube, 0, 0, 0, 0, true);
            Add(t, Blocks.CottonBlock, "Cotton Cloud", 250, 250, 255, 255, 255, 255, 0.3f, ToolClass.None, false, true, true, BlockRender.Cube, 0, 0, 0, 0, false);
            Add(t, Blocks.SherbetStone, "Sherbet Stone", 232, 176, 148, 240, 186, 158, 1.6f, ToolClass.Pickaxe, true, true, true, BlockRender.Cube, 0, 0, 0, 0, false);

            return t;
        }

        private static void Add(BlockDef[] t, byte id, string name, byte r, byte g, byte b, byte tr, byte tg, byte tb,
                                float hardness, ToolClass tool, bool requiresTool, bool solid, bool opaque,
                                BlockRender render, byte dropItem, byte dropMin, byte dropMax,
                                int light, bool transparent)
        {
            t[id] = new BlockDef
            {
                Id = id,
                Name = name,
                Color = new Color32(r, g, b, 255),
                TopColor = new Color32(tr, tg, tb, 255),
                Hardness = hardness,
                PreferredTool = tool,
                RequiresTool = requiresTool,
                Solid = solid,
                Opaque = opaque,
                Render = render,
                DropItem = dropItem,
                DropCountMin = dropMin,
                DropCountMax = dropMax,
                LightEmission = light,
                Transparent = transparent
            };
        }

        public static string NameOf(byte id) { return Def(id).Name; }
        public static bool IsBreakable(byte id) { return id != Blocks.Air && Table[id].Hardness < 9000f; }
        public static bool IsBlock(byte id) { return id != 0 && id < Blocks.Count; }
    }
}
