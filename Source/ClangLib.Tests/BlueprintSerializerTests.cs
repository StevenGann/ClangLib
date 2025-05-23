using ClangLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using System.Text.Json;

namespace ClangLib.Tests;

public class BlueprintSerializerTests
{
    public static IEnumerable<object[]> BlueprintFolders()
    {
        // Discover all blueprint folders in the Blueprints directory
        var baseDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "Blueprints");
        foreach (var dir in Directory.EnumerateDirectories(baseDir))
        {
            if (File.Exists(Path.Combine(dir, "bp.sbc")))
                yield return new object[] { dir };
        }
    }

    [Theory]
    [MemberData(nameof(BlueprintFolders))]
    public void Deserialize_HasNoUnmappedFields(string blueprintDir)
    {
        var blueprint = BlueprintSerializer.Deserialize(blueprintDir);
        foreach (var ship in blueprint.ShipBlueprints)
        foreach (var grid in ship.CubeGrids)
        foreach (var block in grid.CubeBlocks)
            Assert.True(block.OtherFields == null || block.OtherFields.Count == 0, $"Unmapped fields found in block {block.SubtypeName} in {blueprintDir}");
    }

    [Theory]
    [MemberData(nameof(BlueprintFolders))]
    public void Serialize_DoesNotOverwriteBlueprints(string blueprintDir)
    {
        var blueprint = BlueprintSerializer.Deserialize(blueprintDir);
        var tempDir = Path.Combine(Path.GetTempPath(), "ClangLibTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            BlueprintSerializer.Serialize(blueprint, tempDir);
            var outFile = Path.Combine(tempDir, "bp.sbc");
            Assert.True(File.Exists(outFile), $"Serialized file not found: {outFile}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [MemberData(nameof(BlueprintFolders))]
    public void RoundTrip_SerializeDeserialize_EqualsOriginal(string blueprintDir)
    {
        var blueprint = BlueprintSerializer.Deserialize(blueprintDir);
        var tempDir = Path.Combine(Path.GetTempPath(), "ClangLibTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            BlueprintSerializer.Serialize(blueprint, tempDir);
            var loaded = BlueprintSerializer.Deserialize(tempDir);
            // Compare as JSON for deep equality
            var origJson = JsonSerializer.Serialize(blueprint, new JsonSerializerOptions { WriteIndented = false });
            var loadedJson = JsonSerializer.Serialize(loaded, new JsonSerializerOptions { WriteIndented = false });
            Assert.Equal(origJson, loadedJson);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
