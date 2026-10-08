using System.Collections.Generic;
using UnityEngine;
using DivergentGenesis.Items;
using DivergentGenesis.World;

namespace DivergentGenesis.Building
{
    public enum BuildCategory : byte
    {
        Comfort = 0,
        Shelter = 1,
        Defence = 2,
        Settlement = 3,
        Ritual = 4
    }

    public struct KitBlock
    {
        public short X, Y, Z;
        public byte Block;
    }

    public struct KitCost
    {
        public ItemId Item;
        public int Count;
    }

    /// <summary>
    /// One placeable structure: the blocks it stamps, and what it costs.
    ///
    /// Costs are derived from the block list rather than hand-written, so adding a
    /// wall to a cottage automatically raises its price and the catalogue can never
    /// drift out of sync with what actually gets built. Blocks that the player can
    /// simply dig up (dirt, grass, sand, the Node's own ground) are free.
    /// </summary>
    public sealed class BuildKit
    {
        public string Name;
        public string Blurb;
        public BuildCategory Category;

        public readonly List<KitBlock> Cells = new List<KitBlock>(512);
        public readonly List<KitCost> Costs = new List<KitCost>(8);

        public short SizeX, SizeY, SizeZ;
        public bool NeedsFlatGround = true;

        public void Add(int x, int y, int z, byte block)
        {
            if (block == Blocks.Air) return;
            Cells.Add(new KitBlock { X = (short)x, Y = (short)y, Z = (short)z, Block = block });
        }

        /// <summary>Recalculates the bounding box and the material bill.</summary>
        public void Finalise()
        {
            int maxX = 0, maxY = 0, maxZ = 0;
            for (int i = 0; i < Cells.Count; i++)
            {
                var b = Cells[i];
                if (b.X + 1 > maxX) maxX = b.X + 1;
                if (b.Y + 1 > maxY) maxY = b.Y + 1;
                if (b.Z + 1 > maxZ) maxZ = b.Z + 1;
            }
            SizeX = (short)maxX;
            SizeY = (short)maxY;
            SizeZ = (short)maxZ;

            Costs.Clear();
            var counts = new Dictionary<ItemId, int>();
            for (int i = 0; i < Cells.Count; i++)
            {
                byte block = Cells[i].Block;
                if (IsFreeMaterial(block)) continue;
                ItemId id = (ItemId)block;
                int have;
                counts.TryGetValue(id, out have);
                counts[id] = have + 1;
            }

            foreach (var kv in counts)
            {
                int price = Mathf.Clamp(Mathf.RoundToInt(kv.Value * 0.40f), 1, 64);
                Costs.Add(new KitCost { Item = kv.Key, Count = price });
            }
            Costs.Sort((a, b) => b.Count.CompareTo(a.Count));
        }

        /// <summary>Blocks you can dig up are not charged for.</summary>
        public static bool IsFreeMaterial(byte b)
        {
            switch (b)
            {
                case Blocks.Grass:
                case Blocks.Dirt:
                case Blocks.Sand:
                case Blocks.Gravel:
                case Blocks.Stone:
                case Blocks.Snow:
                case Blocks.Water:
                case Blocks.Clay:
                case Blocks.Ash:
                case Blocks.Wheat:
                case Blocks.Terracotta:
                case Blocks.FrostingGrass:
                case Blocks.CandyDirt:
                case Blocks.SherbetStone:
                case Blocks.CottonBlock:
                case Blocks.GumdropLeaves:
                case Blocks.GumdropLog:
                    return true;
                default:
                    return false;
            }
        }

        public int TotalBlocks { get { return Cells.Count; } }
    }

    /// <summary>Everything the player can put down. Built once, in code.</summary>
    public static class KitCatalog
    {
        public static readonly List<BuildKit> All = Build();

