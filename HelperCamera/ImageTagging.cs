using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace Smartphone
{
    public partial class ModEntry
    {
        private const int MaxCropTagsPerImage = 3;
        private const int MaxFruitTreeTagsPerImage = 3;
        private const int MaxAnimalTagsPerImage = 3;
        private const int MaxNpcTagsPerImage = 5;
        private const int MaxForageTagsPerImage = 3;
        private const int MaxFurnitureTagsPerImage = 5;
        private const int MaxDisplayedItemTagsPerImage = 5;
        private const int MaxPetNamesPerTypeTag = 3;
        // private static string PlayerTag => $"#Player {Game1.player?.displayName}".Trim();
        private const int BuildingFrontTilesLeftRight = 2;
        private const int BuildingFrontTilesUp = 2;
        private const int BuildingFrontTilesDown = 1;
        private static string LastTriggeredUnlimitedEventTag = string.Empty;
        private static int LastTriggeredUnlimitedEventYear = -1;
        private static string LastTriggeredUnlimitedEventSeason = string.Empty;
        private static int LastTriggeredUnlimitedEventDay = -1;
        private static readonly Point[] AreaSampleOffsets =
        {
            new Point(0, 0),
            new Point(-1, -1),
            new Point(0, -1),
            new Point(1, -1),
            new Point(-1, 0),
            new Point(1, 0),
            new Point(-1, 1),
            new Point(0, 1),
            new Point(1, 1)
        };

        private static Dictionary<string, Dictionary<string, AreaData>> IndoorAreasByLocation = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, Dictionary<string, AreaData>> OutdoorAreasByLocation = new(StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, Smartphone.Data.ImageMetadata> ImageMetadataStore = new(StringComparer.OrdinalIgnoreCase);
        public static bool IsImageMetadataDirty { get; set; } = false;

        public static Dictionary<string, string> ImageTags
        {
            get
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in ImageMetadataStore)
                {
                    dict[kvp.Key] = kvp.Value.Tag;
                }
                return dict;
            }
        }
        public static bool IsImageTagsDirty
        {
            get => IsImageMetadataDirty;
            set => IsImageMetadataDirty = value;
        }

        private static string ImageMetadataDataPath => $"./userdata/{GetCurrentSaveFolderName()}/image_metadata.json";

        private static string GetCurrentSaveFolderName()
        {
            return ModEntry.GetActiveSaveFolderName();
        }

        private static IEnumerable<string> GetImageFolderPaths()
        {
            string saveRoot = Path.Combine(SHelper.DirectoryPath, "userdata", GetCurrentSaveFolderName());

            // Scan active image folders used by this mod.
            yield return Path.Combine(saveRoot, "photo_player");
            yield return Path.Combine(saveRoot, "photo_imported");
        }

        public static void LoadImageTags()
        {
            var loaded = SHelper.Data.ReadJsonFile<Dictionary<string, Smartphone.Data.ImageMetadata>>(ImageMetadataDataPath)
                         ?? new Dictionary<string, Smartphone.Data.ImageMetadata>();
            ImageMetadataStore = new Dictionary<string, Smartphone.Data.ImageMetadata>(loaded, StringComparer.OrdinalIgnoreCase);

            CleanupMissingImageTagEntries();
        }

        public static void SaveImageTags()
        {
            SHelper.Data.WriteJsonFile(ImageMetadataDataPath, ImageMetadataStore);
            IsImageMetadataDirty = false;
        }

        public static void SetImageTags(string imageName, IEnumerable<string> tags, string location = "", string timeString = "")
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return;

            var cleanedTags = (tags ?? Enumerable.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleanedTags.Count == 0)
            {
                RemoveImageTags(imageName);
                return;
            }

            ImageMetadataStore[imageName] = new Smartphone.Data.ImageMetadata
            {
                Location = location,
                TimeString = timeString,
                Tag = string.Join(";", cleanedTags)
            };
            SaveImageTags();
        }

        public static void SetImageTags(string imageName, IEnumerable<string> tags)
        {
            SetImageTags(imageName, tags, "", "");
        }

        public static void RemoveImageTags(string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return;

            if (ImageMetadataStore.Remove(imageName))
                SaveImageTags();
        }

        private static void CleanupMissingImageTagEntries()
        {
            if (ImageMetadataStore.Count == 0)
                return;

            var existingImageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folderPath in GetImageFolderPaths())
            {
                if (!Directory.Exists(folderPath))
                    continue;

                var imagePaths = Directory.GetFiles(folderPath).Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase));
                foreach (string imagePath in imagePaths)
                {
                    string? imageName = Path.GetFileName(imagePath);
                    if (!string.IsNullOrWhiteSpace(imageName))
                        existingImageNames.Add(imageName);
                }
            }

            bool hasChanges = false;

            foreach (string imageName in ImageMetadataStore.Keys.ToList())
            {
                if (!existingImageNames.Contains(imageName))
                {
                    ImageMetadataStore.Remove(imageName);
                    hasChanges = true;
                }
            }

            if (hasChanges)
                IsImageMetadataDirty = true;
        }

        private static IEnumerable<string> BuildImageTags(Rectangle captureBounds, NPC? npc = null)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddLocationTag(tags, npc?.currentLocation, npc: npc);
            bool isAtEvent = AddCurrentEventTags(tags);
            if (isAtEvent)
                return tags;

            AddBuildingFrontTags(tags, captureBounds);
            AddAreaTags(tags, captureBounds);
            AddWeatherTags(tags);
            AddCharacterTags(tags, captureBounds);
            AddHeldItemTag(tags);
            AddCropAndFruitTreeTags(tags, captureBounds);
            AddForageTags(tags, captureBounds);
            AddFurnitureAndDisplayedTags(tags, captureBounds);
            AddPetTags(tags, captureBounds);
            AddFarmAnimalTags(tags, captureBounds);

            return tags;
        }

        private static void AddFurnitureAndDisplayedTags(HashSet<string> tags, Rectangle captureBounds)
        {
            GameLocation? currentLocation = Game1.currentLocation;
            if (currentLocation == null)
                return;

            var furnitureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var displayedItemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach ((Vector2 tile, Furniture furniture) in GetLocationFurniture(currentLocation))
            {
                if (!IsFurnitureInsideCapture(tile, furniture, captureBounds))
                    continue;

                string furnitureName = GetFurnitureDisplayName(furniture);
                if (!string.IsNullOrWhiteSpace(furnitureName))
                    furnitureNames.Add(furnitureName);

                if (TryGetTableDisplayedItemName(furniture, out string displayedItemName))
                    displayedItemNames.Add(displayedItemName);
            }

            List<string> selectedFurnitureNames = SelectRandomUniqueNames(furnitureNames, MaxFurnitureTagsPerImage);
            if (selectedFurnitureNames.Count > 0)
                tags.Add(SHelper.Translation.Get("camera.tag.furniture", new { list = string.Join(", ", selectedFurnitureNames) }));

            List<string> selectedDisplayedItemNames = SelectRandomUniqueNames(displayedItemNames, MaxDisplayedItemTagsPerImage);
            if (selectedDisplayedItemNames.Count > 0)
                tags.Add(SHelper.Translation.Get("camera.tag.displaying", new { list = string.Join(", ", selectedDisplayedItemNames) }));
        }

        private static IEnumerable<(Vector2 Tile, Furniture Furniture)> GetLocationFurniture(GameLocation location)
        {
            var furnitureByTile = new Dictionary<Vector2, Furniture>();
            AppendFurnitureFromUnknownCollection(GetMemberValue(location, "furniture", "Furniture"), furnitureByTile);

            return furnitureByTile.Select(pair => (pair.Key, pair.Value));
        }

        private static void AppendFurnitureFromUnknownCollection(object? collection, Dictionary<Vector2, Furniture> furnitureByTile)
        {
            if (collection == null)
                return;

            if (collection is IEnumerable enumerable)
                AppendFurnitureFromEnumerable(enumerable, furnitureByTile);

            if (GetMemberValue(collection, "Pairs", "pairs") is IEnumerable pairs)
                AppendFurnitureFromEnumerable(pairs, furnitureByTile);

            if (GetMemberValue(collection, "Entries", "entries") is IEnumerable entries)
                AppendFurnitureFromEnumerable(entries, furnitureByTile);

            if (GetMemberValue(collection, "Values") is IEnumerable values)
                AppendFurnitureFromEnumerable(values, furnitureByTile);
        }

        private static void AppendFurnitureFromEnumerable(IEnumerable entries, Dictionary<Vector2, Furniture> furnitureByTile)
        {
            foreach (object? entry in entries)
            {
                if (TryExtractFurnitureEntry(entry, out Vector2 tile, out Furniture furniture))
                    furnitureByTile[tile] = furniture;
            }
        }

        private static bool TryExtractFurnitureEntry(object? entry, out Vector2 tile, out Furniture furniture)
        {
            tile = Vector2.Zero;
            furniture = null!;

            if (entry == null)
                return false;

            if (entry is Furniture directFurniture)
            {
                if (!TryExtractFurnitureTile(directFurniture, out tile))
                    return false;

                furniture = directFurniture;
                return true;
            }

            object? key;
            object? value;

            if (entry is DictionaryEntry dictionaryEntry)
            {
                key = dictionaryEntry.Key;
                value = dictionaryEntry.Value;
            }
            else
            {
                Type entryType = entry.GetType();
                PropertyInfo? keyProperty = entryType.GetProperty("Key");
                PropertyInfo? valueProperty = entryType.GetProperty("Value");
                if (keyProperty == null || valueProperty == null)
                    return false;

                key = keyProperty.GetValue(entry);
                value = valueProperty.GetValue(entry);
            }

            Furniture? extractedFurniture = value as Furniture;
            if (extractedFurniture == null && value != null)
                extractedFurniture = GetMemberValue(value, "Value") as Furniture;

            if (extractedFurniture == null)
                return false;

            if (!TryExtractVector2FromUnknownValue(key, out tile)
                && !TryExtractFurnitureTile(extractedFurniture, out tile))
                return false;

            furniture = extractedFurniture;
            return true;
        }

        private static bool TryExtractFurnitureTile(Furniture furniture, out Vector2 tile)
        {
            if (TryExtractVector2FromUnknownValue(GetMemberValue(furniture, "tileLocation", "TileLocation"), out tile))
                return true;

            tile = Vector2.Zero;
            return false;
        }

        private static bool IsFurnitureInsideCapture(Vector2 tile, Furniture furniture, Rectangle captureBounds)
        {
            int tilesWide = GetFurnitureTileSize(furniture, "tilesWide", "TilesWide", "getTilesWide", "GetTilesWide");
            int tilesHigh = GetFurnitureTileSize(furniture, "tilesHigh", "TilesHigh", "getTilesHigh", "GetTilesHigh");

            return IsTileAreaInsideCapture(tile, captureBounds, 0, 0, tilesWide * Game1.tileSize, tilesHigh * Game1.tileSize);
        }

        private static int GetFurnitureTileSize(Furniture furniture, string fieldName, string propertyName, string methodName, string alternateMethodName)
        {
            int tileSize = TryReadIntMember(furniture, fieldName, propertyName);
            if (tileSize > 0)
                return tileSize;

            if (TryInvokeIntMethod(furniture, methodName, out int methodTileSize) && methodTileSize > 0)
                return methodTileSize;

            if (TryInvokeIntMethod(furniture, alternateMethodName, out methodTileSize) && methodTileSize > 0)
                return methodTileSize;

            return 1;
        }

        private static string GetFurnitureDisplayName(Furniture furniture)
        {
            string displayName = furniture.DisplayName?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            string name = furniture.Name?.Trim() ?? string.Empty;
            return name;
        }

        private static bool TryGetTableDisplayedItemName(Furniture furniture, out string displayedItemName)
        {
            displayedItemName = string.Empty;
            if (!IsTableFurniture(furniture))
                return false;

            Item? heldItem = TryGetFurnitureHeldItem(furniture);
            if (heldItem == null)
                return false;

            displayedItemName = heldItem.DisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(displayedItemName))
                displayedItemName = heldItem.Name?.Trim() ?? string.Empty;

            return !string.IsNullOrWhiteSpace(displayedItemName);
        }

        private static Item? TryGetFurnitureHeldItem(Furniture furniture)
        {
            object? rawHeldItem = GetMemberValue(furniture, "heldObject", "HeldObject");
            if (rawHeldItem is Item directHeldItem)
                return directHeldItem;

            if (rawHeldItem == null)
                return null;

            return GetMemberValue(rawHeldItem, "Value") as Item;
        }

        private static bool IsTableFurniture(Furniture furniture)
        {
            if (TryInvokeBooleanMember(furniture, "isTable"))
                return true;

            if (TryInvokeBooleanMethod(furniture, "isTable", out bool isTableMethodResult) && isTableMethodResult)
                return true;

            string furnitureName = GetFurnitureDisplayName(furniture);
            return furnitureName.Contains("table", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> SelectRandomUniqueNames(IEnumerable<string> sourceNames, int maxCount)
        {
            var names = sourceNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0 || maxCount <= 0)
                return new List<string>();

            Random random = Game1.random ?? Random.Shared;
            for (int i = names.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (names[i], names[j]) = (names[j], names[i]);
            }

            if (names.Count > maxCount)
                names.RemoveRange(maxCount, names.Count - maxCount);

            return names;
        }

        private static void AddAreaTags(HashSet<string> tags, Rectangle captureBounds)
        {
            GameLocation? currentLocation = Game1.currentLocation;
            if (currentLocation == null)
                return;

            if (!TryGetAreaDefinitionsForCurrentLocation(currentLocation, out Dictionary<string, AreaData>? areaDefinitions)
                || areaDefinitions == null
                || areaDefinitions.Count == 0)
                return;

            Point centerTile = GetCenterTileFromCapture(captureBounds);
            Point[] sampleTiles = BuildAreaSampleTiles(centerTile);

            foreach (var areaEntry in areaDefinitions
                         .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                string areaName = areaEntry.Key?.Trim() ?? string.Empty;
                AreaData area = areaEntry.Value;
                if (string.IsNullOrWhiteSpace(areaName))
                    continue;

                if (areaName.StartsWith("SVE_", StringComparison.OrdinalIgnoreCase))
                {
                    if (SHelper?.ModRegistry?.Get("FlashShifter.StardewValleyExpandedCP") == null)
                        continue;
                }

                if (areaName.StartsWith("RSV_", StringComparison.OrdinalIgnoreCase))
                {
                    if (SHelper?.ModRegistry?.Get("Rafseazz.RSVCP") == null)
                        continue;
                }
                if (!DoAllTilesFitArea(sampleTiles, area))
                    continue;

                string description = area.description?.Trim() ?? string.Empty;
                string areaTag = string.IsNullOrWhiteSpace(description)
                    ? SHelper.Translation.Get("camera.tag.area", new { name = areaName })
                    : SHelper.Translation.Get("camera.tag.area_description", new { name = areaName, description = description });

                string frontOfTag = SHelper.Translation.Get("camera.tag.front_of", new { name = areaName });
                if (!tags.Contains(frontOfTag))
                    tags.Add(areaTag);
                return;
            }
        }

        private static bool TryGetAreaDefinitionsForCurrentLocation(GameLocation location, out Dictionary<string, AreaData>? areaDefinitions)
        {
            areaDefinitions = null;

            string locationName = TryReadStringMember(location, "NameOrUniqueName", "Name");
            if (string.IsNullOrWhiteSpace(locationName))
                return false;

            Dictionary<string, Dictionary<string, AreaData>>? customAreaOverrides = Instance?.areaTags;
            if (customAreaOverrides != null
                && customAreaOverrides.TryGetValue(locationName, out Dictionary<string, AreaData>? customAreas)
                && customAreas != null
                && customAreas.Count > 0)
            {
                areaDefinitions = customAreas;
                return true;
            }

            bool isOutdoors = TryReadBooleanMemberValue(location, out bool outdoorsValue, "isOutdoors", "IsOutdoors") && outdoorsValue;

            if (isOutdoors
                && OutdoorAreasByLocation.TryGetValue(locationName, out Dictionary<string, AreaData>? outdoorAreas)
                && outdoorAreas != null
                && outdoorAreas.Count > 0)
            {
                areaDefinitions = outdoorAreas;
                return true;
            }

            if (IndoorAreasByLocation.TryGetValue(locationName, out Dictionary<string, AreaData>? indoorAreas)
                && indoorAreas != null
                && indoorAreas.Count > 0)
            {
                areaDefinitions = indoorAreas;
                return true;
            }

            if (OutdoorAreasByLocation.TryGetValue(locationName, out outdoorAreas)
                && outdoorAreas != null
                && outdoorAreas.Count > 0)
            {
                areaDefinitions = outdoorAreas;
                return true;
            }

            return false;
        }

        private static Point GetCenterTileFromCapture(Rectangle captureBounds)
        {
            Rectangle normalizedCaptureBounds = GetNormalizedCaptureBounds(captureBounds);

            float centerScreenX = normalizedCaptureBounds.X + (normalizedCaptureBounds.Width / 2f);
            float centerScreenY = normalizedCaptureBounds.Y + (normalizedCaptureBounds.Height / 2f);

            float worldX = Game1.viewport.X + centerScreenX;
            float worldY = Game1.viewport.Y + centerScreenY;

            return new Point(
                (int)Math.Floor(worldX / Game1.tileSize),
                (int)Math.Floor(worldY / Game1.tileSize));
        }

        private static Point[] BuildAreaSampleTiles(Point centerTile)
        {
            var sampleTiles = new Point[AreaSampleOffsets.Length];
            for (int i = 0; i < AreaSampleOffsets.Length; i++)
            {
                Point offset = AreaSampleOffsets[i];
                sampleTiles[i] = new Point(centerTile.X + offset.X, centerTile.Y + offset.Y);
            }

            return sampleTiles;
        }

        private static bool DoAllTilesFitArea(IEnumerable<Point> tiles, AreaData area)
        {
            int minX = Math.Min(area.startX, area.endX);
            int maxX = Math.Max(area.startX, area.endX);
            int minY = Math.Min(area.startY, area.endY);
            int maxY = Math.Max(area.startY, area.endY);

            foreach (Point tile in tiles)
            {
                if (tile.X < minX || tile.X > maxX || tile.Y < minY || tile.Y > maxY)
                    return false;
            }

            return true;
        }

        private static void AddCharacterTags(HashSet<string> tags, Rectangle captureBounds)
        {
            if (Game1.player != null
                && IsPlayerInCurrentLocation()
                && IsCharacterInsideCapture(Game1.player, captureBounds))
            {
                tags.Add(SHelper.Translation.Get("camera.tag.player", new { name = Game1.player?.displayName }).ToString().Trim());
                // AddPlayerClothingTags(tags);
            }

            if (Game1.currentLocation?.characters != null)
            {
                int addedNpcCount = 0;

                foreach (NPC npc in Game1.currentLocation.characters.OfType<NPC>())
                {
                    if (string.IsNullOrWhiteSpace(npc.Name) || npc.IsInvisible)
                        continue;

                    // Pet/horse names are emitted by #cat/#dog/#horse pet tags.
                    if (npc is Pet || npc is Horse)
                        continue;

                    string npcTag = SHelper.Translation.Get("camera.tag.npc", new { name = npc.displayName });
                    if (IsCharacterInsideCapture(npc, captureBounds) && !tags.Contains(npcTag))
                    {
                        tags.Add(npcTag);
                        addedNpcCount++;
                    }

                    if (addedNpcCount >= MaxNpcTagsPerImage)
                        break;
                }
            }
        }

        private static bool IsPlayerInCurrentLocation()
        {
            if (Game1.player?.currentLocation == null || Game1.currentLocation == null)
                return false;

            if (ReferenceEquals(Game1.player.currentLocation, Game1.currentLocation))
                return true;

            string playerLocationName = TryReadStringMember(Game1.player.currentLocation, "NameOrUniqueName", "Name");
            string currentLocationName = TryReadStringMember(Game1.currentLocation, "NameOrUniqueName", "Name");
            return !string.IsNullOrWhiteSpace(playerLocationName)
                && string.Equals(playerLocationName, currentLocationName, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddPlayerClothingTags(HashSet<string> tags)
        {
            if (Game1.player == null)
                return;
            Game1.player.shirtItem?.Value?.Name.Trim();
            Game1.player.pantsItem?.Value?.Name.Trim();
            Game1.player.boots?.Value?.Name.Trim();
            Game1.player.hat?.Value?.Name.Trim();

            string baseOutfit = SHelper.Translation.Get("camera.tag.outfit");
            string outfitTag = baseOutfit;
            if (Game1.player.shirtItem?.Value != null && !string.IsNullOrWhiteSpace(Game1.player.shirtItem.Value.Name))
                outfitTag += SHelper.Translation.Get("camera.tag.outfit.shirt", new { name = Game1.player.shirtItem.Value.Name.Trim() });
            if (Game1.player.pantsItem?.Value != null && !string.IsNullOrWhiteSpace(Game1.player.pantsItem.Value.Name))
                outfitTag += SHelper.Translation.Get("camera.tag.outfit.pants", new { name = Game1.player.pantsItem.Value.Name.Trim() });
            if (Game1.player.boots?.Value != null && !string.IsNullOrWhiteSpace(Game1.player.boots.Value.Name))
                outfitTag += SHelper.Translation.Get("camera.tag.outfit.boots", new { name = Game1.player.boots.Value.Name.Trim() });
            if (Game1.player.hat?.Value != null && !string.IsNullOrWhiteSpace(Game1.player.hat.Value.Name))
                outfitTag += SHelper.Translation.Get("camera.tag.outfit.hat", new { name = Game1.player.hat.Value.Name.Trim() });

            if (outfitTag != baseOutfit)
                tags.Add(outfitTag);

        }

        private static void AddCropAndFruitTreeTags(HashSet<string> tags, Rectangle captureBounds)
        {
            GameLocation? currentLocation = Game1.currentLocation;
            if (currentLocation == null || currentLocation.terrainFeatures == null)
                return;

            var terrainFeatures = currentLocation.terrainFeatures;
            var cropGroups = new Dictionary<string, CropTagGroup>(StringComparer.OrdinalIgnoreCase);
            var fruitTreeGroups = new Dictionary<string, FruitTreeTagGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (Vector2 tile in terrainFeatures.Keys)
            {
                if (!terrainFeatures.TryGetValue(tile, out TerrainFeature? feature) || feature == null)
                    continue;

                if (feature is HoeDirt dirt && dirt.crop != null)
                {
                    if (!IsTileAreaInsideCapture(tile, captureBounds, 0, 0, 64, 64))
                        continue;

                    string cropName = GetCropDisplayName(dirt.crop);
                    if (string.IsNullOrWhiteSpace(cropName))
                        continue;

                    if (!cropGroups.TryGetValue(cropName, out CropTagGroup? cropGroup))
                    {
                        cropGroup = new CropTagGroup();
                        cropGroups[cropName] = cropGroup;
                    }

                    cropGroup.Add(dirt.readyForHarvest());
                    continue;
                }

                if (feature is FruitTree fruitTree)
                {
                    if (!IsTileAreaInsideCapture(tile, captureBounds, -32, -128, 128, 192))
                        continue;

                    string fruitTreeName = NormalizeFruitTreeName(fruitTree.GetDisplayName());
                    if (string.IsNullOrWhiteSpace(fruitTreeName))
                        continue;

                    if (!fruitTreeGroups.TryGetValue(fruitTreeName, out FruitTreeTagGroup? fruitTreeGroup))
                    {
                        fruitTreeGroup = new FruitTreeTagGroup();
                        fruitTreeGroups[fruitTreeName] = fruitTreeGroup;
                    }

                    fruitTreeGroup.Add(TryReadIntMember(fruitTree, "fruitsOnTree", "fruit", "Fruit") > 0);
                }
            }

            int cropTagCount = 0;
            foreach (var cropGroup in cropGroups
                         .OrderByDescending(entry => entry.Value.Count)
                         .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (cropTagCount >= MaxCropTagsPerImage)
                    break;

                string stateKey = cropGroup.Value.GetGrowthState();
                string growthState = SHelper.Translation.Get($"camera.tag.crop.{stateKey}");
                bool isField = cropGroup.Value.Count >= 18;

                string cropTag = isField
                    ? SHelper.Translation.Get("camera.tag.crop_field", new { name = cropGroup.Key, state = growthState })
                    : SHelper.Translation.Get("camera.tag.crop", new { name = cropGroup.Key, state = growthState });

                if (tags.Add(cropTag))
                    cropTagCount++;
            }

            int fruitTreeTagCount = 0;
            foreach (var fruitTreeGroup in fruitTreeGroups
                         .OrderByDescending(entry => entry.Value.Count)
                         .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (fruitTreeTagCount >= MaxFruitTreeTagsPerImage)
                    break;

                string baseTag = fruitTreeGroup.Value.GetRegularTag(fruitTreeGroup.Key, SHelper.Translation);
                bool isOrchard = fruitTreeGroup.Value.Count >= 6;

                string fruitTreeTag = isOrchard
                    ? SHelper.Translation.Get("camera.tag.orchard", new { name = baseTag })
                    : SHelper.Translation.Get("camera.tag.fruit_tree", new { name = baseTag });

                if (tags.Add(fruitTreeTag))
                    fruitTreeTagCount++;
            }
        }

        private static void AddForageTags(HashSet<string> tags, Rectangle captureBounds)
        {
            GameLocation? currentLocation = Game1.currentLocation;
            if (currentLocation == null)
                return;

            AddForageObjectTags(tags, captureBounds, currentLocation);
        }

        private static void AddForageObjectTags(HashSet<string> tags, Rectangle captureBounds, GameLocation currentLocation)
        {
            var locationObjectsByTile = new Dictionary<Vector2, StardewValley.Object>();
            AppendLocationObjectsFromUnknownCollection(GetMemberValue(currentLocation, "objects", "Objects"), locationObjectsByTile);
            if (locationObjectsByTile.Count == 0)
                return;

            var forageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var locationObjectPair in locationObjectsByTile)
            {
                Vector2 tile = locationObjectPair.Key;
                StardewValley.Object locationObject = locationObjectPair.Value;

                if (!IsTileAreaInsideCapture(tile, captureBounds, 0, 0, 64, 64))
                    continue;

                if (!IsForageObject(locationObject, currentLocation))
                    continue;

                string forageName = locationObject.DisplayName?.Trim().ToLowerInvariant() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(forageName))
                    forageName = locationObject.Name?.Trim().ToLowerInvariant() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(forageName))
                    continue;

                if (forageCounts.TryGetValue(forageName, out int count))
                    forageCounts[forageName] = count + 1;
                else
                    forageCounts[forageName] = 1;
            }

            List<string> forageNames = forageCounts
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .Take(MaxForageTagsPerImage)
                .Select(entry => entry.Key)
                .ToList();

            if (forageNames.Count > 0)
                tags.Add(SHelper.Translation.Get("camera.tag.forageable", new { list = string.Join(", ", forageNames) }));
        }

        private static bool IsForageObject(StardewValley.Object locationObject, GameLocation currentLocation)
        {
            if (TryInvokeBooleanMember(locationObject, "isForage"))
                return true;

            if (TryInvokeBooleanMethod(locationObject, "isForage", out bool methodResult, currentLocation) && methodResult)
                return true;

            if (!TryReadBooleanMemberValue(locationObject, out bool isSpawnedObject, "isSpawnedObject", "IsSpawnedObject") || !isSpawnedObject)
                return false;

            if (TryReadBooleanMemberValue(locationObject, out bool canBeGrabbed, "canBeGrabbed", "CanBeGrabbed") && !canBeGrabbed)
                return false;

            string objectName = locationObject.Name?.Trim() ?? string.Empty;
            return !string.Equals(objectName, "Artifact Spot", StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendLocationObjectsFromUnknownCollection(object? collection, Dictionary<Vector2, StardewValley.Object> objectsByTile)
        {
            if (collection == null)
                return;

            if (collection is IEnumerable enumerable)
                AppendLocationObjectsFromEnumerable(enumerable, objectsByTile);

            if (GetMemberValue(collection, "Pairs", "pairs") is IEnumerable pairs)
                AppendLocationObjectsFromEnumerable(pairs, objectsByTile);

            if (GetMemberValue(collection, "Entries", "entries") is IEnumerable entries)
                AppendLocationObjectsFromEnumerable(entries, objectsByTile);
        }

        private static void AppendLocationObjectsFromEnumerable(IEnumerable entries, Dictionary<Vector2, StardewValley.Object> objectsByTile)
        {
            foreach (object? entry in entries)
            {
                if (TryExtractLocationObjectEntry(entry, out Vector2 tile, out StardewValley.Object locationObject))
                    objectsByTile[tile] = locationObject;
            }
        }

        private static bool TryExtractLocationObjectEntry(object? entry, out Vector2 tile, out StardewValley.Object locationObject)
        {
            tile = Vector2.Zero;
            locationObject = null!;

            if (entry == null)
                return false;

            object? key;
            object? value;

            if (entry is DictionaryEntry dictionaryEntry)
            {
                key = dictionaryEntry.Key;
                value = dictionaryEntry.Value;
            }
            else
            {
                Type entryType = entry.GetType();
                PropertyInfo? keyProperty = entryType.GetProperty("Key");
                PropertyInfo? valueProperty = entryType.GetProperty("Value");
                if (keyProperty == null || valueProperty == null)
                    return false;

                key = keyProperty.GetValue(entry);
                value = valueProperty.GetValue(entry);
            }

            if (!TryExtractVector2FromUnknownValue(key, out tile))
                return false;

            StardewValley.Object? extractedObject = value as StardewValley.Object;
            if (extractedObject == null && value != null)
                extractedObject = GetMemberValue(value, "Value") as StardewValley.Object;

            if (extractedObject == null)
                return false;

            locationObject = extractedObject;
            return true;
        }

        private sealed class CropTagGroup
        {
            public int Count { get; private set; }
            public int ReadyCount { get; private set; }
            public int GrowingCount { get; private set; }

            public void Add(bool readyForHarvest)
            {
                Count++;

                if (readyForHarvest)
                    ReadyCount++;
                else
                    GrowingCount++;
            }

            public string GetGrowthState()
            {
                if (ReadyCount == 0)
                    return "growing";

                if (GrowingCount == 0)
                    return "harvestable";

                return ReadyCount >= GrowingCount
                    ? "harvestable"
                    : "growing";
            }
        }

        private sealed class FruitTreeTagGroup
        {
            public int Count { get; private set; }
            public int WithFruitCount { get; private set; }
            public int WithoutFruitCount { get; private set; }

            public void Add(bool hasFruit)
            {
                Count++;

                if (hasFruit)
                    WithFruitCount++;
                else
                    WithoutFruitCount++;
            }

            public string GetRegularTag(string fruitTreeName, ITranslationHelper translation)
            {
                if (WithFruitCount == 0)
                    return fruitTreeName;

                string withFruit = translation.Get("camera.tag.fruit_tree.with_fruit");
                if (WithoutFruitCount == 0)
                    return $"{fruitTreeName} {withFruit}".Trim();

                return WithFruitCount >= WithoutFruitCount
                    ? $"{fruitTreeName} {withFruit}".Trim()
                    : fruitTreeName;
            }
        }

        private static void AddPetTags(HashSet<string> tags, Rectangle captureBounds)
        {
            GameLocation? currentLocation = Game1.currentLocation;
            if (currentLocation?.characters == null)
                return;

            var catNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dogNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var horseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dinoNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Character character in currentLocation.characters)
            {
                if (!IsCharacterInsideCapture(character, captureBounds))
                    continue;

                if (character is Horse horse)
                {
                    AddCharacterNameIfPresent(horseNames, horse);
                    continue;
                }

                if (character is Pet pet)
                {
                    if (IsCatPet(pet))
                        AddCharacterNameIfPresent(catNames, pet);
                    else
                        AddCharacterNameIfPresent(dogNames, pet);
                }
            }

            foreach (FarmAnimal animal in GetFarmAnimalsForCurrentLocation())
            {
                if (!IsCharacterInsideCapture(animal, captureBounds))
                    continue;

                if (!IsDinosaurAnimal(animal))
                    continue;

                AddCharacterNameIfPresent(dinoNames, animal);
            }

            AddPetTypeTag(tags, "cat", catNames);
            AddPetTypeTag(tags, "dog", dogNames);
            AddPetTypeTag(tags, "horse", horseNames);
            AddPetTypeTag(tags, "dino", dinoNames);
        }

        private static void AddCharacterNameIfPresent(HashSet<string> names, Character character)
        {
            string displayName = GetCharacterDisplayName(character);
            if (!string.IsNullOrWhiteSpace(displayName))
                names.Add(displayName);
        }

        private static void AddPetTypeTag(HashSet<string> tags, string petType, HashSet<string> names)
        {
            List<string> selectedNames = names
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxPetNamesPerTypeTag)
                .ToList();

            if (selectedNames.Count > 0)
                tags.Add(SHelper.Translation.Get($"camera.tag.pet.{petType}", new { list = string.Join(", ", selectedNames) }));
        }

        private static bool IsDinosaurAnimal(FarmAnimal animal)
        {
            string animalType = GetFarmAnimalTypeName(animal);
            return animalType.Contains("dinosaur", StringComparison.OrdinalIgnoreCase)
                || animalType.Contains("dino", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCatPet(Pet pet)
        {
            if (TryInvokeBooleanMember(pet, "isCat"))
                return true;

            if (TryInvokeBooleanMember(pet, "isDog"))
                return false;

            string petType = TryReadStringMember(pet, "petType", "PetType", "whichBreed", "WhichBreed")
                .Trim();

            if (petType.Contains("cat", StringComparison.OrdinalIgnoreCase))
                return true;

            if (petType.Contains("dog", StringComparison.OrdinalIgnoreCase))
                return false;

            return false;
        }

        private static string GetCharacterDisplayName(Character character)
        {
            string displayName = TryReadStringMember(character, "displayName", "DisplayName", "Name").Trim();
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            return character.Name?.Trim() ?? string.Empty;
        }

        private static void AddFarmAnimalTags(HashSet<string> tags, Rectangle captureBounds)
        {
            int animalTagCount = 0;

            foreach (FarmAnimal animal in GetFarmAnimalsForCurrentLocation())
            {
                if (animalTagCount >= MaxAnimalTagsPerImage)
                    break;

                if (!IsCharacterInsideCapture(animal, captureBounds))
                    continue;

                string animalType = GetFarmAnimalTypeName(animal);
                if (string.IsNullOrWhiteSpace(animalType))
                    continue;

                bool isBaby = TryInvokeBooleanMember(animal, "isBaby");
                string animalTag = isBaby
                    ? SHelper.Translation.Get("camera.tag.animal.baby", new { type = animalType })
                    : SHelper.Translation.Get("camera.tag.animal", new { type = animalType });

                if (tags.Add(animalTag))
                    animalTagCount++;
            }
        }

        private static IEnumerable<FarmAnimal> GetFarmAnimalsForCurrentLocation()
        {
            var animals = new HashSet<FarmAnimal>();
            GameLocation? location = Game1.currentLocation;
            if (location == null)
                return animals;

            if (location.characters != null)
            {
                foreach (FarmAnimal animal in location.characters.OfType<FarmAnimal>())
                    animals.Add(animal);
            }

            AppendFarmAnimalsFromUnknownCollection(GetMemberValue(location, "animals", "Animals"), animals);

            GameLocation? farm = Game1.getFarm();
            if (farm != null)
                AppendFarmAnimalsFromUnknownCollection(GetMemberValue(farm, "animals", "Animals"), animals);

            return animals.Where(animal => IsAnimalInCurrentLocation(animal, location));
        }

        private static void AppendFarmAnimalsFromUnknownCollection(object? collection, HashSet<FarmAnimal> animals)
        {
            if (collection == null)
                return;

            if (collection is IEnumerable enumerable)
                AppendFarmAnimalsFromEnumerable(enumerable, animals);

            object? values = GetMemberValue(collection, "Values");
            if (values is IEnumerable valuesEnumerable)
                AppendFarmAnimalsFromEnumerable(valuesEnumerable, animals);
        }

        private static void AppendFarmAnimalsFromEnumerable(IEnumerable entries, HashSet<FarmAnimal> animals)
        {
            foreach (object? entry in entries)
            {
                FarmAnimal? animal = TryExtractFarmAnimal(entry);
                if (animal != null)
                    animals.Add(animal);
            }
        }

        private static bool IsAnimalInCurrentLocation(FarmAnimal animal, GameLocation currentLocation)
        {
            GameLocation? animalLocation = animal.currentLocation;
            if (animalLocation == null)
                return string.Equals(currentLocation.Name, "Farm", StringComparison.OrdinalIgnoreCase);

            if (ReferenceEquals(animalLocation, currentLocation))
                return true;

            string animalLocationName = TryReadStringMember(animalLocation, "NameOrUniqueName", "Name");
            string currentLocationName = TryReadStringMember(currentLocation, "NameOrUniqueName", "Name");
            return !string.IsNullOrWhiteSpace(animalLocationName)
                && string.Equals(animalLocationName, currentLocationName, StringComparison.OrdinalIgnoreCase);
        }

        private static FarmAnimal? TryExtractFarmAnimal(object? entry)
        {
            if (entry == null)
                return null;

            if (entry is FarmAnimal directAnimal)
                return directAnimal;

            object? value = entry;
            Type entryType = entry.GetType();
            if (entryType.IsGenericType && entryType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                value = entryType.GetProperty("Value")?.GetValue(entry);

            if (value is FarmAnimal valueAnimal)
                return valueAnimal;

            if (value != null)
                return GetMemberValue(value, "Value") as FarmAnimal;

            return null;
        }

        private static string GetFarmAnimalTypeName(FarmAnimal animal)
        {
            string rawAnimalType = TryReadStringMember(animal, "displayType", "DisplayType", "type", "Type");
            if (string.IsNullOrWhiteSpace(rawAnimalType))
                rawAnimalType = animal.Name;

            string normalized = rawAnimalType.Trim().ToLowerInvariant().Replace('_', ' ');

            foreach (string prefix in new[] { "white ", "brown ", "blue ", "golden ", "void " })
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized[prefix.Length..].Trim();
                    break;
                }
            }

            if (normalized.StartsWith("baby ", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[5..].Trim();

            return string.IsNullOrWhiteSpace(normalized) ? "animal" : normalized;
        }

        private static string GetCropDisplayName(Crop crop)
        {
            try
            {
                string harvestIndex = crop.indexOfHarvest.Value;
                if (!string.IsNullOrWhiteSpace(harvestIndex))
                    return new StardewValley.Object(harvestIndex, 1).DisplayName.Trim().ToLowerInvariant();
            }
            catch
            {
            }

            return "crop";
        }

        private static string NormalizeFruitTreeName(string? rawTreeName)
        {
            if (string.IsNullOrWhiteSpace(rawTreeName))
                return "fruit tree";

            string normalized = rawTreeName.Trim().ToLowerInvariant();
            return normalized.Contains("tree", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"{normalized} tree";
        }

        private static bool IsTileAreaInsideCapture(Vector2 tile, Rectangle captureBounds, int offsetX, int offsetY, int width, int height)
        {
            Vector2 worldTopLeft = new Vector2((tile.X * 64f) + offsetX, (tile.Y * 64f) + offsetY);
            Vector2 screenTopLeft = Game1.GlobalToLocal(Game1.viewport, worldTopLeft);

            var screenBounds = new Rectangle(
                (int)screenTopLeft.X,
                (int)screenTopLeft.Y,
                width,
                height);

            return GetNormalizedCaptureBounds(captureBounds).Contains(screenBounds);
        }

        private static Rectangle GetNormalizedCaptureBounds(Rectangle captureBounds)
        {
            float zoom = Game1.options?.zoomLevel ?? 1f;
            if (zoom <= 0f)
                zoom = 1f;

            return new Rectangle(
                (int)(captureBounds.X / zoom),
                (int)(captureBounds.Y / zoom),
                (int)(captureBounds.Width / zoom),
                (int)(captureBounds.Height / zoom));
        }

        private static void AddHeldItemTag(HashSet<string> tags)
        {
            if (Game1.player == null || !tags.Contains($"#Player {Game1.player?.displayName}".Trim()))
                return;

            Item? heldItem = Game1.player.ActiveObject ?? Game1.player.CurrentItem;
            if (heldItem == null || heldItem is Tool || heldItem is Boots || heldItem is Hat || heldItem is Clothing || heldItem is Furniture || heldItem is Wallpaper)
                return;

            string heldItemName = heldItem.DisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(heldItemName))
                heldItemName = heldItem.Name?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(heldItemName))
                return;

            tags.Add(SHelper.Translation.Get("camera.tag.holding", new { name = heldItemName }));
        }

        private static void AddBuildingFrontTags(HashSet<string> tags, Rectangle captureBounds)
        {
            GameLocation? currentLocation = Game1.currentLocation;
            if (currentLocation == null || currentLocation.buildings == null)
                return;

            foreach (var door in currentLocation.doors)
            {
                foreach (var doorItem in door)
                {
                    Point tile = doorItem.Key;
                    if (!IsTileAreaInsideCapture(tile.ToVector2(), captureBounds, -BuildingFrontTilesLeftRight * 64, -BuildingFrontTilesUp * 64, (BuildingFrontTilesLeftRight + BuildingFrontTilesLeftRight + 1) * 64, (BuildingFrontTilesUp + BuildingFrontTilesDown + 1) * 64))
                        continue;

                    string? buildingName = doorItem.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(buildingName))
                        continue;

                    tags.Add(SHelper.Translation.Get("camera.tag.front_of", new { name = buildingName }));
                }
            }
        }

        private static void AddWeatherTags(HashSet<string> tags)
        {
            tags.Add(SHelper.Translation.Get("camera.tag.weather", new { weather = (Game1.currentLocation.GetWeather().Weather.ToString() == "Festival" ? "Sunny" : Game1.currentLocation.GetWeather().Weather.ToString()) }));
        }

        private static bool AddCurrentEventTags(HashSet<string> tags)
        {
            bool isAtEvent = false;
            if (Game1.CurrentEvent == null)
                return isAtEvent;

            if (Game1.CurrentEvent.isFestival && !string.IsNullOrWhiteSpace(Game1.CurrentEvent.FestivalName))
            {
                tags.Add(SHelper.Translation.Get("camera.tag.festival", new { name = Game1.CurrentEvent.FestivalName.Trim() }));
                isAtEvent = true;
            }

            if (TryGetRecentUnlimitedEventTag(out string unlimitedEventTag))
            {
                tags.Add(unlimitedEventTag);
                isAtEvent = true;
            }

            if (!Game1.CurrentEvent.isFestival && !TryGetRecentUnlimitedEventTag(out _))
            {
                List<string> actorNames = GetCurrentEventActorNames(Game1.CurrentEvent);
                if (actorNames.Count == 1)
                    tags.Add(SHelper.Translation.Get("camera.tag.event", new { name = actorNames[0] }));
                else if (actorNames.Count > 1)
                    tags.Add(SHelper.Translation.Get("camera.tag.event.multiple", new { list = string.Join(", ", actorNames.Take(2)) }));
                isAtEvent = true;
            }
            return isAtEvent;
        }

        private static List<string> GetCurrentEventActorNames(Event currentEvent)
        {
            var actorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            object? actorsObject = GetMemberValue(currentEvent, "actors", "Actors");
            if (actorsObject is not IEnumerable actorsEnumerable)
                return new List<string>();

            foreach (object? actorEntry in actorsEnumerable)
            {
                if (actorEntry == null)
                    continue;

                object? value = actorEntry;
                Type entryType = actorEntry.GetType();
                if (entryType.IsGenericType && entryType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                    value = entryType.GetProperty("Value")?.GetValue(actorEntry);

                if (value is NPC npc && !string.IsNullOrWhiteSpace(npc.Name))
                    actorNames.Add(npc.Name);
            }

            actorNames.Remove(Game1.player?.Name ?? string.Empty);
            return actorNames.ToList();
        }

        private static void RememberTriggeredUnlimitedEvent(string eventType, string npcName)
        {
            string normalizedEventType = (eventType ?? string.Empty).Replace('_', ' ').Trim();
            string normalizedNpcName = (npcName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedEventType) || string.IsNullOrWhiteSpace(normalizedNpcName))
                return;

            LastTriggeredUnlimitedEventTag = SHelper.Translation.Get("camera.tag.unlimited_event", new { eventType = normalizedEventType, npcName = normalizedNpcName });
            LastTriggeredUnlimitedEventYear = Game1.year;
            LastTriggeredUnlimitedEventSeason = Game1.currentSeason ?? string.Empty;
            LastTriggeredUnlimitedEventDay = Game1.dayOfMonth;
        }

        private static void ResetTriggeredUnlimitedEventTag()
        {
            LastTriggeredUnlimitedEventTag = string.Empty;
            LastTriggeredUnlimitedEventYear = -1;
            LastTriggeredUnlimitedEventSeason = string.Empty;
            LastTriggeredUnlimitedEventDay = -1;
        }

        private static bool TryGetRecentUnlimitedEventTag(out string tag)
        {
            tag = string.Empty;
            if (string.IsNullOrWhiteSpace(LastTriggeredUnlimitedEventTag))
                return false;

            bool isSameDay = LastTriggeredUnlimitedEventYear == Game1.year
                && string.Equals(LastTriggeredUnlimitedEventSeason, Game1.currentSeason, StringComparison.OrdinalIgnoreCase)
                && LastTriggeredUnlimitedEventDay == Game1.dayOfMonth;

            if (!isSameDay)
                return false;

            tag = LastTriggeredUnlimitedEventTag;
            return true;
        }

        private static bool TryInvokeBooleanMember(object source, string memberName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            MethodInfo? method = source.GetType().GetMethod(memberName, flags, Type.DefaultBinder, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(bool) && method.Invoke(source, null) is bool methodResult)
                return methodResult;

            object? value = GetMemberValue(source, memberName);
            return TryExtractBooleanFromUnknownValue(value, out bool extracted) && extracted;
        }

        private static bool TryInvokeBooleanMethod(object source, string methodName, out bool result, params object[] arguments)
        {
            result = false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            Type[] argumentTypes = arguments.Select(argument => argument?.GetType() ?? typeof(object)).ToArray();
            MethodInfo? method = source.GetType().GetMethod(methodName, flags, Type.DefaultBinder, argumentTypes, null);
            if (method == null || method.ReturnType != typeof(bool))
                return false;

            if (method.Invoke(source, arguments) is bool methodResult)
            {
                result = methodResult;
                return true;
            }

            return false;
        }

        private static bool TryInvokeIntMethod(object source, string methodName, out int result, params object[] arguments)
        {
            result = -1;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            Type[] argumentTypes = arguments.Select(argument => argument?.GetType() ?? typeof(object)).ToArray();
            MethodInfo? method = source.GetType().GetMethod(methodName, flags, Type.DefaultBinder, argumentTypes, null);
            if (method == null)
                return false;

            object? rawResult = method.Invoke(source, arguments);
            int parsedResult = ExtractIntFromUnknownValue(rawResult);
            if (parsedResult < 0)
                return false;

            result = parsedResult;
            return true;
        }

        private static bool TryReadBooleanMemberValue(object source, out bool value, params string[] memberNames)
        {
            foreach (string memberName in memberNames)
            {
                object? rawValue = GetMemberValue(source, memberName);
                if (TryExtractBooleanFromUnknownValue(rawValue, out bool extracted))
                {
                    value = extracted;
                    return true;
                }
            }

            value = false;
            return false;
        }

        private static int TryReadIntMember(object source, params string[] memberNames)
        {
            foreach (string memberName in memberNames)
            {
                object? value = GetMemberValue(source, memberName);
                int extracted = ExtractIntFromUnknownValue(value);
                if (extracted >= 0)
                    return extracted;
            }

            return -1;
        }

        private static string TryReadStringMember(object source, params string[] memberNames)
        {
            foreach (string memberName in memberNames)
            {
                object? value = GetMemberValue(source, memberName);
                string extracted = ExtractStringFromUnknownValue(value);
                if (!string.IsNullOrWhiteSpace(extracted))
                    return extracted;
            }

            return string.Empty;
        }

        private static object? GetMemberValue(object source, params string[] memberNames)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            foreach (string memberName in memberNames)
            {
                PropertyInfo? property = source.GetType().GetProperty(memberName, flags);
                if (property != null)
                    return property.GetValue(source);

                FieldInfo? field = source.GetType().GetField(memberName, flags);
                if (field != null)
                    return field.GetValue(source);
            }

            return null;
        }

        private static int ExtractIntFromUnknownValue(object? value)
        {
            if (value == null)
                return -1;

            if (value is int intValue)
                return intValue;

            if (value is long longValue && longValue >= 0 && longValue <= int.MaxValue)
                return (int)longValue;

            if (value is short shortValue && shortValue >= 0)
                return shortValue;

            if (value is byte byteValue)
                return byteValue;

            if (value is string textValue && int.TryParse(textValue, out int parsedTextValue))
                return parsedTextValue;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            PropertyInfo? valueProperty = value.GetType().GetProperty("Value", flags);
            if (valueProperty != null)
            {
                object? nestedValue = valueProperty.GetValue(value);
                int nestedInt = ExtractIntFromUnknownValue(nestedValue);
                if (nestedInt >= 0)
                    return nestedInt;
            }

            PropertyInfo? countProperty = value.GetType().GetProperty("Count", flags);
            if (countProperty != null)
            {
                object? countValue = countProperty.GetValue(value);
                int parsedCount = ExtractIntFromUnknownValue(countValue);
                if (parsedCount >= 0)
                    return parsedCount;
            }

            return -1;
        }

        private static bool TryExtractBooleanFromUnknownValue(object? value, out bool extracted)
        {
            extracted = false;
            if (value == null)
                return false;

            if (value is bool boolValue)
            {
                extracted = boolValue;
                return true;
            }

            if (value is int intValue)
            {
                extracted = intValue != 0;
                return true;
            }

            if (value is long longValue)
            {
                extracted = longValue != 0;
                return true;
            }

            if (value is string textValue && bool.TryParse(textValue, out bool parsedTextValue))
            {
                extracted = parsedTextValue;
                return true;
            }

            object? nestedValue = GetMemberValue(value, "Value");
            if (nestedValue != null && !ReferenceEquals(nestedValue, value))
                return TryExtractBooleanFromUnknownValue(nestedValue, out extracted);

            return false;
        }

        private static bool TryExtractVector2FromUnknownValue(object? value, out Vector2 vector)
        {
            vector = Vector2.Zero;
            if (value == null)
                return false;

            if (value is Vector2 directVector)
            {
                vector = directVector;
                return true;
            }

            object? nestedValue = GetMemberValue(value, "Value");
            if (nestedValue is Vector2 nestedVector)
            {
                vector = nestedVector;
                return true;
            }

            if (TryExtractFloatFromUnknownValue(GetMemberValue(value, "X", "x"), out float x)
                && TryExtractFloatFromUnknownValue(GetMemberValue(value, "Y", "y"), out float y))
            {
                vector = new Vector2(x, y);
                return true;
            }

            return false;
        }

        private static bool TryExtractFloatFromUnknownValue(object? value, out float extracted)
        {
            extracted = 0f;
            if (value == null)
                return false;

            if (value is float floatValue)
            {
                extracted = floatValue;
                return true;
            }

            if (value is double doubleValue)
            {
                extracted = (float)doubleValue;
                return true;
            }

            if (value is int intValue)
            {
                extracted = intValue;
                return true;
            }

            if (value is long longValue)
            {
                extracted = longValue;
                return true;
            }

            if (value is string textValue && float.TryParse(textValue, out float parsedTextValue))
            {
                extracted = parsedTextValue;
                return true;
            }

            object? nestedValue = GetMemberValue(value, "Value");
            if (nestedValue != null && !ReferenceEquals(nestedValue, value))
                return TryExtractFloatFromUnknownValue(nestedValue, out extracted);

            return false;
        }

        private static string ExtractStringFromUnknownValue(object? value)
        {
            if (value == null)
                return string.Empty;

            if (value is string stringValue)
                return stringValue;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            PropertyInfo? valueProperty = value.GetType().GetProperty("Value", flags);
            if (valueProperty != null)
            {
                object? nestedValue = valueProperty.GetValue(value);
                if (nestedValue is string nestedString)
                    return nestedString;
            }

            return value.ToString() ?? string.Empty;
        }

        private static bool IsCharacterInsideCapture(Character character, Rectangle captureBounds)
        {
            Rectangle characterBounds = character.GetBoundingBox();
            Vector2 screenTopLeft = Game1.GlobalToLocal(Game1.viewport, new Vector2(characterBounds.X, characterBounds.Y - 64));
            var screenBounds = new Rectangle(
                (int)screenTopLeft.X,
                (int)screenTopLeft.Y,
                characterBounds.Width,
                characterBounds.Height);

            return GetNormalizedCaptureBounds(captureBounds).Contains(screenBounds);
        }

        private static void AddLocationTag(HashSet<string> tags, GameLocation? location, NPC? npc = null)
        {
            string? locationName = location?.Name;
            if (string.IsNullOrWhiteSpace(locationName) && npc == null)
                location = Game1.currentLocation;

            if (npc != null && location?.Name == npc.DefaultMap)
            {
                tags.Add(SHelper.Translation.Get("camera.tag.location.npc_home", new { name = npc.displayName }));
                return;
            }

            if (location == Game1.getFarm())
            {
                tags.Add(SHelper.Translation.Get("camera.tag.location.farm"));
                return;
            }

            if (location is FarmHouse)
            {
                tags.Add(SHelper.Translation.Get("camera.tag.location.home"));
                return;
            }

            tags.Add(SHelper.Translation.Get("camera.tag.location.visiting", new { location = location?.DisplayName }));
        }
    }
}
