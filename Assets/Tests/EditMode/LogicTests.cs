using System.Collections.Generic;
using NUnit.Framework;
using Zombie.Logic;

namespace Zombie.Tests
{
    public class InventoryTests
    {
        [Test]
        public void StartsWithRockOnly()
        {
            var inv = new Inventory();
            Assert.IsTrue(inv.Has(ItemId.Rock));
            Assert.IsFalse(inv.Has(ItemId.StoneHatchet));
            Assert.AreEqual(0, inv.Get(ResourceType.Wood));
        }

        [Test]
        public void CraftSpendsResourcesAndGivesTool()
        {
            var inv = new Inventory();
            inv.Add(ResourceType.Wood, 30);
            inv.Add(ResourceType.Stone, 15);
            Assert.IsTrue(inv.TryCraft(ItemId.StoneHatchet));
            Assert.IsTrue(inv.Has(ItemId.StoneHatchet));
            Assert.AreEqual(0, inv.Get(ResourceType.Wood));
            Assert.AreEqual(0, inv.Get(ResourceType.Stone));
        }

        [Test]
        public void CannotCraftWithoutResources()
        {
            var inv = new Inventory();
            inv.Add(ResourceType.Wood, 29);
            inv.Add(ResourceType.Stone, 15);
            Assert.IsFalse(inv.TryCraft(ItemId.StoneHatchet));
            Assert.AreEqual(29, inv.Get(ResourceType.Wood));
        }

        [Test]
        public void ToolsAreUniquePlaceablesStack()
        {
            var inv = new Inventory();
            inv.Add(ResourceType.Wood, 500);
            Assert.IsTrue(inv.TryCraft(ItemId.WoodenSpear));
            Assert.IsFalse(inv.TryCraft(ItemId.WoodenSpear));
            Assert.IsTrue(inv.TryCraft(ItemId.WoodenSpikes));
            Assert.IsTrue(inv.TryCraft(ItemId.WoodenSpikes));
            Assert.AreEqual(2, inv.Count(ItemId.WoodenSpikes));
            Assert.IsTrue(inv.TryConsume(ItemId.WoodenSpikes));
            Assert.AreEqual(1, inv.Count(ItemId.WoodenSpikes));
        }

        [Test]
        public void DeathLosesEverythingButRock()
        {
            var inv = new Inventory();
            inv.Add(ResourceType.Wood, 100);
            inv.Add(ResourceType.Metal, 5);
            inv.AddItem(ItemId.Hammer);
            inv.AddItem(ItemId.WoodenSpikes, 3);
            inv.ClearOnDeath();
            Assert.AreEqual(0, inv.Get(ResourceType.Wood));
            Assert.AreEqual(0, inv.Get(ResourceType.Metal));
            Assert.IsFalse(inv.Has(ItemId.Hammer));
            Assert.AreEqual(0, inv.Count(ItemId.WoodenSpikes));
            Assert.IsTrue(inv.Has(ItemId.Rock));
        }

        [Test]
        public void ScaledCostNeverBecomesFree()
        {
            var cost = new Cost(10, 1, 0).Scaled(0.1f);
            Assert.AreEqual(1, cost.Wood);
            Assert.AreEqual(1, cost.Stone);
            Assert.AreEqual(0, cost.Metal);
        }
    }

    public class BuildGridTests
    {
        [Test]
        public void FoundationOnlyOnGroundLevel()
        {
            var grid = new BuildGrid();
            Assert.AreEqual(PlaceResult.Ok, grid.Place(PieceKind.Foundation, SlotKey.Cell(0, 0, 0)));
            Assert.AreEqual(PlaceResult.Occupied, grid.Place(PieceKind.Foundation, SlotKey.Cell(0, 0, 0)));
            Assert.AreEqual(PlaceResult.WrongSlot, grid.CanPlace(PieceKind.Foundation, SlotKey.Cell(1, 1, 0)));
        }

        [Test]
        public void WallNeedsFoundationOnEitherSide()
        {
            var grid = new BuildGrid();
            Assert.AreEqual(PlaceResult.NoSupport, grid.CanPlace(PieceKind.Wall, SlotKey.South(0, 0, 0)));
            grid.Place(PieceKind.Foundation, SlotKey.Cell(0, 0, 0));
            Assert.AreEqual(PlaceResult.Ok, grid.CanPlace(PieceKind.Wall, SlotKey.South(0, 0, 0)));
            // North edge of cell (0,0) is the south edge of cell (0,1).
            Assert.AreEqual(PlaceResult.Ok, grid.CanPlace(PieceKind.Wall, SlotKey.South(0, 0, 1)));
            // East edge of cell (0,0) is the west edge of cell (1,0).
            Assert.AreEqual(PlaceResult.Ok, grid.CanPlace(PieceKind.Doorway, SlotKey.West(1, 0, 0)));
            Assert.AreEqual(PlaceResult.NoSupport, grid.CanPlace(PieceKind.Wall, SlotKey.West(2, 0, 0)));
        }

        [Test]
        public void FloorNeedsWallBelow()
        {
            var grid = new BuildGrid();
            grid.Place(PieceKind.Foundation, SlotKey.Cell(0, 0, 0));
            Assert.AreEqual(PlaceResult.NoSupport, grid.CanPlace(PieceKind.Floor, SlotKey.Cell(0, 1, 0)));
            grid.Place(PieceKind.Wall, SlotKey.West(0, 0, 0));
            Assert.AreEqual(PlaceResult.Ok, grid.CanPlace(PieceKind.Floor, SlotKey.Cell(0, 1, 0)));
        }

