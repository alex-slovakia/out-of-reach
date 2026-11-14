# Prevent replacement by the procedural authoring command

**Do not invoke the current `DistantSpaceAuthoring.Build()` after copying the six GMAT textures. It writes new random-star PNGs over the same filenames.** Its location is `Assets/_OutOfReach/Scripts/Presentation/Space/Editor/DistantSpaceAuthoring.cs`. The Bootstrap builder only binds the material in the inspected revision; it does not need a second generator.

The smallest useful follow-up is to replace that method's implementation with read-only validation of the delivered textures/material. Rename its menu action to `OUT OF REACH/Presentation/Validate GMAT star sky` and change the class summary to describe the catalogue projection. Remove the now-unused `System.IO` import. Keep the existing constants, class name and `Build()` signature so an existing editor caller does not lose its entry point.

Suggested replacement body, for root to apply after the current Unity authoring window:

```csharp
{
    string[] faces = { "Front", "Back", "Left", "Right", "Up", "Down" };
    var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
    var shader = Shader.Find("Skybox/6 Sided");
    if (!material || !shader || material.shader != shader)
        throw new InvalidOperationException("The existing six-sided GMAT sky material is required.");
    foreach (string face in faces)
    {
        string path = Root + "/Stars" + face + ".png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (!texture || texture.width != 1024 || texture.height != 1024)
            throw new InvalidOperationException("Missing or unexpected GMAT sky face: " + path);
        if (material.GetTexture("_" + face + "Tex") != texture)
            throw new InvalidOperationException("GMAT sky material face binding mismatch: " + face);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer || !importer.sRGBTexture || importer.mipmapEnabled ||
            importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear)
            throw new InvalidOperationException("GMAT sky face import settings require review: " + path);
    }
    Debug.Log("GMAT_SKY_BINDINGS_VALID | six 1024 faces; source/hash verification is external; Unity sky render still requires inspection");
}
```

This validator does not write PNGs, modify `.meta` files, recreate material assets, or reset sky rotation. It validates the interface only; it does not establish source identity from the pixels. Root should separately compare the six staged SHA-256 hashes against the actual destination files and preserve the documented GUIDs.

The original source directions are fixed. A later common celestial attitude should rotate all stars and planets together, using one authority; do not independently rotate each face or bake a new observer direction into these textures. Walking should be tested against the actual `RenderSettings.skybox` camera path. Earth and Mars are intentionally absent until their authoritative observer/epoch binding is ready.

Publish the generator, input/source hash manifest, verification evidence and derivative modification notice with the six replacement PNGs. Their embedded PNG text also identifies the source, Apache-2.0 licence and raster modifications. Keep the full GMAT licence available in the distribution; the already committed GMAT research package supplies that source and licence.

This is a proposed source edit only. No Unity source file was changed or compiled by this staging task.
