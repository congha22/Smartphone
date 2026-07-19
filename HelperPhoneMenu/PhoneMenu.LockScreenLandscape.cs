using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Smartphone
{
    /// <summary>
    /// Partial class implementation handling landscape lockscreen rendering for HUD icon preview.
    /// </summary>
    public partial class PhoneMenu
    {
        /// <summary>
        /// Draws the landscape lockscreen containing centered time text, date text, and weather summary.
        /// </summary>
        /// <param name="b">The active SpriteBatch to draw with.</param>
        /// <param name="xOffset">Horizontal offset used for screen transition animations.</param>
        internal void DrawLockScreenLandscapeScreen(SpriteBatch b, int xOffset)
        {
            Rectangle contentBounds = GetPhoneContentBounds();
            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
                return;

            DrawWithinPhoneContentClip(b, () =>
            {
                // Draw background image scaled to content bounds
                DrawLockScreenBackground(b, 0, isLandscape: true);

                float centerX = contentBounds.Center.X + xOffset;

                float timeScale = GetPhoneTextScale(LockScreenTimeTextScale);
                float dateScale = GetPhoneTextScale(LockScreenDateTextScale);

                string timeText = Game1.getTimeOfDayString(Game1.timeOfDay);
                string dateText = BuildLockScreenDateText();

                Vector2 timeSize = Game1.dialogueFont.MeasureString(timeText) * timeScale;
                Vector2 dateSize = Game1.smallFont.MeasureString(dateText) * dateScale;

                // Measure total height of Time + Date + Weather block
                float weatherLabelHeight = Game1.smallFont.MeasureString("Today").Y * GetPhoneTextScale(LockScreenWeatherLabelTextScale);
                float weatherIconHeight = LockScreenWeatherIconHeight * LockScreenWeatherIconScale * phoneUiScale;
                float weatherTotalHeight = weatherLabelHeight + ScaleUiValue(4f) + weatherIconHeight;

                float gapTimeDate = ScaleUiValue(8f);
                float gapDateWeather = ScaleUiValue(16f);

                float totalBlockHeight = timeSize.Y + gapTimeDate + dateSize.Y + gapDateWeather + weatherTotalHeight;

                // Center vertically within contentBounds
                float startY = contentBounds.Center.Y - (totalBlockHeight / 2f);

                Vector2 timePos = new Vector2(centerX - (timeSize.X / 2f), startY);
                Vector2 datePos = new Vector2(centerX - (dateSize.X / 2f), startY + timeSize.Y + gapTimeDate);
                float weatherTopY = datePos.Y + dateSize.Y + gapDateWeather;

                // Draw Time Text
                DrawShadowedText(
                    b,
                    Game1.dialogueFont,
                    timeText,
                    timePos,
                    Color.White,
                    new Color(0, 0, 0, 180),
                    timeScale);

                // Draw Date Text
                DrawShadowedText(
                    b,
                    Game1.smallFont,
                    dateText,
                    datePos,
                    Color.White,
                    new Color(0, 0, 0, 180),
                    dateScale);

                // Draw Weather Summary
                DrawLockScreenWeatherSummary(
                    b,
                    contentBounds,
                    centerX,
                    weatherTopY);
            });
        }
    }
}
