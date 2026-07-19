using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    /// <summary>
    /// Partial class implementation handling the lock screen rendering, OS initialization,
    /// unlock animation, and weather summary display on the lock screen.
    /// </summary>
    public partial class PhoneMenu
    {
        #region Lock Screen State Fields

        /// <summary>Whether the phone is currently playing the sliding transition unlock animation.</summary>
        internal bool lockScreenUnlockAnimating = false;

        /// <summary>Time elapsed since the start of the lock screen unlock animation, in seconds.</summary>
        private double lockScreenUnlockElapsedSeconds = 0d;

        /// <summary>Interactive touch/click boundaries for unlocking the phone screen.</summary>
        private Rectangle lockScreenTapBounds = Rectangle.Empty;

        /// <summary>Simulated operating system boot/initialization progress percentage (0-100).</summary>
        private int lockScreenInitializationProgressPercent = 0;

        /// <summary>Time elapsed since the start of the OS boot sequence, in seconds.</summary>
        private double lockScreenInitializationElapsedSeconds = 0d;

        /// <summary>Calculated timestamp for the next boot progress percentage increment.</summary>
        private double lockScreenInitializationNextTickSeconds = 0d;

        /// <summary>Soft corner cache for weather textures to match the UI style.</summary>
        private readonly Dictionary<(int IconIndex, float Scale), Texture2D> lockScreenWeatherIconSoftCache = new();

        internal float lockScreenUnlockDragOffset = 0f;
        internal float lockScreenContentScrollOffset = 0f;
        private float lockScreenContentScrollTarget = 0f;
        private float lockScreenStartScrollOffset = 0f;
        private readonly List<(Rectangle Bounds, int OriginalIndex)> lockScreenCardBounds = new();
        private Rectangle lockScreenClearNotificationsBounds = Rectangle.Empty;
        private Rectangle lockScreenPin1Bounds = Rectangle.Empty;
        private Rectangle lockScreenPin2Bounds = Rectangle.Empty;

        #endregion

        #region Lock Screen Constants

        private const double LockScreenInitializationDurationSeconds = 7d;
        private const double LockScreenInitializationProgressIntervalMinSeconds = 0.06d;
        private const double LockScreenInitializationProgressIntervalMaxSeconds = 0.45d;
        private const float LockScreenInitializationTitleTextScale = 1.08f;
        private const float LockScreenInitializationProgressTextScale = 1.22f;
        private const double LockScreenUnlockDurationSeconds = 0.24d;
        private const float LockScreenTimeTextScale = 1.45f;
        private const float LockScreenDateTextScale = 1.2f;
        private const float LockScreenHintTextScale = 1.05f;
        private const float LockScreenWeatherLabelTextScale = 0.95f * 0.75f;
        private const float LockScreenWeatherPanelTopSpacing = 24f;
        private const float LockScreenWeatherColumnHalfSpacing = 88f;
        private const float LockScreenWeatherIconScale = 4f;
        private const float LockScreenWeatherIconSoftCornerRadius = 6f;
        private const float LockScreenWeatherIconSoftCornerFeather = 2f;
        private const int LockScreenWeatherIconWidth = 12;
        private const int LockScreenWeatherIconHeight = 8;
        private const int LockScreenWeatherIconStartX = 317;
        private const int LockScreenWeatherIconStartY = 421;

        /// <summary>Source rectangle for the Stardew Valley 1.6 green rain weather icon.</summary>
        private static readonly Rectangle LockScreenGreenRainWeatherIconSource = new Rectangle(243, 293, 12, 8);

        #endregion

        #region Lock Screen Drawing Methods

        /// <summary>
        /// Draws the main lock screen containing the current time, date, weather, and unlock hint.
        /// </summary>
        /// <param name="b">The active SpriteBatch to draw with.</param>
        /// <param name="xOffset">Horizontal offset used for screen transition animations.</param>
        internal void DrawLockScreenScreen(SpriteBatch b, int xOffset)
        {
            Rectangle contentBounds = GetPhoneContentBounds();
            lockScreenTapBounds = contentBounds;
            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
                return;

            DrawWithinPhoneContentClip(b, () =>
            {
                int mouseX = Game1.getMouseX();
                int mouseY = Game1.getMouseY();
                float verticalUnlockOffset;
                if (lockScreenUnlockAnimating)
                {
                    verticalUnlockOffset = MathHelper.Lerp(lockScreenUnlockDragOffset, contentBounds.Height, GetLockScreenUnlockProgress());
                }
                else
                {
                    verticalUnlockOffset = lockScreenUnlockDragOffset;
                }

                // Draw background vertically shifted
                DrawLockScreenBackground(b, (int)Math.Round(verticalUnlockOffset));

                float centerX = contentBounds.Center.X + xOffset;

                float scrollYOffset = lockScreenContentScrollOffset;
                float totalTopYOffset = scrollYOffset + verticalUnlockOffset;

                float timeScale = GetPhoneTextScale(LockScreenTimeTextScale);
                float dateScale = GetPhoneTextScale(LockScreenDateTextScale);
                float hintScale = GetPhoneTextScale(LockScreenHintTextScale);

                string timeText = Game1.getTimeOfDayString(Game1.timeOfDay);
                string dateText = BuildLockScreenDateText();

                Vector2 timeSize = Game1.dialogueFont.MeasureString(timeText) * timeScale;
                Vector2 dateSize = Game1.smallFont.MeasureString(dateText) * dateScale;

                bool isHudIconPreview = Game1.activeClickableMenu != this;
                bool disableHudNotifications = isHudIconPreview && (ModEntry.Config?.DisableNotificationOnPhoneIcon ?? false);

                // Use static Y calculations without offset first
                float staticTopY = contentBounds.Top + ScaleUiValue(82f);

                Vector2 staticTimePosition = new Vector2(centerX - (timeSize.X / 2f), staticTopY);
                Vector2 staticDatePosition = new Vector2(centerX - (dateSize.X / 2f), staticTopY + timeSize.Y + ScaleUiValue(10f));

                int footerHeight = ScaleUiValue(72);

                // Scissor test clip for scrollable elements (Time, Date, Weather, Notifications)
                b.End();

                int clipHeight = Math.Max(0, contentBounds.Height - footerHeight - ScaleUiValue(10) - (int)Math.Round(verticalUnlockOffset));
                Rectangle lockScreenClipRect = new Rectangle(
                    contentBounds.X,
                    contentBounds.Y,
                    contentBounds.Width,
                    clipHeight
                );

                Rectangle viewportBounds = Game1.graphics.GraphicsDevice.Viewport.Bounds;
                Rectangle intersectedClipRect = Rectangle.Intersect(lockScreenClipRect, viewportBounds);

                Rectangle previousScissorRect = Game1.graphics.GraphicsDevice.ScissorRectangle;
                Game1.graphics.GraphicsDevice.ScissorRectangle = intersectedClipRect;

                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

                // Draw Time, Date, Weather shifted by totalTopYOffset exactly once
                DrawShadowedText(
                    b,
                    Game1.dialogueFont,
                    timeText,
                    new Vector2(staticTimePosition.X, staticTimePosition.Y - totalTopYOffset),
                    Color.White,
                    new Color(0, 0, 0, 180),
                    timeScale);

                DrawShadowedText(
                    b,
                    Game1.smallFont,
                    dateText,
                    new Vector2(staticDatePosition.X, staticDatePosition.Y - totalTopYOffset),
                    Color.White,
                    new Color(0, 0, 0, 180),
                    dateScale);

                DrawLockScreenWeatherSummary(
                    b,
                    contentBounds,
                    centerX,
                    staticDatePosition.Y + dateSize.Y + ScaleUiValue(LockScreenWeatherPanelTopSpacing) - totalTopYOffset);

                if (!disableHudNotifications)
                {
                    // Draw active unread notifications
                    List<string> msgs = NotificationManager.GetNotificationList();
                    int unreadCount = NotificationManager.GetUnreadNotification();
                    int startIndex = Math.Max(0, msgs.Count - unreadCount);

                    int headerY = GetNotificationCenterStartY(staticDatePosition, dateSize);
                    int headerHeight = GetPhoneScaledLineHeight(Game1.smallFont, 1.15f);
                    int staticMessageY = headerY;

                    if (unreadCount > 0)
                    {
                        string headerText = ModEntry.SHelper.Translation.Get("ui.lockscreen.notification_center");
                        Vector2 headerTextSize = Game1.smallFont.MeasureString(headerText) * GetPhoneTextScale(1.15f);
                        Vector2 headerTextPos = new Vector2(contentBounds.X + ScaleUiValue(12), headerY - totalTopYOffset + (headerHeight - headerTextSize.Y) / 2f);

                        DrawShadowedText(
                            b,
                            Game1.smallFont,
                            headerText,
                            headerTextPos,
                            Color.White * 0.7f,
                            new Color(0, 0, 0, 150),
                            GetPhoneTextScale(1.15f)
                        );

                        string xText = ModEntry.SHelper.Translation.Get("ui.lockscreen.clear");
                        Vector2 xTextSize = Game1.smallFont.MeasureString(xText) * GetPhoneTextScale(0.85f) * phoneUiScale;
                        int xButtonWidth = (int)Math.Round(xTextSize.X + ScaleUiValue(16));
                        int xButtonHeight = (int)Math.Round(xTextSize.Y + ScaleUiValue(8));
                        int xButtonX = contentBounds.Right - ScaleUiValue(12) - xButtonWidth;
                        int xButtonY = headerY - (int)Math.Round(totalTopYOffset) + (headerHeight - xButtonHeight) / 2;

                        int drawnXButtonY = headerY - (int)Math.Round(scrollYOffset + verticalUnlockOffset) + (headerHeight - xButtonHeight) / 2;
                        lockScreenClearNotificationsBounds = new Rectangle(xButtonX, drawnXButtonY, xButtonWidth, xButtonHeight);

                        mouseX = Game1.getMouseX();
                        mouseY = Game1.getMouseY();
                        bool isHovered = new Rectangle(xButtonX, xButtonY, xButtonWidth, xButtonHeight).Contains(mouseX, mouseY);

                        Color xBgColor = isHovered ? new Color(100, 100, 100, 180) : new Color(60, 60, 60, 150);
                        Color xTextColor = isHovered ? Color.White : Color.White * 0.7f;

                        Textures.DrawCard(
                            b,
                            xButtonX,
                            xButtonY,
                            xButtonWidth,
                            xButtonHeight,
                            xBgColor,
                            1f,
                            false
                        );

                        Vector2 xPos = new Vector2(xButtonX + (xButtonWidth - xTextSize.X) / 2f, xButtonY + (xButtonHeight - xTextSize.Y) / 2f);
                        b.DrawString(Game1.smallFont, xText, xPos, xTextColor, 0f, Vector2.Zero, GetPhoneTextScale(0.85f) * phoneUiScale, SpriteEffects.None, 1f);

                        staticMessageY += headerHeight + ScaleUiValue(10);
                    }
                    else
                    {
                        lockScreenClearNotificationsBounds = Rectangle.Empty;
                    }

                    int cardSpacing = ScaleUiValue(12);

                    SpriteFont font = Game1.smallFont;
                    int titleLineHeight = GetPhoneScaledLineHeight(font, 0.85f);
                    int messageLineHeight = GetPhoneScaledLineHeight(font, 0.75f);
                    int wrapWidthBase = GetNotificationWrapWidthBase();
                    int cardWidth = contentBounds.Width - 2 * ScaleUiValue(10);
                    int cardX = contentBounds.X + ScaleUiValue(10);

                    lockScreenCardBounds.Clear();

                    for (int i = msgs.Count - 1; i >= startIndex; i--)
                    {
                        string rawMsg = msgs[i];
                        string msg = rawMsg;
                        string title = "";

                        if (rawMsg.Contains("::"))
                        {
                            var parts = rawMsg.Split(new[] { "::" }, 2, StringSplitOptions.None);
                            title = parts[0];
                            msg = parts[1];
                        }

                        int wrapWidth = (int)Math.Round(GetPhoneScaledWrapWidth(wrapWidthBase) / 0.8f);
                        var messageLines = SplitNotificationIntoLines(msg, font, wrapWidth);
                        if (messageLines.Count > 2)
                        {
                            messageLines = new List<string> { messageLines[0], messageLines[1] + "..." };
                        }

                        int cardHeight = GetLockScreenCardHeight(!string.IsNullOrEmpty(title), messageLines.Count, titleLineHeight, messageLineHeight);

                        int drawCardY = (int)Math.Round(staticMessageY - totalTopYOffset);
                        Rectangle cardBounds = new Rectangle(cardX, drawCardY, cardWidth, cardHeight);

                        if (!lockScreenUnlockAnimating)
                        {
                            lockScreenCardBounds.Add((cardBounds, i));
                        }

                        Textures.DrawCard(
                            b,
                            cardBounds.X,
                            cardBounds.Y,
                            cardBounds.Width,
                            cardBounds.Height,
                            Color.Silver,
                            1f,
                            false
                        );

                        int textY = cardBounds.Y + ScaleUiValue(12);
                        if (!string.IsNullOrEmpty(title))
                        {
                            Vector2 titlePos = new Vector2(cardBounds.X + ScaleUiValue(15), textY);
                            DrawPhoneText(b, font, title, titlePos, new Color(10, 25, 10), 0.85f);
                            textY += titleLineHeight + ScaleUiValue(4);
                        }

                        foreach (var line in messageLines)
                        {
                            Vector2 linePos = new Vector2(cardBounds.X + ScaleUiValue(15), textY);
                            DrawPhoneText(b, font, line, linePos, new Color(30, 45, 30), 0.8f);
                            textY += messageLineHeight;
                        }

                        staticMessageY += cardHeight + cardSpacing;
                    }
                }
                else
                {
                    lockScreenClearNotificationsBounds = Rectangle.Empty;
                    lockScreenCardBounds.Clear();
                }

                // Restore previous scissor clipping rectangle and end scissor test drawing
                b.End();
                Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissorRect;
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

                // Draw fixed footer text at bottom (no card background)
                int footerY = contentBounds.Bottom - footerHeight - (int)Math.Round(verticalUnlockOffset);
                Rectangle footerBounds = new Rectangle(contentBounds.X, footerY, contentBounds.Width, footerHeight);

                // Quick Launch Buttons
                int btnSize = ScaleUiValue(50);
                int btnY = footerBounds.Center.Y - btnSize / 2;
                int padX = ScaleUiValue(40);

                lockScreenPin1Bounds = new Rectangle(contentBounds.X + padX + xOffset, btnY, btnSize, btnSize);
                lockScreenPin2Bounds = new Rectangle(contentBounds.Right - padX - btnSize + xOffset, btnY, btnSize, btnSize);



                var allApps = BuildHomeAppsSnapshotPublic();

                if (IsLockScreenAppPinActive(ModEntry.lockScreenPin1))
                {
                    HomeAppEntryProxy? app1 = allApps.FirstOrDefault(a => string.Equals(a.Id, ModEntry.lockScreenPin1, StringComparison.OrdinalIgnoreCase));
                    if (app1 != null)
                    {
                        bool isHovered = lockScreenPin1Bounds.Contains(mouseX, mouseY);
                        Color bgColor = isHovered ? new Color(120, 120, 120, 180) : new Color(45, 45, 45, 140);
                        Textures.DrawCard(b, lockScreenPin1Bounds.X, lockScreenPin1Bounds.Y, lockScreenPin1Bounds.Width, lockScreenPin1Bounds.Height, bgColor, 1f, false);

                        int iconSize = ScaleUiValue(43);
                        Rectangle iconRect = new Rectangle(
                            lockScreenPin1Bounds.X + (lockScreenPin1Bounds.Width - iconSize) / 2,
                            lockScreenPin1Bounds.Y + (lockScreenPin1Bounds.Height - iconSize) / 2,
                            iconSize,
                            iconSize);

                        if (app1.IconTexture != null)
                        {
                            DrawLockScreenAppIcon(b, app1.IconTexture, iconRect, app1.SourceRect);
                        }
                    }
                }
                else
                {
                    lockScreenPin1Bounds = Rectangle.Empty;
                }

                if (IsLockScreenAppPinActive(ModEntry.lockScreenPin2))
                {
                    HomeAppEntryProxy? app2 = allApps.FirstOrDefault(a => string.Equals(a.Id, ModEntry.lockScreenPin2, StringComparison.OrdinalIgnoreCase));
                    if (app2 != null)
                    {
                        bool isHovered = lockScreenPin2Bounds.Contains(mouseX, mouseY);
                        Color bgColor = isHovered ? new Color(120, 120, 120, 180) : new Color(45, 45, 45, 140);
                        Textures.DrawCard(b, lockScreenPin2Bounds.X, lockScreenPin2Bounds.Y, lockScreenPin2Bounds.Width, lockScreenPin2Bounds.Height, bgColor, 1f, false);

                        int iconSize = ScaleUiValue(43);
                        Rectangle iconRect = new Rectangle(
                            lockScreenPin2Bounds.X + (lockScreenPin2Bounds.Width - iconSize) / 2,
                            lockScreenPin2Bounds.Y + (lockScreenPin2Bounds.Height - iconSize) / 2,
                            iconSize,
                            iconSize);

                        if (app2.IconTexture != null)
                        {
                            DrawLockScreenAppIcon(b, app2.IconTexture, iconRect, app2.SourceRect);
                        }
                    }
                }
                else
                {
                    lockScreenPin2Bounds = Rectangle.Empty;
                }

                bool showUpdateHint = ModEntry.hasNewVersionAvailable;
                string hintText = showUpdateHint
                    ? ModEntry.SHelper.Translation.Get("ui.lockscreen.update_available")
                    : ModEntry.SHelper.Translation.Get("ui.lockscreen.swipe_to_unlock");

                Vector2 hintSize = Game1.smallFont.MeasureString(hintText) * hintScale;
                Vector2 hintPosition = new Vector2(
                    centerX - (hintSize.X / 2f),
                    footerBounds.Center.Y - (hintSize.Y / 2f));

                if (showUpdateHint)
                {
                    Rectangle hintBoxBounds = new Rectangle(
                        (int)Math.Round(hintPosition.X - ScaleUiValue(22f)),
                        (int)Math.Round(hintPosition.Y - ScaleUiValue(8f)),
                        (int)Math.Round(hintSize.X + ScaleUiValue(44f)),
                        (int)Math.Round(hintSize.Y + ScaleUiValue(18f)));

                    Textures.DrawCard(
                        b,
                        hintBoxBounds.X,
                        hintBoxBounds.Y,
                        hintBoxBounds.Width,
                        hintBoxBounds.Height,
                        new Color(180, 36, 36, 165),
                        1f,
                        false);
                }

                DrawShadowedText(
                    b,
                    Game1.smallFont,
                    hintText,
                    hintPosition,
                    showUpdateHint ? Color.White : Color.White * 0.9f,
                    showUpdateHint ? new Color(32, 0, 0, 185) : new Color(0, 0, 0, 170),
                    hintScale);
            });
        }

        /// <summary>
        /// Draws the black screen and progress loader for the OS boot sequence.
        /// </summary>
        private void DrawLockScreenInitializationScreen(SpriteBatch b, int xOffset)
        {
            Rectangle contentBounds = GetPhoneContentBounds(xOffset);
            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
                return;

            DrawWithinPhoneContentClip(b, () =>
            {
                b.Draw(Game1.staminaRect, contentBounds, Color.Black);

                string titleText = ModEntry.SHelper.Translation.Get("ui.lockscreen.initializing");
                string progressText = $"{Math.Clamp(lockScreenInitializationProgressPercent, 0, 100)}%";

                Vector2 titleSize = Game1.dialogueFont.MeasureString(titleText) * LockScreenInitializationTitleTextScale;
                Vector2 progressSize = Game1.dialogueFont.MeasureString(progressText) * LockScreenInitializationProgressTextScale;

                float centerX = contentBounds.Center.X;
                float centerY = contentBounds.Center.Y;

                Vector2 titlePosition = new Vector2(centerX - (titleSize.X / 2f), centerY - titleSize.Y - 6f);
                Vector2 progressPosition = new Vector2(centerX - (progressSize.X / 2f), centerY + 10f);

                DrawShadowedText(
                    b,
                    Game1.dialogueFont,
                    titleText,
                    titlePosition,
                    Color.White,
                    new Color(0, 0, 0, 180),
                    LockScreenInitializationTitleTextScale);

                DrawShadowedText(
                    b,
                    Game1.dialogueFont,
                    progressText,
                    progressPosition,
                    new Color(215, 240, 255),
                    new Color(0, 0, 0, 185),
                    LockScreenInitializationProgressTextScale);
            });
        }

        /// <summary>
        /// Draws today's weather condition next to tomorrow's forecast on the lock screen.
        /// </summary>
        private void DrawLockScreenWeatherSummary(SpriteBatch b, Rectangle contentBounds, float centerX, float topY)
        {
            float weatherColumnHalfSpacing = ScaleUiValue(LockScreenWeatherColumnHalfSpacing);
            float leftCenterX = centerX - weatherColumnHalfSpacing;
            float rightCenterX = centerX + weatherColumnHalfSpacing;

            string todayLabel = ModEntry.SHelper.Translation.Get("ui.weather.today");
            string forecastLabel = ModEntry.SHelper.Translation.Get("ui.weather.forecast");

            float weatherLabelScale = GetPhoneTextScale(LockScreenWeatherLabelTextScale);
            Vector2 todayLabelSize = Game1.smallFont.MeasureString(todayLabel) * weatherLabelScale;
            Vector2 forecastLabelSize = Game1.smallFont.MeasureString(forecastLabel) * weatherLabelScale;

            Vector2 todayLabelPosition = new Vector2(leftCenterX - (todayLabelSize.X / 2f), topY);
            Vector2 forecastLabelPosition = new Vector2(rightCenterX - (forecastLabelSize.X / 2f), topY);

            DrawShadowedText(
                b,
                Game1.smallFont,
                todayLabel,
                todayLabelPosition,
                Color.White,
                new Color(0, 0, 0, 170),
                weatherLabelScale);

            DrawShadowedText(
                b,
                Game1.smallFont,
                forecastLabel,
                forecastLabelPosition,
                Color.White,
                new Color(0, 0, 0, 170),
                weatherLabelScale);

            int todayIconIndex = GetTodayWeatherIconIndex();
            int forecastIconIndex = GetForecastWeatherIconIndex(GetForecastWeatherId(), GetTomorrowSeason());

            float iconTopY = topY + Math.Max(todayLabelSize.Y, forecastLabelSize.Y) + ScaleUiValue(4f);

            Texture2D todayIconTexture = GetOrCreateLockScreenSoftWeatherIconTexture(todayIconIndex);
            Texture2D forecastIconTexture = GetOrCreateLockScreenSoftWeatherIconTexture(forecastIconIndex);

            b.Draw(todayIconTexture, new Vector2(leftCenterX - (todayIconTexture.Width / 2f), iconTopY), Color.White);
            b.Draw(forecastIconTexture, new Vector2(rightCenterX - (forecastIconTexture.Width / 2f), iconTopY), Color.White);

            int dividerX = (int)Math.Round(centerX);
            int dividerY = (int)Math.Round(topY + ScaleUiValue(4f));
            int dividerBottom = (int)Math.Round(iconTopY + Math.Max(todayIconTexture.Height, forecastIconTexture.Height) + ScaleUiValue(6f));
            int dividerHeight = Math.Max(0, dividerBottom - dividerY);

            if (dividerHeight > 0)
                b.Draw(Game1.staminaRect, new Rectangle(dividerX, dividerY, 1, dividerHeight), Color.White * 0.35f);
        }

        /// <summary>
        /// Renders a specific weather icon.
        /// </summary>
        private void DrawLockScreenWeatherIcon(SpriteBatch b, Vector2 position, int iconIndex)
        {
            Texture2D iconTexture = GetOrCreateLockScreenSoftWeatherIconTexture(iconIndex);
            b.Draw(iconTexture, position, Color.White);
        }

        #endregion

        #region Weather Icon Cache Utilities

        /// <summary>
        /// Resolves a weather icon texture, pulling it from cache or generating a rounded version.
        /// </summary>
        private Texture2D GetOrCreateLockScreenSoftWeatherIconTexture(int iconIndex)
        {
            int safeIconIndex = iconIndex == 999
                ? 999
                : Math.Clamp(iconIndex, 0, 7);

            var cacheKey = (safeIconIndex, phoneUiScale);

            if (lockScreenWeatherIconSoftCache.TryGetValue(cacheKey, out Texture2D? cachedTexture)
                && cachedTexture != null
                && !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            Texture2D sourceTexture;
            Rectangle sourceRect;

            if (safeIconIndex == 999)
            {
                sourceTexture = Game1.mouseCursors_1_6;
                sourceRect = LockScreenGreenRainWeatherIconSource;
            }
            else
            {
                sourceTexture = Game1.mouseCursors;
                sourceRect = new Rectangle(
                    LockScreenWeatherIconStartX + (LockScreenWeatherIconWidth * safeIconIndex),
                    LockScreenWeatherIconStartY,
                    LockScreenWeatherIconWidth,
                    LockScreenWeatherIconHeight);
            }

            Texture2D iconTexture = BuildLockScreenSoftWeatherIconTexture(sourceTexture, sourceRect, phoneUiScale);
            lockScreenWeatherIconSoftCache[cacheKey] = iconTexture;
            return iconTexture;
        }

        /// <summary>
        /// Generates a softened, rounded weather icon texture from a base tilesheet region.
        /// </summary>
        private static Texture2D BuildLockScreenSoftWeatherIconTexture(Texture2D sourceTexture, Rectangle sourceRect, float uiScale)
        {
            int sourceWidth = Math.Max(1, sourceRect.Width);
            int sourceHeight = Math.Max(1, sourceRect.Height);
            float iconScale = Math.Max(0.01f, LockScreenWeatherIconScale * uiScale);

            int targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * iconScale));
            int targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * iconScale));

            Color[] sourceData = new Color[sourceWidth * sourceHeight];
            sourceTexture.GetData(0, sourceRect, sourceData, 0, sourceData.Length);

            Color[] targetData = new Color[targetWidth * targetHeight];

            for (int y = 0; y < targetHeight; y++)
            {
                int sourceY = Math.Clamp((int)(y / iconScale), 0, sourceHeight - 1);
                for (int x = 0; x < targetWidth; x++)
                {
                    int sourceX = Math.Clamp((int)(x / iconScale), 0, sourceWidth - 1);
                    Color sourceColor = sourceData[(sourceY * sourceWidth) + sourceX];

                    float cornerAlpha = GetRoundedCornerAlpha(
                        x,
                        y,
                        targetWidth,
                        targetHeight,
                        LockScreenWeatherIconSoftCornerRadius,
                        LockScreenWeatherIconSoftCornerFeather);

                    byte alpha = (byte)Math.Clamp((int)Math.Round(sourceColor.A * cornerAlpha), 0, 255);
                    targetData[(y * targetWidth) + x] = new Color(sourceColor.R, sourceColor.G, sourceColor.B, alpha);
                }
            }

            Texture2D output = new Texture2D(Game1.graphics.GraphicsDevice, targetWidth, targetHeight);
            output.SetData(targetData);
            return output;
        }

        /// <summary>
        /// Computes corner feathering alpha value for creating rounded corner textures.
        /// </summary>
        private static float GetRoundedCornerAlpha(
            int pixelX,
            int pixelY,
            int width,
            int height,
            float cornerRadius,
            float feather)
        {
            float safeRadius = Math.Clamp(cornerRadius, 0f, Math.Min(width, height) / 2f);
            if (safeRadius <= 0f)
                return 1f;

            float safeFeather = Math.Max(0.001f, feather);

            float centerX = width / 2f;
            float centerY = height / 2f;
            float pointX = pixelX + 0.5f;
            float pointY = pixelY + 0.5f;

            float qx = Math.Abs(pointX - centerX) - (centerX - safeRadius);
            float qy = Math.Abs(pointY - centerY) - (centerY - safeRadius);

            float outsideX = Math.Max(qx, 0f);
            float outsideY = Math.Max(qy, 0f);
            float outsideDistance = MathF.Sqrt((outsideX * outsideX) + (outsideY * outsideY));
            float insideDistance = Math.Min(Math.Max(qx, qy), 0f);
            float signedDistance = outsideDistance + insideDistance - safeRadius;

            if (signedDistance >= 0f)
                return 0f;

            if (signedDistance <= -safeFeather)
                return 1f;

            return 1f - ((signedDistance + safeFeather) / safeFeather);
        }

        /// <summary>
        /// Disposes all dynamically generated soft corner weather textures.
        /// </summary>
        private void DisposeLockScreenWeatherIconSoftCache()
        {
            foreach (Texture2D iconTexture in lockScreenWeatherIconSoftCache.Values)
            {
                if (iconTexture != null && !iconTexture.IsDisposed)
                    iconTexture.Dispose();
            }

            lockScreenWeatherIconSoftCache.Clear();
        }

        #endregion

        #region Weather Info Parsers

        private static int GetTodayWeatherIconIndex()
        {
            if (Game1.weatherIcon == 999)
                return 999;

            return Math.Clamp(Game1.weatherIcon, 0, 7);
        }

        private static string GetForecastWeatherId()
        {
            string weatherId = Game1.currentLocation?.GetWeather()?.WeatherForTomorrow;
            if (string.IsNullOrWhiteSpace(weatherId))
                weatherId = Game1.weatherForTomorrow;

            return string.IsNullOrWhiteSpace(weatherId)
                ? Game1.weather_sunny
                : weatherId;
        }

        private static Season GetTomorrowSeason()
        {
            WorldDate tomorrow = new WorldDate(Game1.Date);
            tomorrow.TotalDays++;
            return tomorrow.Season;
        }

        private static int GetForecastWeatherIconIndex(string weatherId, Season season)
        {
            string safeWeatherId = weatherId?.Trim() ?? "";

            if (string.Equals(safeWeatherId, Game1.weather_green_rain, StringComparison.OrdinalIgnoreCase))
                return 999;

            if (string.Equals(safeWeatherId, Game1.weather_festival, StringComparison.OrdinalIgnoreCase))
                return 1;

            if (string.Equals(safeWeatherId, Game1.weather_lightning, StringComparison.OrdinalIgnoreCase))
                return 5;

            if (string.Equals(safeWeatherId, Game1.weather_snow, StringComparison.OrdinalIgnoreCase))
                return 7;

            if (string.Equals(safeWeatherId, Game1.weather_rain, StringComparison.OrdinalIgnoreCase))
                return 4;

            if (string.Equals(safeWeatherId, Game1.weather_debris, StringComparison.OrdinalIgnoreCase))
            {
                return season switch
                {
                    Season.Spring => 3,
                    Season.Fall => 6,
                    Season.Winter => 7,
                    _ => 2
                };
            }

            if (string.Equals(safeWeatherId, Game1.weather_wedding, StringComparison.OrdinalIgnoreCase))
                return 0;

            return 2;
        }

        /// <summary>
        /// Generates the localized, formatted date string displayed on the lock screen.
        /// </summary>
        private static string BuildLockScreenDateText()
        {
            string rawSeason = Game1.currentSeason;
            string seasonName = string.IsNullOrWhiteSpace(rawSeason)
                ? "Spring"
                : char.ToUpperInvariant(rawSeason[0]) + rawSeason.Substring(1).ToLowerInvariant();

            int day = Math.Max(1, Game1.dayOfMonth);
            int year = Math.Max(1, Game1.year);
            return ModEntry.SHelper.Translation.Get("ui.lockscreen.date_format", new { seasonName = seasonName, day = day, year = year });
        }

        #endregion

        #region Lock Screen Initialization Sequence

        /// <summary>
        /// Triggers the OS initialization and booting screen loader state.
        /// </summary>
        private void BeginLockScreenInitializationSequence()
        {
            rootLandingState = RootLandingState.Initializing;
            lockScreenUnlockAnimating = false;
            lockScreenUnlockElapsedSeconds = 0d;
            lockScreenTapBounds = Rectangle.Empty;

            lockScreenContentScrollOffset = 0f;
            lockScreenContentScrollTarget = 0f;
            lockScreenUnlockDragOffset = 0f;

            lockScreenInitializationProgressPercent = 0;
            lockScreenInitializationElapsedSeconds = 0d;
            lockScreenInitializationNextTickSeconds = GetNextLockScreenInitializationTickSeconds();
        }

        /// <summary>
        /// Calculates a random tick interval for increments in the OS loading progress.
        /// </summary>
        private static double GetNextLockScreenInitializationTickSeconds()
        {
            double minDelay = Math.Max(0.01d, LockScreenInitializationProgressIntervalMinSeconds);
            double maxDelay = Math.Max(minDelay, LockScreenInitializationProgressIntervalMaxSeconds);
            return minDelay + (Random.Shared.NextDouble() * (maxDelay - minDelay));
        }

        /// <summary>
        /// Logic updates simulating OS booting progress bar increases.
        /// </summary>
        private void UpdateLockScreenInitialization(GameTime time)
        {
            if (rootLandingState != RootLandingState.Initializing)
                return;

            lockScreenInitializationElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            double cappedElapsedSeconds = Math.Min(lockScreenInitializationElapsedSeconds, LockScreenInitializationDurationSeconds);
            int maxProgressForElapsedTime = (int)Math.Floor((cappedElapsedSeconds / LockScreenInitializationDurationSeconds) * 100d);
            int visibleMaxProgress = Math.Min(99, maxProgressForElapsedTime);

            lockScreenInitializationNextTickSeconds -= time.ElapsedGameTime.TotalSeconds;
            while (lockScreenInitializationNextTickSeconds <= 0d
                && lockScreenInitializationProgressPercent < visibleMaxProgress)
            {
                int remainingToVisibleMax = Math.Max(1, visibleMaxProgress - lockScreenInitializationProgressPercent);
                int progressStep = Random.Shared.Next(1, Math.Min(8, remainingToVisibleMax) + 1);

                lockScreenInitializationProgressPercent = Math.Min(
                    visibleMaxProgress,
                    lockScreenInitializationProgressPercent + progressStep);

                lockScreenInitializationNextTickSeconds += GetNextLockScreenInitializationTickSeconds();
            }

            if (lockScreenInitializationElapsedSeconds < LockScreenInitializationDurationSeconds)
                return;

            lockScreenInitializationProgressPercent = 100;
            lockScreenInitializationElapsedSeconds = LockScreenInitializationDurationSeconds;
            lockScreenInitializationNextTickSeconds = 0d;
            rootLandingState = RootLandingState.LockScreen;
            lockScreenTapBounds = Rectangle.Empty;
            ModEntry.pendingPhoneOsInitialization = false;
        }

        #endregion

        #region Touch Interaction & Animations

        /// <summary>
        /// Dispatches tap inputs for unlocking the phone if clicked within content bounds.
        /// </summary>
        private bool HandleLockScreenTap(int x, int y)
        {
            if (currentApp != null
                || rootLandingState != RootLandingState.LockScreen
                || lockScreenUnlockAnimating)
            {
                return false;
            }

            Rectangle tapBounds = lockScreenTapBounds;
            if (tapBounds.Width <= 0 || tapBounds.Height <= 0)
                tapBounds = GetPhoneContentBounds();

            if (!tapBounds.Contains(x, y))
                return false;

            lockScreenUnlockAnimating = true;
            lockScreenUnlockElapsedSeconds = 0d;
            Game1.playSound("shwip");
            return true;
        }

        /// <summary>
        /// Returns the progress multiplier (0.0 to 1.0) of the unlock slide animation.
        /// </summary>
        private float GetLockScreenUnlockProgress()
        {
            if (!lockScreenUnlockAnimating || LockScreenUnlockDurationSeconds <= 0d)
                return 0f;

            return (float)Math.Clamp(lockScreenUnlockElapsedSeconds / LockScreenUnlockDurationSeconds, 0d, 1d);
        }

        /// <summary>
        /// Updates the unlock slide animation timing every frame.
        /// </summary>
        private void UpdateLockScreenUnlockAnimation(GameTime time)
        {
            if (!lockScreenUnlockAnimating)
                return;

            lockScreenUnlockElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            if (lockScreenUnlockElapsedSeconds < LockScreenUnlockDurationSeconds)
                return;

            lockScreenUnlockAnimating = false;
            lockScreenUnlockElapsedSeconds = 0d;
            rootLandingState = RootLandingState.Home;
            lockScreenTapBounds = Rectangle.Empty;
        }

        private int GetNotificationCenterStartY(Vector2 datePosition, Vector2 dateSize)
        {
            float weatherLabelHeight = Game1.smallFont.MeasureString("Forecast").Y * GetPhoneTextScale(LockScreenWeatherLabelTextScale);
            float iconHeight = LockScreenWeatherIconHeight * LockScreenWeatherIconScale * phoneUiScale;
            return (int)Math.Round(datePosition.Y + dateSize.Y + ScaleUiValue(LockScreenWeatherPanelTopSpacing) + weatherLabelHeight + ScaleUiValue(4f) + iconHeight + ScaleUiValue(20f));
        }

        private void DrawLockScreenBackground(SpriteBatch b, int yOffset, bool isLandscape = false)
        {
            Rectangle contentBounds = GetPhoneContentBounds();
            Rectangle shiftedBounds = new Rectangle(contentBounds.X, contentBounds.Y - yOffset, contentBounds.Width, contentBounds.Height);

            bool fitFullscreen = ModEntry.Config?.BackgroundFitFullscreen ?? false;
            bool landscape = isLandscape || contentBounds.Width > contentBounds.Height;

            // Always draw solid base background texture covering 100% of the screen first so lockscreen is never see-through
            DrawFittedTexture(b, texturePhoneBackground, shiftedBounds, Color.White, fitFullscreen: true);

            Texture2D? bgTex = landscape ? (phoneBackgroundImageLandscape ?? phoneBackgroundImage) : phoneBackgroundImage;
            if (bgTex != null)
            {
                DrawFittedTexture(b, bgTex, shiftedBounds, Color.White, fitFullscreen);
            }
        }

        private int GetLockScreenCardHeight(bool hasTitle, int messageLinesCount, int titleLineHeight, int messageLineHeight)
        {
            int textHeight = (hasTitle ? titleLineHeight + ScaleUiValue(4) : 0) + messageLinesCount * messageLineHeight;
            return textHeight + ScaleUiValue(24);
        }

        private float GetMaxLockScreenScroll()
        {
            Rectangle contentBounds = GetPhoneContentBounds();

            // 1. Calculate time, date, weather block height
            string timeText = Game1.getTimeOfDayString(Game1.timeOfDay);
            string dateText = BuildLockScreenDateText();

            float timeScale = GetPhoneTextScale(LockScreenTimeTextScale);
            float dateScale = GetPhoneTextScale(LockScreenDateTextScale);

            Vector2 timeSize = Game1.dialogueFont.MeasureString(timeText) * timeScale;
            Vector2 dateSize = Game1.smallFont.MeasureString(dateText) * dateScale;

            float topY = contentBounds.Top + ScaleUiValue(82f);
            Vector2 datePosition = new Vector2(contentBounds.Center.X - (dateSize.X / 2f), topY + timeSize.Y + ScaleUiValue(10f));

            int startY = GetNotificationCenterStartY(datePosition, dateSize);
            int topBlockHeight = startY - contentBounds.Top;

            // 2. Calculate height of notifications list
            List<string> msgs = NotificationManager.GetNotificationList();
            int unreadCount = NotificationManager.GetUnreadNotification();
            int startIndex = Math.Max(0, msgs.Count - unreadCount);

            SpriteFont font = Game1.smallFont;
            int titleLineHeight = GetPhoneScaledLineHeight(font, 0.85f);
            int messageLineHeight = GetPhoneScaledLineHeight(font, 0.75f);
            int wrapWidthBase = GetNotificationWrapWidthBase();
            int notificationsHeight = 0;

            int cardSpacing = ScaleUiValue(12);
            for (int i = startIndex; i < msgs.Count; i++)
            {
                string rawMsg = msgs[i];
                string msg = rawMsg;
                string title = "";

                if (rawMsg.Contains("::"))
                {
                    var parts = rawMsg.Split(new[] { "::" }, 2, StringSplitOptions.None);
                    title = parts[0];
                    msg = parts[1];
                }

                int wrapWidth = (int)Math.Round(GetPhoneScaledWrapWidth(wrapWidthBase) / 0.75f);
                var messageLines = SplitNotificationIntoLines(msg, font, wrapWidth);
                if (messageLines.Count > 2)
                {
                    messageLines = new List<string> { messageLines[0], messageLines[1] + "..." };
                }

                int cardHeight = GetLockScreenCardHeight(!string.IsNullOrEmpty(title), messageLines.Count, titleLineHeight, messageLineHeight);
                notificationsHeight += cardHeight + cardSpacing;
            }

            if (msgs.Count > startIndex)
            {
                notificationsHeight -= cardSpacing;
            }

            int totalContentHeight = topBlockHeight + notificationsHeight;
            if (unreadCount > 0)
            {
                totalContentHeight += GetPhoneScaledLineHeight(font, 1.15f) + ScaleUiValue(10);
            }
            int footerHeight = ScaleUiValue(72);
            int viewportHeight = contentBounds.Height - footerHeight - ScaleUiValue(10);

            return Math.Max(0f, totalContentHeight - viewportHeight);
        }

        private void ClampLockScreenScroll()
        {
            float maxScroll = GetMaxLockScreenScroll();
            lockScreenContentScrollTarget = Math.Clamp(lockScreenContentScrollTarget, 0f, maxScroll);
            lockScreenContentScrollOffset = Math.Clamp(lockScreenContentScrollOffset, 0f, maxScroll);
        }

        private bool CheckLockScreenCardClick(int x, int y)
        {
            foreach (var card in lockScreenCardBounds)
            {
                if (card.Bounds.Contains(x, y))
                {
                    OpenNotification();
                    currentApp = "appNotification";
                    rootLandingState = RootLandingState.Home;
                    lockScreenUnlockAnimating = false;
                    lockScreenUnlockElapsedSeconds = 0d;
                    Game1.playSound("smallSelect");
                    return true;
                }
            }
            return false;
        }

        private void UpdateLockScreenScroll(GameTime time)
        {
            float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * ChatScrollLerpSpeed);
            lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);

            ClampLockScreenScroll();
            lockScreenContentScrollOffset = MathHelper.Lerp(lockScreenContentScrollOffset, lockScreenContentScrollTarget, lerpAmount);

            if (Math.Abs(lockScreenContentScrollOffset - lockScreenContentScrollTarget) <= 0.5f)
                lockScreenContentScrollOffset = lockScreenContentScrollTarget;
        }

        private bool IsLockScreenAppPinActive(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return false;
            if (appId.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase)) return true;
            foreach (var app in ModEntry.GetRegisteredPhoneAppsSnapshot())
            {
                if (string.Equals(app.CompositeId, appId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void LaunchAppFromLockScreen(string appId)
        {
            if (string.IsNullOrEmpty(appId)) return;
            rootLandingState = RootLandingState.Home;
            lockScreenUnlockAnimating = false;
            lockScreenUnlockElapsedSeconds = 0d;
            lockScreenTapBounds = Rectangle.Empty;
            TryHandleHomeAppClickPublic(appId);
        }

        private void DrawLockScreenAppIcon(SpriteBatch b, Texture2D texture, Rectangle bounds, Rectangle? sourceRect)
        {
            if (texture == null) return;
            Rectangle source = sourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
            float scale = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
            int dw = Math.Max(1, (int)Math.Round(source.Width * scale));
            int dh = Math.Max(1, (int)Math.Round(source.Height * scale));
            Rectangle drawRect = new Rectangle(bounds.X + (bounds.Width - dw) / 2, bounds.Y + (bounds.Height - dh) / 2, dw, dh);
            b.Draw(texture, drawRect, source, Color.White);
        }

        #endregion
    }
}
