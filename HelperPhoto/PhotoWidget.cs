using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        // --- Widget Cache & Render Target Fields ---
        private static RenderTarget2D? photoWidgetRenderTarget = null;
        private static readonly Dictionary<string, Texture2D> photoWidgetTextureCache = new(StringComparer.OrdinalIgnoreCase);
        private static int photoWidgetLastCheckedDay = -1;
        private static double photoWidgetLastScanTime = 0;

        /// <summary>
        /// Main widget drawing entry point for the Photo App framework.
        /// Supports 2x2 (latest photo) and 4x4 (4 latest photos) widget sizes with hardware alpha corner masking.
        /// </summary>
        public void DrawPhotoWidget(SpriteBatch b, Rectangle rect, AppSize size)
        {
            try
            {
                // 1. Daily Performance Guard: Reset texture cache and tracking on day change
                if (Game1.dayOfMonth != photoWidgetLastCheckedDay)
                {
                    photoWidgetLastCheckedDay = Game1.dayOfMonth;
                    ClearPhotoWidgetTextureCache();
                    capturedImages = null; // Forces re-scanning directory safely
                }

                // 2. Periodic Sync: Re-scan directory folder every 3 seconds to capture newly taken photos instantly
                double currentTime = Game1.currentGameTime.TotalGameTime.TotalSeconds;
                if (currentTime - photoWidgetLastScanTime > 3.0 || capturedImages == null)
                {
                    photoWidgetLastScanTime = currentTime;
                    RefreshPhotoWidgetImagesList();
                }

                // 3. Delegate to size-specific drawing pipelines
                if (size == AppSize.Size2x2)
                {
                    DrawPhotoWidget2x2(b, rect);
                }
                else if (size == AppSize.Size4x4)
                {
                    DrawPhotoWidget4x4(b, rect);
                }
                else
                {
                    // Fallback to standard 1x1 icon sheet asset if an unhandled size scales through
                    Texture2D iconTex = Textures.GetAppTexture("builtin:photo", size);
                    if (iconTex != null)
                    {
                        b.Draw(iconTex, rect, Color.White);
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"Error rendering photo widget content: {ex.Message}", StardewModdingAPI.LogLevel.Trace);
            }
        }

        /// <summary>Renders the 2x2 widget displaying the single latest captured photo.</summary>
        private void DrawPhotoWidget2x2(SpriteBatch b, Rectangle rect)
        {
            Texture2D widgetTex = Textures.GetAppTexture("builtin:photo", AppSize.Size2x2);
            if (widgetTex == null) return;

            // ENHANCEMENT: If no photos exist, draw the white base template and overlay centered black text
            if (capturedImages == null || capturedImages.Count == 0)
            {
                b.Draw(widgetTex, rect, Color.White);
                DrawNoPhotoText(b, rect);
                return;
            }

            // Fetch the single newest photo path (located at the tail of the list)
            string latestPhotoPath = capturedImages[^1];
            Texture2D? photoTex = GetWidgetPhotoTexture(b.GraphicsDevice, latestPhotoPath);

            if (photoTex == null || photoTex.IsDisposed)
            {
                b.Draw(widgetTex, rect, Color.White);
                DrawNoPhotoText(b, rect);
                return;
            }

            // Render content using the alpha masking comopsiting loop
            RenderMaskedWidgetContent(b, rect, widgetTex, (sb, targetBounds) =>
            {
                DrawTextureAspectFilled(sb, photoTex, targetBounds);
            });
        }

        /// <summary>Renders the 4x4 widget displaying a clean 2x2 layout grid of the 4 latest captured photos.</summary>
        private void DrawPhotoWidget4x4(SpriteBatch b, Rectangle rect)
        {
            Texture2D widgetTex = Textures.GetAppTexture("builtin:photo", AppSize.Size4x4);
            if (widgetTex == null) return;

            // ENHANCEMENT: If no photos exist, draw the white base template and overlay centered black text
            if (capturedImages == null || capturedImages.Count == 0)
            {
                b.Draw(widgetTex, rect, Color.White);
                DrawNoPhotoText(b, rect);
                return;
            }

            // Gather up to the 4 latest photos (sorted from newest to oldest)
            List<string> latestPhotos = new();
            int startIdx = Math.Max(0, capturedImages.Count - 4);
            for (int i = capturedImages.Count - 1; i >= startIdx; i--)
            {
                latestPhotos.Add(capturedImages[i]);
            }

            // Render the grid elements bounded cleanly inside the alpha target silhouette
            RenderMaskedWidgetContent(b, rect, widgetTex, (sb, targetBounds) =>
            {
                int quadW = targetBounds.Width / 2;
                int quadH = targetBounds.Height / 2;

                // Define structural quadrants inside the render target buffer space
                Rectangle[] quads = new Rectangle[]
                {
                    new Rectangle(targetBounds.X, targetBounds.Y, quadW, quadH),                  // Top-Left (Newest)
                    new Rectangle(targetBounds.X + quadW, targetBounds.Y, quadW, quadH),          // Top-Right (2nd Newest)
                    new Rectangle(targetBounds.X, targetBounds.Y + quadH, quadW, quadH),          // Bottom-Left (3rd Newest)
                    new Rectangle(targetBounds.X + quadW, targetBounds.Y + quadH, quadW, quadH)   // Bottom-Right (4th Newest)
                };

                for (int i = 0; i < latestPhotos.Count; i++)
                {
                    Texture2D? photoTex = GetWidgetPhotoTexture(sb.GraphicsDevice, latestPhotos[i]);
                    if (photoTex != null && !photoTex.IsDisposed)
                    {
                        DrawTextureAspectFilled(sb, photoTex, quads[i]);
                    }
                }
            });
        }

        /// <summary>
        /// Combines multi-pass XNA BlendStates into a RenderTarget2D canvas.
        /// Extracts the background alpha corner shape from a white base image and crops custom photos cleanly.
        /// </summary>
        private void RenderMaskedWidgetContent(SpriteBatch b, Rectangle rect, Texture2D widgetTex, Action<SpriteBatch, Rectangle> drawContentAction)
        {
            int width = rect.Width;
            int height = rect.Height;

            // Allocation Guard: Setup or dynamically resize render buffer to eliminate draw-hitch memory lag
            EnsurePhotoWidgetRenderTarget(b.GraphicsDevice, width, height);

            // Stash game's active graphics target tree state and scissor bounds securely
            var originalRenderTargets = b.GraphicsDevice.GetRenderTargets();
            Rectangle originalScissorRect = b.GraphicsDevice.ScissorRectangle;

            // 1. End the parent layout's active batch before swapping RenderTargets
            b.End();

            // Set rendering path onto our local masking target
            b.GraphicsDevice.SetRenderTarget(photoWidgetRenderTarget);
            b.GraphicsDevice.Clear(Color.Transparent);

            // STAGE 1: Draw the base widget plate to establish the exact alpha channel mask boundaries
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
            b.Draw(widgetTex, new Rectangle(0, 0, width, height), Color.White);
            b.End();

            // STAGE 2: Composite the photos utilizing alpha destination attenuation (clips sharp corners perfectly)
            BlendState alphaMaskBlend = new BlendState
            {
                ColorSourceBlend = Blend.DestinationAlpha,
                ColorDestinationBlend = Blend.InverseSourceAlpha,
                AlphaSourceBlend = Blend.DestinationAlpha,
                AlphaDestinationBlend = Blend.InverseSourceAlpha
            };

            b.Begin(SpriteSortMode.Deferred, alphaMaskBlend, SamplerState.PointClamp, null, null);
            drawContentAction(b, new Rectangle(0, 0, width, height));
            b.End();

            // Restore game's primary menu viewport graphics handle and layout scissor dimensions
            b.GraphicsDevice.SetRenderTargets(originalRenderTargets);
            b.GraphicsDevice.ScissorRectangle = originalScissorRect;

            // 2. Restart the parent layout's batch using the phone's native scissor clipping rasterizer state
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, appLabelScissorRasterizer);

            // STAGE 3: Draw the completed smoothly corner-clipped composite widget onto the phone interface
            b.Draw(photoWidgetRenderTarget, rect, new Rectangle(0, 0, width, height), Color.White);
        }

        private static void DrawNoPhotoText(SpriteBatch b, Rectangle rect)
        {
            string message = ModEntry.SHelper.Translation.Get("ui.photo.no_photo_yet");
            float uiScale = ModEntry.GetActivePhoneUiScale();
            float scale = 1.0f * uiScale;
            Vector2 textSize = Game1.smallFont.MeasureString(message) * scale;

            Vector2 position = new Vector2(
                rect.X + (rect.Width - textSize.X) / 2f,
                rect.Y + (rect.Height - textSize.Y) / 2f
            );

            b.DrawString(Game1.smallFont, message, position, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
        }

        /// <summary>
        /// Draws a texture centered and cropped using aspect-fill logic via hardware source sampling.
        /// Guarantees that photos do not bleed outside their sub-quadrant cells inside multi-photo layouts.
        /// </summary>
        private static void DrawTextureAspectFilled(SpriteBatch b, Texture2D tex, Rectangle targetRect)
        {
            float texRatio = (float)tex.Width / tex.Height;
            float targetRatio = (float)targetRect.Width / targetRect.Height;

            Rectangle sourceRect;

            if (texRatio > targetRatio)
            {
                // Photo is wider than container: Crop horizontal edges symmetrically
                int cropWidth = (int)(tex.Height * targetRatio);
                int cropX = (tex.Width - cropWidth) / 2;
                sourceRect = new Rectangle(cropX, 0, cropWidth, tex.Height);
            }
            else
            {
                // Photo is taller than container: Crop top and bottom edges symmetrically
                int cropHeight = (int)(tex.Width / targetRatio);
                int cropY = (tex.Height - cropHeight) / 2;
                sourceRect = new Rectangle(0, cropY, tex.Width, cropHeight);
            }

            b.Draw(tex, targetRect, sourceRect, Color.White);
        }

        /// <summary>Maintains a single active render buffer instance to prevent garbage collector stuttering.</summary>
        private static void EnsurePhotoWidgetRenderTarget(GraphicsDevice gd, int width, int height)
        {
            if (photoWidgetRenderTarget == null || photoWidgetRenderTarget.IsDisposed || photoWidgetRenderTarget.Width < width || photoWidgetRenderTarget.Height < height)
            {
                photoWidgetRenderTarget?.Dispose();
                photoWidgetRenderTarget = new RenderTarget2D(
                    gd,
                    Math.Max(width, 1),
                    Math.Max(height, 1),
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.None
                );
            }
        }

        /// <summary>Loads or handles full-resolution textures from disk, caching handles to preserve gameplay performance.</summary>
        private static Texture2D? GetWidgetPhotoTexture(GraphicsDevice gd, string path)
        {
            if (photoWidgetTextureCache.TryGetValue(path, out var cachedTex) && cachedTex != null && !cachedTex.IsDisposed)
            {
                return cachedTex;
            }

            try
            {
                if (File.Exists(path))
                {
                    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    Texture2D loaded = Texture2D.FromStream(gd, stream);
                    photoWidgetTextureCache[path] = loaded;
                    return loaded;
                }
            }
            catch
            {
                // Fail silently to safeguard drawing cycles from file lock race conditions
            }
            return null;
        }

        /// <summary>Synchronizes captured paths list safely if the photo folder contents shift at runtime.</summary>
        private void RefreshPhotoWidgetImagesList()
        {
            try
            {
                capturedImages = GetAllPhotoFilePaths();
            }
            catch
            {
                if (capturedImages == null)
                {
                    capturedImages = new List<string>();
                }
            }
        }

        /// <summary>Cleans up graphics memory references safely on day changes.</summary>
        private static void ClearPhotoWidgetTextureCache()
        {
            foreach (var tex in photoWidgetTextureCache.Values)
            {
                if (tex != null && !tex.IsDisposed)
                    tex.Dispose();
            }
            photoWidgetTextureCache.Clear();
        }
    }
}