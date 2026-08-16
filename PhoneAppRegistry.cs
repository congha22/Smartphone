using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace Smartphone
{
    internal sealed class RegisteredPhoneApp
    {
        public string OwnerModId { get; init; } = "";
        public string AppId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public Dictionary<string, Texture2D> ThemedIconTextures { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Action? OnClick { get; init; }
        public bool ClosePhoneOnLaunch { get; init; }
        public Rectangle? SourceRect { get; init; }
        public Func<int>? GetBadgeCount { get; init; }
        public List<AppSize> SupportedSizes { get; init; } = new() { AppSize.Size1x1 };
        public Action<SpriteBatch, Rectangle, AppSize>? OnDrawWidget { get; init; }
        public Action<SpriteBatch, Rectangle>? OnDrawHudScreen { get; set; }
        public Action<GameTime>? OnUpdateHudScreen { get; set; }
        public bool Landscape { get; set; }

        // Optional interactive overlay rendered next to the HUD slider
        public Action<SpriteBatch, Rectangle>? OnDrawHudOverlay { get; set; }
        public Func<int, int, bool>? OnHudOverlayLeftClick { get; set; }
        public Action<int, int>? OnHudOverlayLeftClickHeld { get; set; }
        public Action? OnHudOverlayReleaseLeftClick { get; set; }
        public Func<int>? GetHudOverlayHeight { get; set; }

        public string CompositeId => BuildCompositeId(this.OwnerModId, this.AppId);

        public static string BuildCompositeId(string ownerModId, string appId)
        {
            return $"{ownerModId.Trim()}::{appId.Trim()}";
        }
    }

    internal sealed class RegisteredChatQuickActionButton
    {
        public string OwnerModId { get; init; } = "";
        public string ActionId { get; init; } = "";
        public Texture2D IconTexture { get; init; } = null!;
        public Action<string> OnClick { get; init; } = null!;
        public bool ClosePhoneOnLaunch { get; init; }
        public int SortOrder { get; init; }
        public Rectangle? SourceRect { get; init; }
        public HashSet<string>? AllowedNpcNames { get; init; }

        public string CompositeId => BuildCompositeId(this.OwnerModId, this.ActionId);

        public static string BuildCompositeId(string ownerModId, string actionId)
        {
            return $"{ownerModId.Trim()}::{actionId.Trim()}";
        }
    }

    public partial class ModEntry
    {
        private static readonly object RegisteredPhoneAppsLock = new();
        private static readonly Dictionary<string, RegisteredPhoneApp> RegisteredPhoneApps = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RegisteredChatQuickActionButton> RegisteredChatQuickActionButtons = new(StringComparer.OrdinalIgnoreCase);
        public static string? ActiveExternalAppId = null;
        public static bool isPhoneOpen = false;

        public static List<string> GetRegisteredAppIds()
        {
            lock (RegisteredPhoneAppsLock)
            {
                return RegisteredPhoneApps.Keys.ToList();
            }
        }

        public static bool IsPassiveHudSupported(string? compositeId)
        {
            if (string.IsNullOrWhiteSpace(compositeId)) return false;
            lock (RegisteredPhoneAppsLock)
            {
                if (RegisteredPhoneApps.TryGetValue(compositeId, out var app))
                {
                    return app.OnDrawHudScreen != null;
                }
            }
            return false;
        }

        public static bool IsExternalAppLandscape(string? compositeId)
        {
            if (string.IsNullOrWhiteSpace(compositeId)) return false;
            lock (RegisteredPhoneAppsLock)
            {
                if (RegisteredPhoneApps.TryGetValue(compositeId, out var app))
                {
                    return app.Landscape;
                }
            }
            return false;
        }

        public static string GetRegisteredAppDisplayName(string appId)
        {
            lock (RegisteredPhoneAppsLock)
            {
                if (RegisteredPhoneApps.TryGetValue(appId, out var app))
                {
                    return app.DisplayName;
                }
                return appId;
            }
        }

        internal static bool RegisterPhoneAppInternal(
            string ownerModId,
            string appId,
            string displayName,
            Action onClick,
            bool closePhoneOnLaunch,
            Rectangle? sourceRect,
            Func<int>? getBadgeCount,
            AppSize[]? supportedSizes = null,
            Action<SpriteBatch, Rectangle, AppSize>? onDrawWidget = null,
            Dictionary<string, Texture2D>? themedIconTextures = null)
        {
            if (onClick == null)
            {
                SMonitor?.Log("RegisterPhoneApp failed: onClick is required for normal apps.", LogLevel.Warn);
                return false;
            }

            return RegisterPhoneAppCore(
                ownerModId,
                appId,
                displayName,
                onClick,
                closePhoneOnLaunch,
                sourceRect,
                getBadgeCount,
                supportedSizes,
                onDrawWidget,
                themedIconTextures);
        }

        private static bool RegisterPhoneAppCore(
            string ownerModId,
            string appId,
            string displayName,
            Action? onClick,
            bool closePhoneOnLaunch,
            Rectangle? sourceRect,
            Func<int>? getBadgeCount,
            AppSize[]? supportedSizes = null,
            Action<SpriteBatch, Rectangle, AppSize>? onDrawWidget = null,
            Dictionary<string, Texture2D>? themedIconTextures = null)
        {
            if (string.IsNullOrWhiteSpace(ownerModId)
                || string.IsNullOrWhiteSpace(appId)
                || string.IsNullOrWhiteSpace(displayName))
            {
                SMonitor?.Log("RegisterPhoneApp failed: ownerModId, appId/groupId, and displayName are required.", LogLevel.Warn);
                return false;
            }

            if (themedIconTextures == null || themedIconTextures.Count == 0 || !themedIconTextures.ContainsKey("default"))
            {
                SMonitor?.Log("RegisterPhoneApp failed: themedIconTextures must not be null/empty and must contain a 'default' theme icon.", LogLevel.Warn);
                return false;
            }

            if (sourceRect.HasValue && (sourceRect.Value.Width <= 0 || sourceRect.Value.Height <= 0))
            {
                SMonitor?.Log($"RegisterPhoneApp failed for '{ownerModId}:{appId}': sourceRect must have positive width and height.", LogLevel.Warn);
                return false;
            }

            List<AppSize> sizes = (supportedSizes != null && supportedSizes.Length > 0)
                ? new List<AppSize>(supportedSizes)
                : new List<AppSize> { AppSize.Size1x1 };

            string key = RegisteredPhoneApp.BuildCompositeId(ownerModId, appId);
            var app = new RegisteredPhoneApp
            {
                OwnerModId = ownerModId.Trim(),
                AppId = appId.Trim(),
                DisplayName = displayName.Trim(),
                ThemedIconTextures = new Dictionary<string, Texture2D>(themedIconTextures, StringComparer.OrdinalIgnoreCase),
                OnClick = onClick,
                ClosePhoneOnLaunch = closePhoneOnLaunch,
                SourceRect = sourceRect,
                GetBadgeCount = getBadgeCount,
                SupportedSizes = sizes,
                OnDrawWidget = onDrawWidget
            };

            bool replaced;
            lock (RegisteredPhoneAppsLock)
            {
                replaced = RegisteredPhoneApps.ContainsKey(key);
                RegisteredPhoneApps[key] = app;
            }

            SMonitor?.Log(
                replaced
                    ? $"Updated smartphone app registration '{key}'."
                    : $"Registered smartphone app '{key}'.",
                LogLevel.Trace);
            return true;
        }

        internal static bool RegisterPassiveHudCallbackInternal(
            string ownerModId,
            string appId,
            Action<SpriteBatch, Rectangle>? onDrawHudScreen,
            Action<GameTime>? onUpdateHudScreen = null,
            bool landscape = false)
        {
            if (string.IsNullOrWhiteSpace(ownerModId) || string.IsNullOrWhiteSpace(appId))
            {
                SMonitor?.Log("RegisterPassiveHudCallback failed: ownerModId and appId are required.", LogLevel.Warn);
                return false;
            }

            string key = RegisteredPhoneApp.BuildCompositeId(ownerModId, appId);
            lock (RegisteredPhoneAppsLock)
            {
                if (RegisteredPhoneApps.TryGetValue(key, out var app))
                {
                    app.OnDrawHudScreen = onDrawHudScreen;
                    app.OnUpdateHudScreen = onUpdateHudScreen;
                    app.Landscape = landscape;
                    SMonitor?.Log($"Registered passive HUD callback for '{key}'.", LogLevel.Trace);
                    return true;
                }
            }

            SMonitor?.Log($"RegisterPassiveHudCallback failed: app '{key}' is not registered yet. Please register the app first.", LogLevel.Warn);
            return false;
        }

        internal static bool RegisterPassiveHudOverlayInternal(
            string ownerModId,
            string appId,
            Action<SpriteBatch, Rectangle> onDrawHudOverlay,
            Func<int, int, bool>? onLeftClick = null,
            Action<int, int>? onLeftClickHeld = null,
            Action? onReleaseLeftClick = null,
            Func<int>? getOverlayHeight = null)
        {
            if (string.IsNullOrWhiteSpace(ownerModId) || string.IsNullOrWhiteSpace(appId))
            {
                SMonitor?.Log("RegisterPassiveHudOverlay failed: ownerModId and appId are required.", LogLevel.Warn);
                return false;
            }

            if (onDrawHudOverlay == null)
            {
                SMonitor?.Log("RegisterPassiveHudOverlay failed: onDrawHudOverlay callback is required.", LogLevel.Warn);
                return false;
            }

            string key = RegisteredPhoneApp.BuildCompositeId(ownerModId, appId);
            lock (RegisteredPhoneAppsLock)
            {
                if (RegisteredPhoneApps.TryGetValue(key, out var app))
                {
                    app.OnDrawHudOverlay = onDrawHudOverlay;
                    app.OnHudOverlayLeftClick = onLeftClick;
                    app.OnHudOverlayLeftClickHeld = onLeftClickHeld;
                    app.OnHudOverlayReleaseLeftClick = onReleaseLeftClick;
                    app.GetHudOverlayHeight = getOverlayHeight;
                    SMonitor?.Log($"Registered passive HUD overlay for '{key}'.", LogLevel.Trace);
                    return true;
                }
            }

            SMonitor?.Log($"RegisterPassiveHudOverlay failed: app '{key}' is not registered. Please register the app first.", LogLevel.Warn);
            return false;
        }

        internal static bool UnregisterPhoneAppInternal(string ownerModId, string appId)
        {
            if (string.IsNullOrWhiteSpace(ownerModId) || string.IsNullOrWhiteSpace(appId))
                return false;

            string key = RegisteredPhoneApp.BuildCompositeId(ownerModId, appId);
            bool removed;
            lock (RegisteredPhoneAppsLock)
            {
                removed = RegisteredPhoneApps.Remove(key);
            }

            if (removed)
                SMonitor?.Log($"Unregistered smartphone app '{key}'.", LogLevel.Trace);

            return removed;
        }





        internal static bool RegisterChatQuickActionButtonInternal(
            string ownerModId,
            string actionId,
            Texture2D iconTexture,
            Action<string> onClick,
            bool closePhoneOnLaunch,
            int sortOrder,
            Rectangle? sourceRect,
            List<string>? npcNames)
        {
            if (string.IsNullOrWhiteSpace(ownerModId)
                || string.IsNullOrWhiteSpace(actionId)
                || iconTexture == null
                || onClick == null)
            {
                SMonitor?.Log("RegisterChatQuickActionButton failed: ownerModId, actionId, iconTexture, and onClick are required.", LogLevel.Warn);
                return false;
            }

            if (sourceRect.HasValue && (sourceRect.Value.Width <= 0 || sourceRect.Value.Height <= 0))
            {
                SMonitor?.Log($"RegisterChatQuickActionButton failed for '{ownerModId}:{actionId}': sourceRect must have positive width and height.", LogLevel.Warn);
                return false;
            }

            HashSet<string>? allowedNpcNames = null;
            if (npcNames != null)
            {
                var sanitizedNames = npcNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .ToList();

                if (sanitizedNames.Count > 0)
                    allowedNpcNames = new HashSet<string>(sanitizedNames, StringComparer.OrdinalIgnoreCase);
            }

            string key = RegisteredChatQuickActionButton.BuildCompositeId(ownerModId, actionId);
            var action = new RegisteredChatQuickActionButton
            {
                OwnerModId = ownerModId.Trim(),
                ActionId = actionId.Trim(),
                IconTexture = iconTexture,
                OnClick = onClick,
                ClosePhoneOnLaunch = closePhoneOnLaunch,
                SortOrder = sortOrder,
                SourceRect = sourceRect,
                AllowedNpcNames = allowedNpcNames
            };

            bool replaced;
            lock (RegisteredPhoneAppsLock)
            {
                replaced = RegisteredChatQuickActionButtons.ContainsKey(key);
                RegisteredChatQuickActionButtons[key] = action;
            }

            SMonitor?.Log(
                replaced
                    ? $"Updated chat quick-action button '{key}'."
                    : $"Registered chat quick-action button '{key}'.",
                LogLevel.Trace);
            return true;
        }

        internal static bool UnregisterChatQuickActionButtonInternal(string ownerModId, string actionId)
        {
            if (string.IsNullOrWhiteSpace(ownerModId) || string.IsNullOrWhiteSpace(actionId))
                return false;

            string key = RegisteredChatQuickActionButton.BuildCompositeId(ownerModId, actionId);

            bool removed;
            lock (RegisteredPhoneAppsLock)
                removed = RegisteredChatQuickActionButtons.Remove(key);

            if (removed)
                SMonitor?.Log($"Unregistered chat quick-action button '{key}'.", LogLevel.Trace);

            return removed;
        }

        internal static List<RegisteredChatQuickActionButton> GetRegisteredChatQuickActionButtonsSnapshot(string selectedNpcName)
        {
            if (string.IsNullOrWhiteSpace(selectedNpcName))
                return new List<RegisteredChatQuickActionButton>();

            List<RegisteredChatQuickActionButton> snapshot;
            lock (RegisteredPhoneAppsLock)
            {
                snapshot = RegisteredChatQuickActionButtons.Values
                    .OrderBy(p => p.SortOrder)
                    .ThenBy(p => p.CompositeId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var visibleActions = new List<RegisteredChatQuickActionButton>();
            foreach (RegisteredChatQuickActionButton action in snapshot)
            {
                if (IsRegisteredChatQuickActionButtonVisibleForNpc(action, selectedNpcName))
                    visibleActions.Add(action);
            }

            return visibleActions;
        }

        internal static bool TryInvokeRegisteredChatQuickActionButton(string compositeId, string selectedNpcName, PhoneMenu menu)
        {
            if (string.IsNullOrWhiteSpace(compositeId) || string.IsNullOrWhiteSpace(selectedNpcName))
                return false;

            RegisteredChatQuickActionButton? action;
            lock (RegisteredPhoneAppsLock)
                RegisteredChatQuickActionButtons.TryGetValue(compositeId, out action);

            if (action == null)
                return false;

            if (!IsRegisteredChatQuickActionButtonVisibleForNpc(action, selectedNpcName))
                return false;

            try
            {
                if (action.ClosePhoneOnLaunch)
                    menu.ClosePhoneMenu();

                action.OnClick.Invoke(selectedNpcName);
                return true;
            }
            catch (Exception ex)
            {
                SMonitor?.Log($"Error while invoking chat quick-action button '{action.CompositeId}': {ex}", LogLevel.Error);
                return false;
            }
        }

        internal static List<RegisteredPhoneApp> GetRegisteredPhoneAppsSnapshot()
        {
            lock (RegisteredPhoneAppsLock)
            {
                return RegisteredPhoneApps.Values
                    .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.CompositeId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        internal static bool TryInvokeRegisteredPhoneApp(string compositeId, PhoneMenu menu)
        {
            if (string.IsNullOrWhiteSpace(compositeId))
                return false;

            RegisteredPhoneApp? app;
            lock (RegisteredPhoneAppsLock)
                RegisteredPhoneApps.TryGetValue(compositeId, out app);

            if (app == null)
                return false;

            try
            {
                if (app.ClosePhoneOnLaunch)
                    menu.ClosePhoneMenu();

                isPhoneOpen = true;
                ActiveExternalAppId = compositeId;
                app.OnClick?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                SMonitor?.Log($"Error while opening smartphone app '{app.CompositeId}': {ex}", LogLevel.Error);
                return false;
            }
        }

        internal static bool OpenPhoneHomeScreenInternal()
        {
            if (!Context.IsWorldReady)
                return false;

            EnsurePhoneMenuUsesCurrentScale();

            phoneMenu.OpenHomeScreen();
            Game1.activeClickableMenu = phoneMenu;
            return true;
        }

        private static bool IsRegisteredChatQuickActionButtonVisibleForNpc(RegisteredChatQuickActionButton action, string selectedNpcName)
        {
            if (action.AllowedNpcNames != null
                && action.AllowedNpcNames.Count > 0
                && !action.AllowedNpcNames.Contains(selectedNpcName))
            {
                return false;
            }

            return true;
        }
    }
}
