using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace OutOfReach.Presentation.LaunchVegetation.Editor
{
    /// <summary>Photographic CC0 foliage on actual ground beside the apron, not a distant procedural field.</summary>
    public static class LaunchVegetationAuthoring
    {
        public const string AssetRoot = "Assets/_OutOfReach/Visuals/LaunchVegetationV03";
        public const string GroupName = "Launch vegetation V03 - photographed foreground";
        public static string LastReport { get; private set; }
        private static readonly Vector2[] PatchCenters =
        {
            new Vector2(-83,-57), new Vector2(-92,-45), new Vector2(-84,-30), new Vector2(-103,-18),
            new Vector2(-83,10), new Vector2(-100,34), new Vector2(-82,53), new Vector2(-104,58),
            new Vector2(82,-51), new Vector2(94,-27), new Vector2(84,9), new Vector2(90,44),
            new Vector2(-56,80), new Vector2(58,84), new Vector2(-118,-52), new Vector2(115,54)
        };

        public static GameObject Apply(Transform site)
        {
            if (!site || site.name != "Launch site - stationary" || Application.isPlaying)
                throw new InvalidOperationException("Use the existing stationary launch site in Edit mode.");
            if ((site.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                throw new InvalidOperationException("Retain metre-correct site scale.");
            Transform terrain = site.Find("Coastal relief and launch pad detail");
            if (!terrain || !terrain.gameObject.activeInHierarchy) throw new InvalidOperationException("Active finished terrain required.");
            var before = site.GetComponentsInChildren<Collider>(true).Select(c => c.GetInstanceID()).OrderBy(x => x).ToArray();
            var sampler = new TerrainSampler(site, terrain);
            List<Bounds> rockFootprints = RockFootprints(site);
            EnsureFolder(AssetRoot + "/Generated");
            Material grass = PrepareMaterial("grass_medium_02", new Color(1.08f,.83f,.43f), .40f);
            Material shrub = PrepareMaterial("shrub_04", new Color(.94f,1f,.88f), .40f);
            Material detail = shrub;
            string[] grassNames = { "coastal_grass_dense_0", "coastal_grass_dense_1", "coastal_grass_dense_2" };
            Material pebble = AssetDatabase.LoadAssetAtPath<Material>("Assets/_OutOfReach/Visuals/ThirdParty/PolyHaven/Materials/PH_namaqualand_boulder_02.mat");
            if (!pebble) throw new InvalidOperationException("Existing licensed boulder material required.");
            string[] shrubNames = { "coastal_scrub_dense_a", "coastal_scrub_dense_b" };
            var sources = new Dictionary<string, Source>();
            foreach (string name in grassNames.Concat(shrubNames).Concat(new[] { "shrub_04_b", "coastal_pebble_scan" }))
                for (int lod = 0; lod < 2; lod++) sources.Add(name + "_LOD" + lod, PrepareModel(name + "_LOD" + lod));
            var previous = site.Find(GroupName);
            var root = new GameObject(GroupName); root.transform.SetParent(site, false);
            int grassCount = 0, shrubCount = 0, detailCount = 0, nearReviewCount = 0, rockRejected = 0, pebbleCount = 0;
            float minGrassHeight = float.PositiveInfinity, maxGrassHeight = 0;
            long highTriangles = 0, lowTriangles = 0;
            var random = new System.Random(150303);
            try
            {
                for (int patch = 0; patch < PatchCenters.Length; patch++)
                {
                    var batches = new List<CombineInstance>[2,4];
                    for (int lod = 0; lod < 2; lod++) for (int type = 0; type < 4; type++) batches[lod,type] = new List<CombineInstance>();
                    Vector2 center = PatchCenters[patch];
                    // Eight irregular grass colonies span the ground margin; sparse shrubs occupy only three colonies.
                    for (int island = 0; island < 8; island++)
                    {
                        float a = Range(random,0,Mathf.PI*2), radius = Range(random,1f,8.5f);
                        Vector2 islandCenter = center + new Vector2(Mathf.Cos(a),Mathf.Sin(a)) * radius;
                        for (int i = 0; i < 8; i++)
                        {
                            Vector2 p = islandCenter + Disc(random,1.65f);
                            if (!TrySeat(p,out Vector3 position,out Quaternion normalRotation)) continue;
                            string model = grassNames[random.Next(grassNames.Length)];
                            float desiredHeight = Range(random,.43f,.70f);
                            float scale = desiredHeight / sources[model+"_LOD0"].Size.y;
                            Add(model,0,position,normalRotation * Quaternion.Euler(0,Range(random,0,360),0),Vector3.one*scale);
                            grassCount++;
                            minGrassHeight = Mathf.Min(minGrassHeight,desiredHeight); maxGrassHeight = Mathf.Max(maxGrassHeight,desiredHeight);
                            if (Vector2.Distance(p,new Vector2(-78,-52)) < 15) nearReviewCount++;
                        }
                        // Broad low shrubs make the green masses; tiny leaf branches are occasional close details.
                        Vector2 bush = islandCenter + Disc(random,.6f);
                        if (island % 3 == 0 && TrySeat(bush,out Vector3 bushPosition,out Quaternion bushRotation))
                        {
                            string model = shrubNames[random.Next(shrubNames.Length)];
                            float size = Range(random,1.1f,1.65f);
                            Add(model,1,bushPosition,bushRotation * Quaternion.Euler(0,Range(random,0,360),0),new Vector3(size*1.35f,size*.65f,size*1.35f));
                            shrubCount++;
                        }
                        if (island == 0 && TrySeat(islandCenter + new Vector2(.5f,.4f),out Vector3 detailPosition,out Quaternion detailRotation))
                        {
                            Add("shrub_04_b",2,detailPosition,detailRotation * Quaternion.Euler(0,Range(random,0,360),0),Vector3.one*Range(random,1.4f,2.1f));
                            detailCount++;
                        }
                    }
                    // Small scanned stones share the actual ground sampler and protected footprints.
                    for (int stone = 0; stone < 20; stone++)
                    {
                        Vector2 p = center + Disc(random,10f);
                        if (!TrySeat(p,out Vector3 position,out Quaternion rotation)) continue;
                        Source source = sources["coastal_pebble_scan_LOD0"];
                        float scale = Range(random,.10f,.40f) / Mathf.Max(source.Size.x,source.Size.z);
                        position.y -= source.Size.y * scale * Range(random,.15f,.35f);
                        Add("coastal_pebble_scan",3,position,rotation*Quaternion.Euler(0,Range(random,0,360),0),new Vector3(scale,scale*Range(random,.7f,1.1f),scale));
                        pebbleCount++;
                    }
                    var patchRoot = new GameObject("Foreground patch " + patch.ToString("00")); patchRoot.transform.SetParent(root.transform,false);
                    var levels = new LOD[2];
                    Material[] materials = { grass, shrub, detail, pebble };
                    for (int lod = 0; lod < 2; lod++)
                    {
                        var renderers = new List<Renderer>();
                        for (int type = 0; type < 4; type++)
                        {
                            if (batches[lod,type].Count == 0) continue;
                            string name = "Patch_"+patch.ToString("00")+"_"+type+"_LOD"+lod;
                            Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                            mesh.CombineMeshes(batches[lod,type].ToArray(),true,true,false); mesh.RecalculateBounds();
                            long triangles = mesh.GetIndexCount(0)/3;
                            if (lod == 0) highTriangles += triangles; else lowTriangles += triangles;
                            renderers.Add(Attach(patchRoot.transform,name,mesh,materials[type]));
                        }
                        levels[lod] = new LOD(lod == 0 ? .22f : .018f,renderers.ToArray());
                    }
                    var group = patchRoot.AddComponent<LODGroup>(); group.fadeMode = LODFadeMode.None;
                    group.SetLODs(levels); group.RecalculateBounds();

                    bool TrySeat(Vector2 p,out Vector3 position,out Quaternion rotation)
                    {
                        rotation = Quaternion.identity;
                        foreach(Bounds rock in rockFootprints)
                            if(p.x>=rock.min.x&&p.x<=rock.max.x&&p.y>=rock.min.z&&p.y<=rock.max.z)
                            {rockRejected++;position=Vector3.zero;return false;}
                        if (Protected(p.x,p.y) || !sampler.Sample(p,out position,out Vector3 normal) || normal.y < .82f)
                        { position = Vector3.zero; return false; }
                        position.y -= .015f; rotation = Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(Vector3.up,normal),.55f); return true;
                    }
                    void Add(string model,int type,Vector3 position,Quaternion rotation,Vector3 scale)
                    {
                        Matrix4x4 plant = Matrix4x4.TRS(position,rotation,scale);
                        for (int lod=0;lod<2;lod++)
                        {
                            Source source = sources[model+"_LOD"+lod];
                            batches[lod,type].Add(new CombineInstance { mesh=source.Mesh, subMeshIndex=0, transform=plant*source.Matrix });
                        }
                    }
                }
                if (grassCount < 650 || shrubCount < 32 || nearReviewCount < 50 || pebbleCount < 200 || highTriangles > 2600000 || lowTriangles > 650000)
                    throw new InvalidOperationException("Vegetation density/budget failed: grass="+grassCount+" bushes="+shrubCount+" nearReview="+nearReviewCount+" high="+highTriangles+" low="+lowTriangles);
                if (!before.SequenceEqual(site.GetComponentsInChildren<Collider>(true).Select(c=>c.GetInstanceID()).OrderBy(x=>x)))
                    throw new InvalidOperationException("Gameplay collider identities changed.");
                if (previous) Object.DestroyImmediate(previous.gameObject);
                LastReport = "grassTufts="+grassCount+" coastalShrubs="+shrubCount+" smallScannedRocks="+pebbleCount+" leafDetails="+detailCount+" within15mOfReferenceCamera="+nearReviewCount
                    +" lod0Triangles="+highTriangles+" lod1Triangles="+lowTriangles+" patches="+PatchCenters.Length
                    +" rockFootprints="+rockFootprints.Count+" rejectedInsideRocks="+rockRejected+" grassHeightRange="+minGrassHeight+".."+maxGrassHeight
                    +" protectedFootprintPlants=0 collidersChanged=0 seed=150303";
                Debug.Log("LAUNCH_VEGETATION_V03_READY "+LastReport);
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        private static bool Protected(float x,float z) => Rect(x,z,-73.5f,73.5f,-63.5f,73.5f)
            || Rect(x,z,-56.5f,96.5f,-1021.5f,-48.5f) || Rect(x,z,-36.5f,36.5f,58.5f,321.5f);
        private static bool Rect(float x,float z,float x0,float x1,float z0,float z1) => x>=x0&&x<=x1&&z>=z0&&z<=z1;
        private static float Range(System.Random random,float low,float high) => low+(float)random.NextDouble()*(high-low);
        private static Vector2 Disc(System.Random random,float radius)
        {float a=Range(random,0,Mathf.PI*2),r=Mathf.Sqrt(Range(random,0,1))*radius;return new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;}

        private readonly struct Source
        {
            public readonly Mesh Mesh; public readonly Matrix4x4 Matrix; public readonly Vector3 Size;
            public Source(Mesh mesh,Matrix4x4 matrix,Vector3 size) { Mesh=mesh;Matrix=matrix;Size=size; }
        }
        private static Source PrepareModel(string name)
        {
            string path=AssetRoot+"/Models/"+name+".fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(!importer)throw new InvalidOperationException("Missing prepared model "+path);
            if(!importer.isReadable||!importer.bakeAxisConversion||importer.addCollider||importer.importAnimation||importer.materialImportMode!=ModelImporterMaterialImportMode.None)
            {
                importer.isReadable=true;importer.bakeAxisConversion=true;importer.addCollider=false;importer.importAnimation=false;
                importer.importCameras=false;importer.importLights=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            MeshFilter[] filters=asset.GetComponentsInChildren<MeshFilter>(true);
            if(filters.Length!=1||filters[0].sharedMesh.subMeshCount!=1)throw new InvalidOperationException("Expected one foliage mesh/material: "+name);
            Mesh mesh=filters[0].sharedMesh;
            // Single-mesh FBX imports may put the axis/unit conversion on the asset root itself.
            // Removing that root matrix turns an upright plant back onto its exported -Z axis.
            // Imported model assets have no scene parent: retain the complete imported transform.
            Matrix4x4 matrix=filters[0].transform.localToWorldMatrix;
            var bounds=new Bounds(matrix.MultiplyPoint3x4(mesh.vertices[0]),Vector3.zero);
            foreach(Vector3 v in mesh.vertices)bounds.Encapsulate(matrix.MultiplyPoint3x4(v));
            if(Mathf.Abs(bounds.min.y)>.025f||bounds.size.y<.1f||bounds.size.y>2f)
                throw new InvalidOperationException("Foliage bottom/axis/metre validation failed: "+name+" "+bounds);
            Debug.Log("VEGETATION_MODEL_Y_UP "+name+" bottom="+bounds.min.y+" size="+bounds.size
                +" importedRootRotation="+asset.transform.localEulerAngles+" importedRootScale="+asset.transform.localScale);
            return new Source(mesh,matrix,bounds.size);
        }

        private static List<Bounds> RockFootprints(Transform site)
        {
            var result=new List<Bounds>();
            foreach(Transform rock in site.GetComponentsInChildren<Transform>(true))
            {
                if(!rock.gameObject.activeInHierarchy||!(rock.name=="PH coast_land_rocks_04"||rock.name=="PH namaqualand_boulder_02"))continue;
                bool initialized=false;Bounds bounds=new Bounds();
                foreach(MeshFilter filter in rock.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!filter.sharedMesh)continue;
                    Bounds local=filter.sharedMesh.bounds;Matrix4x4 matrix=site.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    {
                        Vector3 p=matrix.MultiplyPoint3x4(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z)));
                        if(!initialized){bounds=new Bounds(p,Vector3.zero);initialized=true;}else bounds.Encapsulate(p);
                    }
                }
                if(initialized){bounds.Expand(new Vector3(.56f,0,.56f));result.Add(bounds);}
            }
            return result;
        }
        private static Material PrepareMaterial(string id,Color tint,float cutoff)
        {
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(!shader)throw new InvalidOperationException("Existing URP Lit shader required.");
            string path=AssetRoot+"/Generated/"+id+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(shader){name="Photographed "+id};AssetDatabase.CreateAsset(material,path);}
            // Explicit 2D imports are essential: this project's default texture preset can be Cubemap.
            Texture2D color=Texture(id+"_BaseColor",false,true);
            Texture2D normal=Texture(id+"_Normal",true,false);
            Texture2D packed=Texture(id+"_MetalSmooth",false,false);
            material.shader=shader;material.SetTexture("_BaseMap",color);material.SetTexture("_MainTex",color);
            material.SetTexture("_BumpMap",normal);material.SetTexture("_MetallicGlossMap",packed);
            material.SetColor("_BaseColor",tint);material.SetFloat("_BumpScale",.55f);material.SetFloat("_Smoothness",.12f);
            material.SetFloat("_SpecularHighlights",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_Metallic",0);material.SetFloat("_Cull",0);material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",cutoff);
            material.SetFloat("_Surface",0);material.SetFloat("_ZWrite",1);material.SetFloat("_AlphaToMask",1);
            material.SetFloat("_SrcBlend",1);material.SetFloat("_DstBlend",0);material.SetFloat("_SmoothnessTextureChannel",0);
            material.EnableKeyword("_ALPHATEST_ON");material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","TransparentCutout");
            material.renderQueue=(int)RenderQueue.AlphaTest;material.enableInstancing=true;
            if(material.GetTexture("_BaseMap")!=color||material.GetTexture("_BumpMap")!=normal||material.GetTexture("_MetallicGlossMap")!=packed)
                throw new InvalidOperationException("Photographic vegetation texture bindings were lost: "+id);
            Debug.Log("VEGETATION_MATERIAL_BOUND "+id+" color="+AssetDatabase.GetAssetPath(color)+" size="+color.width+"x"+color.height
                +" normal="+AssetDatabase.GetAssetPath(normal)+" packed="+AssetDatabase.GetAssetPath(packed));
            EditorUtility.SetDirty(material);return material;
        }
        private static Texture2D Texture(string name,bool normal,bool srgb)
        {
            string path=AssetRoot+"/Textures/"+name+".png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new InvalidOperationException("Missing prepared map "+path);
            TextureImporterType type=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            if(importer.textureShape!=TextureImporterShape.Texture2D||importer.textureType!=type||importer.sRGBTexture!=srgb
                ||!importer.mipmapEnabled||importer.alphaIsTransparency!=srgb||importer.maxTextureSize!=2048||importer.anisoLevel!=8
                ||(srgb&&(!importer.mipMapsPreserveCoverage||Mathf.Abs(importer.alphaTestReferenceValue-.40f)>.001f
                    ||importer.alphaSource!=TextureImporterAlphaSource.FromInput)))
            {
                importer.textureShape=TextureImporterShape.Texture2D;
                importer.textureType=type;importer.sRGBTexture=srgb;importer.mipmapEnabled=true;importer.alphaIsTransparency=srgb;
                if(srgb){importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.mipMapsPreserveCoverage=true;importer.alphaTestReferenceValue=.40f;}
                importer.maxTextureSize=2048;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
            }
            Texture2D texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(!texture||texture.width<1024||texture.height<1024)
                throw new InvalidOperationException("Photographic map failed its Texture2D/dimension check: "+path);
            return texture;
        }
        private static Renderer Attach(Transform parent,string name,Mesh generated,Material material)
        {
            string path=AssetRoot+"/Generated/"+name+".asset";Mesh saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved)
            {
                // CopySerialized changes CPU data/bounds without reliably rebuilding an already loaded GPU mesh.
                // Explicit mesh setters invalidate vertex/index buffers while preserving the asset GUID.
                saved.Clear();saved.indexFormat=generated.indexFormat;
                saved.vertices=generated.vertices;saved.normals=generated.normals;saved.tangents=generated.tangents;
                saved.uv=generated.uv;saved.colors=generated.colors;saved.triangles=generated.triangles;
                saved.bounds=generated.bounds;saved.UploadMeshData(false);
                Object.DestroyImmediate(generated);EditorUtility.SetDirty(saved);
            }
            else{saved=generated;AssetDatabase.CreateAsset(saved,path);}
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=2;go.AddComponent<MeshFilter>().sharedMesh=saved;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.TwoSided;
            renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;return renderer;
        }
        private static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;int split=path.LastIndexOf('/');string parent=path.Substring(0,split);EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(split+1));}

        private sealed class TerrainSampler
        {
            private const float Cell = 24f;
            private readonly Dictionary<Vector2Int, List<Triangle>> cells = new Dictionary<Vector2Int, List<Triangle>>();
            private readonly struct Triangle
            {
                public readonly Vector3 A, B, C, Normal;
                public Triangle(Vector3 a, Vector3 b, Vector3 c) { A = a; B = b; C = c; Normal = Vector3.Cross(b - a, c - a).normalized; }
            }
            public TerrainSampler(Transform site, Transform terrain)
            {
                foreach (MeshFilter filter in terrain.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.gameObject.activeInHierarchy || !filter.name.Contains("Terrain") || !filter.sharedMesh) continue;
                    Matrix4x4 matrix = site.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    Vector3[] vertices = filter.sharedMesh.vertices; int[] triangles = filter.sharedMesh.triangles;
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var t = new Triangle(matrix.MultiplyPoint3x4(vertices[triangles[i]]), matrix.MultiplyPoint3x4(vertices[triangles[i + 1]]), matrix.MultiplyPoint3x4(vertices[triangles[i + 2]]));
                        if (t.Normal.y < .2f) continue;
                        float minX = Mathf.Min(t.A.x, t.B.x, t.C.x), maxX = Mathf.Max(t.A.x, t.B.x, t.C.x);
                        float minZ = Mathf.Min(t.A.z, t.B.z, t.C.z), maxZ = Mathf.Max(t.A.z, t.B.z, t.C.z);
                        if (maxX < -300 || minX > 300 || maxZ < -300 || minZ > 300) continue;
                        for (int x = Mathf.FloorToInt(Mathf.Max(minX, -300) / Cell); x <= Mathf.FloorToInt(Mathf.Min(maxX, 300) / Cell); x++)
                            for (int z = Mathf.FloorToInt(Mathf.Max(minZ, -300) / Cell); z <= Mathf.FloorToInt(Mathf.Min(maxZ, 300) / Cell); z++)
                            {
                                var key = new Vector2Int(x, z);
                                if (!cells.TryGetValue(key, out var list)) { list = new List<Triangle>(); cells.Add(key, list); }
                                list.Add(t);
                            }
                    }
                }
                if (cells.Count == 0) throw new InvalidOperationException("No readable upward terrain triangles found.");
            }
            public bool Sample(Vector2 point, out Vector3 position, out Vector3 normal)
            {
                position = Vector3.zero; normal = Vector3.up; float highest = float.NegativeInfinity;
                if (!cells.TryGetValue(new Vector2Int(Mathf.FloorToInt(point.x / Cell), Mathf.FloorToInt(point.y / Cell)), out var list)) return false;
                foreach (Triangle t in list)
                {
                    float denominator = (t.B.z - t.C.z) * (t.A.x - t.C.x) + (t.C.x - t.B.x) * (t.A.z - t.C.z);
                    if (Mathf.Abs(denominator) < .000001f) continue;
                    float a = ((t.B.z - t.C.z) * (point.x - t.C.x) + (t.C.x - t.B.x) * (point.y - t.C.z)) / denominator;
                    float b = ((t.C.z - t.A.z) * (point.x - t.C.x) + (t.A.x - t.C.x) * (point.y - t.C.z)) / denominator;
                    float c = 1 - a - b;
                    if (a < -.0001f || b < -.0001f || c < -.0001f) continue;
                    float y = a * t.A.y + b * t.B.y + c * t.C.y;
                    if (y > highest) { highest = y; position = new Vector3(point.x, y, point.y); normal = t.Normal; }
                }
                return !float.IsNegativeInfinity(highest);
            }
        }
    }
}
