using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ClangLib;
using System.Reflection;

class Program
{
    static void Main(string[] args)
    {
        string dir = ".." + Path.DirectorySeparatorChar + "Blueprints" + Path.DirectorySeparatorChar + "Atalanta Class Shuttle";
        if (!Directory.Exists(dir))
        {
            Console.WriteLine($"Directory not found: {dir}");
            return;
        }

        var blueprintFile = BlueprintSerializer.Deserialize(dir);
        Console.WriteLine($"Loaded {blueprintFile.ShipBlueprints.Count} blueprint(s).");
        if (blueprintFile.ThumbPath != null)
            Console.WriteLine($"Thumbnail found: {blueprintFile.ThumbPath}");
        else
            Console.WriteLine("No thumbnail found.");
        foreach (var bp in blueprintFile.ShipBlueprints)
        {
            Console.WriteLine($"Blueprint: {bp.DisplayName}, Grids: {bp.CubeGrids.Count}");
            foreach (var grid in bp.CubeGrids)
            {
                Console.WriteLine($"  Grid EntityId: {grid.EntityId}, Blocks: {grid.CubeBlocks.Count}");
                foreach (var block in grid.CubeBlocks)
                {
                    Console.WriteLine($"    Block: {block.SubtypeName}, Type: {block.XsiType}, EntityId: {block.EntityId}");
                }
            }
        }

        // Preprocess: set all empty string properties to null
        SetEmptyStringsToNull(blueprintFile);

        // Serialize the entire blueprint data structure to JSON, ignoring nulls
        var jsonOptions = new JsonSerializerOptions {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        string json = JsonSerializer.Serialize(blueprintFile, jsonOptions);
        Console.WriteLine("\n--- Blueprint as JSON ---");
        Console.WriteLine(json);

        // Save JSON to file named after the blueprint
        string? name = blueprintFile.ShipBlueprints.Count > 0 ? blueprintFile.ShipBlueprints[0].DisplayName : null;
        string safeName = string.IsNullOrWhiteSpace(name) ? "blueprint" : Regex.Replace(name, "[^a-zA-Z0-9_-]", "_");
        string jsonPath = safeName + ".json";
        File.WriteAllText(jsonPath, json);
        Console.WriteLine($"\nJSON saved to: {jsonPath}");
    }

    // Recursively set all empty string properties to null
    public static void SetEmptyStringsToNull(object? obj)
    {
        if (obj == null) return;
        var type = obj.GetType();
        if (type == typeof(string)) return;
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            if (prop.PropertyType == typeof(string))
            {
                var value = (string?)prop.GetValue(obj);
                if (value != null && value == "")
                    prop.SetValue(obj, null);
            }
            else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string))
            {
                var value = prop.GetValue(obj) as System.Collections.IEnumerable;
                if (value != null)
                {
                    foreach (var item in value)
                        SetEmptyStringsToNull(item);
                }
            }
            else if (!prop.PropertyType.IsPrimitive && !prop.PropertyType.IsEnum)
            {
                SetEmptyStringsToNull(prop.GetValue(obj));
            }
        }
    }
}