        public static BuildKit Find(string name)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Name == name) return All[i];
            return null;
        }

        public static List<BuildKit> InCategory(BuildCategory category)
        {
            var list = new List<BuildKit>(8);
            for (int i = 0; i < All.Count; i++) if (All[i].Category == category) list.Add(All[i]);
            return list;
        }

        private static List<BuildKit> Build()
        {
            var list = new List<BuildKit>(24);

            // ===================================================== comfort
            list.Add(Single("Campfire", "A light, warmth, and somewhere to cook.", BuildCategory.Comfort,
                            Blocks.Campfire));
            list.Add(Single("Bedroll", "Skip the night. Somewhere to respawn.", BuildCategory.Comfort,
                            Blocks.Bed));
            list.Add(Single("Storage Chest", "Somewhere to put things.", BuildCategory.Comfort,
                            Blocks.Chest));
            list.Add(Single("Workbench", "Unlocks the 3x3 recipes.", BuildCategory.Comfort,
                            Blocks.CraftingTable));
            list.Add(Single("Furnace", "Smelts ore into ingots.", BuildCategory.Comfort,
                            Blocks.Furnace));
            list.Add(LampPost());
            list.Add(TorchPost());

            // ===================================================== shelter
            list.Add(WallSegment());
            list.Add(WallCorner());
            list.Add(FloorPlatform());
            list.Add(RoofSection());
            list.Add(Doorway());
            list.Add(WindowWall());
            list.Add(SmallHut());
            list.Add(Cottage());
            list.Add(Longhouse());
            list.Add(Barn());

            // ===================================================== defence
            list.Add(FenceRun());
            list.Add(Watchtower());
            list.Add(StoneKeep());

            // ===================================================== settlement
            list.Add(Bridge());
            list.Add(FarmPlot());
            list.Add(VillageWell());
            list.Add(MarketStall());

            // ===================================================== ritual
            list.Add(RitualSite());
            list.Add(NodeRiftPad());

            for (int i = 0; i < list.Count; i++) list[i].Finalise();
            return list;
        }

        // ------------------------------------------------------------- simples
        private static BuildKit Single(string name, string blurb, BuildCategory cat, byte block)
        {
            var kit = new BuildKit { Name = name, Blurb = blurb, Category = cat, NeedsFlatGround = false };
            kit.Add(0, 0, 0, block);
            return kit;
        }

        private static BuildKit TorchPost()
        {
            var kit = new BuildKit { Name = "Torch Post", Blurb = "Light that fits in a corner.", Category = BuildCategory.Comfort };
            kit.Add(0, 0, 0, Blocks.Log);
            kit.Add(0, 1, 0, Blocks.Log);
            kit.Add(0, 2, 0, Blocks.Torch);
            return kit;
        }

        private static BuildKit LampPost()
        {
            var kit = new BuildKit { Name = "Lamp Post", Blurb = "A proper street light.", Category = BuildCategory.Comfort };
            kit.Add(0, 0, 0, Blocks.Log);
            kit.Add(0, 1, 0, Blocks.Log);
            kit.Add(0, 2, 0, Blocks.Log);
            kit.Add(0, 3, 0, Blocks.Lamp);
            return kit;
        }

        // ------------------------------------------------------- shelter pieces
        private static BuildKit WallSegment()
        {
            var kit = new BuildKit { Name = "Wall", Blurb = "5 x 3 plaster wall with timber posts.", Category = BuildCategory.Shelter };
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 3; y++)
                kit.Add(x, y, 0, (x == 0 || x == 4) ? Blocks.Log : Blocks.PlasterWall);
            return kit;
        }

        private static BuildKit WallCorner()
        {
            var kit = new BuildKit { Name = "Wall Corner", Blurb = "An L of wall to turn a corner with.", Category = BuildCategory.Shelter };
            for (int i = 0; i < 5; i++)
            for (int y = 0; y < 3; y++)
            {
                kit.Add(i, y, 0, (i == 0 || i == 4) ? Blocks.Log : Blocks.PlasterWall);
                if (i > 0) kit.Add(0, y, i, i == 4 ? Blocks.Log : Blocks.PlasterWall);
            }
            return kit;
        }

        private static BuildKit FloorPlatform()
        {
            var kit = new BuildKit { Name = "Floor", Blurb = "9 x 9 of planks.", Category = BuildCategory.Shelter, NeedsFlatGround = false };
            for (int x = 0; x < 9; x++)
            for (int z = 0; z < 9; z++)
                kit.Add(x, 0, z, Blocks.Planks);
            return kit;
        }

        private static BuildKit RoofSection()
        {
            var kit = new BuildKit { Name = "Roof", Blurb = "A 9 x 9 gable. Overlap them for long roofs.", Category = BuildCategory.Shelter, NeedsFlatGround = false };
            Gable(kit, 9, 9, 0, Blocks.RoofTile);
            return kit;
        }

        private static BuildKit Doorway()
        {
            var kit = new BuildKit { Name = "Doorway", Blurb = "A door in a 3 x 4 frame.", Category = BuildCategory.Shelter };
            for (int x = 0; x < 3; x++)
            for (int y = 0; y < 4; y++)
            {
                if (x == 1 && y >= 1 && y <= 2) continue;
                kit.Add(x, y, 0, Blocks.PlasterWall);
            }
            kit.Add(1, 1, 0, Blocks.Door);
            kit.Add(1, 3, 0, Blocks.Log);
            return kit;
        }

        private static BuildKit WindowWall()
        {
            var kit = new BuildKit { Name = "Window Wall", Blurb = "Plaster with a glazed opening.", Category = BuildCategory.Shelter };
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 3; y++)
            {
                bool glass = x >= 1 && x <= 3 && y == 1;
                kit.Add(x, y, 0, glass ? Blocks.Glass : ((x == 0 || x == 4) ? Blocks.Log : Blocks.PlasterWall));
            }
            return kit;
        }

        // -------------------------------------------------------------- houses
        private static BuildKit SmallHut()
        {
            var kit = new BuildKit { Name = "Small Hut", Blurb = "One room: bed, chest, light.", Category = BuildCategory.Shelter };
            Shell(kit, 5, 3, 5, Blocks.Planks, Blocks.Planks, Blocks.RoofTile, Blocks.Log, true);
            kit.Add(1, 1, 1, Blocks.Bed);
            kit.Add(3, 1, 1, Blocks.Chest);
            kit.Add(2, 2, 2, Blocks.Torch);
            return kit;
        }

        private static BuildKit Cottage()
        {
            var kit = new BuildKit { Name = "Cottage", Blurb = "7 x 4 x 9 with glazing, a bed, storage and a hearth.", Category = BuildCategory.Shelter };
            Shell(kit, 7, 4, 9, Blocks.PlasterWall, Blocks.Planks, Blocks.RoofTile, Blocks.Log, true);
            kit.Add(1, 1, 1, Blocks.Bed);
            kit.Add(5, 1, 1, Blocks.Chest);
            kit.Add(5, 1, 2, Blocks.Chest);
            kit.Add(3, 1, 7, Blocks.CraftingTable);
            kit.Add(1, 1, 7, Blocks.Furnace);
            kit.Add(3, 3, 4, Blocks.Lamp);
            kit.Add(1, 2, 4, Blocks.Torch);
            kit.Add(5, 2, 4, Blocks.Torch);
            return kit;
        }

        private static BuildKit Longhouse()
        {
            var kit = new BuildKit { Name = "Longhouse", Blurb = "9 x 4 x 13. Room for a whole village.", Category = BuildCategory.Settlement };
            Shell(kit, 9, 4, 13, Blocks.Planks, Blocks.Planks, Blocks.RoofTile, Blocks.Log, true);
            for (int z = 2; z < 11; z += 3)
            {
                kit.Add(1, 1, z, Blocks.Bed);
                kit.Add(7, 1, z, Blocks.Bed);
            }
            kit.Add(4, 1, 6, Blocks.CraftingTable);
            kit.Add(4, 1, 7, Blocks.Furnace);
            kit.Add(2, 1, 11, Blocks.Chest);
            kit.Add(6, 1, 11, Blocks.Chest);
            kit.Add(4, 3, 6, Blocks.Lamp);
            kit.Add(4, 3, 2, Blocks.Lamp);
            kit.Add(4, 3, 10, Blocks.Lamp);
            kit.Add(1, 1, 6, Blocks.Bookshelf);
            kit.Add(7, 1, 6, Blocks.Bookshelf);
            return kit;
        }

        private static BuildKit Barn()
        {
            var kit = new BuildKit { Name = "Barn", Blurb = "9 x 5 x 11 for animals and stores.", Category = BuildCategory.Settlement };
            Shell(kit, 9, 5, 11, Blocks.Planks, Blocks.Planks, Blocks.RoofTile, Blocks.Log, true);
            for (int x = 1; x < 8; x += 2)
            {
                kit.Add(x, 1, 4, Blocks.Fence);
                kit.Add(x, 1, 6, Blocks.Fence);
            }
            kit.Add(1, 1, 9, Blocks.Chest);
            kit.Add(7, 1, 9, Blocks.Chest);
            kit.Add(4, 4, 5, Blocks.Lamp);
            return kit;
        }

        // ------------------------------------------------------------ defence
        private static BuildKit FenceRun()
        {
            var kit = new BuildKit { Name = "Fence Run", Blurb = "9 blocks of fence.", Category = BuildCategory.Defence, NeedsFlatGround = false };
            for (int x = 0; x < 9; x++) kit.Add(x, 0, 0, Blocks.Fence);
            return kit;
        }

        private static BuildKit Watchtower()
        {
            var kit = new BuildKit { Name = "Watchtower", Blurb = "12 metres up, with a ladder and a light.", Category = BuildCategory.Defence };
            for (int i = 0; i < 4; i++)
            {
                int px = (i % 2 == 0) ? 0 : 4;
                int pz = (i < 2) ? 0 : 4;
                for (int y = 0; y < 12; y++) kit.Add(px, y, pz, Blocks.Log);
            }
            for (int x = 0; x <= 4; x++)
            for (int z = 0; z <= 4; z++)
            {
                kit.Add(x, 12, z, Blocks.Planks);
                bool edge = x == 0 || x == 4 || z == 0 || z == 4;
                if (edge) kit.Add(x, 13, z, Blocks.Fence);
            }
            for (int y = 1; y < 12; y++) kit.Add(4, y, 2, Blocks.Ladder);
            kit.Add(2, 13, 2, Blocks.Lamp);
            kit.Add(2, 11, 2, Blocks.Air);
            return kit;
        }

        private static BuildKit StoneKeep()
        {
            var kit = new BuildKit { Name = "Stone Keep", Blurb = "7 x 11 x 7 of stone brick with floors and lamps.", Category = BuildCategory.Defence };
            Shell(kit, 7, 10, 7, Blocks.StoneBricks, Blocks.StoneBricks, Blocks.StoneBricks, Blocks.StoneBricks, true);
            for (int x = 0; x < 7; x++)
            for (int z = 0; z < 7; z++)
            {
                bool edge = x == 0 || x == 6 || z == 0 || z == 6;

                // upper floor with a hatch, and crenellations above the parapet
                if (!(x == 3 && z == 3))
                {
                    if (x > 0 && x < 6 && z > 0 && z < 6) kit.Add(x, 5, z, Blocks.Planks);
                    if (x > 0 && x < 6 && z > 0 && z < 6) kit.Add(x, 9, z, Blocks.Planks);
                }
                if (edge && ((x + z) % 2 == 0)) kit.Add(x, 11, z, Blocks.StoneBricks);
            }
            for (int y = 1; y <= 5; y++) kit.Add(3, y, 3, Blocks.Ladder);
            for (int y = 6; y <= 9; y++) kit.Add(1, y, 1, Blocks.Ladder);
            kit.Add(3, 4, 3, Blocks.Lamp);
            kit.Add(3, 8, 3, Blocks.Lamp);
            kit.Add(1, 1, 1, Blocks.Chest);
            kit.Add(5, 1, 5, Blocks.Chest);
            return kit;
        }

        // --------------------------------------------------------- settlement
        private static BuildKit Bridge()
        {
            var kit = new BuildKit { Name = "Bridge", Blurb = "15 long, 3 wide, with rails.", Category = BuildCategory.Settlement, NeedsFlatGround = false };
            for (int z = 0; z < 15; z++)
            for (int x = 0; x < 3; x++)
            {
                kit.Add(x, 0, z, Blocks.Planks);
                if (x != 1) kit.Add(x, 1, z, Blocks.Fence);
                if (z % 5 == 0 && x == 1) kit.Add(x, -1, z, Blocks.Log);
            }
            return kit;
        }

        private static BuildKit FarmPlot()
        {
            var kit = new BuildKit { Name = "Farm Plot", Blurb = "9 x 9 of soil, fenced, with water.", Category = BuildCategory.Settlement, NeedsFlatGround = false };
            for (int x = 0; x < 9; x++)
            for (int z = 0; z < 9; z++)
            {
                bool fence = x == 0 || x == 8 || z == 0 || z == 8;
                if (fence) { kit.Add(x, 0, z, Blocks.Fence); continue; }
                kit.Add(x, 0, z, Blocks.Dirt);
                if ((x + z) % 2 == 0) kit.Add(x, 1, z, Blocks.Wheat);
                if (x == 4 && z == 4) kit.Add(x, 1, z, Blocks.Water);
            }
            return kit;
        }

        private static BuildKit VillageWell()
        {
            var kit = new BuildKit { Name = "Village Well", Blurb = "The centre of any settlement.", Category = BuildCategory.Settlement };
            for (int x = 0; x < 7; x++)
            for (int z = 0; z < 7; z++)
            {
                bool edge = x == 0 || x == 6 || z == 0 || z == 6;
                bool ring = !edge && (x == 1 || x == 5 || z == 1 || z == 5);
                if (edge) kit.Add(x, 0, z, Blocks.Cobblestone);
                else kit.Add(x, 0, z, ring ? Blocks.Cobblestone : Blocks.Water);
                if (ring)
                {
                    int post = (x == 3 && (z == 1 || z == 5)) || (z == 3 && (x == 1 || x == 5)) ? 1 : 0;
                    if (post == 1) for (int y = 1; y <= 3; y++) kit.Add(x, y, z, Blocks.Log);
                }
            }
            for (int x = 0; x < 7; x++)
            for (int z = 0; z < 7; z++)
                kit.Add(x, 4, z, Blocks.RoofTile);
            return kit;
        }

        private static BuildKit MarketStall()
        {
            var kit = new BuildKit { Name = "Market Stall", Blurb = "A counter, an awning and a lamp.", Category = BuildCategory.Settlement };
            for (int x = 0; x < 5; x++)
            for (int z = 0; z < 5; z++)
            {
                kit.Add(x, 0, z, Blocks.Planks);
                bool corner = (x == 0 || x == 4) && (z == 0 || z == 4);
                if (corner) for (int y = 1; y <= 3; y++) kit.Add(x, y, z, Blocks.Log);
            }
            for (int x = 0; x < 5; x++)
            for (int z = 0; z < 5; z++)
                kit.Add(x, 4, z, Blocks.RoofTile);
            kit.Add(1, 1, 0, Blocks.CraftingTable);
            kit.Add(3, 1, 0, Blocks.Chest);
            kit.Add(2, 3, 2, Blocks.Lamp);
            return kit;
        }

        // -------------------------------------------------------------- ritual
        private static BuildKit RitualSite()
        {
            var kit = new BuildKit { Name = "Ritual Altar", Blurb = "Altar plus four pedestals. Sacrifice four tools to use it.", Category = BuildCategory.Ritual };
            for (int x = -2; x <= 2; x++)
            for (int z = -2; z <= 2; z++)
                kit.Add(x, 0, z, Blocks.StoneBricks);

            kit.Add(0, 1, 0, Blocks.RitualAltar);
            kit.Add(-4, 0, 0, Blocks.RitualPedestal);
            kit.Add(4, 0, 0, Blocks.RitualPedestal);
            kit.Add(0, 0, -4, Blocks.RitualPedestal);
            kit.Add(0, 0, 4, Blocks.RitualPedestal);

            for (int a = 0; a < 8; a++)
            {
                float ang = a / 8f * Mathf.PI * 2f;
                int lx = Mathf.RoundToInt(Mathf.Cos(ang) * 6f);
                int lz = Mathf.RoundToInt(Mathf.Sin(ang) * 6f);
                for (int y = 0; y < 5; y++) kit.Add(lx, y, lz, Blocks.StoneBricks);
                kit.Add(lx, 5, lz, Blocks.Lamp);
            }

            for (int a = 0; a < 32; a++)
            {
                float ang = a / 32f * Mathf.PI * 2f;
                kit.Add(Mathf.RoundToInt(Mathf.Cos(ang) * 3f), 0, Mathf.RoundToInt(Mathf.Sin(ang) * 3f), Blocks.CoalBlock);
            }
            return kit;
        }

        private static BuildKit NodeRiftPad()
        {
            var kit = new BuildKit { Name = "Node Rift", Blurb = "A doorway into the Node. Step into it.", Category = BuildCategory.Ritual };
            for (int x = 0; x < 5; x++)
            for (int z = 0; z < 5; z++)
                kit.Add(x, 0, z, Blocks.SherbetStone);

            for (int y = 1; y <= 4; y++)
            {
                kit.Add(0, y, 0, Blocks.RitualPedestal);
                kit.Add(0, y, 4, Blocks.RitualPedestal);
                kit.Add(4, y, 0, Blocks.RitualPedestal);
                kit.Add(4, y, 4, Blocks.RitualPedestal);
            }
            for (int x = 0; x <= 4; x++)
                kit.Add(x, 5, 0, Blocks.SherbetStone);
            for (int x = 1; x <= 3; x++)
            for (int y = 1; y <= 4; y++)
                kit.Add(x, y, 0, Blocks.NodePortal);
            return kit;
        }

        // ============================================================== helpers
        /// <summary>
        /// A rectangular house shell: floor, walls, glazing, a door on the front and
        /// a gabled roof. Used by every building in the catalogue so they all match.
        /// </summary>
        private static void Shell(BuildKit kit, int w, int h, int d, byte wall, byte floor,
                                  byte roof, byte beam, bool windows)
        {
            for (int x = 0; x < w; x++)
            for (int z = 0; z < d; z++)
            {
                kit.Add(x, 0, z, floor);

                bool edge = x == 0 || x == w - 1 || z == 0 || z == d - 1;
                bool corner = (x == 0 || x == w - 1) && (z == 0 || z == d - 1);
                if (!edge) continue;

                for (int y = 1; y <= h; y++)
                {
                    byte b = corner ? beam : wall;

                    if (windows && !corner && y == 2)
                    {
                        bool opening = (x == 0 || x == w - 1) ? (z % 3 == 1) : (x % 3 == 1);
                        if (opening) b = Blocks.Glass;
                    }

                    // a door in the middle of the front wall
                    if (z == 0 && x == w / 2 && (y == 1 || y == 2)) continue;
                    kit.Add(x, y, z, b);
                }
            }

            kit.Add(w / 2, 1, 0, Blocks.Door);
            Gable(kit, w, d, h, roof);
        }

        /// <summary>A gabled roof over a w x d footprint, starting `baseY` up.</summary>
        private static void Gable(BuildKit kit, int w, int d, int baseY, byte roof)
        {
            int layers = Mathf.Min(w, d) / 2 + 1;
            for (int L = 0; L < layers; L++)
            {
                int x0 = -1 + L, x1 = w - L;
                int z0 = -1 + L, z1 = d - L;
                if (x0 > x1 || z0 > z1) break;

                int y = baseY + 1 + L;
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    bool shell = x == x0 || x == x1 || z == z0 || z == z1;
                    if (shell || L == layers - 1) kit.Add(x, y, z, roof);
                }
            }
        }
    }
}