using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Linq;

namespace ClangLib
{
    /// <summary>
    /// Provides methods for manually deserializing Space Engineers blueprint folders (bp.sbc and thumb.png) into strongly-typed C# objects.
    /// </summary>
    public static class BlueprintSerializer
    {
        /// <summary>
        /// Loads and deserializes a Space Engineers blueprint folder.
        /// </summary>
        /// <param name="directoryPath">The path to the blueprint folder containing bp.sbc and optionally thumb.png.</param>
        /// <returns>A <see cref="BlueprintFile"/> object representing the blueprint and its metadata.</returns>
        /// <exception cref="FileNotFoundException">Thrown if bp.sbc is not found in the directory.</exception>
        public static BlueprintFile Deserialize(string directoryPath)
        {
            string bpPath = Path.Combine(directoryPath, "bp.sbc");
            string thumbPath = Path.Combine(directoryPath, "thumb.png");
            if (!File.Exists(bpPath))
                throw new FileNotFoundException($"Blueprint file not found: {bpPath}");

            var doc = XDocument.Load(bpPath);
            var blueprints = new List<ShipBlueprint>();

            var shipBlueprintsElem = doc.Root.Element("ShipBlueprints");
            if (shipBlueprintsElem != null)
            {
                foreach (var shipElem in shipBlueprintsElem.Elements("ShipBlueprint"))
                {
                    var idElem = shipElem.Element("Id");
                    var id = new BlueprintId
                    {
                        Type = (string?)idElem?.Attribute("Type"),
                        Subtype = (string?)idElem?.Attribute("Subtype")
                    };
                    var displayName = (string?)shipElem.Element("DisplayName");
                    var dlcs = new List<string>();
                    foreach (var dlcElem in shipElem.Elements("DLC"))
                        dlcs.Add((string)dlcElem);
                    var cubeGrids = new List<CubeGrid>();
                    var cubeGridsElem = shipElem.Element("CubeGrids");
                    if (cubeGridsElem != null)
                    {
                        foreach (var gridElem in cubeGridsElem.Elements("CubeGrid"))
                        {
                            var grid = new CubeGrid
                            {
                                SubtypeName = (string?)gridElem.Element("SubtypeName"),
                                EntityId = (string?)gridElem.Element("EntityId"),
                                PersistentFlags = (string?)gridElem.Element("PersistentFlags"),
                                PositionAndOrientation = ParsePositionAndOrientation(gridElem.Element("PositionAndOrientation")),
                                LocalPositionAndOrientation = (string?)gridElem.Element("LocalPositionAndOrientation"),
                                GridSizeEnum = (string?)gridElem.Element("GridSizeEnum"),
                                CubeBlocks = new List<CubeBlock>()
                            };
                            var cubeBlocksElem = gridElem.Element("CubeBlocks");
                            if (cubeBlocksElem != null)
                            {
                                foreach (var blockElem in cubeBlocksElem.Elements())
                                {
                                    var block = ParseCubeBlock(blockElem);
                                    grid.CubeBlocks.Add(block);
                                }
                            }
                            cubeGrids.Add(grid);
                        }
                    }
                    blueprints.Add(new ShipBlueprint
                    {
                        Id = id,
                        DisplayName = displayName,
                        DLCs = dlcs,
                        CubeGrids = cubeGrids
                    });
                }
            }
            return new BlueprintFile {
                ShipBlueprints = blueprints,
                ThumbPath = File.Exists(thumbPath) ? thumbPath : null
            };
        }

        /// <summary>
        /// Serializes a <see cref="BlueprintFile"/> object to a Space Engineers-compatible bp.sbc XML file in the specified directory.
        /// </summary>
        /// <param name="blueprintFile">The blueprint file object to serialize.</param>
        /// <param name="directoryPath">The target directory where bp.sbc will be written. The directory will be created if it does not exist.</param>
        /// <remarks>
        /// This method writes the XML structure expected by Space Engineers, including all mapped and unmapped fields for each block.
        /// The output file will be overwritten if it already exists.
        /// </remarks>
        public static void Serialize(BlueprintFile blueprintFile, string directoryPath)
        {
            // Ensure the output directory exists
            Directory.CreateDirectory(directoryPath);
            string bpPath = Path.Combine(directoryPath, "bp.sbc");

            // Define XML namespaces for xsi and xsd
            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
            XNamespace xsd = "http://www.w3.org/2001/XMLSchema";

            // Build the XML document structure
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Definitions",
                    // Add namespace attributes
                    new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                    new XAttribute(XNamespace.Xmlns + "xsd", xsd),
                    // Add ShipBlueprints root
                    new XElement("ShipBlueprints",
                        blueprintFile.ShipBlueprints.ConvertAll(ship =>
                            new XElement("ShipBlueprint",
                                // Set xsi:type for each ShipBlueprint
                                new XAttribute(xsi + "type", "MyObjectBuilder_ShipBlueprintDefinition"),
                                // Add blueprint Id
                                ship.Id != null ?
                                    new XElement("Id",
                                        ship.Id.Type != null ? new XAttribute("Type", ship.Id.Type) : null,
                                        ship.Id.Subtype != null ? new XAttribute("Subtype", ship.Id.Subtype) : null
                                    ) : null,
                                // Add display name
                                ship.DisplayName != null ? new XElement("DisplayName", ship.DisplayName) : null,
                                // Add DLC tags
                                ship.DLCs != null ? ship.DLCs.ConvertAll(dlc => new XElement("DLC", dlc)) : null,
                                // Add CubeGrids
                                ship.CubeGrids != null ? new XElement("CubeGrids",
                                    ship.CubeGrids.ConvertAll(grid =>
                                        new XElement("CubeGrid",
                                            // Add grid properties
                                            grid.SubtypeName != null ? new XElement("SubtypeName", grid.SubtypeName) : null,
                                            grid.EntityId != null ? new XElement("EntityId", grid.EntityId) : null,
                                            grid.PersistentFlags != null ? new XElement("PersistentFlags", grid.PersistentFlags) : null,
                                            grid.PositionAndOrientation != null ? SerializePositionAndOrientation(grid.PositionAndOrientation) : null,
                                            grid.LocalPositionAndOrientation != null ? new XElement("LocalPositionAndOrientation", grid.LocalPositionAndOrientation) : null,
                                            grid.GridSizeEnum != null ? new XElement("GridSizeEnum", grid.GridSizeEnum) : null,
                                            // Add CubeBlocks
                                            grid.CubeBlocks != null ? new XElement("CubeBlocks",
                                                grid.CubeBlocks.ConvertAll(block => SerializeCubeBlock(block, xsi))
                                            ) : null
                                        )
                                    )
                                ) : null
                            )
                        )
                    )
                )
            );

