using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.Locations;

namespace Smartphone
{
    public partial class ModEntry
    {
        private const float NpcCaptureZoomMin = 0.5f;
        private const float NpcCaptureZoomMax = 2f;
        private const int SquareCaptureTolerancePixels = 2;
        private const double PlayerCaptureCursorHideDurationSeconds = 0.5d;
        private const double PlayerCaptureDelaySeconds = 0.05d;
        private const double PlayerCaptureWorldFlashDurationSeconds = 0.5d;
        private const float PlayerCaptureWorldFlashRadiusDefault = 3f;
        private const float PlayerCaptureWorldFlashRadiusMin = 1f;
        private const float PlayerCaptureWorldFlashRadiusMax = 10f;
        private const string PlayerCaptureWorldFlashLightIdPrefix = "Smartphone_PlayerCameraFlash_";
        private const float NpcCaptureFlashRadiusAtOrAboveOne = 2.2f;
        private const float NpcCaptureFlashRadiusBelowOne = 2.7f;
        private const string NpcCaptureFlashLightIdPrefix = "Smartphone_NpcCaptureFlash_";
        private static double playerCaptureDelayRemainingSeconds = 0d;
        private static double playerCaptureCursorHideRemainingSeconds = 0d;
        private static string? activePlayerCaptureFlashLightId;

        private static float PlayerCaptureWorldFlashRadius
        {
            get
            {
                float configuredRadius = Config?.PlayerCaptureWorldFlashRadius ?? PlayerCaptureWorldFlashRadiusDefault;
                if (float.IsNaN(configuredRadius) || float.IsInfinity(configuredRadius))
                    return PlayerCaptureWorldFlashRadiusDefault;

                return Math.Clamp(configuredRadius, PlayerCaptureWorldFlashRadiusMin, PlayerCaptureWorldFlashRadiusMax);
            }
        }

        public static void QueuePlayerPhotoCapture(Rectangle? captureBoundsForFlash = null)
        {
            if (cameraFlashMode)
                TriggerPlayerCaptureWorldFlash(captureBoundsForFlash ?? GetCurrentPlayerPhotoCaptureBounds());

            takeScreenshot = true;
            playerCaptureDelayRemainingSeconds = Math.Max(playerCaptureDelayRemainingSeconds, PlayerCaptureDelaySeconds);
            playerCaptureCursorHideRemainingSeconds = Math.Max(playerCaptureCursorHideRemainingSeconds, PlayerCaptureCursorHideDurationSeconds);
        }

        private static void TriggerPlayerCaptureWorldFlash(Rectangle captureBounds)
        {
            if (!Context.IsWorldReady || Game1.currentLocation == null)
                return;

            if (captureBounds.Width <= 0 || captureBounds.Height <= 0)
                captureBounds = GetCurrentPlayerPhotoCaptureBounds();

            if (!string.IsNullOrEmpty(activePlayerCaptureFlashLightId))
                Game1.currentLightSources.Remove(activePlayerCaptureFlashLightId);

            Point centerTile = GetCenterTileFromCapture(captureBounds);
            Vector2 lightPosition = new Vector2(
                (centerTile.X * Game1.tileSize) + (Game1.tileSize / 2f),
                (centerTile.Y * Game1.tileSize) + (Game1.tileSize / 2f));

            string lightId = $"{PlayerCaptureWorldFlashLightIdPrefix}{Guid.NewGuid():N}";
            activePlayerCaptureFlashLightId = lightId;

            Game1.currentLightSources[lightId] = new LightSource(
                lightId,
                4,
                lightPosition,
                PlayerCaptureWorldFlashRadius,
                LightSource.LightContext.MapLight,
                0L,
                Game1.currentLocation.NameOrUniqueName);

            int durationMs = Math.Max(1, (int)Math.Round(PlayerCaptureWorldFlashDurationSeconds * 1000d));
            DelayedAction.functionAfterDelay(() =>
            {
                Game1.currentLightSources.Remove(lightId);
                if (activePlayerCaptureFlashLightId == lightId)
                    activePlayerCaptureFlashLightId = null;
            }, durationMs);
        }

        public static bool IsPlayerCaptureCursorHidden()
        {
            return takeScreenshot || playerCaptureCursorHideRemainingSeconds > 0d;
        }

        public static void UpdatePlayerCaptureTimers(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0d)
                return;

            if (playerCaptureDelayRemainingSeconds > 0d)
                playerCaptureDelayRemainingSeconds = Math.Max(0d, playerCaptureDelayRemainingSeconds - elapsedSeconds);

            if (playerCaptureCursorHideRemainingSeconds > 0d)
                playerCaptureCursorHideRemainingSeconds = Math.Max(0d, playerCaptureCursorHideRemainingSeconds - elapsedSeconds);
        }

        public static Microsoft.Xna.Framework.Rectangle GetPhoneCameraViewportBounds(int menuX, int menuY)
        {
            float uiScale = GetActivePhoneUiScale();
            return new Microsoft.Xna.Framework.Rectangle(
                menuX + ScalePhoneUiValue(CameraViewportOffsetX, uiScale),
                menuY + ScalePhoneUiValue(CameraViewportOffsetY, uiScale),
                Math.Max(1, ScalePhoneUiValue(CameraViewportWidth, uiScale)),
                Math.Max(1, ScalePhoneUiValue(CameraViewportHeight, uiScale)));
        }

        public static Microsoft.Xna.Framework.Rectangle GetPhoneCameraPreviewBounds(int menuX, int menuY)
        {
            Microsoft.Xna.Framework.Rectangle portraitViewport = GetPhoneCameraViewportBounds(menuX, menuY);
            if (!cameraLandscapeMode)
                return portraitViewport;

            int previewWidth = portraitViewport.Height;
            int previewHeight = portraitViewport.Width;

            return new Microsoft.Xna.Framework.Rectangle(
                portraitViewport.Center.X - (previewWidth / 2),
                portraitViewport.Center.Y - (previewHeight / 2),
                previewWidth,
                previewHeight);
        }

