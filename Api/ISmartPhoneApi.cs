using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Smartphone
{
    public enum AppIconType
    {
        Notification,
        AppStore,
        Camera,
        Photo,
        Setting,
        Calendar
    }

    public enum AppSize
    {
        Size1x1,
        Size2x1,
        Size2x2,
        Size2x3,
        Size2x4,
        Size4x2,
        Size4x3,
        Size4x4,
    }

    public class SelectedPhotoResult
    {
        public string AbsolutePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public byte[]? TextureData { get; set; }
    }

    public interface IContactActionCardButton
    {
        public string Text { get; set; }
        public Color BackgroundColor { get; set; }
        public Color TextColor { get; set; }
        public Action<string>? OnClick { get; set; }
    }

    public interface ISmartPhoneApi
    {
        // =========================================================
        // 1. App & Widget Registration
        // =========================================================

        /// <summary>
        /// Registers a custom app icon on the smartphone home screen.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app (e.g. d5a1lamdtd.markettown).</param>
        /// <param name="appId">A unique app ID within the owner mod (e.g. d5a1lamdtd.markettown.marketlog).</param>
        /// <param name="displayName">Name shown as a label under the app icon (e.g. Market Log).</param>
        /// <param name="iconTexture">Texture used as the app icon (any size but should be square and above 84*84).</param>
        /// <param name="onClick">Callback invoked when the app icon is clicked.</param>
        /// <param name="closePhoneOnLaunch">Whether the phone menu should close before invoking <paramref name="onClick"/>.</param>
        /// <param name="sourceRect">Optional source rectangle if the icon is part of a spritesheet. If null, the full texture is used.</param>
        /// <param name="isVisible">Optional callback to decide whether the icon should currently be visible (e.g. () => Context.IsWorldReady).</param>
        /// <param name="getBadgeCount">Optional callback to draw a badge count on the icon.</param>
        /// <param name="supportedSizes">Optional list of <see cref="AppSize"/> values the app icon supports as widget sizes.
        /// Defaults to <see cref="AppSize.Size1x1"/> only when null or empty.</param>
        /// <returns>True if registration succeeded; otherwise false.</returns>
        bool RegisterPhoneApp(
            string ownerModId,
            string appId,
            string displayName,
            Action onClick,
            bool closePhoneOnLaunch = true,
            Rectangle? sourceRect = null,
            Func<int>? getBadgeCount = null,
            AppSize[]? supportedSizes = null,
            Action<SpriteBatch, Rectangle, AppSize>? onDrawWidget = null,
            Dictionary<string, Texture2D>? themedIconTextures = null
        );

        /// <summary>
        /// Unregisters a previously registered custom app.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app.</param>
        /// <param name="appId">The app ID that was used during registration.</param>
        /// <returns>True if an app was removed; otherwise false.</returns>
        bool UnregisterPhoneApp(string ownerModId, string appId);

        /// <summary>
        /// Gets the resolved 1x1 icon texture for an app.
        /// </summary>
        Texture2D? GetAppIconTexture(string appId);

        /// <summary>
        /// Gets the texture of a built-in app icon.
        /// </summary>
        /// <param name="appIconType">The type of the app icon.</param>
        /// <returns>The Texture2D of the requested app icon, or null.</returns>
        Texture2D? GetAppTexture(AppIconType appIconType);

        // =========================================================
        // 2. Phone Navigation & Positioning
        // =========================================================

        /// <summary>
        /// Opens the smartphone home (landing) screen for the current player.
        /// If the phone is closed, it is opened first.
        /// </summary>
        /// <returns>True if the home screen was opened; otherwise false.</returns>
        bool OpenPhoneHomeScreen();

        /// <summary>
        /// Gets the current on-screen position (top-left corner) of the phone menu.
        /// Use this to open a custom app screen at the same position the phone was at,
        /// so the transition feels seamless.
        /// </summary>
        /// <returns>X and Y screen coordinates of the phone frame's top-left corner.</returns>
        (int x, int y) GetPhonePosition();

        /// <summary>
        /// Updates the current on-screen position (top-left corner) of the phone menu.
        /// Call this in your custom app screen's drag/move handling so that the position is preserved
        /// globally across transitions and when exiting the app.
        /// </summary>
        /// <param name="x">The new X coordinate of the phone frame.</param>
        /// <param name="y">The new Y coordinate of the phone frame.</param>
        void SetPhonePosition(int x, int y);

        /// <summary>
        /// Handles clicks on the phone's built-in bottom navigation buttons.
        /// Call this in your custom app's receiveLeftClick method.
        /// </summary>
        /// <param name="x">The X position of the mouse click.</param>
        /// <param name="y">The Y position of the mouse click.</param>
        /// <param name="phoneX">The current X position of the phone frame.</param>
        /// <param name="phoneY">The current Y position of the phone frame.</param>
        /// <param name="onBack">Optional action to run when the Back button is clicked.</param>
        /// <returns>True if a button was clicked and handled, false otherwise.</returns>
        bool HandlePhoneAppBottomNavClick(int x, int y, int phoneX, int phoneY, Action? onBack = null);

        // =========================================================
        // 3. Theme & Appearance
        // =========================================================

        /// <summary>
        /// Gets the current phone UI scale factor.
        /// Returns 0.75 when small phone size is enabled, or 1.0 for the regular size.
        /// </summary>
        /// <returns>The active phone UI scale multiplier.</returns>
        float GetPhoneUiScale();

        /// <summary>
        /// Gets the current scaled phone frame width in pixels.
        /// </summary>
        /// <returns>The phone frame width after applying the current UI scale.</returns>
        int GetPhoneFrameWidth();

        /// <summary>
        /// Gets the current scaled phone frame height in pixels.
        /// </summary>
        /// <returns>The phone frame height after applying the current UI scale.</returns>
        int GetPhoneFrameHeight();

        /// <summary>
        /// Gets the current scaled phone content area offset from the top-left of the phone frame.
        /// This is useful for positioning custom content inside the phone screen area.
        /// </summary>
        /// <returns>X and Y pixel offsets of the content area within the phone frame.</returns>
        (int offsetX, int offsetY) GetPhoneContentOffset();

        /// <summary>
        /// Gets the phone frame texture (phone_empty.png) from the currently active theme.
        /// This is the border/bezel texture drawn on top of the phone screen.
        /// </summary>
        /// <returns>The phone frame Texture2D, or null if textures are not loaded.</returns>
        Texture2D? GetPhoneFrameTexture();

        /// <summary>
        /// Gets the phone screen background texture (phone_background.png) from the currently active theme.
        /// This is the wallpaper/background drawn behind app content inside the phone screen area.
        /// </summary>
        /// <returns>The phone background Texture2D, or null if textures are not loaded.</returns>
        Texture2D? GetPhoneBackgroundTexture();

        /// <summary>
        /// Gets the phone card texture (card_texture.png) from the currently active theme.
        /// This is the background drawn behind info cards.
        /// </summary>
        /// <returns>The phone card Texture2D, or null if textures are not loaded.</returns>
        Texture2D? GetCardTexture();

        /// <summary>
        /// Sets the theme for a component.
        /// </summary>
        void SetComponentTheme(string component, string theme);

        /// <summary>
        /// Gets the current theme for a component.
        /// </summary>
        string GetComponentTheme(string component);

        // =========================================================
        // 4. Size Control Settings
        // =========================================================

        /// <summary>
        /// Draws the phone scale adjustment buttons (+ and -) if they are enabled in the configuration.
        /// </summary>
        void DrawPhoneSizeButtons(SpriteBatch b, int phoneX, int phoneY, bool landscape = false, bool forceOn = false);

        /// <summary>
        /// Handles clicks on the phone scale adjustment buttons.
        /// </summary>
        /// <returns>True if a button was clicked and handled, false otherwise.</returns>
        bool HandlePhoneSizeButtonsClick(int x, int y, int phoneX, int phoneY);

        /// <summary>
        /// Gets the configured button name for decreasing the phone size.
        /// </summary>
        string GetDecreaseSizeKey();

        /// <summary>
        /// Gets the configured button name for increasing the phone size.
        /// </summary>
        string GetIncreaseSizeKey();

        /// <summary>
        /// Adjusts the phone size by the given amount.
        /// </summary>
        void AdjustPhoneSize(float amount);

        // =========================================================
        // 5. Contacts App
        // =========================================================

        /// <summary>
        /// Registers a new custom card under the contact info screen, allowing up to 4 buttons.
        /// </summary>
        /// <param name="modId">The unique ID of the mod registering the card.</param>
        /// <param name="cardTitle">The title of the card (e.g. "Gift", "Social").</param>
        /// <param name="buttons">A list of button definitions. Max 4 buttons.</param>
        /// <param name="npcNames">An optional list of NPC internal names for which this card is available. If null or empty, it is available for all NPCs.</param>
        /// <returns>True if successfully registered.</returns>
        bool RegisterContactActionCard(string modId, string cardTitle, IList<IContactActionCardButton> buttons, List<string> npcNames = null);

        // =========================================================
        // 6. Notifications app
        // =========================================================

        /// <summary>
        /// Sends a notification to the player's smartphone.
        /// </summary>
        /// <param name="message">The content of the notification (shown in the phone notification message).</param>
        /// <param name="notificationName">(optional) The name of the notification (shown on ingame notification HUD).</param>
        /// <param name="playerId">(optional) The target player's UniqueMultiplayerID as string. If null/empty/invalid, this is broadcast to all online players.</param>
        void SendSmartphoneNotification(string message, string notificationName = "", string playerId = "");

        // =========================================================
        // 7. Photo app
        // =========================================================

        /// <summary>
        /// Captures a photo for StardewSocial programmatically.
        /// </summary>
        string CaptureNpcPhoto(GameLocation targetLocation, Vector2 captureCenter, NPC npc = null, bool landscape = false, bool square = false, List<NPC>? visibleNpcAtTarget = null, float zoomLevel = 1f, int? captureTimeOfDay = null, string saveLocation = null);

        /// <summary>
        /// Gets a player photo texture by its name.
        /// </summary>
        Texture2D GetPlayerPhotoTexture(string photoName);

        /// <summary>
        /// Gets the serialized ImageMetadata JSON string for a specific player photo.
        /// </summary>
        string GetPlayerPhotoMetadata(string photoName);

        /// <summary>
        /// Opens the photo app in selection/view-only mode to retrieve photo texture and/or metadata.
        /// </summary>
        /// <param name="limit">The maximum number of photos that can be selected (must be greater than 0).</param>
        /// <param name="getTexture">Whether to retrieve the texture data.</param>
        /// <param name="getMetadata">Whether to retrieve the metadata.</param>
        /// <param name="onComplete">Callback invoked when user finishes selection or cancels (passes JSON string representing List of SelectedPhotoResult, or empty list).</param>
        void RetrievePhotos(int limit, bool getTexture, bool getMetadata, Action<string> onComplete, bool squareOnly = false);

        // =========================================================
        // 8. Utility
        // =========================================================

        /// <summary>
        /// Registers draw and update callbacks for the passive HUD icon preview mode of an app.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app.</param>
        /// <param name="appId">The app ID that was used during registration.</param>
        /// <param name="onDrawHudScreen">Callback to draw the screen content inside the HUD phone frame.</param>
        /// <param name="onUpdateHudScreen">Optional callback to update live data/animations in passive mode.</param>
        /// <param name="landscape">Whether the app renders in landscape orientation, rotating the HUD icon frame itself.</param>
        /// <returns>True if registration succeeded; otherwise false.</returns>
        bool RegisterPassiveHudCallback(
            string ownerModId,
            string appId,
            Action<SpriteBatch, Rectangle> onDrawHudScreen,
            Action<GameTime>? onUpdateHudScreen = null,
            bool landscape = false
        );

        /// <summary>
        /// Checks if the HUD icon is currently pinned.
        /// </summary>
        bool IsHudPinned();

        /// <summary>
        /// Gets the composite ID of the currently pinned external app.
        /// </summary>
        string? GetPinnedAppId();
    }
}
