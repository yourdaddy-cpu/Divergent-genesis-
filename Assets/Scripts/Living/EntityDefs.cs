using UnityEngine;
using DivergentGenesis.Core;
using DivergentGenesis.Items;
using DivergentGenesis.World;

namespace DivergentGenesis.Living
{
    /// <summary>How an entity is shaped. The rig builder switches on this.</summary>
    public enum EntityArchetype : byte
    {
        Quadruped = 0,
        Biped = 1,
        Bird = 2,
        Blob = 3,
        Dragon = 4,
        Wisp = 5
    }

    /// <summary>What an entity wants. The brain switches on this.</summary>
    public enum EntityBehaviour : byte
    {
        Passive = 0,     // grazes, ignores you until hit
        Skittish = 1,    // bolts the moment you get close
        Predator = 2,    // hunts animals, defends itself against you
        Hostile = 3,     // hunts you
        Villager = 4,    // works, sleeps, trades
        Guard = 5,       // patrols the village, fights anything hostile
        Bandit = 6,      // raids villages, hunts you
        Cute = 7,        // bounces around being adorable, can be petted
        Boss = 8         // the thing the ritual is for
    }

    public enum MobId : byte
    {
        None = 0,

        // overworld wildlife
        Deer = 1, Boar = 2, Sheep = 3, Cow = 4, Pig = 5, Chicken = 6, Rabbit = 7,
        Fox = 8, Wolf = 9, Bear = 10, Frog = 11, Bat = 12,

        // overworld problems
        Hollow = 20, Spider = 21, Slime = 22, Emberling = 23, Wisp = 24,

        // civilisation
        Villager = 30, Farmer = 31, Smith = 32, Scribe = 33, Guard = 34,
        Bandit = 40, BanditArcher = 41, BanditChief = 42,

        // dragons
        EmberDragon = 50, FrostDragon = 51, StormDragon = 52, ElderDragon = 53,

        // the Node
        Fluff = 60, Bunbun = 61, Cloudpup = 62, Jellybean = 63, Starlet = 64,
        Puffcap = 65, Nibble = 66, NodeGuardian = 67
    }

    /// <summary>Full description of one kind of living thing.</summary>
    public sealed class MobDef
    {
        public MobId Id;
        public string Name;
        public EntityArchetype Body;
        public EntityBehaviour Behaviour;

        public float Health = 10f;
        public float Speed = 3.2f;
        public float Damage = 2f;
        public float AttackRange = 1.6f;
        public float AttackCooldown = 1.1f;

        public float Scale = 1f;
        public float BodyLength = 1.1f;
        public float BodyWidth = 0.55f;
        public float LegLength = 0.55f;
        public float HeadSize = 0.42f;

        public Color32 Primary = new Color32(180, 180, 180, 255);
        public Color32 Secondary = new Color32(140, 140, 140, 255);
        public Color32 Accent = new Color32(240, 240, 240, 255);

        public bool HostileToPlayer;
        public bool FleesFromPlayer;
        public bool HuntsAnimals;
        public bool Aquatic;
        public bool Flying;
        public bool Petable;
        public bool Nocturnal;
        public bool NodeOnly;
        public bool OverworldOnly;
        public bool Tradable;
        public bool CanBeBandit;

        public float SightRange = 22f;
        public float FleeRange = 8f;
        public float AggroRange = 18f;

        public int SpawnWeight = 10;        // relative frequency inside its habitat
        public float MinSpawnDistance = 26f;
        public float MaxSpawnDistance = 78f;

        public ItemId Drop1 = ItemId.None;
        public int Drop1Min, Drop1Max;
        public ItemId Drop2 = ItemId.None;
        public int Drop2Min, Drop2Max;

        /// <summary>Radius used for the block highlight and for melee reach.</summary>
        public float Radius { get { return BodyWidth * 0.55f * Scale; } }
        public float Height { get { return (LegLength + BodyWidth + HeadSize) * Scale; } }
    }

    /// <summary>
    /// Every mob in the game, in one flat table.
    ///
    /// Same philosophy as the block and item tables: data, not code. Adding a
    /// creature is a row here plus (optionally) a colour, and it automatically
    /// gets a rig, a brain, spawn rules, drops and an entry in the bestiary.
    /// </summary>
    public static class MobDatabase
    {
        public const int Count = 68;
        private static readonly MobDef[] T = new MobDef[Count];

        public static MobDef Get(MobId id)
        {
            int i = (int)id;
            if (i <= 0 || i >= Count || T[i] == null) return T[(int)MobId.Deer];
            return T[i];
        }

