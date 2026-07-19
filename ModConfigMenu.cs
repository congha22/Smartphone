using ContentPatcher;
using StardewModdingAPI;
using System;

namespace Smartphone
{

    public partial class ModEntry
    {

        public static void ConfigMenu(IContentPatcherAPI api, IManifest ModManifest, IModHelper Helper)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<Smartphone.Data.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () =>
                {
                    Config = new ModConfig();
                },
                save: () =>
                {
                    Helper.WriteConfig(Config);
                }
            );

            // main page: options most players change often
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.title.quick_setup"));

            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.open_phone_key"),
                tooltip: () => Helper.Translation.Get("config.tooltip.open_phone_key"),
                getValue: () => Config.ModKey,
                setValue: value => Config.ModKey = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.disable_update_warning"),
                tooltip: () => Helper.Translation.Get("config.tooltip.disable_update_warning"),
                getValue: () => Config.DisableUpdateWarning,
                setValue: value => Config.DisableUpdateWarning = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.friendship_requirement"),
                tooltip: () => Helper.Translation.Get("config.tooltip.friendship_requirement"),
                getValue: () => Config.FriendshipRequirement,
                setValue: value => Config.FriendshipRequirement = value,
                allowedValues: new string[] { "Meet", "Friend" },
                formatAllowedValue: value => Helper.Translation.Get($"config.value.{value.ToLower()}")
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.prefer_portrait_icon_hud"),
                tooltip: () => Helper.Translation.Get("config.tooltip.prefer_portrait_icon_hud"),
                getValue: () => Config.PreferPortraitIconHud,
                setValue: value => Config.PreferPortraitIconHud = value
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "storage-limits",
                text: () => Helper.Translation.Get("config.page.storage_limits"),
                tooltip: () => Helper.Translation.Get("config.tooltip.storage_limits")
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "display",
                text: () => Helper.Translation.Get("config.page.display"),
                tooltip: () => Helper.Translation.Get("config.tooltip.display")
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "notifications",
                text: () => Helper.Translation.Get("config.page.notifications"),
                tooltip: () => Helper.Translation.Get("config.tooltip.notifications")
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "misc",
                text: () => Helper.Translation.Get("config.page.misc"),
                tooltip: () => Helper.Translation.Get("config.tooltip.misc")
            );

            // storage and limits page
            configMenu.AddPage(mod: ModManifest, pageId: "storage-limits", pageTitle: () => Helper.Translation.Get("config.page.storage_limits"));

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.player_photos_to_keep"),
                tooltip: () => Helper.Translation.Get("config.tooltip.player_photos_to_keep"),
                getValue: () => Config.PlayerMaxPhoto,
                setValue: value => Config.PlayerMaxPhoto = Math.Clamp(value, 1, 500),
                min: 1,
                max: 500
            );

            // display page
            configMenu.AddPage(mod: ModManifest, pageId: "display", pageTitle: () => Helper.Translation.Get("config.page.display"));

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.photo_preview_quality"),
                tooltip: () => Helper.Translation.Get("config.tooltip.photo_preview_quality"),
                getValue: () => Config.PhotoPreviewQuality,
                setValue: value => Config.PhotoPreviewQuality = value,
                allowedValues: new string[] { "Low", "Medium", "High" },
                formatAllowedValue: value => Helper.Translation.Get($"config.value.{value.ToLower()}")
            );

            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => Helper.Translation.Get("config.paragraph.phone_size")
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.phone_size"),
                tooltip: () => Helper.Translation.Get("config.tooltip.phone_size"),
                getValue: () => Math.Clamp(Config.PhoneSize, 0.7f, 1.5f),
                setValue: value =>
                {
                    float clamped = Math.Clamp(value, 0.7f, 1.5f);
                    Config.PhoneSize = MathF.Round(clamped * 10f) / 10f;
                },
                min: 0.7f,
                max: 1.5f,
                interval: 0.1f,
                formatValue: value => $"{value:0.0}"
            );

            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.decrease_phone_size_key"),
                tooltip: () => Helper.Translation.Get("config.tooltip.decrease_phone_size_key"),
                getValue: () => Config.DecreasePhoneSizeKey,
                setValue: value => Config.DecreasePhoneSizeKey = value
            );

            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.increase_phone_size_key"),
                tooltip: () => Helper.Translation.Get("config.tooltip.increase_phone_size_key"),
                getValue: () => Config.IncreasePhoneSizeKey,
                setValue: value => Config.IncreasePhoneSizeKey = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.show_size_button"),
                tooltip: () => Helper.Translation.Get("config.tooltip.show_size_button"),
                getValue: () => Config.ShowSizeButton,
                setValue: value => Config.ShowSizeButton = value,
                allowedValues: new string[] { "Disable", "Hover", "Always" },
                formatAllowedValue: value => Helper.Translation.Get($"config.value.show_size_button.{value.ToLower()}")
            );

            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => Helper.Translation.Get("config.paragraph.phone_icon")
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.show_phone_icon"),
                tooltip: () => Helper.Translation.Get("config.tooltip.show_phone_icon"),
                getValue: () => Config.ShowPhoneIcon,
                setValue: value => Config.ShowPhoneIcon = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.phone_icon_x"),
                tooltip: () => Helper.Translation.Get("config.tooltip.phone_icon_x"),
                getValue: () => Config.HudPhoneIconOffsetX,
                setValue: value => Config.HudPhoneIconOffsetX = Math.Clamp(value, -50000, 50000),
                min: -50000,
                max: 50000,
                interval: 1
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.phone_icon_y"),
                tooltip: () => Helper.Translation.Get("config.tooltip.phone_icon_y"),
                getValue: () => Config.HudPhoneIconOffsetY,
                setValue: value => Config.HudPhoneIconOffsetY = Math.Clamp(value, -50000, 50000),
                min: -50000,
                max: 50000,
                interval: 1
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.phone_icon_scale"),
                tooltip: () => Helper.Translation.Get("config.tooltip.phone_icon_scale"),
                getValue: () => Config.HudPhoneIconScale,
                setValue: value => Config.HudPhoneIconScale = Math.Clamp(value, 1f, 6f),
                min: 1.0f,
                max: 6.0f,
                interval: 0.1f,
                formatValue: value => $"{value:0.00}"
            );

            // notification page
            configMenu.AddPage(mod: ModManifest, pageId: "notifications", pageTitle: () => Helper.Translation.Get("config.page.notifications"));

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.notification_popups"),
                tooltip: () => Helper.Translation.Get("config.tooltip.notification_popups"),
                getValue: () => Config.NotifyNotification,
                setValue: value => Config.NotifyNotification = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.disable_notification_on_phone_icon"),
                tooltip: () => Helper.Translation.Get("config.tooltip.disable_notification_on_phone_icon"),
                getValue: () => Config.DisableNotificationOnPhoneIcon,
                setValue: value => Config.DisableNotificationOnPhoneIcon = value
            );

            // misc page
            configMenu.AddPage(mod: ModManifest, pageId: "misc", pageTitle: () => Helper.Translation.Get("config.page.misc"));

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.allowed_npc"),
                tooltip: () => Helper.Translation.Get("config.tooltip.allowed_npc"),
                getValue: () => Config.AllowedNpc,
                setValue: value => Config.AllowedNpc = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.camera_flash_radius"),
                tooltip: () => Helper.Translation.Get("config.tooltip.camera_flash_radius"),
                getValue: () => Math.Clamp(Config.PlayerCaptureWorldFlashRadius, 1f, 10f),
                setValue: value =>
                {
                    float clamped = Math.Clamp(value, 1f, 10f);
                    Config.PlayerCaptureWorldFlashRadius = MathF.Round(clamped * 10f) / 10f;
                },
                min: 1f,
                max: 10f,
                interval: 0.1f,
                formatValue: value => $"{value:0.0}"
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.restore_stamina"),
                tooltip: () => Helper.Translation.Get("config.tooltip.restore_stamina"),
                getValue: () => Config.RestoreStamina,
                setValue: value => Config.RestoreStamina = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.stamina_restore_rate"),
                tooltip: () => Helper.Translation.Get("config.tooltip.stamina_restore_rate"),
                getValue: () => Config.StaminaRestoreRate,
                setValue: value => Config.StaminaRestoreRate = Math.Clamp(value, 0f, 3f),
                min: 0f,
                max: 3f,
                interval: 0.1f,
                formatValue: value => $"{value:0.0}"
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.background_fit_fullscreen"),
                tooltip: () => Helper.Translation.Get("config.tooltip.background_fit_fullscreen"),
                getValue: () => Config.BackgroundFitFullscreen,
                setValue: value => Config.BackgroundFitFullscreen = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.background_distortion"),
                tooltip: () => Helper.Translation.Get("config.tooltip.background_distortion"),
                getValue: () => Config.BackgroundDistortion,
                setValue: value => Config.BackgroundDistortion = Math.Clamp(value, 0, 10),
                min: 0,
                max: 10,
                interval: 1,
                formatValue: value => value == 0 ? Helper.Translation.Get("config.value.off") : $"{value}"
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.background_blackening"),
                tooltip: () => Helper.Translation.Get("config.tooltip.background_blackening"),
                getValue: () => Config.BackgroundBlackening,
                setValue: value => Config.BackgroundBlackening = (float)Math.Round(Math.Clamp(value, 0f, 0.9f), 2),
                min: 0.0f,
                max: 0.9f,
                interval: 0.05f,
                formatValue: value => $"{value * 100:0}%"
            );
        }

    }

}