            // Save the XML document to bp.sbc
            doc.Save(bpPath);
        }

        /// <summary>
        /// Parses a PositionAndOrientation element from XML.
        /// </summary>
        /// <param name="elem">The XML element to parse.</param>
        /// <returns>A <see cref="PositionAndOrientation"/> object, or null if the element is null.</returns>
        private static PositionAndOrientation? ParsePositionAndOrientation(XElement? elem)
        {
            if (elem == null) return null;
            return new PositionAndOrientation
            {
                Position = ParseVector3(elem.Element("Position")),
                Forward = ParseVector3(elem.Element("Forward")),
                Up = ParseVector3(elem.Element("Up")),
                Orientation = ParseQuaternion(elem.Element("Orientation"))
            };
        }

        /// <summary>
        /// Parses a Vector3 element from XML.
        /// </summary>
        /// <param name="elem">The XML element to parse.</param>
        /// <returns>A <see cref="Vector3"/> object, or null if the element is null.</returns>
        private static Vector3? ParseVector3(XElement? elem)
        {
            if (elem == null) return null;
            return new Vector3
            {
                X = (float?)elem.Attribute("x") ?? 0,
                Y = (float?)elem.Attribute("y") ?? 0,
                Z = (float?)elem.Attribute("z") ?? 0
            };
        }

        /// <summary>
        /// Parses a Quaternion element from XML.
        /// </summary>
        /// <param name="elem">The XML element to parse.</param>
        /// <returns>A <see cref="Quaternion"/> object, or null if the element is null.</returns>
        private static Quaternion? ParseQuaternion(XElement? elem)
        {
            if (elem == null) return null;
            return new Quaternion
            {
                X = (float?)elem.Element("X") ?? 0,
                Y = (float?)elem.Element("Y") ?? 0,
                Z = (float?)elem.Element("Z") ?? 0,
                W = (float?)elem.Element("W") ?? 0
            };
        }

        /// <summary>
        /// Parses a CubeBlock element from XML, mapping all known fields and collecting unmapped fields.
        /// </summary>
        /// <param name="blockElem">The XML element representing a block.</param>
        /// <returns>A <see cref="CubeBlock"/> object with all mapped and unmapped fields.</returns>
        private static CubeBlock ParseCubeBlock(XElement blockElem)
        {
            var block = new CubeBlock
            {
                XsiType = (string?)blockElem.Attribute(XName.Get("type", "http://www.w3.org/2001/XMLSchema-instance")),
                SubtypeName = (string?)blockElem.Element("SubtypeName"),
                EntityId = (string?)blockElem.Element("EntityId"),
                Min = ParseVector3Int(blockElem.Element("Min")),
                BlockOrientation = ParseBlockOrientation(blockElem.Element("BlockOrientation")),
                ColorMaskHSV = ParseVector3(blockElem.Element("ColorMaskHSV")),
                SkinSubtypeId = (string?)blockElem.Element("SkinSubtypeId"),
                BuiltBy = (string?)blockElem.Element("BuiltBy"),
                Owner = (string?)blockElem.Element("Owner"),
                ShareMode = (string?)blockElem.Element("ShareMode"),
                ComponentContainer = ParseComponentContainer(blockElem.Element("ComponentContainer")),
                CustomName = (string?)blockElem.Element("CustomName"),
                ShowOnHUD = (bool?)blockElem.Element("ShowOnHUD") ?? false,
                ShowInTerminal = (bool?)blockElem.Element("ShowInTerminal") ?? false,
                ShowInToolbarConfig = (bool?)blockElem.Element("ShowInToolbarConfig") ?? false,
                ShowInInventory = (bool?)blockElem.Element("ShowInInventory") ?? false,
                NumberInGrid = (int?)blockElem.Element("NumberInGrid"),
                Enabled = (bool?)blockElem.Element("Enabled") ?? false,
                // Map all new explicit fields
                IsLocked = (string?)blockElem.Element("IsLocked"),
                BrakeForce = (string?)blockElem.Element("BrakeForce"),
                AutoLock = (string?)blockElem.Element("AutoLock"),
                FirstLockAttempt = (string?)blockElem.Element("FirstLockAttempt"),
                LockSound = (string?)blockElem.Element("LockSound"),
                UnlockSound = (string?)blockElem.Element("UnlockSound"),
                FailedAttachSound = (string?)blockElem.Element("FailedAttachSound"),
                AttachedEntityId = (string?)blockElem.Element("AttachedEntityId"),
                MasterToSlave = (string?)blockElem.Element("MasterToSlave"),
                GearPivotPosition = (string?)blockElem.Element("GearPivotPosition"),
                OtherPivot = (string?)blockElem.Element("OtherPivot"),
                LockMode = (string?)blockElem.Element("LockMode"),
                IsParkingEnabled = (string?)blockElem.Element("IsParkingEnabled"),
                CurrentProgress = (string?)blockElem.Element("CurrentProgress"),
                DisassembleEnabled = (string?)blockElem.Element("DisassembleEnabled"),
                RepeatAssembleEnabled = (string?)blockElem.Element("RepeatAssembleEnabled"),
                RepeatDisassembleEnabled = (string?)blockElem.Element("RepeatDisassembleEnabled"),
                SlaveEnabled = (string?)blockElem.Element("SlaveEnabled"),
                SpawnName = (string?)blockElem.Element("SpawnName"),
                IsStockpiling = (string?)blockElem.Element("IsStockpiling"),
                FilledRatio = (string?)blockElem.Element("FilledRatio"),
                AutoRefill = (string?)blockElem.Element("AutoRefill"),
                CurrentStoredPower = (string?)blockElem.Element("CurrentStoredPower"),
                ProducerEnabled = (string?)blockElem.Element("ProducerEnabled"),
                MaxStoredPower = (string?)blockElem.Element("MaxStoredPower"),
                SemiautoEnabled = (string?)blockElem.Element("SemiautoEnabled"),
                OnlyDischargeEnabled = (string?)blockElem.Element("OnlyDischargeEnabled"),
                ChargeMode = (string?)blockElem.Element("ChargeMode"),
                BroadcastRadius = (string?)blockElem.Element("BroadcastRadius"),
                ShowShipName = (string?)blockElem.Element("ShowShipName"),
                EnableBroadcasting = (string?)blockElem.Element("EnableBroadcasting"),
                AttachedPB = (string?)blockElem.Element("AttachedPB"),
                IgnoreAllied = (string?)blockElem.Element("IgnoreAllied"),
                IgnoreOther = (string?)blockElem.Element("IgnoreOther"),
                HudText = (string?)blockElem.Element("HudText"),
                IsShooting = (string?)blockElem.Element("IsShooting"),
                IsShootingFromTerminal = (string?)blockElem.Element("IsShootingFromTerminal"),
                IsLargeTurret = (string?)blockElem.Element("IsLargeTurret"),
                MinFov = (string?)blockElem.Element("MinFov"),
                MaxFov = (string?)blockElem.Element("MaxFov"),
                UseConveyorSystem = (string?)blockElem.Element("UseConveyorSystem"),
                GunBase = (string?)blockElem.Element("GunBase"),
                Toolbar = (string?)blockElem.Element("Toolbar"),
                SelectedGunId = (string?)blockElem.Element("SelectedGunId"),
                BuildToolbar = (string?)blockElem.Element("BuildToolbar"),
                OnLockedToolbar = (string?)blockElem.Element("OnLockedToolbar"),
                IsTargetLockingEnabled = (string?)blockElem.Element("IsTargetLockingEnabled"),
                PreviousControlledEntityId = (string?)blockElem.Element("PreviousControlledEntityId"),
                AutoPilotEnabled = (string?)blockElem.Element("AutoPilotEnabled"),
                FlightMode = (string?)blockElem.Element("FlightMode"),
                BindedCamera = (string?)blockElem.Element("BindedCamera"),
                CurrentWaypointIndex = (string?)blockElem.Element("CurrentWaypointIndex"),
                Waypoints = (string?)blockElem.Element("Waypoints"),
                Direction = (string?)blockElem.Element("Direction"),
                DockingModeEnabled = (string?)blockElem.Element("DockingModeEnabled"),
                CollisionAvoidance = (string?)blockElem.Element("CollisionAvoidance"),
                Coords = (string?)blockElem.Element("Coords"),
                Names = (string?)blockElem.Element("Names"),
                WaypointThresholdDistance = (string?)blockElem.Element("WaypointThresholdDistance"),
                IsMainRemoteControl = (string?)blockElem.Element("IsMainRemoteControl"),
                WaitForFreeWay = (string?)blockElem.Element("WaitForFreeWay"),
                IsUpdatedSave = (string?)blockElem.Element("IsUpdatedSave"),
                IntegrityPercent = (string?)blockElem.Element("IntegrityPercent"),
                BuildPercent = (string?)blockElem.Element("BuildPercent"),
                PilotRelativeWorld = (string?)blockElem.Element("PilotRelativeWorld"),
                PilotGunDefinition = (string?)blockElem.Element("PilotGunDefinition"),
                IsInFirstPersonView = (string?)blockElem.Element("IsInFirstPersonView"),
                OxygenLevel = (string?)blockElem.Element("OxygenLevel"),
                PilotJetpackEnabled = (string?)blockElem.Element("PilotJetpackEnabled"),
                TargetData = (string?)blockElem.Element("TargetData"),
                SitAnimation = (string?)blockElem.Element("SitAnimation"),
                DeformationRatio = (string?)blockElem.Element("DeformationRatio"),
                MasterToSlaveTransform = (string?)blockElem.Element("MasterToSlaveTransform"),
                MasterToSlaveGrid = (string?)blockElem.Element("MasterToSlaveGrid"),
                IsMaster = (string?)blockElem.Element("IsMaster"),
                TradingEnabled = (string?)blockElem.Element("TradingEnabled"),
                AutoUnlockTime = (string?)blockElem.Element("AutoUnlockTime"),
                TimeOfConnection = (string?)blockElem.Element("TimeOfConnection"),
                IsPowerTransferOverrideEnabled = (string?)blockElem.Element("IsPowerTransferOverrideEnabled"),
                IsApproaching = (string?)blockElem.Element("IsApproaching"),
                IsConnecting = (string?)blockElem.Element("IsConnecting"),
                Radius = (string?)blockElem.Element("Radius"),
                ReflectorRadius = (string?)blockElem.Element("ReflectorRadius"),
                Falloff = (string?)blockElem.Element("Falloff"),
                Intensity = (string?)blockElem.Element("Intensity"),
                BlinkIntervalSeconds = (string?)blockElem.Element("BlinkIntervalSeconds"),
                BlinkLenght = (string?)blockElem.Element("BlinkLenght"),
                BlinkOffset = (string?)blockElem.Element("BlinkOffset"),
                Offset = (string?)blockElem.Element("Offset"),
                RotationSpeed = (string?)blockElem.Element("RotationSpeed"),
                Capacity = (string?)blockElem.Element("Capacity"),
                UseSingleWeaponMode = (string?)blockElem.Element("UseSingleWeaponMode"),
                IsActive = (string?)blockElem.Element("IsActive"),
                FoV = (string?)blockElem.Element("FoV"),
                // Add all previously unmapped fields below
                ConstructionStockpile = (string?)blockElem.Element("ConstructionStockpile"),
                ColorRed = ParseFloat(blockElem.Element("ColorRed")),
                ColorGreen = ParseFloat(blockElem.Element("ColorGreen")),
                ColorBlue = ParseFloat(blockElem.Element("ColorBlue")),
                IsDepressurizing = ParseBool(blockElem.Element("IsDepressurizing")),
                Range = ParseFloat(blockElem.Element("Range")),
                RemainingAmmo = ParseInt(blockElem.Element("RemainingAmmo")),
                Target = ParseInt(blockElem.Element("Target")),
                IsPotentialTarget = ParseBool(blockElem.Element("IsPotentialTarget")),
                Rotation = ParseFloat(blockElem.Element("Rotation")),
                Elevation = ParseFloat(blockElem.Element("Elevation")),
                EnableIdleRotation = ParseBool(blockElem.Element("EnableIdleRotation")),
                PreviousIdleRotationState = ParseBool(blockElem.Element("PreviousIdleRotationState")),
                TargetCharacters = ParseBool(blockElem.Element("TargetCharacters")),
                TargetingGroup = (string?)blockElem.Element("TargetingGroup"),
                Flags = (string?)blockElem.Element("Flags"),
                HorizonIndicatorEnabled = ParseBool(blockElem.Element("HorizonIndicatorEnabled")),
                SteamId = (string?)blockElem.Element("SteamId"),
                SerialId = (string?)blockElem.Element("SerialId"),
                SteamUserId = (string?)blockElem.Element("SteamUserId"),
                IdleSound = (string?)blockElem.Element("IdleSound"),
                ProgressSound = (string?)blockElem.Element("ProgressSound"),
                TakeOwnership = ParseBool(blockElem.Element("TakeOwnership")),
                SetFaction = ParseBool(blockElem.Element("SetFaction")),
                WardrobeUserId = (string?)blockElem.Element("WardrobeUserId"),
                Threshold = ParseFloat(blockElem.Element("Threshold")),
                ANDGate = ParseBool(blockElem.Element("ANDGate")),
                SelectedEvent = ParseInt(blockElem.Element("SelectedEvent")),
                SelectedBlocks = (string?)blockElem.Element("SelectedBlocks"),
                ConditionInvert = ParseBool(blockElem.Element("ConditionInvert")),
                Delay = ParseInt(blockElem.Element("Delay")),
                CurrentTime = ParseInt(blockElem.Element("CurrentTime")),
                IsCountingDown = ParseBool(blockElem.Element("IsCountingDown")),
                Silent = ParseBool(blockElem.Element("Silent")),
                DetectionRadius = ParseInt(blockElem.Element("DetectionRadius")),
                BroadcastUsingAntennas = ParseBool(blockElem.Element("BroadcastUsingAntennas")),
                IsSurvivalModeForced = ParseBool(blockElem.Element("IsSurvivalModeForced")),
                StoredPower = ParseFloat(blockElem.Element("StoredPower")),
                JumpTarget = (string?)blockElem.Element("JumpTarget"),
                JumpRatio = ParseFloat(blockElem.Element("JumpRatio")),
                Recharging = ParseBool(blockElem.Element("Recharging")),
                SelectedBeaconName = (string?)blockElem.Element("SelectedBeaconName"),
                SelectedBeaconId = ParseInt(blockElem.Element("SelectedBeaconId")),
                TopBlockId = (string?)blockElem.Element("TopBlockId"),
                ShareInertiaTensor = ParseBool(blockElem.Element("ShareInertiaTensor")),
                SafetyDetach = ParseInt(blockElem.Element("SafetyDetach")),
                RotorEntityId = (string?)blockElem.Element("RotorEntityId"),
                WeldedEntityId = (string?)blockElem.Element("WeldedEntityId"),
                TargetVelocity = ParseFloat(blockElem.Element("TargetVelocity")),
                MinAngle = ParseFloat(blockElem.Element("MinAngle")),
                MaxAngle = ParseFloat(blockElem.Element("MaxAngle")),
                CurrentAngle = ParseFloat(blockElem.Element("CurrentAngle")),
                LimitsActive = ParseBool(blockElem.Element("LimitsActive")),
                RotorLock = ParseBool(blockElem.Element("RotorLock")),
                Torque = ParseFloat(blockElem.Element("Torque")),
                BrakingTorque = ParseFloat(blockElem.Element("BrakingTorque")),
                AnyoneCanUse = ParseBool(blockElem.Element("AnyoneCanUse")),
                CustomButtonNames = (string?)blockElem.Element("CustomButtonNames"),
                Opening = ParseFloat(blockElem.Element("Opening")),
                OpenSound = (string?)blockElem.Element("OpenSound"),
                CloseSound = (string?)blockElem.Element("CloseSound"),
                TargetPriority = (string?)blockElem.Element("TargetPriority"),
                UpdateTargetInterval = ParseInt(blockElem.Element("UpdateTargetInterval")),
                SelectedAttackPattern = ParseInt(blockElem.Element("SelectedAttackPattern")),
                CanTargetCharacters = ParseBool(blockElem.Element("CanTargetCharacters")),
                GravityAcceleration = ParseFloat(blockElem.Element("GravityAcceleration")),
                FieldSize = (string?)blockElem.Element("FieldSize"),
                Description = (string?)blockElem.Element("Description"),
                Title = (string?)blockElem.Element("Title"),
                AccessFlag = (string?)blockElem.Element("AccessFlag"),
                ChangeInterval = ParseInt(blockElem.Element("ChangeInterval")),
                Font = (string?)blockElem.Element("Font"),
                FontSize = ParseFloat(blockElem.Element("FontSize")),
                PublicDescription = (string?)blockElem.Element("PublicDescription"),
                PublicTitle = (string?)blockElem.Element("PublicTitle"),
                ShowText = (string?)blockElem.Element("ShowText"),
                FontColor = (string?)blockElem.Element("FontColor"),
                BackgroundColor = (string?)blockElem.Element("BackgroundColor"),
                CurrentShownTexture = ParseInt(blockElem.Element("CurrentShownTexture")),
                TextPadding = ParseInt(blockElem.Element("TextPadding")),
                Version = ParseInt(blockElem.Element("Version")),
                ScriptBackgroundColor = (string?)blockElem.Element("ScriptBackgroundColor"),
                ScriptForegroundColor = (string?)blockElem.Element("ScriptForegroundColor"),
                Sprites = ParseInt(blockElem.Element("Sprites")),
                SelectedRotationIndex = (string?)blockElem.Element("SelectedRotationIndex"),
                TargetLocking = ParseBool(blockElem.Element("TargetLocking")),
                Volume = ParseFloat(blockElem.Element("Volume")),
                CueName = (string?)blockElem.Element("CueName"),
                LoopPeriod = ParseFloat(blockElem.Element("LoopPeriod")),
                IsPlaying = ParseBool(blockElem.Element("IsPlaying")),
                ElapsedSoundSeconds = ParseFloat(blockElem.Element("ElapsedSoundSeconds")),
                IsLoopableSound = ParseBool(blockElem.Element("IsLoopableSound")),
                IsMainCockpit = ParseBool(blockElem.Element("IsMainCockpit")),
                NextItemId = ParseInt(blockElem.Element("NextItemId")),
                SelectedSounds = (string?)blockElem.Element("SelectedSounds"),
                IsJukeboxPlaying = ParseBool(blockElem.Element("IsJukeboxPlaying")),
                CurrentSound = ParseInt(blockElem.Element("CurrentSound")),
                TargetAngularVelocity = (string?)blockElem.Element("TargetAngularVelocity"),
                State = ParseBool(blockElem.Element("State")),
                FieldMin = (string?)blockElem.Element("FieldMin"),
                FieldMax = (string?)blockElem.Element("FieldMax"),
                PlaySound = ParseBool(blockElem.Element("PlaySound")),
                DetectPlayers = ParseBool(blockElem.Element("DetectPlayers")),
                DetectFloatingObjects = ParseBool(blockElem.Element("DetectFloatingObjects")),
                DetectSmallShips = ParseBool(blockElem.Element("DetectSmallShips")),
                DetectLargeShips = ParseBool(blockElem.Element("DetectLargeShips")),
                DetectStations = ParseBool(blockElem.Element("DetectStations")),
                DetectSubgrids = ParseBool(blockElem.Element("DetectSubgrids")),
                DetectAsteroids = ParseBool(blockElem.Element("DetectAsteroids")),
                DetectOwner = ParseBool(blockElem.Element("DetectOwner")),
                DetectFriendly = ParseBool(blockElem.Element("DetectFriendly")),
                DetectNeutral = ParseBool(blockElem.Element("DetectNeutral")),
                DetectEnemy = ParseBool(blockElem.Element("DetectEnemy")),
                SafeZoneId = (string?)blockElem.Element("SafeZoneId"),
                ConnectedEntityId = (string?)blockElem.Element("ConnectedEntityId"),
                Strength = ParseFloat(blockElem.Element("Strength")),
                GyroPower = ParseFloat(blockElem.Element("GyroPower")),
                ProjectedGrids = (string?)blockElem.Element("ProjectedGrids"),
                ProjectionOffset = (string?)blockElem.Element("ProjectionOffset"),
                ProjectionRotation = (string?)blockElem.Element("ProjectionRotation"),
                KeepProjection = ParseBool(blockElem.Element("KeepProjection")),
                ShowOnlyBuildable = ParseBool(blockElem.Element("ShowOnlyBuildable")),
                InstantBuildingEnabled = ParseBool(blockElem.Element("InstantBuildingEnabled")),
                MaxNumberOfProjections = ParseInt(blockElem.Element("MaxNumberOfProjections")),
                MaxNumberOfBlocks = ParseInt(blockElem.Element("MaxNumberOfBlocks")),
                ProjectionsRemaining = ParseInt(blockElem.Element("ProjectionsRemaining")),
                GetOwnershipFromProjector = ParseBool(blockElem.Element("GetOwnershipFromProjector")),
                Scale = ParseFloat(blockElem.Element("Scale")),
                FleeTrigger = (string?)blockElem.Element("FleeTrigger"),
                SelectedBeaconEntityId = ParseLong(blockElem.Element("SelectedBeaconEntityId")),
                SelectedGpsHash = (string?)blockElem.Element("SelectedGpsHash"),
                WaypointZoneSize = ParseInt(blockElem.Element("WaypointZoneSize")),
                LockTarget = ParseBool(blockElem.Element("LockTarget")),
                CustomFleeCoordinates = (string?)blockElem.Element("CustomFleeCoordinates"),
                UseCustomFleeCoordinates = ParseBool(blockElem.Element("UseCustomFleeCoordinates")),
                SelectedGpsHashNew = (string?)blockElem.Element("SelectedGpsHashNew"),
                FleeDistance = ParseFloat(blockElem.Element("FleeDistance")),
                EvasiveManeuvers = ParseBool(blockElem.Element("EvasiveManeuvers")),
                EvasiveManeuverAngle = ParseFloat(blockElem.Element("EvasiveManeuverAngle")),
                EvasiveManeuverIntervalRange = ParseFloat(blockElem.Element("EvasiveManeuverIntervalRange")),
                FleeMode = (string?)blockElem.Element("FleeMode"),
                LastKnownEnemyPosition = (string?)blockElem.Element("LastKnownEnemyPosition"),
                FleeMinHeightOnPlanets = ParseInt(blockElem.Element("FleeMinHeightOnPlanets")),
                PrevToolbarState = ParseBool(blockElem.Element("PrevToolbarState")),
                ParentEntityId = ParseLong(blockElem.Element("ParentEntityId")),
                YieldLastComponent = ParseBool(blockElem.Element("YieldLastComponent")),
            };
            // Only add unmapped fields to OtherFields
            var mapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SubtypeName","EntityId","Min","BlockOrientation","ColorMaskHSV","SkinSubtypeId","BuiltBy","Owner","ShareMode","ComponentContainer","CustomName","ShowOnHUD","ShowInTerminal","ShowInToolbarConfig","ShowInInventory","NumberInGrid","Enabled",
                // Add all new explicit fields here
                "IsLocked","BrakeForce","AutoLock","FirstLockAttempt","LockSound","UnlockSound","FailedAttachSound","AttachedEntityId","MasterToSlave","GearPivotPosition","OtherPivot","LockMode","IsParkingEnabled","CurrentProgress","DisassembleEnabled","RepeatAssembleEnabled","RepeatDisassembleEnabled","SlaveEnabled","SpawnName","IsStockpiling","FilledRatio","AutoRefill","CurrentStoredPower","ProducerEnabled","MaxStoredPower","SemiautoEnabled","OnlyDischargeEnabled","ChargeMode","BroadcastRadius","ShowShipName","EnableBroadcasting","AttachedPB","IgnoreAllied","IgnoreOther","HudText","IsShooting","IsShootingFromTerminal","IsLargeTurret","MinFov","MaxFov","UseConveyorSystem","GunBase","Toolbar","SelectedGunId","BuildToolbar","OnLockedToolbar","IsTargetLockingEnabled","PreviousControlledEntityId","AutoPilotEnabled","FlightMode","BindedCamera","CurrentWaypointIndex","Waypoints","Direction","DockingModeEnabled","CollisionAvoidance","Coords","Names","WaypointThresholdDistance","IsMainRemoteControl","WaitForFreeWay","IsUpdatedSave","IntegrityPercent","BuildPercent","PilotRelativeWorld","PilotGunDefinition","IsInFirstPersonView","OxygenLevel","PilotJetpackEnabled","TargetData","SitAnimation","DeformationRatio","MasterToSlaveTransform","MasterToSlaveGrid","IsMaster","TradingEnabled","AutoUnlockTime","TimeOfConnection","IsPowerTransferOverrideEnabled","IsApproaching","IsConnecting","Radius","ReflectorRadius","Falloff","Intensity","BlinkIntervalSeconds","BlinkLenght","BlinkOffset","Offset","RotationSpeed","Capacity","UseSingleWeaponMode","IsActive","FoV",
                // Add all previously unmapped fields below
                "ConstructionStockpile","ColorRed","ColorGreen","ColorBlue","IsDepressurizing","Range","RemainingAmmo","Target","IsPotentialTarget","Rotation","Elevation","EnableIdleRotation","PreviousIdleRotationState","TargetCharacters","TargetingGroup","Flags","HorizonIndicatorEnabled","SteamId","SerialId","SteamUserId","IdleSound","ProgressSound","TakeOwnership","SetFaction","WardrobeUserId","Threshold","ANDGate","SelectedEvent","SelectedBlocks","ConditionInvert","Delay","CurrentTime","IsCountingDown","Silent","DetectionRadius","BroadcastUsingAntennas","IsSurvivalModeForced","StoredPower","JumpTarget","JumpRatio","Recharging","SelectedBeaconName","SelectedBeaconId","TopBlockId","ShareInertiaTensor","SafetyDetach","RotorEntityId","WeldedEntityId","TargetVelocity","MinAngle","MaxAngle","CurrentAngle","LimitsActive","RotorLock","Torque","BrakingTorque","AnyoneCanUse","CustomButtonNames","Opening","OpenSound","CloseSound","TargetPriority","UpdateTargetInterval","SelectedAttackPattern","CanTargetCharacters","GravityAcceleration","FieldSize","Description","Title","AccessFlag","ChangeInterval","Font","FontSize","PublicDescription","PublicTitle","ShowText","FontColor","BackgroundColor","CurrentShownTexture","TextPadding","Version","ScriptBackgroundColor","ScriptForegroundColor","Sprites","SelectedRotationIndex","IsMainCockpit","NextItemId","SelectedSounds","IsJukeboxPlaying","CurrentSound","TargetAngularVelocity","State","FieldMin","FieldMax","PlaySound","DetectPlayers","DetectFloatingObjects","DetectSmallShips","DetectLargeShips","DetectStations","DetectSubgrids","DetectAsteroids","DetectOwner","DetectFriendly","DetectNeutral","DetectEnemy","SafeZoneId","ConnectedEntityId","Strength","GyroPower","ProjectedGrids",
                "ProjectionOffset","ProjectionRotation","KeepProjection","ShowOnlyBuildable","InstantBuildingEnabled","MaxNumberOfProjections","MaxNumberOfBlocks","ProjectionsRemaining","GetOwnershipFromProjector","Scale","FleeTrigger","SelectedBeaconEntityId","SelectedGpsHash","WaypointZoneSize","LockTarget","CustomFleeCoordinates","UseCustomFleeCoordinates","SelectedGpsHashNew","FleeDistance","EvasiveManeuvers","EvasiveManeuverAngle","EvasiveManeuverIntervalRange","FleeMode","LastKnownEnemyPosition","FleeMinHeightOnPlanets","PrevToolbarState","ParentEntityId","YieldLastComponent",
                "TargetLocking","Volume","CueName","LoopPeriod","IsPlaying","ElapsedSoundSeconds","IsLoopableSound"
            };
            foreach (var elem in blockElem.Elements())
            {
                if (!mapped.Contains(elem.Name.LocalName) && !block.OtherFields.ContainsKey(elem.Name.LocalName))
                    block.OtherFields[elem.Name.LocalName] = elem.Value;
            }
            if (block.OtherFields.Count > 0)
                LogWarningUnmappedFields(block, block.OtherFields);
            return block;
        }

        /// <summary>
        /// Logs a warning for any unmapped fields found in a CubeBlock, including their values.
        /// </summary>
        /// <param name="block">The block containing unmapped fields.</param>
        /// <param name="fields">The dictionary of unmapped fields.</param>
        private static void LogWarningUnmappedFields(CubeBlock block, Dictionary<string, string> fields)
        {
            var details = string.Join(", ", fields.Select(kvp => $"{kvp.Key}=[{kvp.Value}]").ToArray());
            Console.WriteLine($"WARNING: Unmapped fields in block {block.SubtypeName ?? "(unknown)"} (EntityId: {block.EntityId ?? "(none)"}): {details}");
        }

        /// <summary>
        /// Parses a Vector3Int element from XML.
        /// </summary>
        /// <param name="elem">The XML element to parse.</param>
        /// <returns>A <see cref="Vector3Int"/> object, or null if the element is null.</returns>
        private static Vector3Int? ParseVector3Int(XElement? elem)
        {
            if (elem == null) return null;
            return new Vector3Int
            {
                X = (int?)elem.Attribute("x") ?? 0,
                Y = (int?)elem.Attribute("y") ?? 0,
                Z = (int?)elem.Attribute("z") ?? 0
            };
        }

        /// <summary>
        /// Parses a BlockOrientation element from XML.
        /// </summary>
        /// <param name="elem">The XML element to parse.</param>
        /// <returns>A <see cref="BlockOrientation"/> object, or null if the element is null.</returns>
        private static BlockOrientation? ParseBlockOrientation(XElement? elem)
        {
            if (elem == null) return null;
            return new BlockOrientation
            {
                Forward = (string?)elem.Attribute("Forward"),
                Up = (string?)elem.Attribute("Up")
            };
        }

        /// <summary>
        /// Parses a ComponentContainer element from XML, storing component data as raw XML.
        /// </summary>
        /// <param name="elem">The XML element to parse.</param>
        /// <returns>A <see cref="ComponentContainer"/> object, or null if the element is null.</returns>
        private static ComponentContainer? ParseComponentContainer(XElement? elem)
        {
            if (elem == null) return null;
            var container = new ComponentContainer
            {
                Components = new List<ComponentData>()
            };
            var componentsElem = elem.Element("Components");
            if (componentsElem != null)
            {
                foreach (var compDataElem in componentsElem.Elements("ComponentData"))
                {
                    var compData = new ComponentData
                    {
                        TypeId = (string?)compDataElem.Element("TypeId"),
                        Component = compDataElem.Element("Component")?.ToString() // Store as raw XML for now
                    };
                    container.Components.Add(compData);
                }
            }
            return container;
        }

        private static XElement SerializePositionAndOrientation(PositionAndOrientation? pos)
        {
            if (pos == null) return null!;
            return new XElement("PositionAndOrientation",
                pos.Position != null ? SerializeVector3("Position", pos.Position) : null,
                pos.Forward != null ? SerializeVector3("Forward", pos.Forward) : null,
                pos.Up != null ? SerializeVector3("Up", pos.Up) : null,
                pos.Orientation != null ? SerializeQuaternion(pos.Orientation) : null
            );
        }

        private static XElement SerializeVector3(string name, Vector3 v)
        {
            return new XElement(name,
                new XAttribute("x", v.X),
                new XAttribute("y", v.Y),
                new XAttribute("z", v.Z)
            );
        }

        private static XElement SerializeQuaternion(Quaternion q)
        {
            return new XElement("Orientation",
                new XElement("X", q.X),
                new XElement("Y", q.Y),
                new XElement("Z", q.Z),
                new XElement("W", q.W)
            );
        }

        private static XElement SerializeVector3Int(string name, Vector3Int v)
        {
            return new XElement(name,
                new XAttribute("x", v.X),
                new XAttribute("y", v.Y),
                new XAttribute("z", v.Z)
            );
        }

        private static XElement SerializeBlockOrientation(BlockOrientation? o)
        {
            if (o == null) return null!;
            return new XElement("BlockOrientation",
                o.Forward != null ? new XAttribute("Forward", o.Forward) : null,
                o.Up != null ? new XAttribute("Up", o.Up) : null
            );
        }

        private static XElement SerializeComponentContainer(ComponentContainer? c)
        {
            if (c == null) return null!;
            return new XElement("ComponentContainer",
                new XElement("Components",
                    c.Components.ConvertAll(comp =>
                        new XElement("ComponentData",
                            comp.TypeId != null ? new XElement("TypeId", comp.TypeId) : null,
                            comp.Component != null ? XElement.Parse(comp.Component) : null
                        )
                    )
                )
            );
        }

        private static XElement SerializeCubeBlock(CubeBlock block, XNamespace xsi)
        {
            var elem = new XElement("MyObjectBuilder_CubeBlock");
            if (!string.IsNullOrEmpty(block.XsiType))
                elem.SetAttributeValue(xsi + "type", block.XsiType);
            if (block.SubtypeName != null) elem.Add(new XElement("SubtypeName", block.SubtypeName));
            if (block.EntityId != null) elem.Add(new XElement("EntityId", block.EntityId));
            if (block.Min != null) elem.Add(SerializeVector3Int("Min", block.Min));
            if (block.BlockOrientation != null) elem.Add(SerializeBlockOrientation(block.BlockOrientation));
            if (block.ColorMaskHSV != null) elem.Add(SerializeVector3("ColorMaskHSV", block.ColorMaskHSV));
            if (block.SkinSubtypeId != null) elem.Add(new XElement("SkinSubtypeId", block.SkinSubtypeId));
            if (block.BuiltBy != null) elem.Add(new XElement("BuiltBy", block.BuiltBy));
            if (block.Owner != null) elem.Add(new XElement("Owner", block.Owner));
            if (block.ShareMode != null) elem.Add(new XElement("ShareMode", block.ShareMode));
            if (block.ComponentContainer != null) elem.Add(SerializeComponentContainer(block.ComponentContainer));
            if (block.CustomName != null) elem.Add(new XElement("CustomName", block.CustomName));
            elem.Add(new XElement("ShowOnHUD", block.ShowOnHUD));
            elem.Add(new XElement("ShowInTerminal", block.ShowInTerminal));
            elem.Add(new XElement("ShowInToolbarConfig", block.ShowInToolbarConfig));
            elem.Add(new XElement("ShowInInventory", block.ShowInInventory));
            if (block.NumberInGrid != null) elem.Add(new XElement("NumberInGrid", block.NumberInGrid));
            elem.Add(new XElement("Enabled", block.Enabled));
            // Add all explicit fields
            if (block.IsLocked != null) elem.Add(new XElement("IsLocked", block.IsLocked));
            if (block.BrakeForce != null) elem.Add(new XElement("BrakeForce", block.BrakeForce));
            if (block.AutoLock != null) elem.Add(new XElement("AutoLock", block.AutoLock));
            if (block.FirstLockAttempt != null) elem.Add(new XElement("FirstLockAttempt", block.FirstLockAttempt));
            if (block.LockSound != null) elem.Add(new XElement("LockSound", block.LockSound));
            if (block.UnlockSound != null) elem.Add(new XElement("UnlockSound", block.UnlockSound));
            if (block.FailedAttachSound != null) elem.Add(new XElement("FailedAttachSound", block.FailedAttachSound));
            if (block.AttachedEntityId != null) elem.Add(new XElement("AttachedEntityId", block.AttachedEntityId));
            if (block.MasterToSlave != null) elem.Add(new XElement("MasterToSlave", block.MasterToSlave));
            if (block.GearPivotPosition != null) elem.Add(new XElement("GearPivotPosition", block.GearPivotPosition));
            if (block.OtherPivot != null) elem.Add(new XElement("OtherPivot", block.OtherPivot));
            if (block.LockMode != null) elem.Add(new XElement("LockMode", block.LockMode));
            if (block.IsParkingEnabled != null) elem.Add(new XElement("IsParkingEnabled", block.IsParkingEnabled));
            if (block.CurrentProgress != null) elem.Add(new XElement("CurrentProgress", block.CurrentProgress));
            if (block.DisassembleEnabled != null) elem.Add(new XElement("DisassembleEnabled", block.DisassembleEnabled));
            if (block.RepeatAssembleEnabled != null) elem.Add(new XElement("RepeatAssembleEnabled", block.RepeatAssembleEnabled));
            if (block.RepeatDisassembleEnabled != null) elem.Add(new XElement("RepeatDisassembleEnabled", block.RepeatDisassembleEnabled));
            if (block.SlaveEnabled != null) elem.Add(new XElement("SlaveEnabled", block.SlaveEnabled));
            if (block.SpawnName != null) elem.Add(new XElement("SpawnName", block.SpawnName));
            if (block.IsStockpiling != null) elem.Add(new XElement("IsStockpiling", block.IsStockpiling));
            if (block.FilledRatio != null) elem.Add(new XElement("FilledRatio", block.FilledRatio));
            if (block.AutoRefill != null) elem.Add(new XElement("AutoRefill", block.AutoRefill));
            if (block.CurrentStoredPower != null) elem.Add(new XElement("CurrentStoredPower", block.CurrentStoredPower));
            if (block.ProducerEnabled != null) elem.Add(new XElement("ProducerEnabled", block.ProducerEnabled));
            if (block.MaxStoredPower != null) elem.Add(new XElement("MaxStoredPower", block.MaxStoredPower));
            if (block.SemiautoEnabled != null) elem.Add(new XElement("SemiautoEnabled", block.SemiautoEnabled));
            if (block.OnlyDischargeEnabled != null) elem.Add(new XElement("OnlyDischargeEnabled", block.OnlyDischargeEnabled));
            if (block.ChargeMode != null) elem.Add(new XElement("ChargeMode", block.ChargeMode));
            if (block.BroadcastRadius != null) elem.Add(new XElement("BroadcastRadius", block.BroadcastRadius));
            if (block.ShowShipName != null) elem.Add(new XElement("ShowShipName", block.ShowShipName));
            if (block.EnableBroadcasting != null) elem.Add(new XElement("EnableBroadcasting", block.EnableBroadcasting));
            if (block.AttachedPB != null) elem.Add(new XElement("AttachedPB", block.AttachedPB));
            if (block.IgnoreAllied != null) elem.Add(new XElement("IgnoreAllied", block.IgnoreAllied));
            if (block.IgnoreOther != null) elem.Add(new XElement("IgnoreOther", block.IgnoreOther));
            if (block.HudText != null) elem.Add(new XElement("HudText", block.HudText));
            if (block.IsShooting != null) elem.Add(new XElement("IsShooting", block.IsShooting));
            if (block.IsShootingFromTerminal != null) elem.Add(new XElement("IsShootingFromTerminal", block.IsShootingFromTerminal));
            if (block.IsLargeTurret != null) elem.Add(new XElement("IsLargeTurret", block.IsLargeTurret));
            if (block.MinFov != null) elem.Add(new XElement("MinFov", block.MinFov));
            if (block.MaxFov != null) elem.Add(new XElement("MaxFov", block.MaxFov));
            if (block.UseConveyorSystem != null) elem.Add(new XElement("UseConveyorSystem", block.UseConveyorSystem));
            if (block.GunBase != null) elem.Add(new XElement("GunBase", block.GunBase));
            if (block.Toolbar != null) elem.Add(new XElement("Toolbar", block.Toolbar));
            if (block.SelectedGunId != null) elem.Add(new XElement("SelectedGunId", block.SelectedGunId));
            if (block.BuildToolbar != null) elem.Add(new XElement("BuildToolbar", block.BuildToolbar));
            if (block.OnLockedToolbar != null) elem.Add(new XElement("OnLockedToolbar", block.OnLockedToolbar));
            if (block.IsTargetLockingEnabled != null) elem.Add(new XElement("IsTargetLockingEnabled", block.IsTargetLockingEnabled));
            if (block.PreviousControlledEntityId != null) elem.Add(new XElement("PreviousControlledEntityId", block.PreviousControlledEntityId));
            if (block.AutoPilotEnabled != null) elem.Add(new XElement("AutoPilotEnabled", block.AutoPilotEnabled));
            if (block.FlightMode != null) elem.Add(new XElement("FlightMode", block.FlightMode));
            if (block.BindedCamera != null) elem.Add(new XElement("BindedCamera", block.BindedCamera));
            if (block.CurrentWaypointIndex != null) elem.Add(new XElement("CurrentWaypointIndex", block.CurrentWaypointIndex));
            if (block.Waypoints != null) elem.Add(new XElement("Waypoints", block.Waypoints));
            if (block.Direction != null) elem.Add(new XElement("Direction", block.Direction));
            if (block.DockingModeEnabled != null) elem.Add(new XElement("DockingModeEnabled", block.DockingModeEnabled));
            if (block.CollisionAvoidance != null) elem.Add(new XElement("CollisionAvoidance", block.CollisionAvoidance));
            if (block.Coords != null) elem.Add(new XElement("Coords", block.Coords));
            if (block.Names != null) elem.Add(new XElement("Names", block.Names));
            if (block.WaypointThresholdDistance != null) elem.Add(new XElement("WaypointThresholdDistance", block.WaypointThresholdDistance));
            if (block.IsMainRemoteControl != null) elem.Add(new XElement("IsMainRemoteControl", block.IsMainRemoteControl));
            if (block.WaitForFreeWay != null) elem.Add(new XElement("WaitForFreeWay", block.WaitForFreeWay));
            if (block.IsUpdatedSave != null) elem.Add(new XElement("IsUpdatedSave", block.IsUpdatedSave));
            if (block.IntegrityPercent != null) elem.Add(new XElement("IntegrityPercent", block.IntegrityPercent));
            if (block.BuildPercent != null) elem.Add(new XElement("BuildPercent", block.BuildPercent));
            if (block.PilotRelativeWorld != null) elem.Add(new XElement("PilotRelativeWorld", block.PilotRelativeWorld));
            if (block.PilotGunDefinition != null) elem.Add(new XElement("PilotGunDefinition", block.PilotGunDefinition));
            if (block.IsInFirstPersonView != null) elem.Add(new XElement("IsInFirstPersonView", block.IsInFirstPersonView));
            if (block.OxygenLevel != null) elem.Add(new XElement("OxygenLevel", block.OxygenLevel));
            if (block.PilotJetpackEnabled != null) elem.Add(new XElement("PilotJetpackEnabled", block.PilotJetpackEnabled));
            if (block.TargetData != null) elem.Add(new XElement("TargetData", block.TargetData));
            if (block.SitAnimation != null) elem.Add(new XElement("SitAnimation", block.SitAnimation));
            if (block.DeformationRatio != null) elem.Add(new XElement("DeformationRatio", block.DeformationRatio));
            if (block.MasterToSlaveTransform != null) elem.Add(new XElement("MasterToSlaveTransform", block.MasterToSlaveTransform));
            if (block.MasterToSlaveGrid != null) elem.Add(new XElement("MasterToSlaveGrid", block.MasterToSlaveGrid));
            if (block.IsMaster != null) elem.Add(new XElement("IsMaster", block.IsMaster));
            if (block.TradingEnabled != null) elem.Add(new XElement("TradingEnabled", block.TradingEnabled));
            if (block.AutoUnlockTime != null) elem.Add(new XElement("AutoUnlockTime", block.AutoUnlockTime));
            if (block.TimeOfConnection != null) elem.Add(new XElement("TimeOfConnection", block.TimeOfConnection));
            if (block.IsPowerTransferOverrideEnabled != null) elem.Add(new XElement("IsPowerTransferOverrideEnabled", block.IsPowerTransferOverrideEnabled));
            if (block.IsApproaching != null) elem.Add(new XElement("IsApproaching", block.IsApproaching));
            if (block.IsConnecting != null) elem.Add(new XElement("IsConnecting", block.IsConnecting));
            if (block.Radius != null) elem.Add(new XElement("Radius", block.Radius));
            if (block.ReflectorRadius != null) elem.Add(new XElement("ReflectorRadius", block.ReflectorRadius));
            if (block.Falloff != null) elem.Add(new XElement("Falloff", block.Falloff));
            if (block.Intensity != null) elem.Add(new XElement("Intensity", block.Intensity));
            if (block.BlinkIntervalSeconds != null) elem.Add(new XElement("BlinkIntervalSeconds", block.BlinkIntervalSeconds));
            if (block.BlinkLenght != null) elem.Add(new XElement("BlinkLenght", block.BlinkLenght));
            if (block.BlinkOffset != null) elem.Add(new XElement("BlinkOffset", block.BlinkOffset));
            if (block.Offset != null) elem.Add(new XElement("Offset", block.Offset));
            if (block.RotationSpeed != null) elem.Add(new XElement("RotationSpeed", block.RotationSpeed));
            if (block.Capacity != null) elem.Add(new XElement("Capacity", block.Capacity));
            if (block.UseSingleWeaponMode != null) elem.Add(new XElement("UseSingleWeaponMode", block.UseSingleWeaponMode));
            if (block.IsActive != null) elem.Add(new XElement("IsActive", block.IsActive));
            if (block.FoV != null) elem.Add(new XElement("FoV", block.FoV));
            // Add unmapped fields
            if (block.OtherFields != null)
            {
                foreach (var kvp in block.OtherFields)
                {
                    elem.Add(new XElement(kvp.Key, kvp.Value));
                }
            }
            if (block.IsMainCockpit != null) elem.Add(new XElement("IsMainCockpit", block.IsMainCockpit));
            if (block.NextItemId != null) elem.Add(new XElement("NextItemId", block.NextItemId));
            if (block.SelectedSounds != null) elem.Add(new XElement("SelectedSounds", block.SelectedSounds));
            if (block.IsJukeboxPlaying != null) elem.Add(new XElement("IsJukeboxPlaying", block.IsJukeboxPlaying));
            if (block.CurrentSound != null) elem.Add(new XElement("CurrentSound", block.CurrentSound));
            if (block.TargetAngularVelocity != null) elem.Add(new XElement("TargetAngularVelocity", block.TargetAngularVelocity));
            if (block.State != null) elem.Add(new XElement("State", block.State));
            if (block.FieldMin != null) elem.Add(new XElement("FieldMin", block.FieldMin));
            if (block.FieldMax != null) elem.Add(new XElement("FieldMax", block.FieldMax));
            if (block.PlaySound != null) elem.Add(new XElement("PlaySound", block.PlaySound));
            if (block.DetectPlayers != null) elem.Add(new XElement("DetectPlayers", block.DetectPlayers));
            if (block.DetectFloatingObjects != null) elem.Add(new XElement("DetectFloatingObjects", block.DetectFloatingObjects));
            if (block.DetectSmallShips != null) elem.Add(new XElement("DetectSmallShips", block.DetectSmallShips));
            if (block.DetectLargeShips != null) elem.Add(new XElement("DetectLargeShips", block.DetectLargeShips));
            if (block.DetectStations != null) elem.Add(new XElement("DetectStations", block.DetectStations));
            if (block.DetectSubgrids != null) elem.Add(new XElement("DetectSubgrids", block.DetectSubgrids));
            if (block.DetectAsteroids != null) elem.Add(new XElement("DetectAsteroids", block.DetectAsteroids));
            if (block.DetectOwner != null) elem.Add(new XElement("DetectOwner", block.DetectOwner));
            if (block.DetectFriendly != null) elem.Add(new XElement("DetectFriendly", block.DetectFriendly));
            if (block.DetectNeutral != null) elem.Add(new XElement("DetectNeutral", block.DetectNeutral));
            if (block.DetectEnemy != null) elem.Add(new XElement("DetectEnemy", block.DetectEnemy));
            if (block.SafeZoneId != null) elem.Add(new XElement("SafeZoneId", block.SafeZoneId));
            if (block.ConnectedEntityId != null) elem.Add(new XElement("ConnectedEntityId", block.ConnectedEntityId));
            if (block.Strength != null) elem.Add(new XElement("Strength", block.Strength));
            if (block.GyroPower != null) elem.Add(new XElement("GyroPower", block.GyroPower));
            if (block.ProjectedGrids != null) elem.Add(new XElement("ProjectedGrids", block.ProjectedGrids));
            if (block.ProjectionOffset != null) elem.Add(new XElement("ProjectionOffset", block.ProjectionOffset));
            if (block.ProjectionRotation != null) elem.Add(new XElement("ProjectionRotation", block.ProjectionRotation));
            if (block.KeepProjection != null) elem.Add(new XElement("KeepProjection", block.KeepProjection));
            if (block.ShowOnlyBuildable != null) elem.Add(new XElement("ShowOnlyBuildable", block.ShowOnlyBuildable));
            if (block.InstantBuildingEnabled != null) elem.Add(new XElement("InstantBuildingEnabled", block.InstantBuildingEnabled));
            if (block.MaxNumberOfProjections != null) elem.Add(new XElement("MaxNumberOfProjections", block.MaxNumberOfProjections));
            return elem;
        }

        // Add safe parse helpers
        private static float? ParseFloat(XElement? elem)
        {
            if (elem == null) return null;
            var s = (string?)elem;
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (float.TryParse(s, out var v)) return v;
            return null;
        }
        private static int? ParseInt(XElement? elem)
        {
            if (elem == null) return null;
            var s = (string?)elem;
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (int.TryParse(s, out var v)) return v;
            return null;
        }
        private static bool? ParseBool(XElement? elem)
        {
            if (elem == null) return null;
            var s = (string?)elem;
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (bool.TryParse(s, out var v)) return v;
            return null;
        }
        // Add ParseLong helper
        private static long? ParseLong(XElement? elem)
        {
            if (elem == null) return null;
            var s = (string?)elem;
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (long.TryParse(s, out var v)) return v;
            return null;
        }
    }

    /// <summary>
    /// Represents a Space Engineers blueprint folder, including blueprints and thumbnail path.
    /// </summary>
    public class BlueprintFile
    {
        /// <summary>
        /// The list of ship blueprints contained in the file.
        /// </summary>
        public List<ShipBlueprint> ShipBlueprints { get; set; } = new();
        /// <summary>
        /// The path to the thumbnail image (thumb.png), if present.
        /// </summary>
        public string? ThumbPath { get; set; }
    }

    /// <summary>
    /// Represents a single ship blueprint definition.
    /// </summary>
    public class ShipBlueprint
    {
        /// <summary>
        /// The blueprint's unique identifier.
        /// </summary>
        public BlueprintId? Id { get; set; }
        /// <summary>
        /// The display name of the blueprint.
        /// </summary>
        public string? DisplayName { get; set; }
        /// <summary>
        /// The list of DLCs required by this blueprint.
        /// </summary>
        public List<string> DLCs { get; set; } = new();
        /// <summary>
        /// The list of cube grids in this blueprint.
        /// </summary>
        public List<CubeGrid> CubeGrids { get; set; } = new();
    }

    /// <summary>
    /// Represents the unique identifier for a blueprint.
    /// </summary>
    public class BlueprintId
    {
        /// <summary>
        /// The type of the blueprint (usually MyObjectBuilder_ShipBlueprintDefinition).
        /// </summary>
        public string? Type { get; set; }
        /// <summary>
        /// The subtype of the blueprint (usually the blueprint name).
        /// </summary>
        public string? Subtype { get; set; }
    }

    /// <summary>
    /// Represents a cube grid (ship/station) in a blueprint.
    /// </summary>
    public class CubeGrid
    {
        /// <summary>
        /// The subtype name of the grid.
        /// </summary>
        public string? SubtypeName { get; set; }
        /// <summary>
        /// The unique entity ID of the grid.
        /// </summary>
        public string? EntityId { get; set; }
        /// <summary>
        /// The persistent flags for the grid.
        /// </summary>
        public string? PersistentFlags { get; set; }
        /// <summary>
        /// The position and orientation of the grid.
        /// </summary>
        public PositionAndOrientation? PositionAndOrientation { get; set; }
        /// <summary>
        /// The local position and orientation (if present).
        /// </summary>
        public string? LocalPositionAndOrientation { get; set; }
        /// <summary>
        /// The grid size (Small or Large).
        /// </summary>
        public string? GridSizeEnum { get; set; }
        /// <summary>
        /// The list of blocks in the grid.
        /// </summary>
        public List<CubeBlock> CubeBlocks { get; set; } = new();
    }

    /// <summary>
    /// Represents the position and orientation of a grid.
    /// </summary>
    public class PositionAndOrientation
    {
        /// <summary>
        /// The position vector.
        /// </summary>
        public Vector3? Position { get; set; }
        /// <summary>
        /// The forward direction vector.
        /// </summary>
        public Vector3? Forward { get; set; }
        /// <summary>
        /// The up direction vector.
        /// </summary>
        public Vector3? Up { get; set; }
        /// <summary>
        /// The orientation quaternion.
        /// </summary>
        public Quaternion? Orientation { get; set; }
    }

    /// <summary>
    /// Represents a 3D vector.
    /// </summary>
    public class Vector3
    {
        /// <summary>
        /// The X component.
        /// </summary>
        public float X { get; set; }
        /// <summary>
        /// The Y component.
        /// </summary>
        public float Y { get; set; }
        /// <summary>
        /// The Z component.
        /// </summary>
        public float Z { get; set; }
    }

    /// <summary>
    /// Represents a quaternion (for orientation).
    /// </summary>
    public class Quaternion
    {
        /// <summary>
        /// The X component.
        /// </summary>
        public float X { get; set; }
        /// <summary>
        /// The Y component.
        /// </summary>
        public float Y { get; set; }
        /// <summary>
        /// The Z component.
        /// </summary>
        public float Z { get; set; }
        /// <summary>
        /// The W component.
        /// </summary>
        public float W { get; set; }
    }

    /// <summary>
    /// Represents a block in a cube grid, with all known and unmapped fields.
    /// </summary>
    public class CubeBlock
    {
        /// <summary>
        /// The XML xsi:type attribute (block type).
        /// </summary>
        public string? XsiType { get; set; }
        /// <summary>
        /// The block's subtype name.
        /// </summary>
        public string? SubtypeName { get; set; }
        /// <summary>
        /// The block's unique entity ID.
        /// </summary>
        public string? EntityId { get; set; }
        /// <summary>
        /// The block's minimum position in the grid.
        /// </summary>
        public Vector3Int? Min { get; set; }
        /// <summary>
        /// The block's orientation.
        /// </summary>
        public BlockOrientation? BlockOrientation { get; set; }
        /// <summary>
        /// The block's color mask (HSV).
        /// </summary>
        public Vector3? ColorMaskHSV { get; set; }
        /// <summary>
        /// The block's skin subtype ID.
        /// </summary>
        public string? SkinSubtypeId { get; set; }
        /// <summary>
        /// The Steam ID of the builder.
        /// </summary>
        public string? BuiltBy { get; set; }
        /// <summary>
        /// The Steam ID of the owner (if present).
        /// </summary>
        public string? Owner { get; set; }
        /// <summary>
        /// The block's share mode (None, Faction, All).
        /// </summary>
        public string? ShareMode { get; set; }
        /// <summary>
        /// The block's component container (raw XML for now).
        /// </summary>
        public ComponentContainer? ComponentContainer { get; set; }
        /// <summary>
        /// The block's custom name (if present).
        /// </summary>
        public string? CustomName { get; set; }
        /// <summary>
        /// Whether the block is shown on HUD.
        /// </summary>
        public bool ShowOnHUD { get; set; }
        /// <summary>
        /// Whether the block is shown in terminal.
        /// </summary>
        public bool ShowInTerminal { get; set; }
        /// <summary>
        /// Whether the block is shown in toolbar config.
        /// </summary>
        public bool ShowInToolbarConfig { get; set; }
        /// <summary>
        /// Whether the block is shown in inventory.
        /// </summary>
        public bool ShowInInventory { get; set; }
        /// <summary>
        /// The block's number in the grid (if present).
        /// </summary>
        public int? NumberInGrid { get; set; }
        /// <summary>
        /// Whether the block is enabled.
        /// </summary>
        public bool Enabled { get; set; }
        /// <summary>Indicates if the block is locked (Landing Gear, etc).</summary>
        public string? IsLocked { get; set; }
        /// <summary>The brake force for the block (Landing Gear, etc).</summary>
        public string? BrakeForce { get; set; }
        /// <summary>Indicates if auto-lock is enabled (Landing Gear, etc).</summary>
        public string? AutoLock { get; set; }
        /// <summary>Indicates if this is the first lock attempt (Landing Gear, etc).</summary>
        public string? FirstLockAttempt { get; set; }
        /// <summary>The sound played when locking (Landing Gear, etc).</summary>
        public string? LockSound { get; set; }
        /// <summary>The sound played when unlocking (Landing Gear, etc).</summary>
        public string? UnlockSound { get; set; }
        /// <summary>The sound played when lock fails (Landing Gear, etc).</summary>
        public string? FailedAttachSound { get; set; }
        /// <summary>The entity ID attached to (Landing Gear, etc).</summary>
        public string? AttachedEntityId { get; set; }
        /// <summary>Master-to-slave relationship (connectors, etc).</summary>
        public string? MasterToSlave { get; set; }
        /// <summary>Gear pivot position (Landing Gear, etc).</summary>
        public string? GearPivotPosition { get; set; }
        /// <summary>Other pivot (Landing Gear, etc).</summary>
        public string? OtherPivot { get; set; }
        /// <summary>The lock mode (Landing Gear, etc).</summary>
        public string? LockMode { get; set; }
        /// <summary>Indicates if parking is enabled (Landing Gear, etc).</summary>
        public string? IsParkingEnabled { get; set; }
        /// <summary>The current progress (Survival Kit, etc).</summary>
        public string? CurrentProgress { get; set; }
        /// <summary>Indicates if disassemble is enabled (Survival Kit, etc).</summary>
        public string? DisassembleEnabled { get; set; }
        /// <summary>Indicates if repeat assemble is enabled (Survival Kit, etc).</summary>
        public string? RepeatAssembleEnabled { get; set; }
        /// <summary>Indicates if repeat disassemble is enabled (Survival Kit, etc).</summary>
        public string? RepeatDisassembleEnabled { get; set; }
        /// <summary>Indicates if slave is enabled (Survival Kit, etc).</summary>
        public string? SlaveEnabled { get; set; }
        /// <summary>The spawn name (Survival Kit, etc).</summary>
        public string? SpawnName { get; set; }
        /// <summary>Indicates if stockpiling is enabled (Oxygen Tank, etc).</summary>
        public string? IsStockpiling { get; set; }
        /// <summary>The filled ratio (Oxygen Tank, etc).</summary>
        public string? FilledRatio { get; set; }
        /// <summary>Indicates if auto-refill is enabled (Oxygen Tank, etc).</summary>
        public string? AutoRefill { get; set; }
        /// <summary>The current stored power (Battery, etc).</summary>
        public string? CurrentStoredPower { get; set; }
        /// <summary>Indicates if the producer is enabled (Battery, etc).</summary>
        public string? ProducerEnabled { get; set; }
        /// <summary>The max stored power (Battery, etc).</summary>
        public string? MaxStoredPower { get; set; }
        /// <summary>Indicates if semiauto is enabled (Battery, etc).</summary>
        public string? SemiautoEnabled { get; set; }
        /// <summary>Indicates if only discharge is enabled (Battery, etc).</summary>
        public string? OnlyDischargeEnabled { get; set; }
        /// <summary>The charge mode (Battery, etc).</summary>
        public string? ChargeMode { get; set; }
        /// <summary>The broadcast radius (Antenna, etc).</summary>
        public string? BroadcastRadius { get; set; }
        /// <summary>Indicates if the ship name is shown (Antenna, etc).</summary>
        public string? ShowShipName { get; set; }
        /// <summary>Indicates if broadcasting is enabled (Antenna, etc).</summary>
        public string? EnableBroadcasting { get; set; }
        /// <summary>The attached programmable block (Antenna, etc).</summary>
        public string? AttachedPB { get; set; }
        /// <summary>Indicates if allied signals are ignored (Antenna, etc).</summary>
        public string? IgnoreAllied { get; set; }
        /// <summary>Indicates if other signals are ignored (Antenna, etc).</summary>
        public string? IgnoreOther { get; set; }
        /// <summary>The HUD text (Antenna, etc).</summary>
        public string? HudText { get; set; }
        /// <summary>Indicates if the block is shooting (Weapons, etc).</summary>
        public string? IsShooting { get; set; }
        /// <summary>Indicates if shooting from terminal (Weapons, etc).</summary>
        public string? IsShootingFromTerminal { get; set; }
        /// <summary>Indicates if the block is a large turret (Weapons, etc).</summary>
        public string? IsLargeTurret { get; set; }
        /// <summary>The minimum field of view (Weapons, etc).</summary>
        public string? MinFov { get; set; }
        /// <summary>The maximum field of view (Weapons, etc).</summary>
        public string? MaxFov { get; set; }
        /// <summary>Indicates if the conveyor system is used (Weapons, etc).</summary>
        public string? UseConveyorSystem { get; set; }
        /// <summary>The gun base (Weapons, etc).</summary>
        public string? GunBase { get; set; }
        /// <summary>The toolbar configuration (Weapons, etc).</summary>
        public string? Toolbar { get; set; }
        /// <summary>The selected gun ID (Weapons, etc).</summary>
        public string? SelectedGunId { get; set; }
        /// <summary>The build toolbar (Weapons, etc).</summary>
        public string? BuildToolbar { get; set; }
        /// <summary>The toolbar when locked (Weapons, etc).</summary>
        public string? OnLockedToolbar { get; set; }
        /// <summary>Indicates if target locking is enabled (Weapons, etc).</summary>
        public string? IsTargetLockingEnabled { get; set; }
        /// <summary>The previous controlled entity ID (Remote Control, etc).</summary>
        public string? PreviousControlledEntityId { get; set; }
        /// <summary>Indicates if autopilot is enabled (Remote Control, etc).</summary>
        public string? AutoPilotEnabled { get; set; }
        /// <summary>The flight mode (Remote Control, etc).</summary>
        public string? FlightMode { get; set; }
        /// <summary>The bound camera (Remote Control, etc).</summary>
        public string? BindedCamera { get; set; }
        /// <summary>The current waypoint index (Remote Control, etc).</summary>
        public string? CurrentWaypointIndex { get; set; }
        /// <summary>The waypoints (Remote Control, etc).</summary>
        public string? Waypoints { get; set; }
        /// <summary>The direction (Remote Control, etc).</summary>
        public string? Direction { get; set; }
        /// <summary>Indicates if docking mode is enabled (Remote Control, etc).</summary>
        public string? DockingModeEnabled { get; set; }
        /// <summary>Indicates if collision avoidance is enabled (Remote Control, etc).</summary>
        public string? CollisionAvoidance { get; set; }
        /// <summary>The coordinates (Remote Control, etc).</summary>
        public string? Coords { get; set; }
        /// <summary>The names (Remote Control, etc).</summary>
        public string? Names { get; set; }
        /// <summary>The waypoint threshold distance (Remote Control, etc).</summary>
        public string? WaypointThresholdDistance { get; set; }
        /// <summary>Indicates if this is the main remote control (Remote Control, etc).</summary>
        public string? IsMainRemoteControl { get; set; }
        /// <summary>Indicates if waiting for free way (Remote Control, etc).</summary>
        public string? WaitForFreeWay { get; set; }
        /// <summary>Indicates if the save is updated (Remote Control, etc).</summary>
        public string? IsUpdatedSave { get; set; }
        /// <summary>The integrity percent (Cockpit, etc).</summary>
        public string? IntegrityPercent { get; set; }
        /// <summary>The build percent (Cockpit, etc).</summary>
        public string? BuildPercent { get; set; }
        /// <summary>The pilot's relative world (Cockpit, etc).</summary>
        public string? PilotRelativeWorld { get; set; }
        /// <summary>The pilot's gun definition (Cockpit, etc).</summary>
        public string? PilotGunDefinition { get; set; }
        /// <summary>Indicates if in first person view (Cockpit, etc).</summary>
        public string? IsInFirstPersonView { get; set; }
        /// <summary>The oxygen level (Cockpit, etc).</summary>
        public string? OxygenLevel { get; set; }
        /// <summary>Indicates if the pilot's jetpack is enabled (Cockpit, etc).</summary>
        public string? PilotJetpackEnabled { get; set; }
        /// <summary>The target data (Cockpit, etc).</summary>
        public string? TargetData { get; set; }
        /// <summary>The sit animation (Cockpit, etc).</summary>
        public string? SitAnimation { get; set; }
        /// <summary>The deformation ratio (Connector, etc).</summary>
        public string? DeformationRatio { get; set; }
        /// <summary>The master-to-slave transform (Connector, etc).</summary>
        public string? MasterToSlaveTransform { get; set; }
        /// <summary>The master-to-slave grid (Connector, etc).</summary>
        public string? MasterToSlaveGrid { get; set; }
        /// <summary>Indicates if this is the master (Connector, etc).</summary>
        public string? IsMaster { get; set; }
        /// <summary>Indicates if trading is enabled (Connector, etc).</summary>
        public string? TradingEnabled { get; set; }
        /// <summary>The auto unlock time (Connector, etc).</summary>
        public string? AutoUnlockTime { get; set; }
        /// <summary>The time of connection (Connector, etc).</summary>
        public string? TimeOfConnection { get; set; }
        /// <summary>Indicates if power transfer override is enabled (Connector, etc).</summary>
        public string? IsPowerTransferOverrideEnabled { get; set; }
        /// <summary>Indicates if approaching (Connector, etc).</summary>
        public string? IsApproaching { get; set; }
        /// <summary>Indicates if connecting (Connector, etc).</summary>
        public string? IsConnecting { get; set; }
        /// <summary>The radius (Reflector Light, etc).</summary>
        public string? Radius { get; set; }
        /// <summary>The reflector radius (Reflector Light, etc).</summary>
        public string? ReflectorRadius { get; set; }
        /// <summary>The falloff (Reflector Light, etc).</summary>
        public string? Falloff { get; set; }
        /// <summary>The intensity (Reflector Light, etc).</summary>
        public string? Intensity { get; set; }
        /// <summary>The blink interval in seconds (Reflector Light, etc).</summary>
        public string? BlinkIntervalSeconds { get; set; }
        /// <summary>The blink length (Reflector Light, etc).</summary>
        public string? BlinkLenght { get; set; }
        /// <summary>The blink offset (Reflector Light, etc).</summary>
        public string? BlinkOffset { get; set; }
        /// <summary>The offset (Reflector Light, etc).</summary>
        public string? Offset { get; set; }
        /// <summary>The rotation speed (Reflector Light, etc).</summary>
        public string? RotationSpeed { get; set; }
        /// <summary>The capacity (Reactor, etc).</summary>
        public string? Capacity { get; set; }
        /// <summary>Indicates if single weapon mode is used (Weapons, etc).</summary>
        public string? UseSingleWeaponMode { get; set; }
        /// <summary>Indicates if the block is active (Camera, etc).</summary>
        public string? IsActive { get; set; }
        /// <summary>The field of view (Camera, etc).</summary>
        public string? FoV { get; set; }
        /// <summary>
        /// Any additional unmapped fields found in the XML.
        /// </summary>
        public Dictionary<string, string> OtherFields { get; set; } = new();
        // Add all previously unmapped fields below
        public string? ConstructionStockpile { get; set; }
        public float? ColorRed { get; set; }
        public float? ColorGreen { get; set; }
        public float? ColorBlue { get; set; }
        public bool? IsDepressurizing { get; set; }
        public float? Range { get; set; }
        public int? RemainingAmmo { get; set; }
        public int? Target { get; set; }
        public bool? IsPotentialTarget { get; set; }
        public float? Rotation { get; set; }
        public float? Elevation { get; set; }
        public bool? EnableIdleRotation { get; set; }
        public bool? PreviousIdleRotationState { get; set; }
        public bool? TargetCharacters { get; set; }
        public string? TargetingGroup { get; set; }
        public string? Flags { get; set; }
        public bool? HorizonIndicatorEnabled { get; set; }
        public string? SteamId { get; set; }
        public string? SerialId { get; set; }
        public string? SteamUserId { get; set; }
        public string? IdleSound { get; set; }
        public string? ProgressSound { get; set; }
        public bool? TakeOwnership { get; set; }
        public bool? SetFaction { get; set; }
        public string? WardrobeUserId { get; set; }
        public float? Threshold { get; set; }
        public bool? ANDGate { get; set; }
        public int? SelectedEvent { get; set; }
        public string? SelectedBlocks { get; set; }
        public bool? ConditionInvert { get; set; }
        public int? Delay { get; set; }
        public int? CurrentTime { get; set; }
        public bool? IsCountingDown { get; set; }
        public bool? Silent { get; set; }
        public int? DetectionRadius { get; set; }
        public bool? BroadcastUsingAntennas { get; set; }
        public bool? IsSurvivalModeForced { get; set; }
        public float? StoredPower { get; set; }
        public string? JumpTarget { get; set; }
        public float? JumpRatio { get; set; }
        public bool? Recharging { get; set; }
        public string? SelectedBeaconName { get; set; }
        public int? SelectedBeaconId { get; set; }
        public string? TopBlockId { get; set; }
        public bool? ShareInertiaTensor { get; set; }
        public int? SafetyDetach { get; set; }
        public string? RotorEntityId { get; set; }
        public string? WeldedEntityId { get; set; }
        public float? TargetVelocity { get; set; }
        public float? MinAngle { get; set; }
        public float? MaxAngle { get; set; }
        public float? CurrentAngle { get; set; }
        public bool? LimitsActive { get; set; }
        public bool? RotorLock { get; set; }
        public float? Torque { get; set; }
        public float? BrakingTorque { get; set; }
        public bool? AnyoneCanUse { get; set; }
        public string? CustomButtonNames { get; set; }
        public float? Opening { get; set; }
        public string? OpenSound { get; set; }
        public string? CloseSound { get; set; }
        public string? TargetPriority { get; set; }
        public int? UpdateTargetInterval { get; set; }
        public int? SelectedAttackPattern { get; set; }
        public bool? CanTargetCharacters { get; set; }
        public float? GravityAcceleration { get; set; }
        public string? FieldSize { get; set; }
        public string? Description { get; set; }
        public string? Title { get; set; }
        public string? AccessFlag { get; set; }
        public int? ChangeInterval { get; set; }
        public string? Font { get; set; }
        public float? FontSize { get; set; }
        public string? PublicDescription { get; set; }
        public string? PublicTitle { get; set; }
        public string? ShowText { get; set; }
        public string? FontColor { get; set; }
        public string? BackgroundColor { get; set; }
        public int? CurrentShownTexture { get; set; }
        public int? TextPadding { get; set; }
        public int? Version { get; set; }
        public string? ScriptBackgroundColor { get; set; }
        public string? ScriptForegroundColor { get; set; }
        public int? Sprites { get; set; }
        public string? SelectedRotationIndex { get; set; }
        public bool? TargetLocking { get; set; }
        public float? Volume { get; set; }
        public string? CueName { get; set; }
        public float? LoopPeriod { get; set; }
        public bool? IsPlaying { get; set; }
        public float? ElapsedSoundSeconds { get; set; }
        public bool? IsLoopableSound { get; set; }
        public bool? IsMainCockpit { get; set; }
        public int? NextItemId { get; set; }
        public string? SelectedSounds { get; set; }
        public bool? IsJukeboxPlaying { get; set; }
        public int? CurrentSound { get; set; }
        public string? TargetAngularVelocity { get; set; }
        public bool? State { get; set; }
        public string? FieldMin { get; set; }
        public string? FieldMax { get; set; }
        public bool? PlaySound { get; set; }
        public bool? DetectPlayers { get; set; }
        public bool? DetectFloatingObjects { get; set; }
        public bool? DetectSmallShips { get; set; }
        public bool? DetectLargeShips { get; set; }
        public bool? DetectStations { get; set; }
        public bool? DetectSubgrids { get; set; }
        public bool? DetectAsteroids { get; set; }
        public bool? DetectOwner { get; set; }
        public bool? DetectFriendly { get; set; }
        public bool? DetectNeutral { get; set; }
        public bool? DetectEnemy { get; set; }
        public string? SafeZoneId { get; set; }
        public string? ConnectedEntityId { get; set; }
        public float? Strength { get; set; }
        public float? GyroPower { get; set; }
        public string? ProjectedGrids { get; set; }
        public string? ProjectionOffset { get; set; }
        public string? ProjectionRotation { get; set; }
        public bool? KeepProjection { get; set; }
        public bool? ShowOnlyBuildable { get; set; }
        public bool? InstantBuildingEnabled { get; set; }
        public int? MaxNumberOfProjections { get; set; }
        public int? MaxNumberOfBlocks { get; set; }
        public int? ProjectionsRemaining { get; set; }
        public bool? GetOwnershipFromProjector { get; set; }
        public float? Scale { get; set; }
        public string? FleeTrigger { get; set; }
        public long? SelectedBeaconEntityId { get; set; }
        public string? SelectedGpsHash { get; set; }
        public int? WaypointZoneSize { get; set; }
        public bool? LockTarget { get; set; }
        public string? CustomFleeCoordinates { get; set; }
        public bool? UseCustomFleeCoordinates { get; set; }
        public string? SelectedGpsHashNew { get; set; }
        public float? FleeDistance { get; set; }
        public bool? EvasiveManeuvers { get; set; }
        public float? EvasiveManeuverAngle { get; set; }
        public float? EvasiveManeuverIntervalRange { get; set; }
        public string? FleeMode { get; set; }
        public string? LastKnownEnemyPosition { get; set; }
        public int? FleeMinHeightOnPlanets { get; set; }
        public bool? PrevToolbarState { get; set; }
        public long? ParentEntityId { get; set; }
        public bool? YieldLastComponent { get; set; }
    }

    /// <summary>
    /// Represents a 3D integer vector (for block positions).
    /// </summary>
    public class Vector3Int
    {
        /// <summary>
        /// The X component.
        /// </summary>
        public int X { get; set; }
        /// <summary>
        /// The Y component.
        /// </summary>
        public int Y { get; set; }
        /// <summary>
        /// The Z component.
        /// </summary>
        public int Z { get; set; }
    }

    /// <summary>
    /// Represents the orientation of a block.
    /// </summary>
    public class BlockOrientation
    {
        /// <summary>
        /// The forward direction.
        /// </summary>
        public string? Forward { get; set; }
        /// <summary>
        /// The up direction.
        /// </summary>
        public string? Up { get; set; }
    }

    /// <summary>
    /// Represents a container for block components.
    /// </summary>
    public class ComponentContainer
    {
        /// <summary>
        /// The list of component data (raw XML for now).
        /// </summary>
        public List<ComponentData> Components { get; set; } = new();
    }

    /// <summary>
    /// Represents a single component's data in a block's component container.
    /// </summary>
    public class ComponentData
    {
        /// <summary>
        /// The type ID of the component.
        /// </summary>
        public string? TypeId { get; set; }
        /// <summary>
        /// The raw XML of the component.
        /// </summary>
        public string? Component { get; set; } // Raw XML for now
    }
}
