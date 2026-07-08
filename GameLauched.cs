using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ContentPatcher;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp.Processing;
using Smartphone.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Crops;
using StardewValley.GameData.LocationContexts;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;
using StardewValley.Monsters;
using StardewValley.Network;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using StardewValley.Triggers;
using xTile.Dimensions;
using xTile.Tiles;
using static StardewValley.Minigames.MineCart;

namespace Smartphone
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry
    {
        internal const int PhoneFrameBaseWidth = 700;
        internal const int PhoneFrameBaseHeight = 1100;
        internal const int PhoneDefaultMenuOffsetX = 450;
        internal const int PhoneDefaultMenuOffsetY = 550;
        internal const int PhoneFrameContentOffsetX = 90;
        internal const int PhoneFrameContentOffsetY = 166;
        internal const float PhoneSmallUiScale = 0.75f;

        private const int CameraViewportOffsetX = PhoneFrameContentOffsetX;
        private const int CameraViewportOffsetY = PhoneFrameContentOffsetY;
        private const int CameraViewportWidth = 520;
        private const int CameraViewportHeight = 810;


        public static HashSet<string> ContactableNpcs = new(StringComparer.OrdinalIgnoreCase);
        // *************************** ENTRY ***************************
        //


        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();


            ModEntry.Instance = this;


            SMonitor = Monitor;
            SHelper = helper;


            Textures.LoadTextures();
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += OnGameLauched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.TimeChanged += OnTimeChange;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            helper.Events.Display.WindowResized += OnWindowResized;

            helper.Events.Display.Rendered += OnRendered;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

            // dev tool: prepare for grid overlay
            solidPixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            solidPixel.SetData(new[] { Color.White });
        }







        //
        // *************************** END OF ENTRY ***************************
        //

        private void OnGameLauched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            this.Monitor.Log("Loading Smartphone", LogLevel.Info);
            var api = this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");

            ConfigMenu(api, this.ModManifest, Helper);

            AppStoreManager.Initialize();

        }



        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;


            bool canOpenPhoneMenu = Game1.activeClickableMenu == null && Game1.currentMinigame == null;

            bool isPhoneMenuOpen = Game1.activeClickableMenu != null && Game1.activeClickableMenu == phoneMenu;
            bool isTyping = Game1.keyboardDispatcher.Subscriber != null;
            if (!isTyping && (canOpenPhoneMenu || isPhoneMenuOpen) && e.Button == Config.DecreasePhoneSizeKey)
            {
                AdjustPhoneSize(-0.1f);
                Helper.Input.Suppress(e.Button);
                return;
            }
            if (!isTyping && (canOpenPhoneMenu || isPhoneMenuOpen) && e.Button == Config.IncreasePhoneSizeKey)
            {
                AdjustPhoneSize(0.1f);
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (e.Button == Config.ModKey && canOpenPhoneMenu)
            {
                OpenPhoneFromHudTrigger();
                return;
            }

            if (HandleHudIconInteraction(e, canOpenPhoneMenu))
            {
                return;
            }

            // DEVTOOL
            // if (e.Button == SButton.O && Game1.activeClickableMenu == null && true)
            // {
            //     isGridVisible = !isGridVisible;
            //     Game1.chatBox.addInfoMessage($"Grid {(isGridVisible ? "enabled" : "disabled")}.");
            //     ToggleGrid(e);
            //     firstClickTile = null;
            //     return;
            // }
            // if (e.Button == SButton.MouseLeft)
            // {
            //     var tile = e.Cursor.Tile;
            //     Game1.chatBox.addErrorMessage((IsWalkableWarpTile(Game1.currentLocation, (int)tile.X, (int)tile.Y) && IsWalkableWarpTile(Game1.currentLocation, (int)tile.X, (int)tile.Y - 1)).ToString());
            // }
        }

        internal void AdjustPhoneSize(float amount)
        {
            if (Config == null)
                return;

            float currentSize = Config.PhoneSize;
            float newSize = currentSize + amount;
            newSize = Math.Clamp(newSize, 0.7f, 1.5f);
            newSize = MathF.Round(newSize, 1);

            if (Math.Abs(newSize - currentSize) > 0.001f)
            {
                Config.PhoneSize = newSize;
                Helper.WriteConfig(Config);

                if (phoneMenu != null)
                {
                    phoneMenu.UpdateScale(newSize);
                }
            }
        }


        private void OnSaving(object sender, SavingEventArgs e)
        {
            if (phoneMenu != null)
                phoneMenu.ClosePhoneMenu();
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            RefreshActiveSaveFolderName();
            RefreshInitStateForCurrentSave();
            hasNewVersionAvailable = false;

            LoadImageTags();

            PhoneMenu.UpdateNpcNumbers();

            string targetModId = this.ModManifest.UniqueID;
            var modInfo = this.Helper.ModRegistry.Get(targetModId);

            if (modInfo != null)
            {
                Task.Run(async () =>
                {
                    bool hasNewerVersion = await CheckForNewerVersion(modInfo);
                    hasNewVersionAvailable = hasNewerVersion;

                    if (hasNewerVersion && !Config.DisableUpdateWarning)
                    {
                        DelayedAction.functionAfterDelay(() =>
                        {
                            try
                            {
                                SMonitor.Log($"Smartphone: Newer version available", LogLevel.Warn);

                                NotificationManager.AddNotification(ModEntry.SHelper.Translation.Get("notification.update_warning"));
                            }
                            catch (Exception ex)
                            {
                                SMonitor.Log($"Smartphone: Unable to notify about newer version: {ex}", LogLevel.Trace);
                            }
                        }, 10000);
                    }
                });
            }


        }



        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            NotificationManager.LoadNotificationData();
            PhoneMenu.RefreshCalendarData();

            PhoneMenu.UpdateNpcNumbers();
        }

        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            ClearActiveSaveFolderName();
            ModEntry.ContactableNpcs.Clear();
            PhoneMenu.phoneAppDataLoaded = false;

            pendingInitNotification = false;
            pendingPhoneOsInitialization = false;
            hasNewVersionAvailable = false;

            hudPhoneRenderTarget?.Dispose();
            hudPhoneRenderTarget = null;
        }

        private void OnTimeChange(object sender, TimeChangedEventArgs e)
        {
            if (pendingInitNotification && Context.IsPlayerFree)
            {
                Game1.drawLetterMessage(ModEntry.SHelper.Translation.Get("mail.first_time"));

                NotificationManager.AddNotification(ModEntry.SHelper.Translation.Get("notification.first_time"), "Smartphone");

                pendingInitNotification = false;
            }

            NotificationManager.AddNotification(Game1.timeOfDay.ToString(), "Smartphone");
        }


        private void OnWindowResized(object sender, WindowResizedEventArgs e)
        {
            if (phoneMenu == null)
                return;

            phoneMenu.ResetToDefaultPosition();
        }



        internal static float GetConfiguredPhoneUiScale()
        {
            return Config != null ? Math.Clamp(Config.PhoneSize, 0.7f, 1.5f) : 1f;
        }

        internal static float GetActivePhoneUiScale()
        {
            if (phoneMenu != null)
                return phoneMenu.PhoneUiScale;

            return GetConfiguredPhoneUiScale();
        }

        internal static int ScalePhoneUiValue(int baseValue, float scale)
        {
            return (int)Math.Round(baseValue * scale);
        }

        private static float ResolvePhoneUiScale(float? scale)
        {
            return scale ?? GetConfiguredPhoneUiScale();
        }

        internal static int GetScaledPhoneFrameWidth(float? scale = null)
        {
            return Math.Max(1, ScalePhoneUiValue(PhoneFrameBaseWidth, ResolvePhoneUiScale(scale)));
        }

        internal static int GetScaledPhoneFrameHeight(float? scale = null)
        {
            return Math.Max(1, ScalePhoneUiValue(PhoneFrameBaseHeight, ResolvePhoneUiScale(scale)));
        }

        internal static int GetScaledPhoneDefaultMenuOffsetX(float? scale = null)
        {
            return ScalePhoneUiValue(PhoneDefaultMenuOffsetX, ResolvePhoneUiScale(scale));
        }

        internal static int GetScaledPhoneDefaultMenuOffsetY(float? scale = null)
        {
            return ScalePhoneUiValue(PhoneDefaultMenuOffsetY, ResolvePhoneUiScale(scale));
        }

        internal static int GetScaledPhoneContentOffsetX(float? scale = null)
        {
            return ScalePhoneUiValue(PhoneFrameContentOffsetX, ResolvePhoneUiScale(scale));
        }

        internal static int GetScaledPhoneContentOffsetY(float? scale = null)
        {
            return ScalePhoneUiValue(PhoneFrameContentOffsetY, ResolvePhoneUiScale(scale));
        }

        internal static int GetScaledCameraViewportWidth(float? scale = null)
        {
            return Math.Max(1, ScalePhoneUiValue(CameraViewportWidth, ResolvePhoneUiScale(scale)));
        }

        internal static int GetScaledCameraViewportHeight(float? scale = null)
        {
            return Math.Max(1, ScalePhoneUiValue(CameraViewportHeight, ResolvePhoneUiScale(scale)));
        }

        internal static void EnsurePhoneMenuUsesCurrentScale()
        {
            float configuredScale = GetConfiguredPhoneUiScale();
            if (phoneMenu == null || !phoneMenu.UsesPhoneUiScale(configuredScale))
                phoneMenu = new PhoneMenu();
        }

        public static void OpenPhoneFromHudTrigger()
        {
            EnsurePhoneMenuUsesCurrentScale();
            PhoneMenu.UpdateNpcNumbers();

            phoneMenu.OpenLockScreen();
            Game1.activeClickableMenu = phoneMenu;
        }






        private static async Task<(bool IsLatest, string? LatestVersion, string? LatestUrl)> CheckForModUpdate(IModInfo modInfo)
        {
            if (modInfo?.Manifest == null)
                return (true, null, null);

            var request = new
            {
                mods = new[]
                {
                    new
                    {
                        id = modInfo.Manifest.UniqueID,
                        updateKeys = modInfo.Manifest.UpdateKeys,
                        installedVersion = modInfo.Manifest.Version.ToString(),
                        isBroken = false
                    }
                },
                apiVersion = Constants.ApiVersion.ToString(),
                gameVersion = Game1.version.ToString(),
                platform = Constants.TargetPlatform.ToString(),
                includeExtendedMetadata = false
            };
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request, jsonOptions),
                Encoding.UTF8,
                "application/json"
            );

            using HttpResponseMessage response = await new HttpClient().PostAsync($"https://smapi.io/api/v{Constants.ApiVersion}/mods", content);
            response.EnsureSuccessStatusCode();

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement result = document.RootElement[0];

            if (result.TryGetProperty("suggestedUpdate", out JsonElement suggestedUpdate) && suggestedUpdate.ValueKind == JsonValueKind.Object)
            {
                return (
                    false,
                    suggestedUpdate.GetProperty("version").GetString(),
                    suggestedUpdate.GetProperty("url").GetString()
                );
            }

            return (true, null, null);
        }

        public static async Task<bool> CheckForNewerVersion(IModInfo? modInfo)
        {
            if (modInfo?.Manifest == null)
                return false;

            var update = await CheckForModUpdate(modInfo);
            return !update.IsLatest;
        }

        public static void RefreshInitStateForCurrentSave()
        {
            string saveFolderPath = Path.Combine(
                SHelper.DirectoryPath,
                "userdata",
                GetActiveSaveFolderName());

            bool isFirstTimeForSave = !Directory.Exists(saveFolderPath);
            if (isFirstTimeForSave)
            {
                Directory.CreateDirectory(saveFolderPath);
            }

            pendingInitNotification = isFirstTimeForSave;
            pendingPhoneOsInitialization = isFirstTimeForSave;
        }

        private void OnOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.player == null)
                return;

            if (Config.RestoreStamina && Game1.activeClickableMenu != null)
            {
                var menu = Game1.activeClickableMenu;
                if (menu == phoneMenu || IsCustomPhoneMenu(menu))
                {
                    if (Game1.player.Stamina < Game1.player.MaxStamina)
                    {
                        Game1.player.Stamina = Math.Min(Game1.player.MaxStamina, Game1.player.Stamina + Config.StaminaRestoreRate);
                    }
                }
            }
        }

        private bool IsCustomPhoneMenu(IClickableMenu menu)
        {
            if (menu == null) return false;
            if (menu is PhoneMenu) return true;

            try
            {
                var fields = menu.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (field.FieldType.Name == "ISmartPhoneApi" || field.FieldType.GetInterface("ISmartPhoneApi") != null)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Fallback
            }
            return false;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            UpdateHudIconDragging();
        }
    }
}
