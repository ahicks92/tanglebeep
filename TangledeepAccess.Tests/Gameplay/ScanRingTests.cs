using System.Collections.Generic;
using TangledeepAccess.Gameplay;
using Xunit;

namespace TangledeepAccess.Tests.Gameplay {
    public class ScanRingTests {
        // Identity is by reference, so use distinct object instances as ids (strings interned would
        // alias, so wrap each in a fresh object).
        private static readonly object A = new object();
        private static readonly object B = new object();
        private static readonly object C = new object();

        private static IList<ScanRing.Entry> Set(params (object id, int x, int y)[] items) {
            var list = new List<ScanRing.Entry>();
            foreach (var it in items) {
                list.Add(new ScanRing.Entry(it.id, it.x, it.y));
            }
            return list;
        }

        [Fact]
        public void EmptyRingYieldsNull() {
            Assert.Null(new ScanRing().Next());
        }

        [Fact]
        public void CyclesThroughAllEntries() {
            var ring = new ScanRing();
            ring.Reconcile(Set((A, 1, 0), (B, 2, 0), (C, 3, 0)));
            Assert.Equal(3, ring.Count);

            // Three distinct ids over three Next() calls, then it wraps to the first again.
            var first = ring.Next().Value.Id;
            var second = ring.Next().Value.Id;
            var third = ring.Next().Value.Id;
            Assert.Equal(3, new HashSet<object> { first, second, third }.Count);
            Assert.Same(first, ring.Next().Value.Id); // wrapped
        }

        [Fact]
        public void NewcomerIsInsertedAtCursorSoItPingsNext() {
            var ring = new ScanRing();
            ring.Reconcile(Set((A, 0, 0), (B, 0, 0)));
            ring.Next(); // consume one; cursor now points at the other survivor

            // C appears. It must be the very next thing played, ahead of the remaining old entry.
            ring.Reconcile(Set((A, 0, 0), (B, 0, 0), (C, 0, 0)));
            Assert.Same(C, ring.Next().Value.Id);
        }

        [Fact]
        public void MovingEntityKeepsItsSlotAndRefreshesCoordinates() {
            var ring = new ScanRing();
            ring.Reconcile(Set((A, 1, 1)));
            ring.Reconcile(Set((A, 4, -2))); // same id, new position; not a newcomer

            Assert.Equal(1, ring.Count);
            ScanRing.Entry e = ring.Next().Value;
            Assert.Same(A, e.Id);
            Assert.Equal(4, e.X);
            Assert.Equal(-2, e.Y);
        }

        [Fact]
        public void GoneEntityIsDroppedAndNeverPlayed() {
            var ring = new ScanRing();
            ring.Reconcile(Set((A, 0, 0), (B, 0, 0), (C, 0, 0)));
            ring.Reconcile(Set((A, 0, 0), (C, 0, 0))); // B left view

            Assert.Equal(2, ring.Count);
            var ids = new HashSet<object> { ring.Next().Value.Id, ring.Next().Value.Id };
            Assert.DoesNotContain(B, ids);
        }

        [Fact]
        public void SweepIsReorderedByXThenYOnEachLap() {
            var ring = new ScanRing();
            // Inserted out of spatial order; reconcile preserves insertion order, so the first lap
            // plays in that order, but the second lap sweeps by x then y.
            ring.Reconcile(Set((A, 2, 5), (B, 1, 9), (C, 1, 3)));

            // First lap: insertion order (no sort yet).
            Assert.Same(A, ring.Next().Value.Id);
            Assert.Same(B, ring.Next().Value.Id);
            Assert.Same(C, ring.Next().Value.Id); // wrap here sorts for the next lap

            // Second lap: sorted by x ascending, then y ascending -> C(1,3), B(1,9), A(2,5).
            Assert.Same(C, ring.Next().Value.Id);
            Assert.Same(B, ring.Next().Value.Id);
            Assert.Same(A, ring.Next().Value.Id);
        }

        [Fact]
        public void NewcomerStillPingsNextEvenAfterALapSort() {
            var ring = new ScanRing();
            ring.Reconcile(Set((A, 5, 0), (B, 1, 0)));
            ring.Next(); // A
            ring.Next(); // B; wrap sorts to B(1),A(5)

            // C appears mid-lap and must still play immediately, ahead of the sorted survivors.
            ring.Reconcile(Set((A, 5, 0), (B, 1, 0), (C, 9, 0)));
            Assert.Same(C, ring.Next().Value.Id);
        }

        [Fact]
        public void RemovingTheUpcomingEntryAdvancesToTheNextSurvivor() {
            var ring = new ScanRing();
            ring.Reconcile(Set((A, 0, 0), (B, 0, 0), (C, 0, 0)));
            ring.Next(); // played A; B is now next
            ring.Reconcile(Set((A, 0, 0), (C, 0, 0))); // remove B (the upcoming one)
            Assert.Same(C, ring.Next().Value.Id); // not A: the cursor tracked the removal
        }
    }
}