        public static bool IsDefined(MobId id)
        {
            int i = (int)id;
            return i > 0 && i < Count && T[i] != null;
        }

        /// <summary>Every mob that has a definition. Used by the bestiary and spawn rolls.</summary>
        public static MobId[] All
        {
            get
            {
                var list = new System.Collections.Generic.List<MobId>(Count);
                for (int i = 1; i < Count; i++) if (T[i] != null) list.Add((MobId)i);
                return list.ToArray();
            }
        }

        private static MobDef New(MobId id, string name, EntityArchetype body, EntityBehaviour behaviour)
        {
            var d = new MobDef { Id = id, Name = name, Body = body, Behaviour = behaviour };
            T[(int)id] = d;
            return d;
        }

        private static Color32 C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }

        static MobDatabase()
        {
            // ============================================================== wildlife
            var deer = New(MobId.Deer, "Deer", EntityArchetype.Quadruped, EntityBehaviour.Skittish);
            deer.Health = 10f; deer.Speed = 5.4f; deer.Scale = 1.05f;
            deer.BodyLength = 1.35f; deer.BodyWidth = 0.55f; deer.LegLength = 0.85f; deer.HeadSize = 0.40f;
            deer.Primary = C(158, 112, 72); deer.Secondary = C(206, 178, 138); deer.Accent = C(88, 62, 42);
            deer.FleesFromPlayer = true; deer.FleeRange = 11f; deer.SpawnWeight = 16;
            deer.Drop1 = ItemId.RawMeat; deer.Drop1Min = 1; deer.Drop1Max = 2;
            deer.Drop2 = ItemId.Bone; deer.Drop2Min = 0; deer.Drop2Max = 2;

            var boar = New(MobId.Boar, "Boar", EntityArchetype.Quadruped, EntityBehaviour.Passive);
            boar.Health = 14f; boar.Speed = 3.6f; boar.Damage = 2.5f; boar.Scale = 0.95f;
            boar.BodyLength = 1.25f; boar.BodyWidth = 0.66f; boar.LegLength = 0.45f; boar.HeadSize = 0.42f;
            boar.Primary = C(78, 62, 54); boar.Secondary = C(112, 92, 78); boar.Accent = C(226, 220, 206);
            boar.SpawnWeight = 12;
            boar.Drop1 = ItemId.RawMeat; boar.Drop1Min = 1; boar.Drop1Max = 3;

            var sheep = New(MobId.Sheep, "Sheep", EntityArchetype.Quadruped, EntityBehaviour.Passive);
            sheep.Health = 8f; sheep.Speed = 2.6f; sheep.Scale = 0.85f;
            sheep.BodyLength = 1.0f; sheep.BodyWidth = 0.62f; sheep.LegLength = 0.52f; sheep.HeadSize = 0.36f;
            sheep.Primary = C(238, 236, 230); sheep.Secondary = C(206, 202, 194); sheep.Accent = C(210, 190, 176);
            sheep.SpawnWeight = 16;
            sheep.Drop1 = ItemId.RawMeat; sheep.Drop1Min = 1; sheep.Drop1Max = 2;
            sheep.Drop2 = ItemId.CottonBlock; sheep.Drop2Min = 0; sheep.Drop2Max = 1;

            var cow = New(MobId.Cow, "Cow", EntityArchetype.Quadruped, EntityBehaviour.Passive);
            cow.Health = 10f; cow.Speed = 2.4f; cow.Scale = 1.0f;
            cow.BodyLength = 1.25f; cow.BodyWidth = 0.68f; cow.LegLength = 0.58f; cow.HeadSize = 0.40f;
            cow.Primary = C(70, 54, 44); cow.Secondary = C(226, 222, 216); cow.Accent = C(196, 152, 132);
            cow.SpawnWeight = 14;
            cow.Drop1 = ItemId.RawMeat; cow.Drop1Min = 1; cow.Drop1Max = 3;
            cow.Drop2 = ItemId.CottonBlock; cow.Drop2Min = 0; cow.Drop2Max = 1;

            var pig = New(MobId.Pig, "Pig", EntityArchetype.Quadruped, EntityBehaviour.Passive);
            pig.Health = 8f; pig.Speed = 2.8f; pig.Scale = 0.82f;
            pig.BodyLength = 1.0f; pig.BodyWidth = 0.60f; pig.LegLength = 0.42f; pig.HeadSize = 0.34f;
            pig.Primary = C(232, 172, 168); pig.Secondary = C(206, 148, 146); pig.Accent = C(248, 214, 212);
            pig.SpawnWeight = 14;
            pig.Drop1 = ItemId.RawMeat; pig.Drop1Min = 1; pig.Drop1Max = 3;

            var chicken = New(MobId.Chicken, "Chicken", EntityArchetype.Bird, EntityBehaviour.Skittish);
            chicken.Health = 4f; chicken.Speed = 3.0f; chicken.Scale = 0.6f;
            chicken.BodyLength = 0.42f; chicken.BodyWidth = 0.34f; chicken.LegLength = 0.32f; chicken.HeadSize = 0.20f;
            chicken.Primary = C(248, 246, 240); chicken.Secondary = C(224, 200, 120); chicken.Accent = C(226, 96, 78);
            chicken.FleesFromPlayer = true; chicken.FleeRange = 7f; chicken.SpawnWeight = 14;
            chicken.Drop1 = ItemId.RawMeat; chicken.Drop1Min = 1; chicken.Drop1Max = 1;

            var rabbit = New(MobId.Rabbit, "Rabbit", EntityArchetype.Quadruped, EntityBehaviour.Skittish);
            rabbit.Health = 4f; rabbit.Speed = 4.6f; rabbit.Scale = 0.55f;
            rabbit.BodyLength = 0.42f; rabbit.BodyWidth = 0.28f; rabbit.LegLength = 0.20f; rabbit.HeadSize = 0.20f;
            rabbit.Primary = C(196, 174, 152); rabbit.Secondary = C(232, 220, 204); rabbit.Accent = C(238, 232, 226);
            rabbit.FleesFromPlayer = true; rabbit.FleeRange = 8f; rabbit.SpawnWeight = 12;
            rabbit.Drop1 = ItemId.RawMeat; rabbit.Drop1Min = 1; rabbit.Drop1Max = 1;

            var fox = New(MobId.Fox, "Fox", EntityArchetype.Quadruped, EntityBehaviour.Predator);
            fox.Health = 8f; fox.Speed = 5.2f; fox.Damage = 2.5f; fox.Scale = 0.7f;
            fox.BodyLength = 0.75f; fox.BodyWidth = 0.34f; fox.LegLength = 0.36f; fox.HeadSize = 0.26f;
            fox.Primary = C(212, 112, 56); fox.Secondary = C(240, 238, 232); fox.Accent = C(58, 48, 44);
            fox.HuntsAnimals = true; fox.SpawnWeight = 7;

            var wolf = New(MobId.Wolf, "Wolf", EntityArchetype.Quadruped, EntityBehaviour.Predator);
            wolf.Health = 14f; wolf.Speed = 5.8f; wolf.Damage = 4f; wolf.Scale = 0.85f;
            wolf.BodyLength = 0.95f; wolf.BodyWidth = 0.40f; wolf.LegLength = 0.44f; wolf.HeadSize = 0.30f;
            wolf.Primary = C(146, 146, 152); wolf.Secondary = C(196, 198, 204); wolf.Accent = C(58, 56, 58);
            wolf.HuntsAnimals = true; wolf.AggroRange = 12f; wolf.SpawnWeight = 6;

            var bear = New(MobId.Bear, "Bear", EntityArchetype.Quadruped, EntityBehaviour.Predator);
            bear.Health = 30f; bear.Speed = 4.4f; bear.Damage = 8f; bear.Scale = 1.35f;
            bear.BodyLength = 1.5f; bear.BodyWidth = 0.92f; bear.LegLength = 0.62f; bear.HeadSize = 0.52f;
            bear.Primary = C(96, 68, 48); bear.Secondary = C(134, 100, 70); bear.Accent = C(58, 42, 32);
            bear.HuntsAnimals = true; bear.HostileToPlayer = true; bear.AggroRange = 16f;
            bear.SpawnWeight = 3; bear.Health = 30f;

            var frog = New(MobId.Frog, "Frog", EntityArchetype.Blob, EntityBehaviour.Skittish);
            frog.Health = 4f; frog.Speed = 2.4f; frog.Scale = 0.45f;
            frog.BodyWidth = 0.26f; frog.LegLength = 0.0f; frog.HeadSize = 0.14f;
            frog.Primary = C(96, 176, 92); frog.Secondary = C(140, 208, 128); frog.Accent = C(38, 46, 40);
            frog.FleesFromPlayer = true; frog.Aquatic = true; frog.SpawnWeight = 9;

            var bat = New(MobId.Bat, "Bat", EntityArchetype.Wisp, EntityBehaviour.Skittish);
            bat.Health = 4f; bat.Speed = 5.0f; bat.Scale = 0.5f;
            bat.BodyWidth = 0.18f; bat.LegLength = 0f; bat.HeadSize = 0.12f;
            bat.Primary = C(64, 56, 66); bat.Secondary = C(104, 92, 106); bat.Accent = C(196, 152, 176);
            bat.FleesFromPlayer = true; bat.Flying = true; bat.Nocturnal = true; bat.SpawnWeight = 8;

            // =============================================================== hostile
            var hollow = New(MobId.Hollow, "Hollow", EntityArchetype.Biped, EntityBehaviour.Hostile);
            hollow.Health = 20f; hollow.Speed = 2.6f; hollow.Damage = 5f; hollow.Scale = 1.0f;
            hollow.BodyLength = 0.5f; hollow.BodyWidth = 0.62f; hollow.LegLength = 0.82f; hollow.HeadSize = 0.40f;
            hollow.Primary = C(96, 130, 116); hollow.Secondary = C(66, 92, 86); hollow.Accent = C(226, 88, 96);
            hollow.HostileToPlayer = true; hollow.Nocturnal = true; hollow.AggroRange = 22f;
            hollow.SpawnWeight = 14; hollow.Drop1 = ItemId.Bone; hollow.Drop1Min = 1; hollow.Drop1Max = 2;

            var spider = New(MobId.Spider, "Cave Spider", EntityArchetype.Quadruped, EntityBehaviour.Hostile);
            spider.Health = 16f; spider.Speed = 4.2f; spider.Damage = 4f; spider.Scale = 0.9f;
            spider.BodyLength = 0.85f; spider.BodyWidth = 0.62f; spider.LegLength = 0.46f; spider.HeadSize = 0.28f;
            spider.Primary = C(58, 44, 52); spider.Secondary = C(96, 62, 72); spider.Accent = C(226, 74, 82);
            spider.HostileToPlayer = true; spider.Nocturnal = true; spider.SpawnWeight = 9;

            var slime = New(MobId.Slime, "Slime", EntityArchetype.Blob, EntityBehaviour.Hostile);
            slime.Health = 12f; slime.Speed = 3.0f; slime.Damage = 3f; slime.Scale = 0.9f;
            slime.BodyWidth = 0.62f; slime.LegLength = 0f; slime.HeadSize = 0.20f;
            slime.Primary = C(118, 208, 130); slime.Secondary = C(88, 178, 104); slime.Accent = C(220, 255, 226);
            slime.HostileToPlayer = true; slime.SpawnWeight = 10;
            slime.Drop1 = ItemId.CuteEssence; slime.Drop1Min = 0; slime.Drop1Max = 1;

            var emberling = New(MobId.Emberling, "Emberling", EntityArchetype.Blob, EntityBehaviour.Hostile);
            emberling.Health = 14f; emberling.Speed = 3.4f; emberling.Damage = 5f; emberling.Scale = 0.7f;
            emberling.BodyWidth = 0.42f; emberling.LegLength = 0f; emberling.HeadSize = 0.18f;
            emberling.Primary = C(244, 132, 48); emberling.Secondary = C(212, 74, 32); emberling.Accent = C(255, 226, 140);
            emberling.HostileToPlayer = true; emberling.SpawnWeight = 5;
            emberling.Drop1 = ItemId.Coal; emberling.Drop1Min = 1; emberling.Drop1Max = 2;

            var wisp = New(MobId.Wisp, "Wisp", EntityArchetype.Wisp, EntityBehaviour.Hostile);
            wisp.Health = 10f; wisp.Speed = 3.8f; wisp.Damage = 4f; wisp.Scale = 0.6f;
            wisp.BodyWidth = 0.30f; wisp.LegLength = 0f; wisp.HeadSize = 0.16f;
            wisp.Primary = C(140, 224, 255); wisp.Secondary = C(96, 176, 240); wisp.Accent = C(240, 252, 255);
            wisp.HostileToPlayer = true; wisp.Flying = true; wisp.Nocturnal = true; wisp.SpawnWeight = 7;
            wisp.Drop1 = ItemId.CuteEssence; wisp.Drop1Min = 1; wisp.Drop1Max = 2;

            // ============================================================== villagers
            var villager = New(MobId.Villager, "Villager", EntityArchetype.Biped, EntityBehaviour.Villager);
            villager.Health = 20f; villager.Speed = 2.4f; villager.Scale = 0.95f;
            villager.BodyLength = 0.52f; villager.BodyWidth = 0.66f; villager.LegLength = 0.80f; villager.HeadSize = 0.42f;
            villager.Primary = C(186, 142, 108); villager.Secondary = C(120, 92, 176); villager.Accent = C(86, 66, 52);
            villager.Tradable = true; villager.SpawnWeight = 12;

            var farmer = New(MobId.Farmer, "Farmer", EntityArchetype.Biped, EntityBehaviour.Villager);
            farmer.Health = 20f; farmer.Speed = 2.4f; farmer.Scale = 0.95f;
            farmer.BodyLength = 0.52f; farmer.BodyWidth = 0.66f; farmer.LegLength = 0.80f; farmer.HeadSize = 0.42f;
            farmer.Primary = C(186, 142, 108); farmer.Secondary = C(126, 156, 82); farmer.Accent = C(232, 206, 122);
            farmer.Tradable = true; farmer.SpawnWeight = 9;

            var smith = New(MobId.Smith, "Blacksmith", EntityArchetype.Biped, EntityBehaviour.Villager);
            smith.Health = 24f; smith.Speed = 2.3f; smith.Damage = 4f; smith.Scale = 1.05f;
            smith.BodyLength = 0.54f; smith.BodyWidth = 0.72f; smith.LegLength = 0.80f; smith.HeadSize = 0.42f;
            smith.Primary = C(176, 132, 98); smith.Secondary = C(70, 70, 78); smith.Accent = C(214, 214, 220);
            smith.Tradable = true; smith.SpawnWeight = 5;

            var scribe = New(MobId.Scribe, "Scribe", EntityArchetype.Biped, EntityBehaviour.Villager);
            scribe.Health = 18f; scribe.Speed = 2.5f; scribe.Scale = 0.92f;
            scribe.BodyLength = 0.50f; scribe.BodyWidth = 0.64f; scribe.LegLength = 0.80f; scribe.HeadSize = 0.42f;
            scribe.Primary = C(192, 150, 116); scribe.Secondary = C(226, 224, 232); scribe.Accent = C(246, 206, 88);
            scribe.Tradable = true; scribe.SpawnWeight = 5;

            var guard = New(MobId.Guard, "Village Guard", EntityArchetype.Biped, EntityBehaviour.Guard);
            guard.Health = 30f; guard.Speed = 3.4f; guard.Damage = 6f; guard.Scale = 1.05f;
            guard.BodyLength = 0.54f; guard.BodyWidth = 0.72f; guard.LegLength = 0.82f; guard.HeadSize = 0.42f;
            guard.Primary = C(178, 134, 100); guard.Secondary = C(96, 106, 130); guard.Accent = C(216, 218, 224);
            guard.AggroRange = 26f; guard.SpawnWeight = 4;
            guard.Drop1 = ItemId.IronSword; guard.Drop1Min = 0; guard.Drop1Max = 1;

            // =============================================================== bandits
            var bandit = New(MobId.Bandit, "Bandit", EntityArchetype.Biped, EntityBehaviour.Bandit);
            bandit.Health = 26f; bandit.Speed = 3.6f; bandit.Damage = 6f; bandit.Scale = 1.0f;
            bandit.BodyLength = 0.52f; bandit.BodyWidth = 0.68f; bandit.LegLength = 0.82f; bandit.HeadSize = 0.40f;
            bandit.Primary = C(172, 128, 96); bandit.Secondary = C(74, 58, 52); bandit.Accent = C(196, 72, 64);
            bandit.HostileToPlayer = true; bandit.CanBeBandit = true; bandit.AggroRange = 24f;
            bandit.SpawnWeight = 8; bandit.Drop1 = ItemId.Coin; bandit.Drop1Min = 1; bandit.Drop1Max = 4;

            var archer = New(MobId.BanditArcher, "Bandit Archer", EntityArchetype.Biped, EntityBehaviour.Bandit);
            archer.Health = 20f; archer.Speed = 3.5f; archer.Damage = 5f; archer.AttackRange = 14f; archer.Scale = 0.98f;
            archer.BodyLength = 0.52f; archer.BodyWidth = 0.66f; archer.LegLength = 0.82f; archer.HeadSize = 0.40f;
            archer.Primary = C(166, 122, 92); archer.Secondary = C(84, 74, 56); archer.Accent = C(206, 168, 88);
            archer.HostileToPlayer = true; archer.CanBeBandit = true; archer.AggroRange = 28f;
            archer.SpawnWeight = 5; archer.Drop1 = ItemId.Coin; archer.Drop1Min = 1; archer.Drop1Max = 3;

            var chief = New(MobId.BanditChief, "Bandit Chief", EntityArchetype.Biped, EntityBehaviour.Bandit);
            chief.Health = 46f; chief.Speed = 3.7f; chief.Damage = 9f; chief.Scale = 1.18f;
            chief.BodyLength = 0.58f; chief.BodyWidth = 0.78f; chief.LegLength = 0.84f; chief.HeadSize = 0.44f;
            chief.Primary = C(150, 108, 84); chief.Secondary = C(58, 44, 44); chief.Accent = C(226, 176, 64);
            chief.HostileToPlayer = true; chief.CanBeBandit = true; chief.AggroRange = 30f;
            chief.SpawnWeight = 2;
            chief.Drop1 = ItemId.Coin; chief.Drop1Min = 8; chief.Drop1Max = 18;
            chief.Drop2 = ItemId.Diamond; chief.Drop2Min = 0; chief.Drop2Max = 1;

            // =============================================================== dragons
            var ember = New(MobId.EmberDragon, "Ember Dragon", EntityArchetype.Dragon, EntityBehaviour.Hostile);
            ember.Health = 120f; ember.Speed = 8.5f; ember.Damage = 12f; ember.AttackRange = 4.5f;
            ember.AttackCooldown = 2.4f; ember.Scale = 1.6f;
            ember.BodyLength = 3.2f; ember.BodyWidth = 0.9f; ember.LegLength = 0.7f; ember.HeadSize = 0.9f;
            ember.Primary = C(178, 54, 44); ember.Secondary = C(96, 30, 30); ember.Accent = C(255, 172, 72);
            ember.HostileToPlayer = true; ember.Flying = true; ember.AggroRange = 46f; ember.SightRange = 60f;
            ember.SpawnWeight = 1; ember.MinSpawnDistance = 60f; ember.MaxSpawnDistance = 150f;
            ember.Drop1 = ItemId.DragonScale; ember.Drop1Min = 3; ember.Drop1Max = 7;
            ember.Drop2 = ItemId.DragonHeart; ember.Drop2Min = 1; ember.Drop2Max = 1;

            var frost = New(MobId.FrostDragon, "Frost Dragon", EntityArchetype.Dragon, EntityBehaviour.Hostile);
            frost.Health = 140f; frost.Speed = 8.0f; frost.Damage = 11f; frost.AttackRange = 4.5f;
            frost.AttackCooldown = 2.4f; frost.Scale = 1.6f;
            frost.BodyLength = 3.2f; frost.BodyWidth = 0.9f; frost.LegLength = 0.7f; frost.HeadSize = 0.9f;
            frost.Primary = C(126, 176, 226); frost.Secondary = C(60, 96, 150); frost.Accent = C(226, 246, 255);
            frost.HostileToPlayer = true; frost.Flying = true; frost.AggroRange = 46f; frost.SightRange = 60f;
            frost.SpawnWeight = 1; frost.MinSpawnDistance = 60f; frost.MaxSpawnDistance = 150f;
            frost.Drop1 = ItemId.DragonScale; frost.Drop1Min = 3; frost.Drop1Max = 7;
            frost.Drop2 = ItemId.DragonHeart; frost.Drop2Min = 1; frost.Drop2Max = 1;

            var storm = New(MobId.StormDragon, "Storm Dragon", EntityArchetype.Dragon, EntityBehaviour.Hostile);
            storm.Health = 160f; storm.Speed = 9.5f; storm.Damage = 13f; storm.AttackRange = 5f;
            storm.AttackCooldown = 2.2f; storm.Scale = 1.7f;
            storm.BodyLength = 3.4f; storm.BodyWidth = 0.95f; storm.LegLength = 0.7f; storm.HeadSize = 0.95f;
            storm.Primary = C(122, 108, 178); storm.Secondary = C(58, 50, 96); storm.Accent = C(212, 196, 255);
            storm.HostileToPlayer = true; storm.Flying = true; storm.AggroRange = 52f; storm.SightRange = 70f;
            storm.SpawnWeight = 1; storm.MinSpawnDistance = 80f; storm.MaxSpawnDistance = 180f;
            storm.Drop1 = ItemId.DragonScale; storm.Drop1Min = 4; storm.Drop1Max = 9;
            storm.Drop2 = ItemId.DragonHeart; storm.Drop2Min = 1; storm.Drop2Max = 1;

            var elder = New(MobId.ElderDragon, "The Node Sovereign", EntityArchetype.Dragon, EntityBehaviour.Boss);
            elder.Health = 620f; elder.Speed = 9.0f; elder.Damage = 16f; elder.AttackRange = 6f;
            elder.AttackCooldown = 1.9f; elder.Scale = 2.6f;
            elder.BodyLength = 5.2f; elder.BodyWidth = 1.4f; elder.LegLength = 1.0f; elder.HeadSize = 1.5f;
            elder.Primary = C(146, 62, 194); elder.Secondary = C(70, 26, 104); elder.Accent = C(255, 128, 232);
            elder.HostileToPlayer = true; elder.Flying = true; elder.NodeOnly = true;
            elder.AggroRange = 200f; elder.SightRange = 240f; elder.SpawnWeight = 0;
            elder.MinSpawnDistance = 9999f; elder.MaxSpawnDistance = 9999f;
            elder.Drop1 = ItemId.DragonHeart; elder.Drop1Min = 3; elder.Drop1Max = 5;
            elder.Drop2 = ItemId.NodeShard; elder.Drop2Min = 12; elder.Drop2Max = 24;

            // ============================================================ the Node
            var fluff = New(MobId.Fluff, "Fluff", EntityArchetype.Blob, EntityBehaviour.Cute);
            fluff.Health = 12f; fluff.Speed = 2.0f; fluff.Scale = 0.55f;
            fluff.BodyWidth = 0.42f; fluff.LegLength = 0f; fluff.HeadSize = 0.20f;
            fluff.Primary = C(255, 246, 250); fluff.Secondary = C(255, 206, 232); fluff.Accent = C(120, 208, 236);
            fluff.NodeOnly = true; fluff.Petable = true; fluff.SpawnWeight = 18;
            fluff.Drop1 = ItemId.CuteEssence; fluff.Drop1Min = 1; fluff.Drop1Max = 2;

            var bunbun = New(MobId.Bunbun, "Bunbun", EntityArchetype.Quadruped, EntityBehaviour.Cute);
            bunbun.Health = 14f; bunbun.Speed = 3.4f; bunbun.Scale = 0.62f;
            bunbun.BodyLength = 0.5f; bunbun.BodyWidth = 0.34f; bunbun.LegLength = 0.24f; bunbun.HeadSize = 0.24f;
            bunbun.Primary = C(255, 228, 240); bunbun.Secondary = C(246, 190, 218); bunbun.Accent = C(158, 226, 210);
            bunbun.NodeOnly = true; bunbun.Petable = true; bunbun.SpawnWeight = 18;
            bunbun.Drop1 = ItemId.CuteEssence; bunbun.Drop1Min = 1; bunbun.Drop1Max = 2;
            bunbun.Drop2 = ItemId.Marshmallow; bunbun.Drop2Min = 0; bunbun.Drop2Max = 2;

            var cloudpup = New(MobId.Cloudpup, "Cloudpup", EntityArchetype.Quadruped, EntityBehaviour.Cute);
            cloudpup.Health = 18f; cloudpup.Speed = 3.8f; cloudpup.Scale = 0.8f;
            cloudpup.BodyLength = 0.72f; cloudpup.BodyWidth = 0.44f; cloudpup.LegLength = 0.34f; cloudpup.HeadSize = 0.30f;
            cloudpup.Primary = C(248, 250, 255); cloudpup.Secondary = C(214, 232, 250); cloudpup.Accent = C(140, 196, 246);
            cloudpup.NodeOnly = true; cloudpup.Petable = true; cloudpup.SpawnWeight = 14;
            cloudpup.Drop1 = ItemId.CottonBlock; cloudpup.Drop1Min = 1; cloudpup.Drop1Max = 3;

            var jelly = New(MobId.Jellybean, "Jellybean", EntityArchetype.Blob, EntityBehaviour.Cute);
            jelly.Health = 10f; jelly.Speed = 2.6f; jelly.Scale = 0.6f;
            jelly.BodyWidth = 0.36f; jelly.LegLength = 0f; jelly.HeadSize = 0.16f;
            jelly.Primary = C(150, 236, 190); jelly.Secondary = C(110, 206, 236); jelly.Accent = C(255, 250, 210);
            jelly.NodeOnly = true; jelly.Petable = true; jelly.SpawnWeight = 16;
            jelly.Drop1 = ItemId.Gumdrop; jelly.Drop1Min = 1; jelly.Drop1Max = 3;

            var starlet = New(MobId.Starlet, "Starlet", EntityArchetype.Wisp, EntityBehaviour.Cute);
            starlet.Health = 10f; starlet.Speed = 3.2f; starlet.Scale = 0.55f;
            starlet.BodyWidth = 0.26f; starlet.LegLength = 0f; starlet.HeadSize = 0.14f;
            starlet.Primary = C(255, 236, 156); starlet.Secondary = C(255, 200, 236); starlet.Accent = C(255, 255, 240);
            starlet.NodeOnly = true; starlet.Petable = true; starlet.Flying = true; starlet.SpawnWeight = 12;
            starlet.Drop1 = ItemId.CuteEssence; starlet.Drop1Min = 1; starlet.Drop1Max = 3;

            var puff = New(MobId.Puffcap, "Puffcap", EntityArchetype.Blob, EntityBehaviour.Cute);
            puff.Health = 16f; puff.Speed = 1.6f; puff.Scale = 0.75f;
            puff.BodyWidth = 0.48f; puff.LegLength = 0f; puff.HeadSize = 0.18f;
            puff.Primary = C(238, 176, 220); puff.Secondary = C(206, 138, 200); puff.Accent = C(255, 244, 222);
            puff.NodeOnly = true; puff.Petable = true; puff.SpawnWeight = 12;
            puff.Drop1 = ItemId.CandyCane; puff.Drop1Min = 1; puff.Drop1Max = 2;

            var nibble = New(MobId.Nibble, "Nibble", EntityArchetype.Bird, EntityBehaviour.Cute);
            nibble.Health = 10f; nibble.Speed = 3.6f; nibble.Scale = 0.58f;
            nibble.BodyLength = 0.40f; nibble.BodyWidth = 0.30f; nibble.LegLength = 0.26f; nibble.HeadSize = 0.20f;
            nibble.Primary = C(190, 244, 236); nibble.Secondary = C(146, 214, 226); nibble.Accent = C(255, 176, 200);
            nibble.NodeOnly = true; nibble.Petable = true; nibble.SpawnWeight = 14;
            nibble.Drop1 = ItemId.CuteEssence; nibble.Drop1Min = 1; nibble.Drop1Max = 2;

            var nodeGuardian = New(MobId.NodeGuardian, "Node Guardian", EntityArchetype.Dragon, EntityBehaviour.Hostile);
            nodeGuardian.Health = 90f; nodeGuardian.Speed = 6.4f; nodeGuardian.Damage = 9f;
            nodeGuardian.AttackRange = 3.6f; nodeGuardian.AttackCooldown = 2.0f; nodeGuardian.Scale = 1.2f;
            nodeGuardian.BodyLength = 2.2f; nodeGuardian.BodyWidth = 0.7f; nodeGuardian.LegLength = 0.6f; nodeGuardian.HeadSize = 0.7f;
            nodeGuardian.Primary = C(112, 210, 224); nodeGuardian.Secondary = C(60, 138, 168); nodeGuardian.Accent = C(255, 176, 224);
            nodeGuardian.HostileToPlayer = true; nodeGuardian.NodeOnly = true; nodeGuardian.Flying = true;
            nodeGuardian.AggroRange = 40f; nodeGuardian.SpawnWeight = 3;
            nodeGuardian.MinSpawnDistance = 40f; nodeGuardian.MaxSpawnDistance = 110f;
            nodeGuardian.Drop1 = ItemId.NodeShard; nodeGuardian.Drop1Min = 3; nodeGuardian.Drop1Max = 6;
        }

        /// <summary>
        /// Fills a spawn table for a habitat. Called a lot, so it does not allocate.
        /// </summary>
        public static int FillTable(MobDef[] into, bool night, bool node, bool overworld)
        {
            int n = 0;
            for (int i = 1; i < Count && n < into.Length; i++)
            {
                var d = T[i];
                if (d == null || d.Behaviour == EntityBehaviour.Boss) continue;
                if (d.SpawnWeight <= 0) continue;
                if (d.NodeOnly && !node) continue;
                if (d.OverworldOnly && node) continue;
                if (d.Nocturnal && !night && !node) continue;
                if (!d.Nocturnal && night && !node && d.Behaviour != EntityBehaviour.Hostile &&
                    d.Behaviour != EntityBehaviour.Villager && d.Behaviour != EntityBehaviour.Guard) continue;
                into[n++] = d;
            }
            return n;
        }
    }
}