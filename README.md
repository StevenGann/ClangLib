# ClangLib

**ClangLib** is a .NET library for reading and writing Space Engineers blueprint folders (`bp.sbc` and `thumb.png`) as strongly-typed C# objects. It enables programmatic inspection, manipulation, and serialization of blueprints, making it ideal for modding tools, automation, and analysis of Space Engineers content.

## Features

- **Deserialize** blueprint folders into C# objects (`BlueprintFile`, `ShipBlueprint`, `CubeGrid`, `CubeBlock`, etc.)
- **Serialize** C# objects back to blueprint XML format compatible with Space Engineers
- **Preserves** all mapped and unmapped fields for maximum compatibility
- **Supports** thumbnail images (`thumb.png`)
- **Comprehensive test suite** using xUnit
- **Demo app** for converting blueprints to JSON

## Getting Started

### Requirements

- .NET 9.0 SDK or later

### Installation

Clone the repository and build the solution:

```sh
git clone https://github.com/yourusername/ClangLib.git
cd ClangLib/Source
dotnet build
```

### Usage

#### Deserialize a Blueprint

```csharp
using ClangLib;

string blueprintDir = @"path\to\your\blueprint";
var blueprintFile = BlueprintSerializer.Deserialize(blueprintDir);

// Access ship blueprints, grids, and blocks
foreach (var ship in blueprintFile.ShipBlueprints)
{
    Console.WriteLine($"Blueprint: {ship.DisplayName}");
    foreach (var grid in ship.CubeGrids)
    {
        Console.WriteLine($"  Grid: {grid.EntityId}, Blocks: {grid.CubeBlocks.Count}");
    }
}
```

#### Serialize a Blueprint

```csharp
// Modify blueprintFile as needed, then save:
BlueprintSerializer.Serialize(blueprintFile, @"path\to\output\dir");
```

#### Demo: Convert Blueprint to JSON

The demo app (`ClangLib.Demo`) loads a blueprint, prints its structure, and saves it as JSON:

```sh
cd Source/ClangLib.Demo
dotnet run
```

Example output:
```
Loaded 1 blueprint(s).
Thumbnail found: ../Blueprints/Atalanta Class Shuttle/thumb.png
Blueprint: Atalanta Shuttle, Grids: 1
  Grid EntityId: 123456, Blocks: 42
    Block: LargeBlockArmor, Type: MyObjectBuilder_CubeBlock, EntityId: 654321
...
JSON saved to: Atalanta_Shuttle.json
```

## Data Model

- `BlueprintFile`: Contains a list of `ShipBlueprint` and optional thumbnail path.
- `ShipBlueprint`: Contains metadata, DLCs, and a list of `CubeGrid`.
- `CubeGrid`: Represents a ship/station grid, with blocks and orientation.
- `CubeBlock`: Represents a block, with all known and unmapped fields preserved.
- Additional types: `BlueprintId`, `PositionAndOrientation`, `Vector3`, `Quaternion`, `BlockOrientation`, `ComponentContainer`, etc.

## Testing

Unit tests are provided in `ClangLib.Tests` using xUnit:

```sh
cd Source/ClangLib.Tests
dotnet test
```

Test coverage includes:
- Deserialization of all blueprints in the `Blueprints` directory
- Ensuring no unmapped fields remain after deserialization
- Round-trip serialization/deserialization equality
- File overwrite safety

## License

MIT License (c) 2025 Steven Gann

See [LICENSE](./LICENSE) for details. 