        [Test]
        public void SecondStoryWallStandsOnFloorOrWall()
        {
            var grid = new BuildGrid();
            grid.Place(PieceKind.Foundation, SlotKey.Cell(0, 0, 0));
            grid.Place(PieceKind.Wall, SlotKey.South(0, 0, 0));
            Assert.AreEqual(PlaceResult.Ok, grid.CanPlace(PieceKind.Wall, SlotKey.South(0, 1, 0)));
            Assert.AreEqual(PlaceResult.NoSupport, grid.CanPlace(PieceKind.Wall, SlotKey.West(0, 1, 0)));
            grid.Place(PieceKind.Floor, SlotKey.Cell(0, 1, 0));
            Assert.AreEqual(PlaceResult.Ok, grid.CanPlace(PieceKind.Wall, SlotKey.West(0, 1, 0)));
        }

        [Test]
        public void DestroyingFoundationCollapsesWhatItHolds()
        {
            var grid = new BuildGrid();
            grid.Place(PieceKind.Foundation, SlotKey.Cell(0, 0, 0));
            grid.Place(PieceKind.Wall, SlotKey.South(0, 0, 0));
            grid.Place(PieceKind.Wall, SlotKey.West(0, 0, 0));
            grid.Place(PieceKind.Floor, SlotKey.Cell(0, 1, 0));
            grid.Place(PieceKind.Wall, SlotKey.South(0, 1, 0));
            grid.Place(PieceKind.Foundation, SlotKey.Cell(1, 0, 0));
            grid.Place(PieceKind.Stairs, SlotKey.Volume(1, 0, 0));
            Assert.AreEqual(7, grid.Count);

            var removed = new List<SlotKey>();
            grid.Remove(SlotKey.Cell(0, 0, 0), removed);

            // Foundation, both walls, the floor and the upper wall go; the neighbour cell with stairs stays.
            Assert.AreEqual(5, removed.Count);
            Assert.AreEqual(SlotKey.Cell(0, 0, 0), removed[0]);
            Assert.AreEqual(2, grid.Count);
            Assert.IsTrue(grid.IsOccupied(SlotKey.Volume(1, 0, 0)));

            removed.Clear();
            grid.Remove(SlotKey.Cell(1, 0, 0), removed);
            Assert.AreEqual(2, removed.Count);
            Assert.AreEqual(0, grid.Count);
        }

        [Test]
        public void WallKeptWhenNeighbourFoundationRemains()
        {
            var grid = new BuildGrid();
            grid.Place(PieceKind.Foundation, SlotKey.Cell(0, 0, 0));
            grid.Place(PieceKind.Foundation, SlotKey.Cell(0, 0, 1));
            grid.Place(PieceKind.Wall, SlotKey.South(0, 0, 1)); // shared edge

            var removed = new List<SlotKey>();
            grid.Remove(SlotKey.Cell(0, 0, 0), removed);

            Assert.AreEqual(1, removed.Count);
            Assert.IsTrue(grid.IsOccupied(SlotKey.South(0, 0, 1)));
        }

        [Test]
        public void StairsAndFloorAboveExcludeEachOther()
        {
            var grid = new BuildGrid();
            grid.Place(PieceKind.Foundation, SlotKey.Cell(0, 0, 0));
            grid.Place(PieceKind.Wall, SlotKey.South(0, 0, 0));
            grid.Place(PieceKind.Stairs, SlotKey.Volume(0, 0, 0));
            Assert.AreEqual(PlaceResult.BlockedByStairs, grid.CanPlace(PieceKind.Floor, SlotKey.Cell(0, 1, 0)));
        }
    }

    public class WavePlannerTests
    {
        [Test]
        public void FirstNightIsSmallAndShamblersOnly()
        {
            var plan = WavePlanner.Plan(1, 1, 150);
            Assert.AreEqual(WavePlanner.BaseCount, plan.Total);
            Assert.AreEqual(0, plan.Runners);
            Assert.AreEqual(0, plan.Brutes);
            Assert.AreEqual(1, plan.Directions);
        }

        [Test]
        public void HordesGrowEveryNight()
        {
            int previous = 0;
            for (int night = 1; night <= 15; night++)
            {
                var plan = WavePlanner.Plan(night, 1, 150);
                Assert.Greater(plan.Total, previous, "night " + night);
                previous = plan.Total;
            }
        }

        [Test]
        public void BrutesFromNightThree()
        {
            Assert.AreEqual(0, WavePlanner.Plan(2, 1, 150).Brutes);
            Assert.Greater(WavePlanner.Plan(3, 1, 150).Brutes, 0);
            Assert.Greater(WavePlanner.Plan(2, 1, 150).Runners, 0);
        }

        [Test]
        public void CapIsRespectedAndHealthGrowsInstead()
        {
            var plan = WavePlanner.Plan(60, 4, 150);
            Assert.AreEqual(150, plan.Total);
            Assert.Greater(plan.HealthMultiplier, 1f);
            Assert.AreEqual(3, plan.Directions);
        }

        [Test]
        public void MorePlayersMeansBiggerHorde()
        {
            Assert.Greater(WavePlanner.Plan(4, 4, 150).Total, WavePlanner.Plan(4, 1, 150).Total);
        }
    }
}
