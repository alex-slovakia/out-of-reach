using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Composition.Editor
{
    /// <summary>New scene-instance hardware and measured old-rail proxies. Never changes a source model or saves a scene.</summary>
    public static class I06InteriorHandholdAuthoring
    {
        public const string RootName = "I06 interior handholds";
        public sealed class Result
        {
            public Transform Root;
            public I06InteriorHandholds Registry;
            public CapsuleCollider[] Rails;
            public Transform[] ContactMarkers;
            public int ExistingRailProxyCount, NewVisibleRailCount;
        }

        /// <summary>Supply the actual delivered ship-space root and existing URP amber/metal/graphite materials.</summary>
        public static Result Create(Transform shipSpace, Material amber, Material metal, Material graphite)
        {
            if (Application.isPlaying || !shipSpace || EditorUtility.IsPersistent(shipSpace)
                || (shipSpace.lossyScale - Vector3.one).sqrMagnitude > .000001f || shipSpace.Find(RootName)
                || !amber || !metal || !graphite)
                throw new ArgumentException("Use one unmodified scene ship root at metre scale and existing materials, in Edit mode.");
            // Pin the mounting surfaces to the actual delivered collision layout. Do not silently fit a different ship.
            RequireBox(shipSpace, "COL_Cockpit_Ceiling", new Vector3(0,3.22f,0), new Vector3(6,.2f,6));
            RequireBox(shipSpace, "COL_Cockpit_ConsoleTop", new Vector3(0,.96f,-1.95f), new Vector3(3.1f,.12f,.85f));
            RequireBox(shipSpace, "COL_Engineering_PowerRack", new Vector3(-2.02f,1.15f,6.1f), new Vector3(.58f,2.3f,1.6f));
            RequireBox(shipSpace, "COL_Engineering_EclssRack", new Vector3(2.02f,1.15f,8.1f), new Vector3(.58f,2.3f,1.6f));
            RequireBox(shipSpace, "COL_Habitat_Berth", new Vector3(-2.1125f,1.275f,13.25f), new Vector3(.535f,2.55f,1.446f));
            RequireBox(shipSpace, "COL_Habitat_Galley", new Vector3(-2.145f,1.2f,14.75f), new Vector3(.47f,2.4f,1.446f));
            RequireBox(shipSpace, "COL_Habitat_GrowCassette", new Vector3(2.1175f,1.227f,14.75f), new Vector3(.525f,1.654f,1.006f));
            RequireVisible(shipSpace, "A7_Engineering"); RequireVisible(shipSpace, "A7_Airlock");
            Transform root = Child(RootName, shipSpace);
            var rails = new List<CapsuleCollider>(); var markers = new List<Transform>(); int existing = 0;
            try
            {
                // Source a7_ship.py:997–1010. Exact amber30mm bars; cover only straight shaft, not bends/standoffs.
                foreach (float z in new[] {3f,17f}) foreach (int face in new[] {-1,1}) foreach (int side in new[] {-1,1})
                {
                    Existing(root, "Existing portal " + z + " " + face + " " + side,
                        new Vector3(side*1.10f,1.30f,z+face*.14f), .54f, rails, markers); existing++;
                }
                // Interior face only. No exterior floating rail registrations.
                foreach (int side in new[] {-1,1})
                { Existing(root,"Existing outer portal inside "+side,new Vector3(side*1.10f,1.30f,20.86f),.54f,rails,markers); existing++; }
                Existing(root,"Existing ECLSS vertical bar",new Vector3(1.668f,1.28f,7.43f),.50f,rails,markers); existing++;

                // Catch the low console from floor and modest elevated approaches. Supported from the real ceiling.
                // Central console/RecoveryBeacon rays target y1.45; shaft at1.50 leaves that intended ray below it.
                for (int side=-1;side<=1;side+=2)
                {
                    Vector3 ceiling = new Vector3(side*1.0f,3.12f,-1.38f);
                    Plate(root,"Console ceiling plate "+side,ceiling,Vector3.down,metal);
                    SolidTube(root,"Console support "+side,ceiling+Vector3.down*.012f,
                        new Vector3(side*1.0f,1.50f,-1.38f),.016f,metal);
                }
                NewRail(root,"Console lower",new Vector3(-1,1.50f,-1.38f),new Vector3(1,1.50f,-1.38f),amber,graphite,rails,markers);
                NewRail(root,"Console upper",new Vector3(-1,1.95f,-1.38f),new Vector3(1,1.95f,-1.38f),amber,graphite,rails,markers);

                foreach (float y in new[] {1.50f,1.95f})
                {
                    Supported(root,"Power "+y,new Vector3(-1.58f,y,5.45f),new Vector3(-1.58f,y,6.75f),
                        new Vector3(-1.73f,y,5.45f),new Vector3(-1.73f,y,6.75f),Vector3.right,amber,metal,graphite,rails,markers);
                    Supported(root,"ECLSS "+y,new Vector3(1.58f,y,7.55f),new Vector3(1.58f,y,8.65f),
                        new Vector3(1.73f,y,7.55f),new Vector3(1.73f,y,8.65f),Vector3.left,amber,metal,graphite,rails,markers);
                    // Rear jamb area, away from SleepEntry z13.03. Do not run a bar through its entrance target.
                    Supported(root,"Berth rear approach "+y,new Vector3(-1.695f,y,13.47f),new Vector3(-1.695f,y,13.92f),
                        new Vector3(-1.845f,y,13.47f),new Vector3(-1.845f,y,13.92f),Vector3.right,amber,metal,graphite,rails,markers);
                }
                // Low water-side handle avoids the real open drawer and table at z14.05–14.89.
                Supported(root,"Galley water approach",new Vector3(-1.76f,1.50f,15.01f),new Vector3(-1.76f,1.50f,15.41f),
                    new Vector3(-1.91f,1.50f,15.01f),new Vector3(-1.91f,1.50f,15.41f),Vector3.right,amber,metal,graphite,rails,markers);
                // Upper shaft clears the open drawer top1.775m; visible longer stand-offs reach the cabinet face.
                Supported(root,"Galley upper approach",new Vector3(-1.50f,1.95f,14.15f),new Vector3(-1.50f,1.95f,15.35f),
                    new Vector3(-1.91f,1.95f,14.15f),new Vector3(-1.91f,1.95f,15.35f),Vector3.right,amber,metal,graphite,rails,markers);
                foreach (float y in new[] {1.50f,1.90f})
                    Supported(root,"Grow approach "+y,new Vector3(1.705f,y,14.35f),new Vector3(1.705f,y,15.15f),
                        new Vector3(1.855f,y,14.35f),new Vector3(1.855f,y,15.15f),Vector3.left,amber,metal,graphite,rails,markers);

                var registry=root.gameObject.AddComponent<I06InteriorHandholds>(); registry.ConfigureRails(rails.ToArray());
                EditorUtility.SetDirty(registry);
                return new Result {Root=root,Registry=registry,Rails=rails.ToArray(),ContactMarkers=markers.ToArray(),
                    ExistingRailProxyCount=existing,NewVisibleRailCount=rails.Count-existing};
            }
            catch {Object.DestroyImmediate(root.gameObject);throw;}
        }

        private static void Existing(Transform root,string name,Vector3 centre,float height,List<CapsuleCollider> rails,List<Transform> markers)
        {
            Transform t=Child(name,root); t.localPosition=centre;
            var c=t.gameObject.AddComponent<CapsuleCollider>();c.radius=.015f;c.height=height;c.direction=1;c.isTrigger=false;
            rails.Add(c);markers.Add(Marker(t));
        }
        private static void Supported(Transform root,string name,Vector3 a,Vector3 b,Vector3 mountA,Vector3 mountB,Vector3 outNormal,
            Material amber,Material metal,Material graphite,List<CapsuleCollider> rails,List<Transform> markers)
        {
            Plate(root,name+" plate A",mountA,outNormal,metal);Plate(root,name+" plate B",mountB,outNormal,metal);
            SolidTube(root,name+" stand-off A",mountA+outNormal*.012f,a,.016f,metal);
            SolidTube(root,name+" stand-off B",mountB+outNormal*.012f,b,.016f,metal);
            NewRail(root,name,a,b,amber,graphite,rails,markers);
        }
        private static void NewRail(Transform root,string name,Vector3 a,Vector3 b,Material amber,Material graphite,
            List<CapsuleCollider> rails,List<Transform> markers)
        {
            CapsuleCollider c=SolidTube(root,name,a,b,.015f,amber);rails.Add(c);markers.Add(Marker(c.transform));
            Vector3 axis=(b-a).normalized;
            SolidTube(root,name+" collar A",a+axis*.035f,a+axis*.055f,.023f,graphite);
            SolidTube(root,name+" collar B",b-axis*.055f,b-axis*.035f,.023f,graphite);
        }
        private static CapsuleCollider SolidTube(Transform root,string name,Vector3 a,Vector3 b,float radius,Material material)
        {
            float length=Vector3.Distance(a,b);if(length<.01f)throw new ArgumentException("Degenerate support/rail.");
            Transform t=Child(name,root);t.localPosition=(a+b)*.5f;t.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
            Visual(t,"Visible shaft",PrimitiveType.Cylinder,Vector3.zero,new Vector3(radius*2,length*.5f,radius*2),material);
            Visual(t,"Visible end A",PrimitiveType.Sphere,Vector3.down*length*.5f,Vector3.one*radius*2,material);
            Visual(t,"Visible end B",PrimitiveType.Sphere,Vector3.up*length*.5f,Vector3.one*radius*2,material);
            var c=t.gameObject.AddComponent<CapsuleCollider>();c.direction=1;c.radius=radius;c.height=length+radius*2;c.isTrigger=false;
            return c;
        }
        private static void Plate(Transform root,string name,Vector3 surface,Vector3 normal,Material metal)
        {
            Transform t=Child(name,root);t.localPosition=surface+normal*.006f;
            t.localRotation=Quaternion.LookRotation(normal,Mathf.Abs(Vector3.Dot(normal,Vector3.up))>.9f?Vector3.forward:Vector3.up);
            Visual(t,"Visible mounting plate",PrimitiveType.Cube,Vector3.zero,new Vector3(.08f,.08f,.012f),metal);
            var c=t.gameObject.AddComponent<BoxCollider>();c.size=new Vector3(.08f,.08f,.012f);
        }
        private static void Visual(Transform parent,string name,PrimitiveType primitive,Vector3 localPosition,Vector3 scale,Material material)
        {
            GameObject go=GameObject.CreatePrimitive(primitive);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=localPosition;go.transform.localScale=scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
            go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.On;
        }
        private static Transform Marker(Transform rail) {return Child("Contact reference - not a player spawn",rail);}
        private static Transform Child(string name,Transform parent) {var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
        private static void RequireVisible(Transform ship,string name)
        {
            Transform t=ship.GetComponentsInChildren<Transform>(true).SingleOrDefault(x=>x.name==name);
            if(!t||!t.GetComponentsInChildren<Renderer>().Any(r=>r.enabled))throw new InvalidOperationException("Expected visible original model "+name);
        }
        private static void RequireBox(Transform ship,string name,Vector3 centre,Vector3 size)
        {
            BoxCollider box=ship.GetComponentsInChildren<BoxCollider>(true).SingleOrDefault(c=>c.name==name);
            if(!box||!box.enabled||!box.gameObject.activeInHierarchy||box.isTrigger
                ||Vector3.Distance(ship.InverseTransformPoint(box.transform.TransformPoint(box.center)),centre)>.002f
                ||Quaternion.Angle(ship.rotation,box.transform.rotation)>.01f
                ||Vector3.Distance(Vector3.Scale(box.size,box.transform.lossyScale),size)>.002f)
                throw new InvalidOperationException("Actual mounting surface disagrees with pinned ship layout: "+name);
        }
    }
}