        public static Microsoft.Xna.Framework.Rectangle GetPlayerPhotoCaptureBounds(int menuX, int menuY)
        {
            Microsoft.Xna.Framework.Rectangle viewportBounds = GetPhoneCameraPreviewBounds(menuX, menuY);
            return BuildCaptureBounds(viewportBounds, cameraLandscapeMode, cameraSquareMode);
        }

        public static Microsoft.Xna.Framework.Rectangle GetCurrentPlayerPhotoCaptureBounds()
        {
            return GetPlayerPhotoCaptureBounds(currentMenuX, currentMenuY);
        }

        private static Microsoft.Xna.Framework.Rectangle BuildCaptureBounds(Microsoft.Xna.Framework.Rectangle viewportBounds, bool landscape, bool square)
        {
            (int captureWidth, int captureHeight) = GetCaptureDimensions(landscape, square);

            float zoom = Math.Clamp(cameraZoomFactor, 1f, 2f);
            captureWidth = Math.Max(1, (int)Math.Round(captureWidth / zoom));
            captureHeight = Math.Max(1, (int)Math.Round(captureHeight / zoom));

            captureWidth = Math.Min(captureWidth, viewportBounds.Width);
            captureHeight = Math.Min(captureHeight, viewportBounds.Height);

            int captureX = viewportBounds.X + ((viewportBounds.Width - captureWidth) / 2);
            int captureY = viewportBounds.Y + ((viewportBounds.Height - captureHeight) / 2);

            return new Microsoft.Xna.Framework.Rectangle(captureX, captureY, captureWidth, captureHeight);
        }

        private static (int Width, int Height) GetCaptureDimensions(bool landscape, bool square)
        {
            float uiScale = GetActivePhoneUiScale();
            int viewportWidth = Math.Max(1, ScalePhoneUiValue(CameraViewportWidth, uiScale));
            int viewportHeight = Math.Max(1, ScalePhoneUiValue(CameraViewportHeight, uiScale));

            if (square)
                return (viewportWidth, viewportWidth);

            if (landscape)
                return (viewportHeight, viewportWidth);

            return (viewportWidth, viewportHeight);
        }

