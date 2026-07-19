using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;

namespace Smartphone
{
    public class ModConfig
    {
        public SButton ModKey { get; set; } = SButton.H;

        public int PlayerMaxPhoto { get; set; } = 100;
        public float PlayerCaptureWorldFlashRadius { get; set; } = 3f;

        public SButton DecreasePhoneSizeKey { get; set; } = SButton.OemComma;
        public SButton IncreasePhoneSizeKey { get; set; } = SButton.OemPeriod;
        public float PhoneSize { get; set; } = 1f;
        public string ShowSizeButton { get; set; } = "Always";
        public int HudPhoneIconOffsetX { get; set; } = 0;
        public int HudPhoneIconOffsetY { get; set; } = 0;
        public float HudPhoneIconScale { get; set; } = 1.0f;

        public bool NotifyNotification { get; set; } = true;
        public bool DisableNotificationOnPhoneIcon { get; set; } = false;

        public bool DisableUpdateWarning { get; set; } = false;
        public bool ShowPhoneIcon { get; set; } = true;
        public bool PreferPortraitIconHud { get; set; } = true;

        public string AllowedNpc { get; set; } = "Abigail, Alex, Caroline, Clint, Demetrius, Elliott, Emily, Evelyn, George, Gus, Haley, Harvey, Jas, Jodi, Kent, Leah, Leo, Lewis, Linus, Marnie, Maru, Pam, Penny, Pierre, Robin, Sam, Sandy, Sebastian, Shane, Vincent, Willy, Wizard, Alesia, Andy, Camilla, Claire, Gunther, Isaac, MarlonFay, Martin, MorrisTod, Olivia, Sophia, Susan, Scarlett, Victor";
        public string FriendshipRequirement { get; set; } = "Meet";

        public bool RestoreStamina { get; set; } = true;
        public float StaminaRestoreRate { get; set; } = 0.5f;

        public bool BackgroundFitFullscreen { get; set; } = true;
        public int BackgroundDistortion { get; set; } = 4;
        public float BackgroundBlackening { get; set; } = 0.10f;

        public string PhotoPreviewQuality { get; set; } = "Medium";
    }
}
