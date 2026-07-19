using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Smartphone
{
    public partial class ModEntry
    {
        private const int HudPhoneMaxHeight = 150;
        private const int HudPhoneMinHeight = 96;
        private const int HudPhoneRightMargin = 18;
        private const int HudPhoneTopMargin = 12;
        private const int HudPhoneBottomMargin = 12;
        private const int HudPhoneAboveEnergyOffset = 188;
        private const int HudPhoneBadgeMinimumSize = 20;
        private const int HudPhoneFrameContentOffsetX = 90;
        private const int HudPhoneFrameContentOffsetY = 166;
        
        private bool isDraggingHudIcon = false;
        private int dragStartMouseX;
        private int dragStartMouseY;
        private int dragStartOffsetX;
        private int dragStartOffsetY;
        private bool hasDraggedHudIcon = false;
        private bool isDraggingHudSlider = false;
        private RenderTarget2D? hudPhoneRenderTarget;

        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!ShouldDrawHudPhoneIcon())
                return;

            DrawHudPhoneIcon(e.SpriteBatch);
        }

        private static bool ShouldDrawHudPhoneIcon()
        {
            return Config.ShowPhoneIcon
                && Context.IsWorldReady
                && Game1.displayHUD
                && Game1.activeClickableMenu == null
                && Game1.currentMinigame == null
                && !Game1.game1.takingMapScreenshot;
        }

        private Microsoft.Xna.Framework.Rectangle GetHudPhoneIconBounds(bool isLandscape = false)
        {
            Texture2D? frameTexture = Textures.PhoneEmpty;
            if (frameTexture == null || frameTexture.IsDisposed)
                return Microsoft.Xna.Framework.Rectangle.Empty;

            int viewportWidth = Math.Max(1, Game1.uiViewport.Width);
            int viewportHeight = Math.Max(1, Game1.uiViewport.Height);

            // Swap dimensions if landscape
            int textureWidth = isLandscape ? frameTexture.Height : frameTexture.Width;
            int textureHeight = isLandscape ? frameTexture.Width : frameTexture.Height;

            int baseIconHeight = Math.Clamp(viewportHeight / 7, HudPhoneMinHeight, HudPhoneMaxHeight);
            int baseIconWidth = Math.Max(1, (int)Math.Round(textureWidth * (baseIconHeight / (float)Math.Max(1, textureHeight))));

            int defaultX = viewportWidth - baseIconWidth - HudPhoneRightMargin;
            int aboveEnergyOffset = Math.Max(HudPhoneAboveEnergyOffset, viewportHeight / 5);
            int defaultY = viewportHeight - baseIconHeight - aboveEnergyOffset;

            int configuredOffsetX = Math.Clamp(Config?.HudPhoneIconOffsetX ?? 0, -50000, 50000);
            int configuredOffsetY = Math.Clamp(Config?.HudPhoneIconOffsetY ?? 0, -50000, 50000);

            int centerX = defaultX + baseIconWidth / 2 + configuredOffsetX;
            int centerY = defaultY + baseIconHeight / 2 + configuredOffsetY;

            float scale = Config?.HudPhoneIconScale ?? 1f;
            int iconHeight = Math.Max(1, (int)Math.Round(baseIconHeight * scale));
            int iconWidth = Math.Max(1, (int)Math.Round(baseIconWidth * scale));

            int x = centerX - iconWidth / 2;
            int y = centerY - iconHeight / 2;

            int clampedX = Math.Clamp(x, HudPhoneTopMargin, Math.Max(HudPhoneTopMargin, viewportWidth - iconWidth - HudPhoneTopMargin));
            int clampedY = Math.Clamp(y, HudPhoneTopMargin, Math.Max(HudPhoneTopMargin, viewportHeight - iconHeight - HudPhoneBottomMargin));

            // Adjust in-memory config offsets if they were clamped during active dragging to avoid dead-zones
            if (isDraggingHudIcon && Config != null)
            {
                int newOffsetX = clampedX + iconWidth / 2 - defaultX - baseIconWidth / 2;
                int newOffsetY = clampedY + iconHeight / 2 - defaultY - baseIconHeight / 2;

                if (Config.HudPhoneIconOffsetX != newOffsetX || Config.HudPhoneIconOffsetY != newOffsetY)
                {
                    Config.HudPhoneIconOffsetX = newOffsetX;
                    Config.HudPhoneIconOffsetY = newOffsetY;

                    // Reset drag baseline coordinates so the relative drag remains in sync
                    dragStartOffsetX = newOffsetX;
                    dragStartOffsetY = newOffsetY;
                    dragStartMouseX = Game1.getMouseX(true);
                    dragStartMouseY = Game1.getMouseY(true);
                }
            }

            return new Microsoft.Xna.Framework.Rectangle(clampedX, clampedY, iconWidth, iconHeight);
        }

        private void DrawHudPhoneIcon(SpriteBatch spriteBatch)
        {
            Texture2D? frameTexture = Textures.PhoneEmpty;
            if (frameTexture == null || frameTexture.IsDisposed)
                return;

            // Is the active screen landscape?
            bool isLandscape = false;
            if (isHudPinned && ActiveExternalAppId != null && RegisteredPhoneApps.TryGetValue(ActiveExternalAppId, out var extAppLand))
            {
                isLandscape = extAppLand.Landscape;
            }
            else if (!isHudPinned && Config != null && !Config.PreferPortraitIconHud)
            {
                isLandscape = true;
            }

            Microsoft.Xna.Framework.Rectangle iconBounds = GetHudPhoneIconBounds(isLandscape);
            if (iconBounds.Width <= 0 || iconBounds.Height <= 0)
                return;

            // Ensure phone menu instance is initialized and scale-synced
            EnsurePhoneMenuUsesCurrentScale();

            int targetWidth = isLandscape ? 854 : 520;
            int targetHeight = isLandscape ? 520 : 854;
            float iconScale = isLandscape
                ? iconBounds.Height / (float)Math.Max(1, frameTexture.Width)
                : iconBounds.Height / (float)Math.Max(1, frameTexture.Height);

            Microsoft.Xna.Framework.Rectangle contentBounds;
            if (isLandscape)
            {
                contentBounds = new Microsoft.Xna.Framework.Rectangle(
                    iconBounds.X + (int)Math.Round(HudPhoneFrameContentOffsetY * iconScale),
                    iconBounds.Y + (int)Math.Round(HudPhoneFrameContentOffsetX * iconScale),
                    Math.Max(1, (int)Math.Round(targetWidth * iconScale)),
                    Math.Max(1, (int)Math.Round(targetHeight * iconScale))
                );
            }
            else
            {
                contentBounds = new Microsoft.Xna.Framework.Rectangle(
                    iconBounds.X + (int)Math.Round(HudPhoneFrameContentOffsetX * iconScale),
                    iconBounds.Y + (int)Math.Round(HudPhoneFrameContentOffsetY * iconScale),
                    Math.Max(1, (int)Math.Round(targetWidth * iconScale)),
                    Math.Max(1, (int)Math.Round(targetHeight * iconScale))
                );
            }

            if (phoneMenu != null)
            {
                // Verify/allocate RenderTarget
                if (hudPhoneRenderTarget == null || hudPhoneRenderTarget.Width != targetWidth || hudPhoneRenderTarget.Height != targetHeight)
                {
                    hudPhoneRenderTarget?.Dispose();
                    hudPhoneRenderTarget = new RenderTarget2D(
                        Game1.graphics.GraphicsDevice,
                        targetWidth,
                        targetHeight,
                        false,
                        SurfaceFormat.Color,
                        DepthFormat.None,
                        0,
                        RenderTargetUsage.PreserveContents
                    );
                }

                // 1. End active HUD SpriteBatch to switch RenderTarget
                spriteBatch.End();

                // 2. Save current render target configuration
                var originalRenderTargets = Game1.graphics.GraphicsDevice.GetRenderTargets();

                // 3. Bind offscreen target and clear it
                Game1.graphics.GraphicsDevice.SetRenderTarget(hudPhoneRenderTarget);
                Game1.graphics.GraphicsDevice.Clear(Color.Transparent);

                // 4. Save phone menu state
                int oldX = phoneMenu.xPositionOnScreen;
                int oldY = phoneMenu.yPositionOnScreen;
                int oldWidth = phoneMenu.width;
                int oldHeight = phoneMenu.height;
                float oldScale = phoneMenu.phoneUiScale;
                bool oldAnimating = phoneMenu.lockScreenUnlockAnimating;
                float oldDragOffset = phoneMenu.lockScreenUnlockDragOffset;
                float oldScrollOffset = phoneMenu.lockScreenContentScrollOffset;
                PhoneMenu.RootLandingState oldState = phoneMenu.rootLandingState;

                try
                {
                    // Set phoneMenu to fit the RenderTarget exactly at 1.0f scale
                    if (isLandscape)
                    {
                        phoneMenu.xPositionOnScreen = -HudPhoneFrameContentOffsetY;
                        phoneMenu.yPositionOnScreen = -HudPhoneFrameContentOffsetX;
                        phoneMenu.width = PhoneFrameBaseHeight;
                        phoneMenu.height = PhoneFrameBaseWidth;
                    }
                    else
                    {
                        phoneMenu.xPositionOnScreen = -HudPhoneFrameContentOffsetX;
                        phoneMenu.yPositionOnScreen = -HudPhoneFrameContentOffsetY;
                        phoneMenu.width = PhoneFrameBaseWidth;
                        phoneMenu.height = PhoneFrameBaseHeight;
                    }
                    phoneMenu.phoneUiScale = 1.0f;

                    if (!isHudPinned)
                    {
                        phoneMenu.lockScreenUnlockAnimating = false;
                        phoneMenu.lockScreenUnlockDragOffset = 0f;
                        phoneMenu.lockScreenContentScrollOffset = 0f;
                        phoneMenu.rootLandingState = PhoneMenu.RootLandingState.LockScreen;
                    }

                    // 5. Draw screen content to off-screen buffer using standard clamp sampling
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                    if (isHudPinned)
                    {
                        if (ActiveExternalAppId != null && RegisteredPhoneApps.TryGetValue(ActiveExternalAppId, out var extApp) && extApp.OnDrawHudScreen != null)
                        {
                            extApp.OnDrawHudScreen(spriteBatch, new Rectangle(0, 0, targetWidth, targetHeight));
                        }
                        else
                        {
                            phoneMenu.DrawScreenContent(spriteBatch);
                        }
                    }
                    else
                    {
                        if (isLandscape)
                        {
                            phoneMenu.DrawLockScreenLandscapeScreen(spriteBatch, 0);
                        }
                        else
                        {
                            phoneMenu.DrawLockScreenScreen(spriteBatch, 0);
                        }
                    }
                    spriteBatch.End();
                }
                finally
                {
                    // Always restore the phone menu state
                    phoneMenu.xPositionOnScreen = oldX;
                    phoneMenu.yPositionOnScreen = oldY;
                    phoneMenu.width = oldWidth;
                    phoneMenu.height = oldHeight;
                    phoneMenu.phoneUiScale = oldScale;
                    phoneMenu.lockScreenUnlockAnimating = oldAnimating;
                    phoneMenu.lockScreenUnlockDragOffset = oldDragOffset;
                    phoneMenu.lockScreenContentScrollOffset = oldScrollOffset;
                    phoneMenu.rootLandingState = oldState;
                }

                // 6. Restore original screen/backbuffer render target
                Game1.graphics.GraphicsDevice.SetRenderTargets(originalRenderTargets);

                // 7. Restart SpriteBatch with Linear Filtering to draw the downscaled capture smoothly
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
                spriteBatch.Draw(hudPhoneRenderTarget, contentBounds, Color.White);
                spriteBatch.End();

                // 8. Restore normal PointClamp SpriteBatch for crisp bezel and HUD elements
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            }

            // Draw bezel frame on top of the rendered screen content
            if (isLandscape)
            {
                spriteBatch.Draw(
                    frameTexture,
                    new Vector2(iconBounds.X, iconBounds.Y + iconBounds.Height),
                    null,
                    Color.White,
                    -MathHelper.PiOver2,
                    Vector2.Zero,
                    iconScale,
                    SpriteEffects.None,
                    0f
                );
            }
            else
            {
                spriteBatch.Draw(frameTexture, iconBounds, Color.White);
            }

            if (iconBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
                DrawHudPhoneIconHoverOutline(spriteBatch, iconBounds);

            int sliderWidth = 120;
            int sliderHeight = 16;
            int sliderPadding = 8;
            int sliderX = iconBounds.Center.X - (sliderWidth / 2);
            int sliderY = (iconBounds.Bottom + sliderPadding + sliderHeight > Game1.uiViewport.Height)
                ? iconBounds.Top - sliderPadding - sliderHeight
                : iconBounds.Bottom + sliderPadding;

            Microsoft.Xna.Framework.Rectangle sliderBounds = new Microsoft.Xna.Framework.Rectangle(sliderX - 6, sliderY, sliderWidth + 12, sliderHeight);

            bool isHoveringIcon = iconBounds.Contains(Game1.getMouseX(true), Game1.getMouseY(true));
            bool isHoveringSlider = sliderBounds.Contains(Game1.getMouseX(true), Game1.getMouseY(true));
            bool showSlider = (isHoveringIcon || isHoveringSlider || isDraggingHudSlider) && !isDraggingHudIcon;

            if (showSlider)
            {
                // 1. Draw background panel (glassmorphism/semi-transparent)
                Microsoft.Xna.Framework.Rectangle bgRect = new Microsoft.Xna.Framework.Rectangle(sliderBounds.X, sliderBounds.Y, sliderBounds.Width, sliderBounds.Height);
                spriteBatch.Draw(Game1.staminaRect, bgRect, Color.Black * 0.6f);

                // Draw border for the panel
                Color borderColor = Color.White * 0.3f;
                spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(bgRect.X, bgRect.Y, bgRect.Width, 1), borderColor);
                spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(bgRect.X, bgRect.Bottom - 1, bgRect.Width, 1), borderColor);
                spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(bgRect.X, bgRect.Y, 1, bgRect.Height), borderColor);
                spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(bgRect.Right - 1, bgRect.Y, 1, bgRect.Height), borderColor);

                // 2. Draw track line
                Microsoft.Xna.Framework.Rectangle trackRect = new Microsoft.Xna.Framework.Rectangle(sliderX, sliderY + (sliderHeight / 2) - 1, sliderWidth, 2);
                spriteBatch.Draw(Game1.staminaRect, trackRect, Color.Gray * 0.8f);

                // 3. Draw knob
                float minScale = 1f;
                float maxScale = 6f;
                float currentScale = Config?.HudPhoneIconScale ?? 1f;
                float percent = Math.Clamp((currentScale - minScale) / (maxScale - minScale), 0f, 1f);

                int knobSize = 10;
                int knobX = sliderX + (int)Math.Round(percent * sliderWidth) - (knobSize / 2);
                int knobY = sliderY + (sliderHeight / 2) - (knobSize / 2);
                Microsoft.Xna.Framework.Rectangle knobRect = new Microsoft.Xna.Framework.Rectangle(knobX, knobY, knobSize, knobSize);

                bool isHoveringKnob = knobRect.Contains(Game1.getMouseX(true), Game1.getMouseY(true));
                Color knobColor = (isHoveringKnob || isDraggingHudSlider) ? Color.White : Color.LightGray;

                spriteBatch.Draw(Game1.staminaRect, knobRect, knobColor);
            }
        }

        private static void DrawHudPhoneIconHoverOutline(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle iconBounds)
        {
            Microsoft.Xna.Framework.Rectangle outlineBounds = new Microsoft.Xna.Framework.Rectangle(
                iconBounds.X - 2,
                iconBounds.Y - 2,
                iconBounds.Width + 4,
                iconBounds.Height + 4);

            Color outlineColor = Color.White * 0.65f;

            spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(outlineBounds.X, outlineBounds.Y, outlineBounds.Width, 2), outlineColor);
            spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(outlineBounds.X, outlineBounds.Bottom - 2, outlineBounds.Width, 2), outlineColor);
            spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(outlineBounds.X, outlineBounds.Y, 2, outlineBounds.Height), outlineColor);
            spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(outlineBounds.Right - 2, outlineBounds.Y, 2, outlineBounds.Height), outlineColor);
        }

        private void UpdateSliderDrag()
        {
            int viewportWidth = Math.Max(1, Game1.uiViewport.Width);
            int viewportHeight = Math.Max(1, Game1.uiViewport.Height);

            Texture2D? frameTexture = Textures.PhoneEmpty;
            if (frameTexture == null || frameTexture.IsDisposed)
                return;

            int baseIconHeight = Math.Clamp(viewportHeight / 7, HudPhoneMinHeight, HudPhoneMaxHeight);
            int baseIconWidth = Math.Max(1, (int)Math.Round(frameTexture.Width * (baseIconHeight / (float)Math.Max(1, frameTexture.Height))));
            int defaultX = viewportWidth - baseIconWidth - HudPhoneRightMargin;
            int centerX = defaultX + baseIconWidth / 2 + (Config?.HudPhoneIconOffsetX ?? 0);

            int sliderWidth = 120;
            int sliderX = centerX - (sliderWidth / 2);

            int currentMouseX = Game1.getMouseX(true);
            int relativeMouseX = currentMouseX - sliderX;
            float percent = Math.Clamp((float)relativeMouseX / sliderWidth, 0f, 1f);

            float minScale = 1f;
            float maxScale = 6f;
            float newScale = minScale + percent * (maxScale - minScale);
            newScale = MathF.Round(newScale, 2);

            if (Config != null)
            {
                Config.HudPhoneIconScale = newScale;
            }
        }

        internal bool HandleHudIconInteraction(ButtonPressedEventArgs e, bool canOpenPhoneMenu)
        {
            if (e.Button == SButton.MouseLeft
                && canOpenPhoneMenu
                && ShouldDrawHudPhoneIcon())
            {
                bool isLandscape = false;
                if (isHudPinned && ActiveExternalAppId != null && RegisteredPhoneApps.TryGetValue(ActiveExternalAppId, out var extApp))
                {
                    isLandscape = extApp.Landscape;
                }
                else if (!isHudPinned && Config != null && !Config.PreferPortraitIconHud)
                {
                    isLandscape = true;
                }

                var iconBounds = GetHudPhoneIconBounds(isLandscape);
                int sliderWidth = 120;
                int sliderHeight = 16;
                int sliderPadding = 8;

                int sliderX = iconBounds.Center.X - (sliderWidth / 2);
                int sliderY = (iconBounds.Bottom + sliderPadding + sliderHeight > Game1.uiViewport.Height)
                    ? iconBounds.Top - sliderPadding - sliderHeight
                    : iconBounds.Bottom + sliderPadding;

                Microsoft.Xna.Framework.Rectangle sliderBounds = new Microsoft.Xna.Framework.Rectangle(sliderX - 6, sliderY, sliderWidth + 12, sliderHeight);

                bool isHoveringIcon = iconBounds.Contains(Game1.getMouseX(true), Game1.getMouseY(true));
                bool isHoveringSlider = sliderBounds.Contains(Game1.getMouseX(true), Game1.getMouseY(true));
                bool showSlider = (isHoveringIcon || isHoveringSlider || isDraggingHudSlider) && !isDraggingHudIcon;

                if (showSlider && isHoveringSlider)
                {
                    isDraggingHudSlider = true;
                    Helper.Input.Suppress(SButton.MouseLeft);
                    UpdateSliderDrag();
                    return true;
                }
                else if (isHoveringIcon)
                {
                    isDraggingHudIcon = true;
                    dragStartMouseX = Game1.getMouseX(true);
                    dragStartMouseY = Game1.getMouseY(true);
                    dragStartOffsetX = Config.HudPhoneIconOffsetX;
                    dragStartOffsetY = Config.HudPhoneIconOffsetY;
                    hasDraggedHudIcon = false;
                    Helper.Input.Suppress(SButton.MouseLeft);
                    return true;
                }
            }
            return false;
        }

        internal void UpdateHudIconDragging()
        {
            if (Game1.activeClickableMenu == null)
            {
                isPhoneOpen = false;
            }

            if (isHudPinned && phoneMenu != null && !isPhoneOpen)
            {
                phoneMenu.update(Game1.currentGameTime);

                if (ActiveExternalAppId != null && RegisteredPhoneApps.TryGetValue(ActiveExternalAppId, out var extApp))
                {
                    extApp.OnUpdateHudScreen?.Invoke(Game1.currentGameTime);
                }
            }
            if (isDraggingHudIcon || isDraggingHudSlider)
            {
                bool isPhysicallyDown = Helper.Input.IsDown(SButton.MouseLeft) || Helper.Input.IsSuppressed(SButton.MouseLeft);

                if (!isPhysicallyDown)
                {
                    if (isDraggingHudIcon)
                    {
                        isDraggingHudIcon = false;
                        if (hasDraggedHudIcon)
                        {
                            Helper.WriteConfig(Config);
                        }
                        else
                        {
                            OpenPhoneFromHudTrigger();
                        }
                        hasDraggedHudIcon = false;
                    }
                    else if (isDraggingHudSlider)
                    {
                        isDraggingHudSlider = false;
                        Helper.WriteConfig(Config);
                    }
                    return;
                }
            }

            if (isDraggingHudIcon)
            {
                int currentMouseX = Game1.getMouseX(true);
                int currentMouseY = Game1.getMouseY(true);
                int deltaX = currentMouseX - dragStartMouseX;
                int deltaY = currentMouseY - dragStartMouseY;

                if (Math.Abs(deltaX) > 5 || Math.Abs(deltaY) > 5)
                {
                    hasDraggedHudIcon = true;
                }

                Config.HudPhoneIconOffsetX = dragStartOffsetX + deltaX;
                Config.HudPhoneIconOffsetY = dragStartOffsetY + deltaY;
            }
            else if (isDraggingHudSlider)
            {
                UpdateSliderDrag();
            }
        }
    }
}