        private static Microsoft.Xna.Framework.Rectangle ClampCaptureBoundsToBackBuffer(Microsoft.Xna.Framework.Rectangle requestedBounds, int backBufferWidth, int backBufferHeight)
        {
            int clampedWidth = Math.Max(1, Math.Min(requestedBounds.Width, backBufferWidth));
            int clampedHeight = Math.Max(1, Math.Min(requestedBounds.Height, backBufferHeight));
            int clampedX = Math.Clamp(requestedBounds.X, 0, Math.Max(0, backBufferWidth - clampedWidth));
            int clampedY = Math.Clamp(requestedBounds.Y, 0, Math.Max(0, backBufferHeight - clampedHeight));

            return new Microsoft.Xna.Framework.Rectangle(clampedX, clampedY, clampedWidth, clampedHeight);
        }
        private static Microsoft.Xna.Framework.Rectangle ConvertUiBoundsToBackBuffer(Microsoft.Xna.Framework.Rectangle uiBounds, int backBufferWidth, int backBufferHeight)
        {
            int uiViewportWidth = Math.Max(1, Game1.uiViewport.Width);
            int uiViewportHeight = Math.Max(1, Game1.uiViewport.Height);

            float scaleX = backBufferWidth / (float)uiViewportWidth;
            float scaleY = backBufferHeight / (float)uiViewportHeight;

            int left = (int)Math.Floor(uiBounds.Left * scaleX);
            int top = (int)Math.Floor(uiBounds.Top * scaleY);
            int right = (int)Math.Ceiling(uiBounds.Right * scaleX);
            int bottom = (int)Math.Ceiling(uiBounds.Bottom * scaleY);

            return new Microsoft.Xna.Framework.Rectangle(
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top));
        }
        private void OnRendered(object sender, RenderedEventArgs e)
        {
            // dev tool grid drawing
            if (isGridVisible && Context.IsWorldReady)
            {
                DrawGrid();
            }

            if (takeScreenshot)
            {
                if (playerCaptureDelayRemainingSeconds > 0d)
                    return;

                takeScreenshot = false;

                GraphicsDevice graphics = Game1.graphics.GraphicsDevice;

                int backBufferWidth = graphics.PresentationParameters.BackBufferWidth;
                int backBufferHeight = graphics.PresentationParameters.BackBufferHeight;

                // Create a RenderTarget the same size as the back buffer
                using RenderTarget2D copyTarget = new RenderTarget2D(graphics, backBufferWidth, backBufferHeight);

                // Copy current back buffer into our render target
                graphics.SetRenderTarget(copyTarget);
                graphics.Clear(Color.Black);

                // Draw the backbuffer content into the render target
                graphics.SetRenderTarget(null);
                Color[] rawData = new Color[backBufferWidth * backBufferHeight];
                graphics.GetBackBufferData(rawData);

                Microsoft.Xna.Framework.Rectangle requestedCaptureBounds = GetCurrentPlayerPhotoCaptureBounds();
                Microsoft.Xna.Framework.Rectangle requestedBackBufferBounds = ConvertUiBoundsToBackBuffer(requestedCaptureBounds, backBufferWidth, backBufferHeight);
                Microsoft.Xna.Framework.Rectangle captureBounds = ClampCaptureBoundsToBackBuffer(requestedBackBufferBounds, backBufferWidth, backBufferHeight);

                int cropX = captureBounds.X;
                int cropY = captureBounds.Y;
                int cropWidth = captureBounds.Width;
                int cropHeight = captureBounds.Height;

                // Extract pixel data from the cropped region
                Color[] croppedData = new Color[cropWidth * cropHeight];
                for (int y = 0; y < cropHeight; y++)
                {
                    for (int x = 0; x < cropWidth; x++)
                    {
                        int srcIndex = (cropY + y) * backBufferWidth + (cropX + x);
                        int destIndex = y * cropWidth + x;
                        croppedData[destIndex] = rawData[srcIndex];
                    }
                }

                // Create cropped texture
                using Texture2D croppedTexture = new Texture2D(graphics, cropWidth, cropHeight);
                croppedTexture.SetData(croppedData);

                SaveCapturedPhoto(croppedTexture, Game1.currentLocation?.DisplayName, BuildImageTags(requestedCaptureBounds).ToList(), true, cameraSquareMode);
            }
        }

        public static string CaptureNpcPhoto(GameLocation targetLocation, Vector2 captureCenter, NPC npc = null, bool landscape = false, bool square = false, List<NPC>? visibleNpcAtTarget = null, float zoomLevel = 1f, int? captureTimeOfDay = null, string saveLocation = null, bool forceFlash = false)
        {
            if (!Context.IsWorldReady || targetLocation == null || Game1.graphics?.GraphicsDevice == null || Game1.game1 == null)
                return "";

            GraphicsDevice graphics = Game1.graphics.GraphicsDevice;
            int effectiveCaptureTime = NormalizeCaptureTimeOfDay(captureTimeOfDay);
            var renderStateSnapshot = new PhotoRenderStateSnapshot();
            int temporarySpriteCount = targetLocation.temporarySprites.Count;
            CaptureMapAppearanceSnapshot? mapAppearanceSnapshot = null;
            (int baseCaptureWidth, int baseCaptureHeight) = GetCaptureDimensions(landscape, square);
            (int captureWidth, int captureHeight) = GetZoomedCaptureDimensions(baseCaptureWidth, baseCaptureHeight, zoomLevel);
            var captureBounds = new Microsoft.Xna.Framework.Rectangle(0, 0, captureWidth, captureHeight);
            List<string> tags = new List<string>();

            using RenderTarget2D renderTarget = new RenderTarget2D(graphics, captureWidth, captureHeight);

            try
            {
                Game1.currentLocation = targetLocation;
                Game1.viewport = BuildNpcCaptureViewport(targetLocation, captureCenter, captureWidth, captureHeight);
                Game1.timeOfDay = effectiveCaptureTime;
                mapAppearanceSnapshot = PrepareLocationRenderState(targetLocation, captureWidth, captureHeight);
                TryAddNpcCaptureFlashLight(targetLocation, captureCenter, zoomLevel, effectiveCaptureTime, forceFlash);

                graphics.SetRenderTarget(renderTarget);
                graphics.Clear(Color.Black);

                Game1.game1.DrawWorld(Game1.currentGameTime, renderTarget);
                tags = BuildImageTags(captureBounds, npc: npc).ToList();
            }
            catch (Exception ex)
            {
                RecoverWorldDrawStateAfterCaptureFailure();
                SMonitor.Log($"Failed to capture off-screen photo: {ex}", LogLevel.Error);
                return "";
            }
            finally
            {
                graphics.SetRenderTarget(null);
                mapAppearanceSnapshot?.Restore();
                RestoreTemporarySpritesAfterCapture(targetLocation, temporarySpriteCount);
                renderStateSnapshot.Restore();
            }

            if (visibleNpcAtTarget != null)
            {
                foreach (var character in visibleNpcAtTarget)
                {
                    character.IsInvisible = false;
                }
            }

            return SaveCapturedPhoto(renderTarget, targetLocation.DisplayName, tags, false, square, saveLocation);
        }

        /// <summary>
        /// Renders <paramref name="targetLocation"/> centred on <paramref name="captureCenter"/> into
        /// an existing <paramref name="renderTarget"/> without saving anything to disk.
        /// Intended for low-frequency live-feed use (e.g. one call every 3 seconds).
        /// </summary>
        /// <returns>True on success; false if the capture could not be completed.</returns>
        public static bool CaptureLiveFeedFrame(
            GameLocation targetLocation,
            Vector2 captureCenter,
            RenderTarget2D renderTarget,
            float zoomLevel = 1f,
            bool forceFlash = false)
        {
            if (!Context.IsWorldReady || targetLocation == null ||
                renderTarget == null || renderTarget.IsDisposed ||
                Game1.graphics?.GraphicsDevice == null || Game1.game1 == null)
                return false;

            GraphicsDevice graphics = Game1.graphics.GraphicsDevice;
            int effectiveCaptureTime = NormalizeCaptureTimeOfDay(null);
            var renderStateSnapshot = new PhotoRenderStateSnapshot();
            int temporarySpriteCount = targetLocation.temporarySprites.Count;
            CaptureMapAppearanceSnapshot? mapAppearanceSnapshot = null;

            (int zoomedW, int zoomedH) = GetZoomedCaptureDimensions(renderTarget.Width, renderTarget.Height, zoomLevel);
            bool useIntermediateTarget = (zoomedW != renderTarget.Width || zoomedH != renderTarget.Height);

            RenderTarget2D captureTarget = useIntermediateTarget
                ? new RenderTarget2D(graphics, zoomedW, zoomedH)
                : renderTarget;

            try
            {
                Game1.currentLocation = targetLocation;
                Game1.viewport = BuildNpcCaptureViewport(
                    targetLocation,
                    captureCenter,
                    zoomedW,
                    zoomedH);
                Game1.timeOfDay = effectiveCaptureTime;
                mapAppearanceSnapshot = PrepareLocationRenderState(
                    targetLocation, zoomedW, zoomedH);

                if (forceFlash)
                {
                    TryAddNpcCaptureFlashLight(targetLocation, captureCenter, zoomLevel, effectiveCaptureTime, forceFlash: true);
                }

                graphics.SetRenderTarget(captureTarget);
                graphics.Clear(Color.Black);

                Game1.game1.DrawWorld(Game1.currentGameTime, captureTarget);

                if (useIntermediateTarget)
                {
                    graphics.SetRenderTarget(renderTarget);
                    graphics.Clear(Color.Black);

                    using (SpriteBatch b = new SpriteBatch(graphics))
                    {
                        b.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
                        b.Draw(captureTarget, new Microsoft.Xna.Framework.Rectangle(0, 0, renderTarget.Width, renderTarget.Height), Color.White);
                        b.End();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                RecoverWorldDrawStateAfterCaptureFailure();
                SMonitor.Log($"CaptureLiveFeedFrame failed for '{targetLocation?.Name}': {ex}", LogLevel.Trace);
                return false;
            }
            finally
            {
                graphics.SetRenderTarget(null);
                mapAppearanceSnapshot?.Restore();
                RestoreTemporarySpritesAfterCapture(targetLocation, temporarySpriteCount);
                renderStateSnapshot.Restore();
                if (useIntermediateTarget)
                {
                    captureTarget.Dispose();
                }
            }
        }

        private static void TryAddNpcCaptureFlashLight(GameLocation targetLocation, Vector2 captureCenterTile, float zoomLevel, int captureTimeOfDay, bool forceFlash = false)
        {
            if (!forceFlash && !ShouldEnableNpcCaptureFlash(targetLocation, captureTimeOfDay))
                return;

            float flashRadius = GetNpcCaptureFlashRadiusForZoom(zoomLevel);
            Vector2 flashPosition = (captureCenterTile * Game1.tileSize) + new Vector2(Game1.tileSize / 2f, Game1.tileSize / 2f);
            string lightId = $"{NpcCaptureFlashLightIdPrefix}{Guid.NewGuid():N}";

            Game1.currentLightSources[lightId] = new LightSource(
                lightId,
                4,
                flashPosition,
                flashRadius,
                LightSource.LightContext.MapLight,
                0L,
                targetLocation.NameOrUniqueName);
        }

        private static bool ShouldEnableNpcCaptureFlash(GameLocation targetLocation, int captureTimeOfDay)
        {
            if (targetLocation == null)
                return false;

            if (!targetLocation.IsOutdoors)
                return captureTimeOfDay > 2100;

            bool isBadWeather = targetLocation.IsRainingHere()
                || targetLocation.IsSnowingHere()
                || targetLocation.IsLightningHere()
                || targetLocation.IsGreenRainingHere();

            if (isBadWeather && captureTimeOfDay > 1900)
                return true;

            if (Game1.GetSeasonForLocation(targetLocation) == Season.Winter && captureTimeOfDay > 1830)
                return true;

            return captureTimeOfDay > 2000;
        }

        private static float GetNpcCaptureFlashRadiusForZoom(float zoomLevel)
        {
            float safeZoomLevel = zoomLevel;
            if (float.IsNaN(safeZoomLevel) || float.IsInfinity(safeZoomLevel))
                safeZoomLevel = 1f;

            return safeZoomLevel < 1f
                ? NpcCaptureFlashRadiusBelowOne
                : NpcCaptureFlashRadiusAtOrAboveOne;
        }

        private static (int Width, int Height) GetZoomedCaptureDimensions(int baseCaptureWidth, int baseCaptureHeight, float zoomLevel)
        {
            float safeZoomLevel = zoomLevel;
            if (float.IsNaN(safeZoomLevel) || float.IsInfinity(safeZoomLevel))
                safeZoomLevel = 1f;

            safeZoomLevel = Math.Clamp(safeZoomLevel, NpcCaptureZoomMin, NpcCaptureZoomMax);

            int captureWidth = Math.Max(1, (int)Math.Round(baseCaptureWidth / safeZoomLevel));
            int captureHeight = Math.Max(1, (int)Math.Round(baseCaptureHeight / safeZoomLevel));
            return (captureWidth, captureHeight);
        }

        private static int NormalizeCaptureTimeOfDay(int? captureTimeOfDay)
        {
            int fallbackTime = Game1.timeOfDay > 0 ? Game1.timeOfDay : 600;
            int rawTime = captureTimeOfDay ?? fallbackTime;

            int hour = Math.Clamp(rawTime / 100, 6, 26);
            int minute = Math.Clamp(rawTime % 100, 0, 59);
            return (hour * 100) + minute;
        }

        private static void RecoverWorldDrawStateAfterCaptureFailure()
        {
            try
            {
                Game1.spriteBatch?.End();
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception ex)
            {
                SMonitor.Log($"SpriteBatch recovery failed after NPC photo capture error: {ex}", LogLevel.Trace);
            }

            object? mapDisplayDevice = TryGetStaticMemberValue(typeof(Game1), "mapDisplayDevice", "MapDisplayDevice");
            if (mapDisplayDevice != null)
            {
                try
                {
                    TryInvokeNoArgumentMethod(mapDisplayDevice, "EndScene");
                }
                catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (Exception ex)
                {
                    SMonitor.Log($"Map display device recovery failed after NPC photo capture error: {ex}", LogLevel.Trace);
                }
            }

            try
            {
                Game1.graphics?.GraphicsDevice?.SetRenderTarget(null);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Render target recovery failed after NPC photo capture error: {ex}", LogLevel.Trace);
            }
        }

        private static object? TryGetStaticMemberValue(Type sourceType, params string[] memberNames)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            foreach (string memberName in memberNames)
            {
                PropertyInfo? property = sourceType.GetProperty(memberName, flags);
                if (property != null)
                    return property.GetValue(null);

                FieldInfo? field = sourceType.GetField(memberName, flags);
                if (field != null)
                    return field.GetValue(null);
            }

            return null;
        }

        private static bool TrySetStaticMemberValue(Type sourceType, object? value, params string[] memberNames)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            foreach (string memberName in memberNames)
            {
                PropertyInfo? property = sourceType.GetProperty(memberName, flags);
                if (property?.CanWrite == true)
                {
                    property.SetValue(null, value);
                    return true;
                }

                FieldInfo? field = sourceType.GetField(memberName, flags);
                if (field != null)
                {
                    field.SetValue(null, value);
                    return true;
                }
            }

            return false;
        }

        private static bool TryInvokeNoArgumentMethod(object source, string methodName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            MethodInfo? method = source.GetType().GetMethod(methodName, flags, Type.DefaultBinder, Type.EmptyTypes, null);
            if (method == null)
                return false;

            method.Invoke(source, null);
            return true;
        }

        private static string SaveCapturedPhoto(Texture2D capturedTexture, string? locationName, IEnumerable<string> tags, bool isPlayerPhoto, bool enforceSquareOutput = false, string saveLocation = null)
        {
            string folderPath;
            string filename = $"{Guid.NewGuid():N}.jpg";
            string path;

            if (!string.IsNullOrWhiteSpace(saveLocation))
            {
                folderPath = Path.GetDirectoryName(saveLocation);
                if (!string.IsNullOrWhiteSpace(folderPath))
                    Directory.CreateDirectory(folderPath);
                path = saveLocation;
                filename = Path.GetFileName(saveLocation);
            }
            else
            {
                folderPath = Path.Combine(SHelper.DirectoryPath, "userdata", GetCurrentSaveFolderName(), "photo_player");
                Directory.CreateDirectory(folderPath);
                path = Path.Combine(folderPath, filename);
            }

            string resolvedLocationName = string.IsNullOrWhiteSpace(locationName)
                ? "UnknownLocation"
                : locationName;

            Texture2D? squareNormalizedTexture = TryNormalizeSquareCapture(capturedTexture, enforceSquareOutput);
            try
            {
                using Texture2D opaqueTexture = CreateOpaqueTexture(squareNormalizedTexture ?? capturedTexture);
                SaveCompressedJpeg(opaqueTexture, path, 256000); // 250KB limit
            }
            finally
            {
                squareNormalizedTexture?.Dispose();
            }

            string timeString = ModEntry.SHelper.Translation.Get("ui.camera.photo_time_format", new { shortDayName = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth), dayOfMonth = Game1.dayOfMonth, seasonName = Utility.getSeasonNameFromNumber(Utility.getSeasonNumber(Game1.currentSeason)), year = Game1.year, time = Game1.getTimeOfDayString(Game1.timeOfDay) });
            var tagList = (tags ?? Enumerable.Empty<string>()).ToList();

            if (string.IsNullOrWhiteSpace(saveLocation))
            {
                SetImageTags(filename, tagList, resolvedLocationName, timeString);
                EnforcePhotoRetention(folderPath, GetPhotoRetentionLimit(), path);
            }

            if (isPlayerPhoto && string.IsNullOrWhiteSpace(saveLocation))
                Game1.addHUDMessage(new HUDMessage(ModEntry.SHelper.Translation.Get("ui.camera.photo_saved"), HUDMessage.newQuest_type));

            var metadata = new Smartphone.Data.ImageMetadata
            {
                Location = resolvedLocationName,
                TimeString = timeString,
                Tag = string.Join(";", tagList)
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(metadata);
        }

        private static void SaveCompressedJpeg(Texture2D originalTexture, string path, int maxSizeBytes)
        {
            int currentWidth = originalTexture.Width;
            int currentHeight = originalTexture.Height;
            Color[] currentColors = new Color[currentWidth * currentHeight];
            originalTexture.GetData(currentColors);

            int quality = 100;
            byte[] currentData = EncodeJpegWithImageSharp(currentColors, currentWidth, currentHeight, quality);

            while (currentData.Length > maxSizeBytes && quality > 70)
            {
                quality -= 10;
                currentData = EncodeJpegWithImageSharp(currentColors, currentWidth, currentHeight, quality);
            }

            if (currentData.Length <= maxSizeBytes)
            {
                File.WriteAllBytes(path, currentData);
                return;
            }

            while (currentData.Length > maxSizeBytes && currentWidth > 16 && currentHeight > 16)
            {
                float scale = (float)Math.Sqrt((double)maxSizeBytes / currentData.Length) * 0.9f;
                int newWidth = Math.Max(1, (int)(currentWidth * scale));
                int newHeight = Math.Max(1, (int)(currentHeight * scale));

                Color[] newColors = new Color[newWidth * newHeight];
                for (int y = 0; y < newHeight; y++)
                {
                    int oldY = y * currentHeight / newHeight;
                    for (int x = 0; x < newWidth; x++)
                    {
                        int oldX = x * currentWidth / newWidth;
                        newColors[y * newWidth + x] = currentColors[oldY * currentWidth + oldX];
                    }
                }

                currentWidth = newWidth;
                currentHeight = newHeight;
                currentColors = newColors;

                currentData = EncodeJpegWithImageSharp(currentColors, currentWidth, currentHeight, quality);
            }

            File.WriteAllBytes(path, currentData);
        }

        private static byte[] EncodeJpegWithImageSharp(Color[] colors, int width, int height, int quality)
        {
            using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    var rowSpan = accessor.GetRowSpan(y);
                    for (int x = 0; x < width; x++)
                    {
                        var c = colors[y * width + x];
                        rowSpan[x] = new SixLabors.ImageSharp.PixelFormats.Rgba32(c.R, c.G, c.B, c.A);
                    }
                }
            });

            using var ms = new MemoryStream();
            image.Save(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = quality });
            return ms.ToArray();
        }

        private static Texture2D? TryNormalizeSquareCapture(Texture2D capturedTexture, bool enforceSquareOutput)
        {
            if (!enforceSquareOutput)
                return null;

            int width = capturedTexture.Width;
            int height = capturedTexture.Height;
            int difference = Math.Abs(width - height);
            if (difference == 0 || difference > SquareCaptureTolerancePixels)
                return null;

            int squareSize = Math.Max(width, height);
            Color[] sourceData = new Color[width * height];
            Color[] squareData = new Color[squareSize * squareSize];
            capturedTexture.GetData(sourceData);

            // Back-buffer rounding can skew an intended square capture by a pixel or two.
            for (int y = 0; y < squareSize; y++)
            {
                int sourceY = MapResizeCoordinate(y, height, squareSize);
                int sourceRow = sourceY * width;
                int destinationRow = y * squareSize;

                for (int x = 0; x < squareSize; x++)
                {
                    int sourceX = MapResizeCoordinate(x, width, squareSize);
                    squareData[destinationRow + x] = sourceData[sourceRow + sourceX];
                }
            }

            Texture2D squareTexture = new Texture2D(capturedTexture.GraphicsDevice, squareSize, squareSize);
            squareTexture.SetData(squareData);
            return squareTexture;
        }

        private static int MapResizeCoordinate(int destinationCoordinate, int sourceLength, int destinationLength)
        {
            if (sourceLength <= 1 || destinationLength <= 1)
                return 0;

            float scale = (sourceLength - 1f) / (destinationLength - 1f);
            return Math.Clamp((int)Math.Round(destinationCoordinate * scale), 0, sourceLength - 1);
        }

        private static int GetPhotoRetentionLimit()
        {
            int configuredLimit = Config?.PlayerMaxPhoto ?? 100;
            return Math.Clamp(configuredLimit, 1, 500);
        }

        private static void EnforcePhotoRetention(string folderPath, int maxPhotos, string keepPhotoPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                    return;

                int safeLimit = Math.Clamp(maxPhotos, 1, 500);
                string[] photoPaths = Directory.GetFiles(folderPath)
                    .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (photoPaths.Length <= safeLimit)
                    return;

                int overflow = photoPaths.Length - safeLimit;
                var deletedPhotoNames = new List<string>();

                foreach (string photoPath in photoPaths
                             .OrderBy(path => File.GetLastWriteTimeUtc(path))
                             .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    if (deletedPhotoNames.Count >= overflow)
                        break;

                    if (!string.IsNullOrWhiteSpace(keepPhotoPath)
                        && string.Equals(photoPath, keepPhotoPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(photoPath);
                        string? deletedPhotoName = Path.GetFileName(photoPath);
                        if (!string.IsNullOrWhiteSpace(deletedPhotoName))
                            deletedPhotoNames.Add(deletedPhotoName);
                    }
                    catch (Exception ex)
                    {
                        SMonitor.Log($"Failed to delete old photo '{photoPath}': {ex}", LogLevel.Warn);
                    }
                }

                RemoveImageTagsForDeletedPhotos(deletedPhotoNames);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed to enforce photo retention for '{folderPath}': {ex}", LogLevel.Warn);
            }
        }

        private static void RemoveImageTagsForDeletedPhotos(IEnumerable<string> deletedPhotoNames)
        {
            bool hasChanges = false;

            foreach (string photoName in deletedPhotoNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(photoName))
                    continue;

                if (ImageTags.Remove(photoName))
                    hasChanges = true;
            }

            if (hasChanges)
                SaveImageTags();
        }

        private static xTile.Dimensions.Rectangle BuildNpcCaptureViewport(GameLocation location, Vector2 captureCenter, int captureWidth, int captureHeight)
        {
            object? map = GetMemberValue(location, "Map", "map");

            int mapWidth = map == null ? -1 : TryReadIntMember(map, "DisplayWidth", "displayWidth");
            int mapHeight = map == null ? -1 : TryReadIntMember(map, "DisplayHeight", "displayHeight");

            mapWidth = Math.Max(captureWidth, mapWidth);
            mapHeight = Math.Max(captureHeight, mapHeight);

            Vector2 pixelCenter = (captureCenter * Game1.tileSize) + new Vector2(Game1.tileSize / 2f, Game1.tileSize / 2f);
            int viewX = (int)Math.Round(pixelCenter.X) - (captureWidth / 2);
            int viewY = (int)Math.Round(pixelCenter.Y) - (captureHeight / 2);

            viewX = Math.Clamp(viewX, 0, Math.Max(0, mapWidth - captureWidth));
            viewY = Math.Clamp(viewY, 0, Math.Max(0, mapHeight - captureHeight));

            return new xTile.Dimensions.Rectangle(viewX, viewY, captureWidth, captureHeight);
        }

        private sealed class PhotoRenderStateSnapshot
        {
            private readonly xTile.Dimensions.Rectangle viewport;
            private readonly Viewport graphicsViewport;
            private readonly GameLocation currentLocation;
            private readonly int timeOfDay;
            private readonly Color ambientLight;
            private readonly Color outdoorLight;
            private readonly bool drawLighting;
            private readonly string? viewingLocation;
            private readonly bool screenGlow;
            private readonly bool screenGlowHold;
            private readonly bool screenGlowUp;
            private readonly float screenGlowAlpha;
            private readonly float screenGlowRate;
            private readonly float screenGlowMax;
            private readonly Color screenGlowColor;
            private readonly RenderTarget2D? lightmap;
            private readonly Dictionary<string, LightSource> lightSources = new Dictionary<string, LightSource>(StringComparer.Ordinal);

            public PhotoRenderStateSnapshot()
            {
                viewport = Game1.viewport;
                graphicsViewport = Game1.graphics.GraphicsDevice.Viewport;
                currentLocation = Game1.currentLocation;
                timeOfDay = Game1.timeOfDay;
                ambientLight = Game1.ambientLight;
                outdoorLight = Game1.outdoorLight;
                drawLighting = Game1.drawLighting;
                viewingLocation = Game1.player?.viewingLocation?.Value;
                screenGlow = Game1.screenGlow;
                screenGlowHold = Game1.screenGlowHold;
                screenGlowUp = Game1.screenGlowUp;
                screenGlowAlpha = Game1.screenGlowAlpha;
                screenGlowRate = Game1.screenGlowRate;
                screenGlowMax = Game1.screenGlowMax;
                screenGlowColor = Game1.screenGlowColor;
                lightmap = Game1.lightmap;

                foreach (KeyValuePair<string, LightSource> lightSource in Game1.currentLightSources)
                    lightSources[lightSource.Key] = lightSource.Value;
            }

            public void Restore()
            {
                RenderTarget2D? captureLightmap = Game1.lightmap;
                if (!ReferenceEquals(captureLightmap, lightmap))
                {
                    if (TrySetStaticMemberValue(typeof(Game1), lightmap, "_lightmap") && captureLightmap != null)
                        captureLightmap.Dispose();
                }

                Game1.graphics.GraphicsDevice.Viewport = graphicsViewport;
                Game1.viewport = viewport;
                Game1.currentLocation = currentLocation;
                Game1.timeOfDay = timeOfDay;
                Game1.ambientLight = ambientLight;
                Game1.outdoorLight = outdoorLight;
                Game1.drawLighting = drawLighting;
                if (Game1.player?.viewingLocation != null)
                    Game1.player.viewingLocation.Value = viewingLocation;
                Game1.screenGlow = screenGlow;
                Game1.screenGlowHold = screenGlowHold;
                Game1.screenGlowUp = screenGlowUp;
                Game1.screenGlowAlpha = screenGlowAlpha;
                Game1.screenGlowRate = screenGlowRate;
                Game1.screenGlowMax = screenGlowMax;
                Game1.screenGlowColor = screenGlowColor;

                Game1.currentLightSources.Clear();
                foreach (KeyValuePair<string, LightSource> lightSource in lightSources)
                    Game1.currentLightSources[lightSource.Key] = lightSource.Value;
            }
        }

        private sealed class CaptureMapAppearanceSnapshot
        {
            private readonly GameLocation location;
            private readonly List<CaptureTileState> tileStates = new List<CaptureTileState>();
            private readonly List<Vector2> lightGlows = new List<Vector2>();

            public CaptureMapAppearanceSnapshot(GameLocation location)
            {
                this.location = location;

                var savedTiles = new HashSet<string>(StringComparer.Ordinal);
                CaptureTileStates("DayTiles", savedTiles);
                CaptureTileStates("NightTiles", savedTiles);

                foreach (Vector2 lightGlow in location.lightGlows)
                    lightGlows.Add(lightGlow);
            }

            public void Restore()
            {
                foreach (CaptureTileState tileState in tileStates)
                {
                    xTile.Tiles.Tile? tile = location.Map.RequireLayer(tileState.LayerId).Tiles[tileState.Position.X, tileState.Position.Y];
                    if (tile != null)
                        tile.TileIndex = tileState.TileIndex;
                }

                location.lightGlows.Clear();
                foreach (Vector2 lightGlow in lightGlows)
                    location.lightGlows.Add(lightGlow);
            }

            private void CaptureTileStates(string propertyName, HashSet<string> savedTiles)
            {
                string[] propertyValues = location.GetMapPropertySplitBySpaces(propertyName);
                for (int i = 0; i + 3 < propertyValues.Length; i += 4)
                {
                    string layerId = propertyValues[i];
                    if (!int.TryParse(propertyValues[i + 1], out int tileX)
                        || !int.TryParse(propertyValues[i + 2], out int tileY))
                    {
                        continue;
                    }

                    string tileKey = $"{layerId}\u001f{tileX}\u001f{tileY}";
                    if (!savedTiles.Add(tileKey))
                        continue;

                    xTile.Tiles.Tile? tile = location.Map.RequireLayer(layerId).Tiles[tileX, tileY];
                    if (tile != null)
                        tileStates.Add(new CaptureTileState(layerId, new Point(tileX, tileY), tile.TileIndex));
                }
            }

            private readonly struct CaptureTileState
            {
                public CaptureTileState(string layerId, Point position, int tileIndex)
                {
                    LayerId = layerId;
                    Position = position;
                    TileIndex = tileIndex;
                }

                public string LayerId { get; }

                public Point Position { get; }

                public int TileIndex { get; }
            }
        }

        private static CaptureMapAppearanceSnapshot PrepareLocationRenderState(GameLocation targetLocation, int captureWidth, int captureHeight)
        {
            SetViewingLocationForCapture(targetLocation);
            targetLocation.setUpLocationSpecificFlair();
            CaptureMapAppearanceSnapshot mapAppearanceSnapshot = ApplyMapAppearanceForCapture(targetLocation);
            AllocateLightmapForCapture(captureWidth, captureHeight);

            Game1.currentLightSources.Clear();

            RefreshAmbientLightForCapture(targetLocation);
            RefreshOutdoorLightForCapture(targetLocation);
            string lightIdPrefix = $"{targetLocation.NameOrUniqueName}_MapLight_";
            AddSharedLightsForCapture(targetLocation, lightIdPrefix);
            if (!targetLocation.ignoreLights.Value)
                AddMapLightsForCapture(targetLocation, lightIdPrefix);
            RefreshDrawLightingForCapture(targetLocation);
            return mapAppearanceSnapshot;
        }

        private static CaptureMapAppearanceSnapshot ApplyMapAppearanceForCapture(GameLocation targetLocation)
        {
            CaptureMapAppearanceSnapshot snapshot = new CaptureMapAppearanceSnapshot(targetLocation);

            int nightTileTime = Game1.getTrulyDarkTime(targetLocation) - 100;
            bool useDayTiles = Game1.timeOfDay < nightTileTime
                && (!targetLocation.IsRainingHere() || string.Equals(targetLocation.Name, "SandyHouse", StringComparison.Ordinal));

            if (useDayTiles)
                targetLocation.addLightGlows();
            else
                targetLocation.switchOutNightTiles();

            return snapshot;
        }

        private static void SetViewingLocationForCapture(GameLocation targetLocation)
        {
            if (Game1.player?.viewingLocation == null)
                return;

            string targetViewName = string.IsNullOrWhiteSpace(targetLocation.Name)
                ? targetLocation.NameOrUniqueName
                : targetLocation.Name;
            Game1.player.viewingLocation.Value = targetViewName;
        }

        private static void RefreshAmbientLightForCapture(GameLocation targetLocation)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo? updateAmbientLighting = typeof(GameLocation).GetMethod("_updateAmbientLighting", flags);
            if (updateAmbientLighting != null)
            {
                updateAmbientLighting.Invoke(targetLocation, null);
                return;
            }

            if (targetLocation.IsOutdoors && !targetLocation.ignoreOutdoorLighting.Value)
                Game1.ambientLight = targetLocation.IsRainingHere() ? new Color(255, 200, 80) : Color.White;
        }

        private static void RefreshOutdoorLightForCapture(GameLocation targetLocation)
        {
            if (Game1.timeOfDay >= Game1.getTrulyDarkTime(targetLocation))
            {
                int interpolatedTime = (int)((float)(Game1.timeOfDay - Game1.timeOfDay % 100) + ((Game1.timeOfDay % 100) / 10f) * 16.66f);
                float darkness = Math.Min(0.93f, 0.75f + ((interpolatedTime - Game1.getTrulyDarkTime(targetLocation)) + ((float)Game1.gameTimeInterval / Game1.realMilliSecondsPerGameTenMinutes * 16.6f)) * 0.000625f);
                Game1.outdoorLight = (targetLocation.IsRainingHere() ? Game1.ambientLight : Game1.eveningColor) * darkness;
                return;
            }

            if (Game1.timeOfDay >= Game1.getStartingToGetDarkTime(targetLocation))
            {
                int interpolatedTime = (int)((float)(Game1.timeOfDay - Game1.timeOfDay % 100) + ((Game1.timeOfDay % 100) / 10f) * 16.66f);
                float darkness = Math.Min(0.93f, 0.3f + ((interpolatedTime - Game1.getStartingToGetDarkTime(targetLocation)) + ((float)Game1.gameTimeInterval / Game1.realMilliSecondsPerGameTenMinutes * 16.6f)) * 0.00225f);
                Game1.outdoorLight = (targetLocation.IsRainingHere() ? Game1.ambientLight : Game1.eveningColor) * darkness;
                return;
            }

            Game1.outdoorLight = targetLocation.IsRainingHere()
                ? Game1.ambientLight * 0.3f
                : Game1.ambientLight;
        }

        private static void RefreshDrawLightingForCapture(GameLocation targetLocation)
        {
            bool shouldDrawLighting = (targetLocation.IsOutdoors && !Game1.outdoorLight.Equals(Color.White))
                || !Game1.ambientLight.Equals(Color.White);

            if (targetLocation is MineShaft mineShaft && !mineShaft.getLightingColor(Game1.currentGameTime).Equals(Color.White))
                shouldDrawLighting = true;

            if (Game1.player.hasBuff("26"))
                shouldDrawLighting = true;

            Game1.drawLighting = shouldDrawLighting;
        }

        private static void AllocateLightmapForCapture(int captureWidth, int captureHeight)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo? allocateLightmap = typeof(Game1).GetMethod("allocateLightmap", flags);
            if (allocateLightmap == null)
                return;

            if (Game1.lightmap != null && !TrySetStaticMemberValue(typeof(Game1), null, "_lightmap"))
                return;

            allocateLightmap.Invoke(null, new object[] { captureWidth, captureHeight });
        }

        private static void RestoreTemporarySpritesAfterCapture(GameLocation targetLocation, int temporarySpriteCount)
        {
            while (targetLocation.temporarySprites.Count > temporarySpriteCount)
                targetLocation.temporarySprites.RemoveAt(targetLocation.temporarySprites.Count - 1);
        }

        private static void AddMapLightsForCapture(GameLocation targetLocation, string lightIdPrefix)
        {
            AddMapPropertyLights(targetLocation, lightIdPrefix, "Light", LightSource.LightContext.MapLight);

            if (!Game1.isTimeToTurnOffLighting(targetLocation) && !targetLocation.IsRainingHere())
            {
                AddMapPropertyLights(targetLocation, lightIdPrefix, "WindowLight", LightSource.LightContext.WindowLight);

                foreach (Vector2 lightGlow in targetLocation.lightGlows)
                {
                    Game1.currentLightSources.Add(new LightSource(
                        $"{lightIdPrefix}_{lightGlow.X}_{lightGlow.Y}_Glow",
                        6,
                        lightGlow,
                        1f,
                        LightSource.LightContext.WindowLight,
                        0L,
                        targetLocation.NameOrUniqueName));
                }
            }
        }

        private static void AddMapPropertyLights(GameLocation targetLocation, string lightIdPrefix, string propertyName, LightSource.LightContext context)
        {
            string[] propertyValues = targetLocation.GetMapPropertySplitBySpaces(propertyName);
            for (int i = 0; i + 2 < propertyValues.Length; i += 3)
            {
                if (!int.TryParse(propertyValues[i], out int tileX)
                    || !int.TryParse(propertyValues[i + 1], out int tileY)
                    || !int.TryParse(propertyValues[i + 2], out int textureIndex))
                {
                    continue;
                }

                Vector2 position = new Vector2((tileX * 64) + 32, (tileY * 64) + 32);
                string suffix = context == LightSource.LightContext.WindowLight ? "_Window" : string.Empty;

                Game1.currentLightSources.Add(new LightSource(
                    $"{lightIdPrefix}_{tileX}_{tileY}{suffix}",
                    textureIndex,
                    position,
                    1f,
                    context,
                    0L,
                    targetLocation.NameOrUniqueName));
            }
        }

        private static void AddSharedLightsForCapture(GameLocation targetLocation, string lightIdPrefix)
        {
            foreach (KeyValuePair<string, LightSource> sharedLight in targetLocation.sharedLights.Pairs)
            {
                if (sharedLight.Key.StartsWith(lightIdPrefix, StringComparison.Ordinal))
                    continue;

                Game1.currentLightSources[sharedLight.Key] = sharedLight.Value;
            }
        }

        private static Texture2D CreateOpaqueTexture(Texture2D source)
        {
            Color[] rawData = new Color[source.Width * source.Height];
            source.GetData(rawData);

            for (int i = 0; i < rawData.Length; i++)
            {
                Color pixel = rawData[i];
                if (pixel.A != byte.MaxValue)
                    rawData[i] = new Color(pixel.R, pixel.G, pixel.B, byte.MaxValue);
            }

            Texture2D opaqueTexture = new Texture2D(source.GraphicsDevice, source.Width, source.Height);
            opaqueTexture.SetData(rawData);
            return opaqueTexture;
        }
    }
}
