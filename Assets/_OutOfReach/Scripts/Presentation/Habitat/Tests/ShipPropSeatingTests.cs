using NUnit.Framework;
using OutOfReach.Presentation.Habitat;
using UnityEngine;

namespace OutOfReach.Tests.HabitatProps
{
    public sealed class ShipPropSeatingTests
    {
        [Test]
        public void HidesCockpitMountsAndSeatsMedicalBoxOnQuilt()
        {
            var ship = new GameObject(ShipPropSeating.ShipRootName);
            try
            {
                var group = new GameObject(ShipPropSeating.GroupName).transform;
                group.SetParent(ship.transform, false);
                Transform Mount(string name, Vector3 at)
                {
                    var t = new GameObject(name).transform; t.SetParent(group, false); t.localPosition = at; return t;
                }
                Transform instrument = Mount("Secured vintage_spacecraft_instrument", new Vector3(2.13f, 1.25f, -1.15f));
                Transform binoculars = Mount("Secured binoculars", new Vector3(-2.13f, .76f, -.65f));
                Transform medical = Mount("Secured medical_box", new Vector3(2.13f, 1.32f, 12.1f));
                Transform food = Mount("Secured long_life_food", new Vector3(-1.89f, 1.6f, 15.2f));

                Assert.AreEqual(3, ShipPropSeating.Apply(ship.transform));
                Assert.IsFalse(instrument.gameObject.activeSelf);
                Assert.IsFalse(binoculars.gameObject.activeSelf);
                Assert.AreEqual(ShipPropSeating.QuiltFaceX, medical.localPosition.x, 1e-4f);
                Assert.AreEqual(1.32f, medical.localPosition.y, 1e-4f);
                Assert.AreEqual(12.1f, medical.localPosition.z, 1e-4f);
                Assert.IsTrue(food.gameObject.activeSelf);
                Assert.AreEqual(new Vector3(-1.89f, 1.6f, 15.2f), food.localPosition);
                Assert.AreEqual(0, ShipPropSeating.Apply(ship.transform), "idempotent");
            }
            finally { Object.DestroyImmediate(ship); }
        }
    }
}
