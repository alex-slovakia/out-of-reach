using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfReach.Presentation.Terminal.Editor
{
    /// <summary>Run only in a root-approved import copy. Creates assets, never opens or modifies a scene.</summary>
    public static class PhysicalTerminalBuilder
    {
        private const string Root="Assets/_OutOfReach/Visuals/Props/MissionTerminal";
        private const string ModelPath=Root+"/Models/MissionTerminal.fbx";
        private const string ImagePath=Root+"/Data/epic_1b_20261111110606.jpg";
        private const string PrefabPath=Root+"/MissionTerminal.prefab";
        private const string ImageHash="d5a5fc0ce1505848b9ddd8fd1e6b020d21d44d532d24f05ee083202c8d8c9463";
        private const string StaticImagePath=Root+"/Data/modis_terra_slovakia_20261114_0815utc.jpg";
        private const string StaticImageHash="7e1d762d2daef4c63aeb954d58266849ebe769bd27190aae11c2e0c3a84ae700";
        private const string TextureRoot=Root+"/Textures/";
        private const string ModelHash="034d9d8a3f4e62265397f395516f44d2ae532f118d5f26c7002ca739b1e15d52";

        [MenuItem("Out Of Reach/Terminal/Build And Validate Physical Terminal")]
        public static void BuildAndValidate()
        {
            Require(Application.unityVersion=="6000.3.28f1","Use exact Unity 6000.3.28f1.");
            Require(Hash(ImagePath)==ImageHash,"NASA source bytes changed.");
            Require(Hash(ModelPath)==ModelHash,"Approved TERMINAL-ART-V2 model bytes changed.");
            EnsureFolder(Root+"/Materials");
            var baseMap=PrepareMap("MissionTerminal_BaseColor.png",true,"d6f1de848e712eb8142bdfb7a44e0421efe20ca662491fada164d273e908ad57");
            var labels=PrepareMap("MissionTerminal_Labels.png",true,"88153be6c9cf4d03c48090a381cc8c7edc86844f7ab1e442ba181997d238506d");
            var metallic=PrepareMap("MissionTerminal_MetallicSmoothness.png",false,"b2939d9247c220e334ee6d5d6ea6cf9e690dce176470e82f2f9b3e685f2c6675");
            var occlusion=PrepareMap("MissionTerminal_Occlusion.png",false,"52119ff4fefbc09d4d0e8171666046290d090311b12e770d778e8a6351a457cf");
            var modelImporter=AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Require(modelImporter,"FBX must be imported first.");
            modelImporter.globalScale=1; modelImporter.useFileScale=true;
            modelImporter.importAnimation=false; modelImporter.animationType=ModelImporterAnimationType.None;
            modelImporter.importCameras=false; modelImporter.importLights=false; modelImporter.addCollider=false;
            modelImporter.preserveHierarchy=true; modelImporter.bakeAxisConversion=false;
            modelImporter.isReadable=true; // Small static prop; permits deterministic UV/winding validation.
            modelImporter.importNormals=ModelImporterNormals.Import;
            modelImporter.importTangents=ModelImporterTangents.CalculateMikk;
            modelImporter.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            modelImporter.SaveAndReimport();
            var imageImporter=AssetImporter.GetAtPath(ImagePath) as TextureImporter;
            Require(imageImporter,"NASA image importer missing.");
            imageImporter.textureType=TextureImporterType.Default; imageImporter.sRGBTexture=true;
            imageImporter.textureShape=TextureImporterShape.Texture2D;
            imageImporter.mipmapEnabled=true; imageImporter.maxTextureSize=2048;
            imageImporter.npotScale=TextureImporterNPOTScale.None; imageImporter.wrapMode=TextureWrapMode.Clamp;
            imageImporter.textureCompression=TextureImporterCompression.Uncompressed;
            imageImporter.SaveAndReimport();
            var body=Lit("MT_TerminalBody",Color.white,1,0);
            body.SetTexture("_BaseMap",baseMap); body.SetTexture("_MetallicGlossMap",metallic);
            body.SetTexture("_OcclusionMap",occlusion); body.SetFloat("_OcclusionStrength",1);
            body.EnableKeyword("_METALLICSPECGLOSSMAP"); body.EnableKeyword("_OCCLUSIONMAP");
            var print=Lit("MT_Labels",Color.white,0,.55f); print.SetTexture("_BaseMap",labels);
            // Restrained non-emissive default until the integrated lamp is reviewed with ship exposure.
            var amber=Lit("MT_Amber",new Color(.30f,.12f,.01f),0,.18f);
            var materialMap=new Dictionary<string,Material>
            {
                {"MT_TerminalBody",body}, {"MT_Labels",print}, {"MT_Amber",amber}
            };
            var screenMaterial=MaterialAsset("MT_Screen",Shader.Find("OutOfReach/PhysicalEarthTerminal"));
            screenMaterial.SetTexture("_Observation",AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath));
            screenMaterial.SetFloat("_ObservationAvailable",0); // Mission time is not known until root binds the calendar.
            screenMaterial.SetVector("_GlobeRotation",new Vector4(0,0,0,1));
            screenMaterial.SetVector("_Pointer",Vector4.zero); screenMaterial.SetVector("_Ripple",new Vector4(0,0,10,0));
            EditorUtility.SetDirty(screenMaterial); materialMap.Add("MT_Screen",screenMaterial);
            var labelMaterial=MaterialAsset("MT_DepthLabel",Shader.Find("OutOfReach/Ship Depth Label"));
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Require(font,"Built-in runtime font missing.");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            Require(model,"Imported model missing.");
            var root=new GameObject("MissionTerminal");
            try
            {
                var shell=(GameObject)PrefabUtility.InstantiatePrefab(model);
                shell.name="Hardware"; shell.transform.SetParent(root.transform,false);
                foreach(var renderer in shell.GetComponentsInChildren<Renderer>(true))
                {
                    var source=renderer.sharedMaterials;
                    for(int i=0;i<source.Length;i++)
                    {
                        string key=source[i] ? source[i].name : string.Empty;
                        Require(materialMap.TryGetValue(key,out var replacement),"Unmapped material: "+key);
                        source[i]=replacement;
                    }
                    renderer.sharedMaterials=source;
                }
                var screen=Find(shell.transform,"ActiveDisplay").GetComponent<MeshRenderer>();
                Require(screen,"Display renderer missing.");
                screen.shadowCastingMode=ShadowCastingMode.Off; screen.receiveShadows=false;
                // Explicit integration frames avoid inheriting FBX exporter-axis rotations from empty objects.
                var frame=Frame(root.transform,"DisplayFrame",Vector3.zero,Quaternion.identity);
                var grip=Frame(root.transform,"LeftGripFrame",new Vector3(-.426f,0,-.04f),Quaternion.Euler(0,0,90));
                Frame(root.transform,"InteractionAnchor",new Vector3(0,-.1f,-.08f),Quaternion.identity);
                Frame(root.transform,"FocusCameraAnchor",new Vector3(.012f,.025f,-.66f),Quaternion.identity);
                Frame(root.transform,"WallMountAnchor",new Vector3(0,-.038f,.318f),Quaternion.identity);
                // Exported reference empties stay as source evidence but get distinct names in the prefab.
                foreach(var t in shell.GetComponentsInChildren<Transform>(true))
                    if(t != shell.transform && !t.GetComponent<Renderer>() && t.childCount==0) t.name="Source_"+t.name;
                Label(frame,"Title","AURORA  /  EARTH ARCHIVE",new Vector2(-.296f,.176f),.011f,font,labelMaterial);
                Label(frame,"Mode","SCHEMATIC DISC PROJECTION",new Vector2(-.296f,-.144f),.0075f,font,labelMaterial);
                Label(frame,"Truth","Drag rotates the projection.\nReverse side: unobserved. Not georeferenced.",new Vector2(-.296f,-.161f),.0064f,font,labelMaterial);
                var mission=Label(frame,"MissionUtc","MISSION UTC\nAwaiting mission calendar",new Vector2(.029f,.140f),.0073f,font,labelMaterial);
                var observed=Label(frame,"ObservedUtc","OBSERVED UTC\n2026-11-11 11:01:17",new Vector2(.029f,.100f),.0073f,font,labelMaterial);
                var age=Label(frame,"ImageAge","IMAGE AGE\nAwaiting mission calendar",new Vector2(.029f,.060f),.0073f,font,labelMaterial);
                var status=Label(frame,"Status","ARCHIVED OBSERVATION\nNot a live feed or forecast.\nAURORA-7 position: not propagated.",new Vector2(.029f,.018f),.0061f,font,labelMaterial);
                Label(frame,"OriginalInset","ORIGINAL EPIC DISC",new Vector2(.083f,-.078f),.0064f,font,labelMaterial);
                Label(frame,"Credit","Image: NASA EPIC Team / NOAA DSCOVR\nepic.gsfc.nasa.gov  /  2026-11-11",new Vector2(.029f,-.169f),.0059f,font,labelMaterial);
                Label(frame,"Control","ESC  /  CLOSE",new Vector2(.218f,.174f),.0065f,font,labelMaterial);
                // V2 already has a baked hardware nameplate. Do not cover it with V1's extra TextMesh.
                var collider=root.AddComponent<BoxCollider>(); collider.center=new Vector3(0,0,.040f); collider.size=new Vector3(.766f,.510f,.080f);
                var presenter=root.AddComponent<PhysicalEarthTerminalPresenter>();
                presenter.ConfigureScene(null,frame,new Vector2(.64f,.4f),screen,null,null,grip);
                presenter.ConfigureDisplay(AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath),mission,observed,age,status);
                // R33: dated MODIS Terra Slovakia image, shown as the inset of the interactive EPIC terminal.
                var staticImage=AssetDatabase.LoadAssetAtPath<Texture2D>(StaticImagePath);
                Require(staticImage && Hash(StaticImagePath)==StaticImageHash,"R33 static NASA GIBS image missing or changed.");
                presenter.ConfigureStaticImage(staticImage,false); // Slovakia inset beside the draggable EPIC globe; static mode stays off.
                Validate(root,screen);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath,out bool saved);
                Require(saved,"Prefab save failed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            foreach(var material in materialMap.Values) AssetDatabase.SaveAssetIfDirty(material);
            AssetDatabase.SaveAssetIfDirty(labelMaterial);
            var persisted=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(persisted && persisted.GetComponent<PhysicalEarthTerminalPresenter>(),"Persisted presenter missing.");
            Debug.Log("TERM_V2_BUILD_PASS | approved FBX + four map hashes + NASA hash | four meshes / 14268 triangles / five slots | metre scale | four display corners/UVs/winding | five direct anchors | no camera/listener/light | persisted prefab. Runtime input, exposure and finger contact still require integration playtest.");
        }

        private static void Validate(GameObject root,MeshRenderer screen)
        {
            Require(root.GetComponentsInChildren<Camera>(true).Length==0,"Unexpected camera.");
            Require(root.GetComponentsInChildren<AudioListener>(true).Length==0,"Unexpected audio listener.");
            Require(root.GetComponentsInChildren<Light>(true).Length==0,"Unexpected light.");
            var hardware=root.transform.Find("Hardware");
            Require(hardware,"Hardware root missing.");
            var filters=hardware.GetComponentsInChildren<MeshFilter>(true);
            Require(filters.Length==4 && hardware.GetComponentsInChildren<Renderer>(true).Length==4,"V2 must contain exactly four hardware meshes/renderers.");
            int triangleCount=0,slotCount=0;
            var names=new HashSet<string>();
            foreach(var filter in filters)
            {
                Require(filter.sharedMesh,"Missing hardware mesh.");
                triangleCount+=filter.sharedMesh.triangles.Length/3;
                var renderer=filter.GetComponent<MeshRenderer>(); Require(renderer,"Hardware renderer missing.");
                foreach(var material in renderer.sharedMaterials)
                { Require(material,"Null hardware material."); names.Add(material.name); slotCount++; }
            }
            Require(triangleCount==14268 && slotCount==5 && names.SetEquals(new[]{"MT_TerminalBody","MT_Labels","MT_Amber","MT_Screen"}),"Wrong V2 mesh/material payload.");
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script: "+t.name);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                Require(filter.sharedMesh && filter.sharedMesh.uv.Length==filter.sharedMesh.vertexCount,"Mesh missing UV: "+filter.name);
            // FBX vertex axes can be baked; bounds in world space are the authoritative physical envelope.
            Require(Mathf.Abs(screen.bounds.size.x-.64f)<.002f && Mathf.Abs(screen.bounds.size.y-.40f)<.002f,
                "Display imported at wrong size or orientation: "+screen.bounds.size);
            Require(Mathf.Abs(screen.bounds.center.z)<.002f,"Screen is not on display plane.");
            var mesh=screen.GetComponent<MeshFilter>().sharedMesh;
            var triangles=mesh.triangles; var vertices=mesh.vertices;
            Vector3 a=screen.transform.TransformPoint(vertices[triangles[0]]);
            Vector3 b=screen.transform.TransformPoint(vertices[triangles[1]]);
            Vector3 c=screen.transform.TransformPoint(vertices[triangles[2]]);
            Require(Vector3.Dot(Vector3.Cross(b-a,c-a).normalized,Vector3.back)>.99f,"Display winding faces away from user.");
            var uv=mesh.uv;
            foreach(var corner in new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,0),new Vector2(1,1)})
            {
                bool matched=false;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=screen.transform.TransformPoint(vertices[i]);
                    var expected=new Vector3((corner.x-.5f)*.64f,(corner.y-.5f)*.4f,0);
                    if(Vector3.Distance(p,expected)<.0002f && Vector2.Distance(uv[i],corner)<.0002f) matched=true;
                }
                Require(matched,"Display corner/UV mismatch: "+corner);
            }
            var frame=root.transform.Find("DisplayFrame");
            Require(frame && frame.localPosition==Vector3.zero && Quaternion.Angle(frame.localRotation,Quaternion.identity)<.001f,"Invalid direct display frame.");
            var grip=root.transform.Find("LeftGripFrame");
            Require(grip && Vector3.Distance(grip.localPosition,new Vector3(-.426f,0,-.04f))<.0001f && Quaternion.Angle(grip.localRotation,Quaternion.Euler(0,0,90))<.001f,"Invalid direct grip frame.");
            foreach(string name in new[]{"DisplayFrame","LeftGripFrame","InteractionAnchor","FocusCameraAnchor","WallMountAnchor"})
                Require(root.transform.Find(name),"Missing integration anchor: "+name);
        }
        private static TextMesh Label(Transform parent,string name,string text,Vector2 p,float height,Font font,Material material)
        {
            var label=new GameObject(name); label.transform.SetParent(parent,false);
            // Keep text behind the calibrated fingertip's 1 mm front clearance, with normal depth testing.
            label.transform.localPosition=new Vector3(p.x,p.y,-.0004f);
            var mesh=label.AddComponent<TextMesh>(); mesh.text=text; mesh.font=font; mesh.fontSize=64;
            mesh.characterSize=height*.13f; mesh.anchor=TextAnchor.UpperLeft; mesh.alignment=TextAlignment.Left;
            mesh.color=new Color(.65f,.80f,.84f,1); mesh.lineSpacing=1.1f;
            label.AddComponent<ShipLabelAtlasBinder>().Configure(mesh,label.GetComponent<MeshRenderer>(),font,material);
            return mesh;
        }
        private static Transform Frame(Transform parent,string name,Vector3 position,Quaternion rotation)
        {
            var result=new GameObject(name).transform; result.SetParent(parent,false);
            result.localPosition=position; result.localRotation=rotation; return result;
        }
        private static Transform Find(Transform root,string name)
        {
            Transform found=null;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(t.name==name) { Require(!found,"Duplicate source anchor: "+name); found=t; }
            Require(found,"Missing source object: "+name); return found;
        }
        private static Material Lit(string name,Color linearColor,float metal,float roughness)
        {
            var material=MaterialAsset(name,Shader.Find("Universal Render Pipeline/Lit"));
            material.shaderKeywords=Array.Empty<string>();
            material.SetFloat("_WorkflowMode",1); material.SetFloat("_Surface",0);
            material.SetFloat("_Blend",0); material.SetFloat("_AlphaClip",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.One); material.SetFloat("_DstBlend",(float)BlendMode.Zero);
            material.SetFloat("_ZWrite",1); material.SetFloat("_Cull",(float)CullMode.Back);
            material.SetFloat("_SmoothnessTextureChannel",0);
            material.SetTexture("_BaseMap",null); material.SetTexture("_MetallicGlossMap",null);
            material.SetTexture("_OcclusionMap",null); material.SetTexture("_BumpMap",null);
            material.SetTexture("_EmissionMap",null); material.SetColor("_EmissionColor",Color.black);
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            material.SetOverrideTag("RenderType","Opaque"); material.renderQueue=-1;
            // Blender material JSON records linear values; URP Inspector colors are authored in sRGB.
            material.SetColor("_BaseColor",linearColor.gamma); material.SetFloat("_Metallic",metal);
            material.SetFloat("_Smoothness",1-roughness); EditorUtility.SetDirty(material); return material;
        }
        private static Texture2D PrepareMap(string filename,bool srgb,string expectedHash)
        {
            string path=TextureRoot+filename;
            Require(Hash(path)==expectedHash,"Approved map bytes changed: "+filename);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer,"Map importer missing: "+filename);
            importer.textureType=TextureImporterType.Default; importer.textureShape=TextureImporterShape.Texture2D;
            importer.sRGBTexture=srgb; importer.mipmapEnabled=true; importer.maxTextureSize=2048;
            importer.npotScale=TextureImporterNPOTScale.None; importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaSource=TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency=false;
            importer.wrapMode=TextureWrapMode.Clamp; importer.filterMode=FilterMode.Trilinear; importer.anisoLevel=2;
            importer.isReadable=false; importer.SaveAndReimport();
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(texture && texture.width==2048 && texture.height==2048,"Expected 2048 square Texture2D: "+filename);
            return texture;
        }
        private static Material MaterialAsset(string name,Shader shader)
        {
            Require(shader,"Required shader missing: "+name);
            string path=Root+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) { material=new Material(shader){name=name}; AssetDatabase.CreateAsset(material,path); }
            else material.shader=shader;
            return material;
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        private static string Hash(string path)
        {
            using(var sha=SHA256.Create()) using(var stream=File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        private static void Require(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException("Physical terminal: "+message);
        }
    }
}
