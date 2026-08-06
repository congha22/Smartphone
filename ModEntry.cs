
using System;
using System.Diagnostics.Metrics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Smartphone.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.Menus;
using StardewValley.Objects;

namespace Smartphone
{
    /// <summary>The mod entry point.</summary>
    public class ModApi : ISmartPhoneApi
    {
        private readonly IMonitor monitor;

        public ModApi(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        public List<string> GetContactableNpcsInternal()
        {
            try
            {
                PhoneMenu.UpdateNpcNumbers();
            }
            catch (Exception ex)
            {
                this.monitor?.Log($"Error populating ContactableNpcs dynamically: {ex.Message}", LogLevel.Error);
            }
            return ModEntry.ContactableNpcs.ToList();
        }

        public bool RegisterContactActionCard(string modId, string cardTitle, IList<IContactActionCardButton> buttons, List<string> npcNames = null)
        {
            if (buttons == null || buttons.Count == 0 || buttons.Count > 4) return false;

            // Remove any existing card with the same ModId and Title so it can be updated
            ModEntry.ContactActionCardsManager.Cards.RemoveAll(c => c.ModId == modId && c.Title == cardTitle);

            // Register the new card
            ModEntry.ContactActionCardsManager.Cards.Add(new ModEntry.ContactActionCardsManager.Card
            {
                ModId = modId,
                Title = cardTitle,
                Buttons = buttons,
                AvailableNpcNames = npcNames
            });

            return true;
        }
        public void SendSmartphoneNotification(string message, string notificationName = "", string playerId = "")
        {
            NotificationManager.AddNotification(message, notificationName);
        }

        public string CaptureNpcPhoto(GameLocation targetLocation, Vector2 captureCenter, NPC npc = null, bool landscape = false, bool square = false, List<NPC>? visibleNpcAtTarget = null, float zoomLevel = 1f, int? captureTimeOfDay = null, string saveLocation = null, bool forceFlash = false)
        {
            return ModEntry.CaptureNpcPhoto(targetLocation, captureCenter, npc, landscape, square, visibleNpcAtTarget, zoomLevel, captureTimeOfDay, saveLocation, forceFlash);
        }

        public bool CaptureLiveFeedFrame(GameLocation targetLocation, Vector2 captureCenter, RenderTarget2D renderTarget, float zoomLevel = 1f, bool forceFlash = false)
        {
            return ModEntry.CaptureLiveFeedFrame(targetLocation, captureCenter, renderTarget, zoomLevel, forceFlash);
        }

        public Texture2D GetPlayerPhotoTexture(string photoName)
        {
            return ModEntry.GetPlayerPhotoTextureInternal(photoName);
        }

        public string GetPlayerPhotoMetadata(string photoName)
        {
            return ModEntry.GetPlayerPhotoMetadataInternal(photoName);
        }

        public bool RegisterPhoneApp(
            string ownerModId,
            string appId,
            string displayName,
            Action onClick,
            bool closePhoneOnLaunch = true,
            Rectangle? sourceRect = null,
            Func<int>? getBadgeCount = null,
            AppSize[]? supportedSizes = null,
            Action<SpriteBatch, Rectangle, AppSize>? onDrawWidget = null,
            Dictionary<string, Texture2D>? themedIconTextures = null)
        {
            return ModEntry.RegisterPhoneAppInternal(
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

        public bool RegisterPassiveHudCallback(
            string ownerModId,
            string appId,
            Action<SpriteBatch, Rectangle> onDrawHudScreen,
            Action<GameTime>? onUpdateHudScreen = null,
            bool landscape = false)
        {
            return ModEntry.RegisterPassiveHudCallbackInternal(
                ownerModId,
                appId,
                onDrawHudScreen,
                onUpdateHudScreen,
                landscape);
        }

        public bool IsHudPinned()
        {
            return ModEntry.isHudPinned;
        }

        public void SetHudPinned(bool pinned)
        {
            ModEntry.isHudPinned = pinned;
        }

        public string? GetPinnedAppId()
        {
            return ModEntry.ActiveExternalAppId;
        }

        public void SetComponentTheme(string component, string theme)
        {
            AssetHelper.SetComponentTheme(component, theme);
        }

        public string GetComponentTheme(string component)
        {
            return AssetHelper.GetComponentTheme(component);
        }

        public Texture2D? GetAppIconTexture(string appId)
        {
            return Textures.GetAppTexture(appId, AppSize.Size1x1);
        }

        public bool UnregisterPhoneApp(string ownerModId, string appId)
        {
            return ModEntry.UnregisterPhoneAppInternal(ownerModId, appId);
        }




        public bool OpenPhoneHomeScreen()
        {
            return ModEntry.OpenPhoneHomeScreenInternal();
        }

        public bool RegisterChatQuickActionButton(
            string ownerModId,
            string actionId,
            Texture2D iconTexture,
            Action<string> onClick,
            bool closePhoneOnLaunch = false,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            List<string>? npcNames = null)
        {
            return ModEntry.RegisterChatQuickActionButtonInternal(
                ownerModId,
                actionId,
                iconTexture,
                onClick,
                closePhoneOnLaunch,
                sortOrder,
                sourceRect,
                npcNames);
        }

        public bool UnregisterChatQuickActionButton(string ownerModId, string actionId)
        {
            return ModEntry.UnregisterChatQuickActionButtonInternal(ownerModId, actionId);
        }

        public Texture2D? GetAppTexture(AppIconType appIconType)
        {
            switch (appIconType)
            {
                case AppIconType.Notification: return Textures.AppNotification;
                case AppIconType.AppStore: return Textures.AppAppStore;
                case AppIconType.Camera: return Textures.AppCamera;
                case AppIconType.Photo: return Textures.AppPhoto;
                case AppIconType.Setting: return Textures.AppSetting;
                case AppIconType.Calendar: return Textures.AppCalendar;
                default: return null;
            }
        }

        public void RetrievePhotos(int limit, bool getTexture, bool getMetadata, Action<string> onComplete, bool squareOnly = false)
        {
            ModEntry.RetrievePhotosInternal(limit, getTexture, getMetadata, onComplete, squareOnly);
        }


        public float GetPhoneUiScale()
        {
            return ModEntry.GetActivePhoneUiScale();
        }

        public int GetPhoneFrameWidth()
        {
            return ModEntry.GetScaledPhoneFrameWidth(this.GetPhoneUiScale());
        }

        public int GetPhoneFrameHeight()
        {
            return ModEntry.GetScaledPhoneFrameHeight(this.GetPhoneUiScale());
        }

        public (int offsetX, int offsetY) GetPhoneContentOffset()
        {
            float scale = this.GetPhoneUiScale();
            return (ModEntry.GetScaledPhoneContentOffsetX(scale), ModEntry.GetScaledPhoneContentOffsetY(scale));
        }

        public Texture2D? GetPhoneFrameTexture()
        {
            var texture = Textures.PhoneEmpty;
            return (texture != null && !texture.IsDisposed) ? texture : null;
        }

        public Texture2D? GetPhoneBackgroundTexture()
        {
            var texture = Textures.PhoneBackground;
            return (texture != null && !texture.IsDisposed) ? texture : null;
        }

        public Texture2D? GetCardTexture()
        {
            var texture = Textures.CardTexture;
            return (texture != null && !texture.IsDisposed) ? texture : null;
        }

        public (int x, int y) GetPhonePosition()
        {
            return (ModEntry.currentMenuX, ModEntry.currentMenuY);
        }

        public void SetPhonePosition(int x, int y)
        {
            ModEntry.currentMenuX = x;
            ModEntry.currentMenuY = y;
            if (ModEntry.phoneMenu != null)
            {
                ModEntry.phoneMenu.xPositionOnScreen = x;
                ModEntry.phoneMenu.yPositionOnScreen = y;
            }
        }

        public bool HandlePhoneAppBottomNavClick(int x, int y, int phoneX, int phoneY, Action? onBack = null)
        {
            float uiScale = ModEntry.GetActivePhoneUiScale();
            int scale(int val) => ModEntry.ScalePhoneUiValue(val, uiScale);

            var backBounds = new Rectangle(phoneX + scale(182), phoneY + scale(975), scale(64), scale(64));
            var lockBounds = new Rectangle(phoneX + scale(455), phoneY + scale(975), scale(64), scale(64));
            var homeBounds = new Rectangle(phoneX + scale(315), phoneY + scale(975), scale(64), scale(64));

            if (backBounds.Contains(x, y))
            {
                Game1.playSound("cancel");
                if (onBack != null)
                {
                    onBack();
                }
                else
                {
                    Game1.activeClickableMenu?.exitThisMenuNoSound();
                    ModEntry.OpenPhoneFromHudTrigger(forceDefault: true);
                }
                return true;
            }

            if (lockBounds.Contains(x, y))
            {
                Game1.playSound("cancel");
                Game1.activeClickableMenu?.exitThisMenuNoSound();
                return true;
            }

            if (homeBounds.Contains(x, y))
            {
                Game1.playSound("bigSelect");
                Game1.activeClickableMenu?.exitThisMenuNoSound();
                PhoneMenu.currentApp = null;
                ModEntry.OpenPhoneFromHudTrigger(forceDefault: true);
                return true;
            }

            return false;
        }

        public void DrawPhoneSizeButtons(SpriteBatch b, int phoneX, int phoneY, bool landscape = false, bool forceOn = false)
        {
            if (!forceOn && ModEntry.Config.ShowSizeButton == "Disable") return;

            float uiScale = ModEntry.GetActivePhoneUiScale();
            int scale(int val) => ModEntry.ScalePhoneUiValue(val, uiScale);

            int smallButtonY = phoneY + scale(170);
            int smallButtonW = scale(40);
            int smallButtonH = scale(40);

            Rectangle incRect, decRect, pinRect;
            bool isPinnedSupported = ModEntry.IsPassiveHudSupported(ModEntry.ActiveExternalAppId);

            if (landscape)
            {
                int frameWidth = ModEntry.GetScaledPhoneFrameWidth(uiScale);
                incRect = new Rectangle(phoneX + scale(170), phoneY + frameWidth - scale(610) - smallButtonW, smallButtonW, smallButtonH);
                decRect = new Rectangle(phoneX + scale(170) + scale(45), phoneY + frameWidth - scale(610) - smallButtonW, smallButtonW, smallButtonH);
                pinRect = new Rectangle(phoneX + scale(170) + scale(90), phoneY + frameWidth - scale(610) - smallButtonW, smallButtonW, smallButtonH);
            }
            else
            {
                incRect = new Rectangle(phoneX + scale(610), smallButtonY, smallButtonW, smallButtonH);
                decRect = new Rectangle(phoneX + scale(610), smallButtonY + scale(45), smallButtonW, smallButtonH);
                pinRect = new Rectangle(phoneX + scale(610), smallButtonY + scale(90), smallButtonW, smallButtonH);
            }

            bool showButtons = forceOn || ModEntry.Config.ShowSizeButton == "Always";
            if (!showButtons)
            {
                int mx = Game1.getMouseX(true);
                int my = Game1.getMouseY(true);
                if (decRect.Contains(mx, my) || incRect.Contains(mx, my) || (isPinnedSupported && pinRect.Contains(mx, my)))
                {
                    showButtons = true;
                }
            }

            if (showButtons)
            {
                Textures.DrawCard(b, decRect.X, decRect.Y, decRect.Width, decRect.Height, Color.White * 0.6f, 1f, false);
                Textures.DrawCard(b, incRect.X, incRect.Y, incRect.Width, incRect.Height, Color.White * 0.6f, 1f, false);

                float scaleFactor = 1f;
                Vector2 decSize = Game1.smallFont.MeasureString("-") * scaleFactor;
                b.DrawString(Game1.smallFont, "-", new Vector2(decRect.Center.X - decSize.X / 2f, decRect.Center.Y - decSize.Y / 2f), Color.Black, 0f, Vector2.Zero, scaleFactor, SpriteEffects.None, 1f);

                Vector2 incSize = Game1.smallFont.MeasureString("+") * scaleFactor;
                b.DrawString(Game1.smallFont, "+", new Vector2(incRect.Center.X - incSize.X / 2f, incRect.Center.Y - incSize.Y / 2f), Color.Black, 0f, Vector2.Zero, scaleFactor, SpriteEffects.None, 1f);

                if (isPinnedSupported)
                {
                    Color buttonBgColor = ModEntry.isHudPinned ? new Color(100, 220, 100) * 0.8f : Color.White * 0.6f;
                    Textures.DrawCard(b, pinRect.X, pinRect.Y, pinRect.Width, pinRect.Height, buttonBgColor, 1f, false);

                    Vector2 pinSize = Game1.smallFont.MeasureString("!") * scaleFactor;
                    b.DrawString(Game1.smallFont, "!", new Vector2(pinRect.Center.X - pinSize.X / 2f, pinRect.Center.Y - pinSize.Y / 2f), Color.Black, 0f, Vector2.Zero, scaleFactor, SpriteEffects.None, 1f);
                }
            }
        }

        public bool HandlePhoneSizeButtonsClick(int x, int y, int phoneX, int phoneY)
        {
            if (ModEntry.Config.ShowSizeButton == "Disable") return false;

            float uiScale = ModEntry.GetActivePhoneUiScale();
            int scale(int val) => ModEntry.ScalePhoneUiValue(val, uiScale);

            int smallButtonY = phoneY + scale(170);
            int smallButtonW = scale(40);
            int smallButtonH = scale(40);

            bool landscape = ModEntry.IsExternalAppLandscape(ModEntry.ActiveExternalAppId);
            bool isPinnedSupported = ModEntry.IsPassiveHudSupported(ModEntry.ActiveExternalAppId);

            Rectangle incRect, decRect, pinRect;
            if (landscape)
            {
                int frameWidth = ModEntry.GetScaledPhoneFrameWidth(uiScale);
                incRect = new Rectangle(phoneX + scale(170), phoneY + frameWidth - scale(610) - smallButtonW, smallButtonW, smallButtonH);
                decRect = new Rectangle(phoneX + scale(170) + scale(45), phoneY + frameWidth - scale(610) - smallButtonW, smallButtonW, smallButtonH);
                pinRect = new Rectangle(phoneX + scale(170) + scale(90), phoneY + frameWidth - scale(610) - smallButtonW, smallButtonW, smallButtonH);
            }
            else
            {
                incRect = new Rectangle(phoneX + scale(610), smallButtonY, smallButtonW, smallButtonH);
                decRect = new Rectangle(phoneX + scale(610), smallButtonY + scale(45), smallButtonW, smallButtonH);
                pinRect = new Rectangle(phoneX + scale(610), smallButtonY + scale(90), smallButtonW, smallButtonH);
            }

            if (decRect.Contains(x, y))
            {
                ModEntry.Instance.AdjustPhoneSize(-0.1f);
                return true;
            }
            if (incRect.Contains(x, y))
            {
                ModEntry.Instance.AdjustPhoneSize(0.1f);
                return true;
            }
            if (isPinnedSupported && pinRect.Contains(x, y))
            {
                ModEntry.isHudPinned = !ModEntry.isHudPinned;
                Game1.playSound("drumkit6");
                return true;
            }

            return false;
        }

        public string GetDecreaseSizeKey()
        {
            return ModEntry.Config?.DecreasePhoneSizeKey.ToString() ?? "OemComma";
        }

        public string GetIncreaseSizeKey()
        {
            return ModEntry.Config?.IncreasePhoneSizeKey.ToString() ?? "OemPeriod";
        }

        public void AdjustPhoneSize(float amount)
        {
            ModEntry.Instance.AdjustPhoneSize(amount);
        }

        public bool RegisterPassiveHudOverlay(
            string ownerModId,
            string appId,
            Action<SpriteBatch, Rectangle> onDrawHudOverlay,
            Func<int, int, bool>? onLeftClick = null,
            Action<int, int>? onLeftClickHeld = null,
            Action? onReleaseLeftClick = null,
            Func<int>? getOverlayHeight = null)
        {
            return ModEntry.RegisterPassiveHudOverlayInternal(
                ownerModId,
                appId,
                onDrawHudOverlay,
                onLeftClick,
                onLeftClickHeld,
                onReleaseLeftClick,
                getOverlayHeight);
        }
    }

    public partial class ModEntry : Mod
    {
        public static class ContactActionCardsManager
        {
            public class Card
            {
                public string ModId { get; set; } = string.Empty;
                public string Title { get; set; } = string.Empty;
                public IList<IContactActionCardButton> Buttons { get; set; } = new List<IContactActionCardButton>();
                public List<string>? AvailableNpcNames { get; set; } = null;
            }

            public static List<Card> Cards = new();
        }

        public static ModEntry Instance;

        public static IMonitor SMonitor;
        public static IModHelper SHelper;

        internal ModApi apiInstance;

        public override object GetApi()
        {
            return this.apiInstance ??= new ModApi(Monitor);
        }

        public static ModConfig Config;

        public static JToken eventString;

        public static string currentPhoneTheme = "Default";
        public static string currentPhoneBackground = "";
        public static string currentPhoneLandscapeBackground = "";
        public static string currentPhoneSound = "getNewSpecialItem";
        public static string currentPhoneTextColor = "Black";
        public static string lockScreenPin1 = "builtin:phone";
        public static string lockScreenPin2 = "builtin:camera";

        public static bool takeScreenshot = false;
        public static int currentMenuX;
        public static int currentMenuY;
        public static bool cameraLandscapeMode = false;
        public static bool cameraSquareMode = false;
        public static bool cameraFlashMode = false;
        public static float cameraZoomFactor = 1f;

        public static bool pendingInitNotification = false;
        public static bool pendingPhoneOsInitialization = false;
        public static bool hasNewVersionAvailable = false;


        private static readonly object SaveFolderNameLock = new();
        private static string activeSaveFolderName = string.Empty;

        public static PhoneMenu phoneMenu;
        private Dictionary<string, Dictionary<string, AreaData>> areaTags = new();
        public static string GetActiveSaveFolderName()
        {
            lock (SaveFolderNameLock)
            {
                if (string.IsNullOrWhiteSpace(activeSaveFolderName))
                    activeSaveFolderName = ResolveSaveFolderNameFromContext();

                return activeSaveFolderName;
            }
        }

        public static string GetSaveDataPath(string fileName = "")
        {
            string normalizedFileName = (fileName ?? string.Empty)
                .Trim()
                .TrimStart('/', '\\');

            if (string.IsNullOrWhiteSpace(normalizedFileName))
                return $"./userdata/{GetActiveSaveFolderName()}";

            return $"./userdata/{GetActiveSaveFolderName()}/{normalizedFileName}";
        }

        public static void RefreshActiveSaveFolderName()
        {
            string resolved = ResolveSaveFolderNameFromContext();
            lock (SaveFolderNameLock)
                activeSaveFolderName = resolved;
        }

        public static void SetActiveSaveFolderName(string saveFolderName)
        {
            string normalizedSaveFolderName = NormalizeSaveFolderName(saveFolderName);
            if (string.IsNullOrWhiteSpace(normalizedSaveFolderName))
                return;

            lock (SaveFolderNameLock)
                activeSaveFolderName = normalizedSaveFolderName;
        }

        public static void ClearActiveSaveFolderName()
        {
            lock (SaveFolderNameLock)
                activeSaveFolderName = string.Empty;
        }

        private static string ResolveSaveFolderNameFromContext()
        {
            string constantsSaveFolder = NormalizeSaveFolderName(Constants.SaveFolderName);

            if (!string.IsNullOrWhiteSpace(constantsSaveFolder))
            {
                int underscoreIndex = constantsSaveFolder.IndexOf('_');

                // If an underscore is found, return from the underscore onwards
                if (underscoreIndex != -1)
                {
                    return constantsSaveFolder.Substring(underscoreIndex); // e.g., "_12345"
                }
            }

            long uniqueId = 0;

            if (Context.IsWorldReady && Context.IsMultiplayer && Game1.MasterPlayer != null)
            {
                uniqueId = Game1.MasterPlayer.UniqueMultiplayerID;
            }
            else if (Context.IsWorldReady && Game1.player != null)
            {
                uniqueId = Game1.player.UniqueMultiplayerID;
            }

            if (uniqueId > 0)
                return $"_{uniqueId}";

            if (!string.IsNullOrWhiteSpace(constantsSaveFolder))
            {
                int lastUnderscore = constantsSaveFolder.LastIndexOf('_');
                if (lastUnderscore >= 0 && lastUnderscore < constantsSaveFolder.Length - 1)
                {
                    string possibleId = constantsSaveFolder.Substring(lastUnderscore + 1);
                    if (long.TryParse(possibleId, out _))
                        return $"_{possibleId}";
                }

                return constantsSaveFolder;
            }

            return "default";
        }

        private static string NormalizeSaveFolderName(string saveFolderName)
        {
            string normalizedValue = (saveFolderName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
                return string.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(normalizedValue.Length);
            foreach (char character in normalizedValue)
            {
                if (character == '/'
                    || character == '\\'
                    || Array.IndexOf(invalidChars, character) >= 0)
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString().Trim();
        }



        public static Texture2D GetPlayerPhotoTextureInternal(string photoName)
        {
            string photoPath = Path.Combine(SHelper.DirectoryPath, "userdata", GetActiveSaveFolderName(), "photo_player", photoName);
            if (File.Exists(photoPath))
            {
                using var stream = new FileStream(photoPath, FileMode.Open, FileAccess.Read);
                return Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
            }
            return null;
        }

        public static string GetPlayerPhotoMetadataInternal(string photoName)
        {
            if (ImageMetadataStore.TryGetValue(photoName, out var metadata))
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(metadata);
            }
            return null;
        }

        // =========================================================================================
        // =========================================================================================
        // =========================================================================================





        public static void RetrievePhotosInternal(int limit, bool getTexture, bool getMetadata, Action<string> onComplete, bool squareOnly = false)
        {
            if (limit <= 0)
            {
                onComplete?.Invoke("[]");
                return;
            }

            EnsurePhoneMenuUsesCurrentScale();

            // Set state on phone menu
            phoneMenu.StartPhotoSelectionApiMode(limit, getTexture, getMetadata, onComplete, squareOnly);

            // Open the phone menu and bypass lock screen, going directly to the photo app
            phoneMenu.rootLandingState = PhoneMenu.RootLandingState.Home;
            phoneMenu.OpenPhotoApp();
            Game1.activeClickableMenu = phoneMenu;
        }

        public static void LaunchNpcPhoneDialogue(NPC npc)
        {
            npc.checkForNewCurrentDialogue(Game1.player.getFriendshipHeartLevelForNPC(npc.Name));

            if (npc.currentMarriageDialogue != null && npc.currentMarriageDialogue.Count > 0)
            {
                if (npc.CurrentDialogue == null)
                    npc.CurrentDialogue = new Stack<Dialogue>();

                for (int i = npc.currentMarriageDialogue.Count - 1; i >= 0; i--)
                {
                    var dialogueRef = npc.currentMarriageDialogue[i];
                    Dialogue actualDialogue = dialogueRef.GetDialogue(npc);

                    if (actualDialogue != null)
                    {
                        npc.CurrentDialogue.Push(actualDialogue);
                    }
                }

                npc.currentMarriageDialogue.Clear();
            }

            var dialogueStack = npc.CurrentDialogue;

            if (dialogueStack != null && dialogueStack.Count > 0)
            {
                Game1.drawDialogue(npc);

                if (dialogueStack.Count > 0)
                {
                    void OnUpdateTicked(object? sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
                    {
                        if (Game1.activeClickableMenu == null)
                        {
                            if (npc.CurrentDialogue != null && npc.CurrentDialogue.Count > 0)
                            {
                                Game1.drawDialogue(npc);
                            }

                            if (npc.CurrentDialogue == null || npc.CurrentDialogue.Count == 0)
                            {
                                SHelper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
                            }
                        }
                    }

                    SHelper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
                }
            }
            else
            {
                Game1.activeClickableMenu = new DialogueBox(SHelper.Translation.Get("ui.message.npc_hello_thanks_calling", new { npcName = npc.displayName }));
            }

            bool isFriend = Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship? friendship);

            if (isFriend && !friendship.TalkedToToday)
            {
                friendship.Points += 20;
                friendship.TalkedToToday = true;
            }

        }
    }
}