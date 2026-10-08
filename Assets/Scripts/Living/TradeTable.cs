using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.Items;

namespace DivergentGenesis.Living
{
    /// <summary>One thing a villager will do for you, in either direction.</summary>
    public struct Trade
    {
        public ItemId Give;
        public int GiveCount;
        public ItemId Cost;
        public int CostCount;
        public string Label;
    }

    /// <summary>
    /// What every trader in the world offers.
    ///
    /// Overworld villagers deal in Coins; Node villagers deal in Gumdrops, which is
    /// the only currency that makes sense in a dimension made of dessert. Which of
    /// the three lines a villager offers depends on its variant, so two farmers in
    /// the same village are not identical.
    /// </summary>
    public static class TradeTable
    {
        public static Trade[] For(MobDef def, int variant)
        {
            if (def == null || !def.Tradable) return Empty;

            if (DimensionState.NodeActive) return NodeTrades(variant);

            switch (def.Id)
            {
                case MobId.Farmer: return FarmerTrades(variant);
                case MobId.Smith: return SmithTrades(variant);
                case MobId.Scribe: return ScribeTrades(variant);
                case MobId.Guard: return GuardTrades(variant);
                default: return VillagerTrades(variant);
            }
        }

        private static readonly Trade[] Empty = new Trade[0];

        private static Trade T(ItemId give, int giveCount, ItemId cost, int costCount)
        {
            return new Trade
            {
                Give = give,
                GiveCount = giveCount,
                Cost = cost,
                CostCount = costCount,
                Label = giveCount + " " + ItemDef.Get(give).Name + "  for  " + costCount + " " + ItemDef.Get(cost).Name
            };
        }

        private static Trade[] VillagerTrades(int variant)
        {
            var all = new[]
            {
                T(ItemId.Coin, 3, ItemId.Wheat, 8),
                T(ItemId.Bread, 2, ItemId.Coin, 2),
                T(ItemId.Torch, 8, ItemId.Coin, 1),
                T(ItemId.Planks, 16, ItemId.Coin, 1)
            };
            return Pick(all, variant);
        }

        private static Trade[] FarmerTrades(int variant)
        {
            var all = new[]
            {
                T(ItemId.Coin, 2, ItemId.Wheat, 6),
                T(ItemId.WheatSeeds, 8, ItemId.Coin, 1),
                T(ItemId.Apple, 3, ItemId.Coin, 2),
                T(ItemId.Coin, 3, ItemId.Melon, 2)
            };
            return Pick(all, variant);
        }

        private static Trade[] SmithTrades(int variant)
        {
            var all = new[]
            {
                T(ItemId.IronIngot, 1, ItemId.Coin, 4),
                T(ItemId.IronPickaxe, 1, ItemId.Coin, 8),
                T(ItemId.Coal, 6, ItemId.Coin, 2),
                T(ItemId.Anvil, 1, ItemId.Coin, 12)
            };
            return Pick(all, variant);
        }

        private static Trade[] ScribeTrades(int variant)
        {
            var all = new[]
            {
                T(ItemId.Bookshelf, 1, ItemId.Coin, 4),
                T(ItemId.Glass, 4, ItemId.Coin, 2),
                T(ItemId.Lamp, 2, ItemId.Coin, 3),
                T(ItemId.Diamond, 1, ItemId.Coin, 24)
            };
            return Pick(all, variant);
        }

        private static Trade[] GuardTrades(int variant)
        {
            var all = new[]
            {
                T(ItemId.IronSword, 1, ItemId.Coin, 9),
                T(ItemId.IronIngot, 2, ItemId.Coin, 7),
                T(ItemId.Bread, 3, ItemId.Coin, 3),
                T(ItemId.Coin, 6, ItemId.DragonScale, 1)
            };
            return Pick(all, variant);
        }

        private static Trade[] NodeTrades(int variant)
        {
            var all = new[]
            {
                T(ItemId.Marshmallow, 3, ItemId.Gumdrop, 1),
                T(ItemId.CandyCane, 3, ItemId.Gumdrop, 2),
                T(ItemId.CuteEssence, 1, ItemId.Gumdrop, 3),
                T(ItemId.CuteCookie, 2, ItemId.Gumdrop, 2),
                T(ItemId.Gumdrop, 6, ItemId.CandyCane, 2)
            };
            return Pick(all, variant);
        }

        /// <summary>Three trades, rotated by the villager's variant.</summary>
        private static Trade[] Pick(Trade[] pool, int variant)
        {
            int start = Mathf.Abs(variant) % pool.Length;
            var result = new Trade[3];
            for (int i = 0; i < 3; i++) result[i] = pool[(start + i) % pool.Length];
            return result;
        }
    }
}