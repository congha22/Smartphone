using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        // ─────────────────────────────────────────────────────────────
        //  Layout constants (base units, scaled at runtime)
        // ─────────────────────────────────────────────────────────────

        private const int PhotoNavBarHeightBase = 54;   // fixed top bar
        private const int PhotoCellColsCount = 3;
        private const int PhotoCellHeightDivisor = 4;    // 25 % of content height
        private const int PhotoDividerPx = 2;    // white gap between cells

        private const int PhotoAlbumSectionPadBase = 14;
        private const int PhotoAlbumTitleHeightBase = 50;
        private const int PhotoAlbumTileCols = 3;
        private const int PhotoAlbumTileGapBase = 6;
        private const int PhotoAlbumTileTextHeight = 26;   // extra height below album image
        private const int PhotoAlbumTileNamePadBase = 4;

        private const int PhotoScrollSpeed = 8;    // wheel pixels per notch
        private const float PhotoScrollLerpSpeed = 18f;

        private const int PhotoNavBarTextOffsetBase = 8;
        private const int PhotoNavBtnWidthBase = 64;
        private const int PhotoNavBtnHeightBase = 38;

        private const int PhotoModalWidthBase = 440;
        private const int PhotoModalPadBase = 24;
        private const int PhotoModalBtnHeightBase = 44;
        private const int PhotoModalBtnGapBase = 12;

        private const int PhotoDropdownItemHBase = 44;
        private const int PhotoDropdownWidthBase = 200;

        private const int PhotoDetailFavSizeBase = 40;
        private const int PhotoDetailNavSizeBase = 52;
        private const int PhotoDetailBtnPadBase = 10;
        private const int PhotoDetailBarHeightBase = 62;

        private const int PhotoSelectBarHeightBase = 52;

        private const string PhotoAlbumDataPath = "photo_albums.json";

        // API selection mode state
        private bool photoSelectionApiMode = false;
        private int photoSelectionApiLimit = 1;
        private bool photoSelectionApiGetTexture = false;
        private bool photoSelectionApiGetMetadata = false;
        private Action<string>? photoSelectionApiCallback = null;
        private bool photoSelectionApiSquareOnly = false;
        private readonly Dictionary<string, float> photoAspectRatioCache = new(StringComparer.OrdinalIgnoreCase);
        private Rectangle photoBtnApiCancelBounds = Rectangle.Empty;
        private Rectangle photoBtnApiDoneBounds = Rectangle.Empty;

        // ─────────────────────────────────────────────────────────────
        //  State
        // ─────────────────────────────────────────────────────────────

        // Scroll
        private float photoScrollOffset = 0f;
        private float photoScrollTarget = 0f;

        // Selection mode
        private bool photoSelectMode = false;
        private readonly HashSet<int> photoSelectedIndices = new();

        // Detail view
        private int photoDetailIndex = -1;
        private Texture2D? photoDetailTexture = null;
        private int photoDetailTextureIndex = -1;

        // Overlay states
        private bool photoActionDropdownOpen = false;
        private bool photoDeleteConfirmOpen = false;
        private bool photoAlbumPickerOpen = false;
        private bool photoAlbumCreationOpen = false;
        // Set true by ReceiveLeftClickPhotoApp whenever it consumes a press, so that
        // the paired ReleaseLeftClickPhotoApp skips grid/tile hit testing entirely.
        private bool photoClickHandledOnPress = false;

        // Album-name editable text
        private string photoNewAlbumName = string.Empty;
        private int photoNewAlbumNameCursorIndex = 0;
        private int photoNewAlbumNameSelectionAnchorIndex = 0;
        private readonly List<TextEditSnapshot> photoAlbumNameUndoHistory = new();

        // Album delete mode & confirmation
        private bool photoAlbumDeleteMode = false;
        private bool photoAlbumDeleteConfirmOpen = false;
        private string photoAlbumToDelete = string.Empty;
        private Rectangle photoBtnAlbumDeleteToggleBounds = Rectangle.Empty;
        private Rectangle photoAlbumConfirmYesBounds = Rectangle.Empty;
        private Rectangle photoAlbumConfirmNoBounds = Rectangle.Empty;

        private void ApplyTouchScrollDelta(int pixelDelta)
        {
            if (currentApp == null)
            {
                if (layoutManager != null)
                {
                    layoutManager.ApplyTouchScrollDelta(pixelDelta);
                }
            }
            else if (currentApp == "appSetting")
            {
                ApplyTouchScrollDeltaSettingApp(pixelDelta);
            }
            else if (currentApp == "appNotification")
            {
                ApplyTouchScrollDeltaNotificationApp(pixelDelta);
            }
            else if (currentApp == "appStore")
            {
                ApplyTouchScrollDeltaAppStore(pixelDelta);
            }
            else if (currentApp == "appPhoto" && photoDetailIndex < 0)
            {
                ApplyPhotoTouchScrollDelta(pixelDelta);
            }
            else if (currentApp == "appPhone")
            {
                ApplyTouchScrollDeltaPhoneApp(pixelDelta);
            }
        }

        // Filter (album / people / location)
        private string? photoCurrentFilter = null;   // null = all photos
        private string photoFilterType = "all";  // "all","album","people","location","favourites"

        // Collapsed states & scroll preservation
        private bool peopleSectionCollapsed = true;
        private bool locationSectionCollapsed = true;
        private float landingPageScrollOffset = 0f;
        private Rectangle photoBtnPeopleToggleBounds = Rectangle.Empty;
        private Rectangle photoBtnLocationToggleBounds = Rectangle.Empty;

        // Thumbnail cache: photo index → Texture2D (loaded lazily)
        private readonly Dictionary<int, Texture2D> photoThumbnailCache = new();

        // Album data (loaded from JSON)
        private Smartphone.Data.PhotoAlbumStore photoAlbumStore = new();

        // ─────────────────────────────────────────────────────────────
        //  Cached click-test bounds (rebuilt each draw frame)
        // ─────────────────────────────────────────────────────────────
        private Rectangle photoBtnSelectBounds = Rectangle.Empty;
        private Rectangle photoBtnActionBounds = Rectangle.Empty;
        private Rectangle photoBtnDoneBounds = Rectangle.Empty;
        private Rectangle photoBtnBackFilterBounds = Rectangle.Empty;

        // Grid cells for visible filtered list
        private readonly List<Rectangle> photoGridCellBounds = new();
        private readonly List<int> photoGridCellPhotoIndices = new();  // maps cell → photo index

        // Album/people/location tile bounds
        private readonly List<Rectangle> photoAlbumTileBounds = new();
        private readonly List<string> photoAlbumTileKeys = new();
        private readonly List<string> photoAlbumTileTypes = new();  // "album","people","location","favourites"

        // Dropdown item bounds
        private readonly List<Rectangle> photoDropdownItemBounds = new();
        private readonly List<string> photoDropdownItems = new();

        // Confirmation dialog
        private Rectangle photoConfirmYesBounds = Rectangle.Empty;
        private Rectangle photoConfirmNoBounds = Rectangle.Empty;

        // Album picker tile bounds
        private readonly List<Rectangle> photoAlbumPickerBounds = new();
        private readonly List<string> photoAlbumPickerKeys = new();

        // Album creation text input area
        private Rectangle photoAlbumInputAreaBounds = Rectangle.Empty;
        private Rectangle photoAlbumInputConfirmBounds = Rectangle.Empty;
        private Rectangle photoAlbumInputCancelBounds = Rectangle.Empty;

        // Detail view buttons
        private Rectangle photoDetailFavBounds = Rectangle.Empty;
        private Rectangle photoDetailActionBounds = Rectangle.Empty;
        private Rectangle photoDetailPrevBounds = Rectangle.Empty;
        private Rectangle photoDetailNextBounds = Rectangle.Empty;
        private Rectangle photoDetailInfoBounds = Rectangle.Empty;

        // ─────────────────────────────────────────────────────────────
        //  Computed helpers
        // ─────────────────────────────────────────────────────────────

        private int PhotoNavBarHeight => ScaleUiValue(PhotoNavBarHeightBase);
        private int PhotoCellHeight => GetPhoneContentBounds().Height / PhotoCellHeightDivisor;
        private int PhotoCellWidth => GetPhoneContentBounds().Width / PhotoCellColsCount;
        private int PhotoScrollViewportH => GetPhoneContentBounds().Height;
        private int PhotoDivider => ScaleUiValue(PhotoDividerPx);

        // ─────────────────────────────────────────────────────────────
        //  Open / Close
        // ─────────────────────────────────────────────────────────────

        internal void StartPhotoSelectionApiMode(int limit, bool getTexture, bool getMetadata, Action<string> callback, bool squareOnly = false)
        {
            photoSelectionApiMode = true;
            photoSelectionApiLimit = limit;
            photoSelectionApiGetTexture = getTexture;
            photoSelectionApiGetMetadata = getMetadata;
            photoSelectionApiCallback = callback;
            photoSelectionApiSquareOnly = squareOnly;
        }

        private void TriggerPhotoSelectionApiCallback(List<SelectedPhotoResult>? results)
        {
            var cb = photoSelectionApiCallback;
            photoSelectionApiCallback = null;
            photoSelectionApiMode = false;
            photoSelectMode = false;
            photoSelectedIndices.Clear();
            string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(results ?? new List<SelectedPhotoResult>());
            cb?.Invoke(jsonString);
        }

        private static List<string> GetAllPhotoFilePaths()
        {
            string playerFolder = GetCaptureFolderPath("photo_player");
            string importedFolder = GetCaptureFolderPath("photo_imported");

            try { Directory.CreateDirectory(playerFolder); } catch { }
            try { Directory.CreateDirectory(importedFolder); } catch { }

            var files = new List<string>();

            if (Directory.Exists(playerFolder))
            {
                files.AddRange(Directory.GetFiles(playerFolder)
                    .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                             || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)));
            }

            if (Directory.Exists(importedFolder))
            {
                files.AddRange(Directory.GetFiles(importedFolder)
                    .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                             || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)));
            }

            return files
                .OrderBy(f => File.GetCreationTime(f))
                .ToList();
        }

        internal void OpenPhotoApp()
        {
            if (!photoSelectionApiMode)
            {
                photoSelectionApiCallback = null;
                photoSelectionApiGetTexture = false;
                photoSelectionApiGetMetadata = false;
                photoSelectionApiLimit = 1;
                photoSelectionApiSquareOnly = false;
            }

            ApplyPhoneBackground(ModEntry.currentPhoneBackground);
            LoadPhotoAlbumStore();

            // Sort oldest first so index 0 is oldest, last is newest
            capturedImages = GetAllPhotoFilePaths();

            // Reset state
            photoSelectMode = photoSelectionApiMode;
            photoSelectedIndices.Clear();
            photoDetailIndex = -1;
            photoActionDropdownOpen = false;
            photoDeleteConfirmOpen = false;
            photoAlbumPickerOpen = false;
            photoAlbumCreationOpen = false;
            photoNewAlbumName = string.Empty;
            photoCurrentFilter = null;
            photoFilterType = "all";
            photoAlbumDeleteMode = false;
            photoAlbumDeleteConfirmOpen = false;
            peopleSectionCollapsed = true;
            locationSectionCollapsed = true;

            DisposeThumbnailCache();
            DisposePhotoDetailTexture();

            // Scroll to show last 3 rows at top, album section peeking below
            int totalRows = (int)Math.Ceiling(capturedImages.Count / (double)PhotoCellColsCount);
            int gridHeight = totalRows * PhotoCellHeight;
            int showRows = Math.Min(3, totalRows);
            photoScrollTarget = Math.Max(0f, gridHeight - showRows * PhotoCellHeight);
            photoScrollOffset = photoScrollTarget;
            landingPageScrollOffset = photoScrollTarget;

            currentApp = "appPhoto";
        }

        private void ClosePhotoApp()
        {
            DisposeThumbnailCache();
            DisposePhotoDetailTexture();
            if (photoSelectionApiMode)
            {
                TriggerPhotoSelectionApiCallback(null);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Drawing
        // ─────────────────────────────────────────────────────────────

        private void DrawPhotoApp(SpriteBatch b)
        {
            b.Draw(Game1.staminaRect, GetUiViewportBounds(), Color.Black * 0.6f);
            DrawPhoneScreenBackground(b, xOffset: 0);
            DrawPhoneFrame(b);
            backButton.draw(b, Color.Tan, 1f);
            lockButton.draw(b, Color.Tan, 1f);
            homeButton.draw(b, Color.Tan, 1f);

            Rectangle content = GetPhoneContentBounds();
            if (content.Width <= 0 || content.Height <= 0) return;

            // ── Detail view ──────────────────────────────────────────
            if (photoDetailIndex >= 0)
            {
                DrawPhotoDetailView(b, content);
                DrawPhotoOverlays(b, content);
                return;
            }

            // ── Nav bar (fixed, not scrolled) ────────────────────────
            Rectangle navBar = new(content.X, content.Y - ScaleUiValue(55), content.Width, PhotoNavBarHeight);
            DrawPhotoNavBar(b, navBar, content);

            // ── Scrollable photo grid + sections ─────────────────────
            Rectangle viewport = new(content.X, content.Y,
                content.Width, PhotoScrollViewportH);

            DrawPhotoScrollableSection(b, viewport, content);

            // ── Overlays (above scrollable content) ──────────────────
            DrawPhotoOverlays(b, content);
        }

        // ── Nav bar ──────────────────────────────────────────────────

        private void DrawPhotoNavBar(SpriteBatch b, Rectangle navBar, Rectangle content)
        {
            int btnW = ScaleUiValue(PhotoNavBtnWidthBase);
            int btnH = ScaleUiValue(PhotoNavBtnHeightBase);
            int btnY = navBar.Center.Y - btnH / 2;
            int textPad = ScaleUiValue(PhotoNavBarTextOffsetBase);
            float textScale = GetPhoneTextScale(1f) * phoneUiScale;

            string title;
            if (photoFilterType == "all")
                title = ModEntry.SHelper.Translation.Get("ui.photo.title");
            else if (photoFilterType == "favourites")
                title = ModEntry.SHelper.Translation.Get("ui.photo.favourites");
            else if (photoFilterType == "imported")
                title = ModEntry.SHelper.Translation.Get("ui.photo.imported");
            else
                title = photoCurrentFilter ?? ModEntry.SHelper.Translation.Get("ui.photo.album");

            Vector2 titleSize = Game1.smallFont.MeasureString(title) * textScale;

            photoBtnBackFilterBounds = Rectangle.Empty;

            Vector2 titlePos = new(
                navBar.Left + ScaleUiValue(70),
                navBar.Center.Y - titleSize.Y / 2f);
            b.DrawString(Game1.smallFont, title, titlePos, Color.Black, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);

            if (photoSelectionApiMode)
            {
                photoBtnApiCancelBounds = new Rectangle(navBar.Left + ScaleUiValue(190), btnY, btnW, btnH);
                photoBtnApiDoneBounds = new Rectangle(navBar.Left + ScaleUiValue(260), btnY, btnW, btnH);
                photoBtnSelectBounds = Rectangle.Empty;
                photoBtnActionBounds = Rectangle.Empty;
                photoBtnDoneBounds = Rectangle.Empty;

                DrawPhoneRoundButton(b, photoBtnApiCancelBounds, ModEntry.SHelper.Translation.Get("ui.button.cancel"), Color.Black, Color.White);
                DrawPhoneRoundButton(b, photoBtnApiDoneBounds, ModEntry.SHelper.Translation.Get("ui.photo.done"), Color.Black, Color.White);
            }
            else if (photoSelectMode)
            {
                // [action ▾]   [Done]
                photoBtnActionBounds = new Rectangle(navBar.Left + ScaleUiValue(190), btnY, btnW, btnH);
                photoBtnDoneBounds = new Rectangle(navBar.Left + ScaleUiValue(260), btnY, btnW, btnH);
                photoBtnSelectBounds = Rectangle.Empty;
                photoBtnApiCancelBounds = Rectangle.Empty;
                photoBtnApiDoneBounds = Rectangle.Empty;

                string selLabel = photoSelectedIndices.Count == 0
                    ? ModEntry.SHelper.Translation.Get("ui.photo.select")
                    : ModEntry.SHelper.Translation.Get("ui.photo.action");
                DrawPhoneRoundButton(b, photoBtnActionBounds, selLabel, Color.Black, Color.White);
                DrawPhoneRoundButton(b, photoBtnDoneBounds, ModEntry.SHelper.Translation.Get("ui.photo.done"), Color.Black, Color.White);
            }
            else
            {
                // [Select]
                photoBtnSelectBounds = new Rectangle(navBar.Left + ScaleUiValue(260), btnY, btnW, btnH);
                photoBtnActionBounds = Rectangle.Empty;
                photoBtnDoneBounds = Rectangle.Empty;
                photoBtnApiCancelBounds = Rectangle.Empty;
                photoBtnApiDoneBounds = Rectangle.Empty;
                DrawPhoneRoundButton(b, photoBtnSelectBounds, ModEntry.SHelper.Translation.Get("ui.photo.select"), Color.Black, Color.White);
            }
        }

        // ── Scrollable section ────────────────────────────────────────

        private void DrawPhotoScrollableSection(SpriteBatch b, Rectangle viewport, Rectangle content)
        {
            Rectangle clipRect = Rectangle.Intersect(viewport, Game1.graphics.GraphicsDevice.Viewport.Bounds);
            if (clipRect.Width <= 0 || clipRect.Height <= 0) return;

            Rectangle prevScissor = Game1.graphics.GraphicsDevice.ScissorRectangle;
            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                null, appLabelScissorRasterizer);

            int cellW = PhotoCellWidth;
            int cellH = PhotoCellHeight;
            int div = PhotoDivider;

            // Build filtered photo list
            List<int> photoIndices = GetFilteredPhotoIndices();

            if (photoFilterType == "imported" && photoIndices.Count == 0)
            {
                string msg = ModEntry.SHelper.Translation.Get("ui.photo.imported_empty_help");
                float textScale = GetPhoneTextScale(0.72f) * phoneUiScale;
                int maxWrapWidth = (int)((viewport.Width - ScaleUiValue(40)) / textScale);
                string parsedText = Game1.parseText(msg, Game1.smallFont, maxWrapWidth);
                Vector2 textSize = Game1.smallFont.MeasureString(parsedText) * textScale;
                Vector2 textPos = new(
                    viewport.X + (viewport.Width - textSize.X) / 2f,
                    viewport.Y + (viewport.Height - textSize.Y) / 2f
                );
                b.DrawString(Game1.smallFont, parsedText, textPos, Color.DarkSlateGray, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);
            }

            // Rebuild grid cell bounds
            photoGridCellBounds.Clear();
            photoGridCellPhotoIndices.Clear();

            int rows = (int)Math.Ceiling(photoIndices.Count / (double)PhotoCellColsCount);
            int gridTotalHeight = rows * cellH;

            int gridOriginY = viewport.Y - (int)photoScrollOffset;

            for (int i = 0; i < photoIndices.Count; i++)
            {
                int row = i / PhotoCellColsCount;
                int col = i % PhotoCellColsCount;
                int cx = viewport.X + col * cellW;
                int cy = gridOriginY + row * cellH;

                var cellRect = new Rectangle(cx, cy, cellW, cellH);
                photoGridCellBounds.Add(cellRect);
                photoGridCellPhotoIndices.Add(photoIndices[i]);

                // Skip drawing if not visible
                if (cy + cellH < viewport.Y || cy > viewport.Bottom) continue;

                // Thumbnail
                Texture2D? thumb = GetOrLoadThumbnail(photoIndices[i]);
                if (thumb != null && !thumb.IsDisposed)
                {
                    float scale = Math.Max((float)cellW / thumb.Width, (float)cellH / thumb.Height);
                    float sourceWidth = cellW / scale;
                    float sourceHeight = cellH / scale;
                    float sourceX = (thumb.Width - sourceWidth) / 2f;
                    float sourceY = (thumb.Height - sourceHeight) / 2f;
                    Rectangle sourceRect = new Rectangle((int)Math.Round(sourceX), (int)Math.Round(sourceY), (int)Math.Round(sourceWidth), (int)Math.Round(sourceHeight));
                    b.Draw(thumb, cellRect, sourceRect, Color.White);
                }
                else
                    b.Draw(Game1.staminaRect, cellRect, new Color(50, 50, 50));

                // Select-mode checkbox
                if (photoSelectMode)
                {
                    bool selected = photoSelectedIndices.Contains(photoIndices[i]);
                    int checkSize = ScaleUiValue(22);
                    Rectangle checkRect = new(cx + cellW - checkSize - div, cy + div, checkSize, checkSize);
                    b.Draw(Game1.staminaRect, checkRect, new Color(0, 0, 0, 140));
                    if (selected)
                    {
                        // Draw check mark via colour
                        b.Draw(Game1.staminaRect, checkRect, new Color(30, 150, 255, 220));
                        // Simple cross-hatch to indicate selection
                        b.Draw(Game1.staminaRect, new Rectangle(checkRect.X + 4, checkRect.Center.Y - 1, checkRect.Width - 8, 2), Color.White);
                        b.Draw(Game1.staminaRect, new Rectangle(checkRect.Center.X - 1, checkRect.Y + 4, 2, checkRect.Height - 8), Color.White);
                    }
                    // Semi-dark overlay when selected
                    if (selected)
                        b.Draw(Game1.staminaRect, cellRect, new Color(30, 150, 255, 60));
                }
            }

            // ── Horizontal + vertical dividers ────────────────────────
            for (int row = 0; row <= rows; row++)
            {
                int lineY = gridOriginY + row * cellH;
                if (lineY >= viewport.Y - div && lineY <= viewport.Bottom + div)
                    b.Draw(Game1.staminaRect, new Rectangle(viewport.X, lineY, viewport.Width, div), Color.White);
            }
            for (int col = 1; col < PhotoCellColsCount; col++)
            {
                int lineX = viewport.X + col * cellW;
                b.Draw(Game1.staminaRect, new Rectangle(lineX, viewport.Y, div, viewport.Height + (int)photoScrollOffset), Color.White);
            }

            // ── Album / People / Location sections (only when not filtered and not in API selection mode) ─
            if (photoFilterType == "all" && !photoSelectionApiMode)
            {
                int sectionY = gridOriginY + gridTotalHeight;
                DrawPhotoSections(b, viewport, content, sectionY);
            }
            else
            {
                photoAlbumTileBounds.Clear();
                photoAlbumTileKeys.Clear();
                photoAlbumTileTypes.Clear();
                photoBtnPeopleToggleBounds = Rectangle.Empty;
                photoBtnLocationToggleBounds = Rectangle.Empty;
            }

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = prevScissor;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        // ── Album / People / Location sections ───────────────────────

        private void DrawPhotoSections(SpriteBatch b, Rectangle viewport, Rectangle content, int startY)
        {
            int sectionPad = ScaleUiValue(PhotoAlbumSectionPadBase);
            int titleH = ScaleUiValue(PhotoAlbumTitleHeightBase);
            int tileGap = ScaleUiValue(PhotoAlbumTileGapBase);
            int tileW = (viewport.Width - tileGap * (PhotoAlbumTileCols + 1)) / PhotoAlbumTileCols;
            int tileImgH = tileW;                                        // square thumbnail
            int tileH = tileImgH + ScaleUiValue(PhotoAlbumTileTextHeight);
            float titleScale = GetPhoneTextScale(0.9f) * phoneUiScale;
            float labelScale = GetPhoneTextScale(0.7f) * phoneUiScale;

            photoAlbumTileBounds.Clear();
            photoAlbumTileKeys.Clear();
            photoAlbumTileTypes.Clear();

            int y = startY + sectionPad;

            // ── "Albums" ──────────────────────────────────────────────
            DrawPhotoSectionTitle(b, viewport, y, ModEntry.SHelper.Translation.Get("ui.photo.albums"), titleScale);

            if (photoSelectionApiMode)
            {
                photoBtnAlbumDeleteToggleBounds = Rectangle.Empty;
            }
            else
            {
                // Draw Delete Toggle button on the right side of the Albums bar
                int toggleW = ScaleUiValue(60);
                int toggleH = ScaleUiValue(50);
                int toggleX = viewport.Right - toggleW;
                int toggleY = y + ScaleUiValue(PhotoAlbumTitleHeightBase) / 2 - toggleH / 2;
                photoBtnAlbumDeleteToggleBounds = new Rectangle(toggleX, toggleY, toggleW, toggleH);

                Color toggleBg = photoAlbumDeleteMode ? new Color(220, 80, 80) : new Color(180, 180, 180);
                DrawPhoneRoundButton(b, photoBtnAlbumDeleteToggleBounds, ModEntry.SHelper.Translation.Get("ui.photo.delete"), Color.White, toggleBg);
            }

            y += titleH;

            // Built-in: Favourites, Imported
            var builtinAlbums = new List<(string Name, string Type)>
            {
                ("Favourites", "favourites"),
                ("Imported", "imported"),
            };

            // Custom albums
            var allAlbums = builtinAlbums
                .Concat(photoAlbumStore.Albums.Select(a => (a.Name, "album")))
                .ToList();

            y = DrawPhotoTileRow(b, viewport, y, allAlbums, tileW, tileH, tileImgH, tileGap, sectionPad, labelScale);
            y += sectionPad;

            // ── "People" ──────────────────────────────────────────────
            var peopleGroups = GetPeopleGroups();
            if (peopleGroups.Count > 0)
            {
                DrawPhotoSectionTitle(b, viewport, y, ModEntry.SHelper.Translation.Get("ui.photo.people"), titleScale);

                // Draw + / - collapse toggle button on the right side of People bar
                int pToggleW = ScaleUiValue(50);
                int pToggleH = ScaleUiValue(50);
                int pToggleX = viewport.Right - pToggleW;
                int pToggleY = y + ScaleUiValue(PhotoAlbumTitleHeightBase) / 2 - pToggleH / 2;
                photoBtnPeopleToggleBounds = new Rectangle(pToggleX, pToggleY, pToggleW, pToggleH);

                string pToggleLabel = peopleSectionCollapsed ? "+" : "-";
                DrawPhoneRoundButton(b, photoBtnPeopleToggleBounds, pToggleLabel, Color.White, new Color(180, 180, 180));

                y += titleH;
                if (!peopleSectionCollapsed)
                {
                    var peopleTiles = peopleGroups.Select(p => (p, "people")).ToList();
                    y = DrawPhotoTileRow(b, viewport, y, peopleTiles, tileW, tileH, tileImgH, tileGap, sectionPad, labelScale);
                }
                y += sectionPad;
            }
            else
            {
                photoBtnPeopleToggleBounds = Rectangle.Empty;
            }

            // ── "Locations" ───────────────────────────────────────────
            var locationGroups = GetLocationGroups();
            if (locationGroups.Count > 0)
            {
                DrawPhotoSectionTitle(b, viewport, y, ModEntry.SHelper.Translation.Get("ui.photo.locations"), titleScale);

                // Draw + / - collapse toggle button on the right side of Locations bar
                int lToggleW = ScaleUiValue(50);
                int lToggleH = ScaleUiValue(50);
                int lToggleX = viewport.Right - lToggleW;
                int lToggleY = y + ScaleUiValue(PhotoAlbumTitleHeightBase) / 2 - lToggleH / 2;
                photoBtnLocationToggleBounds = new Rectangle(lToggleX, lToggleY, lToggleW, lToggleH);

                string lToggleLabel = locationSectionCollapsed ? "+" : "-";
                DrawPhoneRoundButton(b, photoBtnLocationToggleBounds, lToggleLabel, Color.White, new Color(180, 180, 180));

                y += titleH;
                if (!locationSectionCollapsed)
                {
                    var locationTiles = locationGroups.Select(l => (l, "location")).ToList();
                    y = DrawPhotoTileRow(b, viewport, y, locationTiles, tileW, tileH, tileImgH, tileGap, sectionPad, labelScale);
                }
                y += sectionPad;
            }
            else
            {
                photoBtnLocationToggleBounds = Rectangle.Empty;
            }
        }

        private void DrawPhotoSectionTitle(SpriteBatch b, Rectangle viewport, int y, string text, float scale)
        {
            if (y + ScaleUiValue(PhotoAlbumTitleHeightBase) < viewport.Y || y > viewport.Bottom) return;
            int pad = ScaleUiValue(PhotoAlbumSectionPadBase);
            int sectionHeight = ScaleUiValue(PhotoAlbumTitleHeightBase);

            // Replace the flat staminaRect with the textured menu box
            Textures.DrawCard(
                b,
                viewport.X,
                y,
                viewport.Width,
                sectionHeight,
                Color.White,
                1f,
                false
            );

            // Text drawing logic stays the disposable same
            Vector2 pos = new(
                viewport.X + pad,
                y + sectionHeight / 2f - (Game1.smallFont.MeasureString(text).Y * scale / 2f)
            );
            b.DrawString(Game1.smallFont, text, pos, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
        }

        private int DrawPhotoTileRow(SpriteBatch b, Rectangle viewport, int startY,
            List<(string Name, string Type)> tiles, int tileW, int tileH, int tileImgH,
            int tileGap, int sectionPad, float labelScale)
        {
            int y = startY;
            int colCount = PhotoAlbumTileCols;
            int rows = (int)Math.Ceiling(tiles.Count / (double)colCount);

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < colCount; col++)
                {
                    int idx = row * colCount + col;
                    if (idx >= tiles.Count) break;

                    var (name, type) = tiles[idx];
                    int x = viewport.X + tileGap + col * (tileW + tileGap);
                    Rectangle tileRect = new(x, y, tileW, tileH);
                    Rectangle imgRect = new(x, y, tileW, tileImgH);
                    Rectangle labelRect = new(x, y + tileImgH, tileW, tileH - tileImgH);

                    photoAlbumTileBounds.Add(tileRect);
                    photoAlbumTileKeys.Add(name);
                    photoAlbumTileTypes.Add(type);

                    // Skip if not visible
                    if (y + tileH < viewport.Y || y > viewport.Bottom) continue;

                    // Thumbnail for album cover (first photo)
                    Texture2D? cover = GetAlbumCoverThumbnail(name, type);
                    if (cover != null && !cover.IsDisposed)
                    {
                        float scale = Math.Max((float)imgRect.Width / cover.Width, (float)imgRect.Height / cover.Height);
                        float sourceWidth = imgRect.Width / scale;
                        float sourceHeight = imgRect.Height / scale;
                        float sourceX = (cover.Width - sourceWidth) / 2f;
                        float sourceY = (cover.Height - sourceHeight) / 2f;
                        Rectangle sourceRect = new Rectangle((int)Math.Round(sourceX), (int)Math.Round(sourceY), (int)Math.Round(sourceWidth), (int)Math.Round(sourceHeight));
                        b.Draw(cover, imgRect, sourceRect, Color.White);
                    }
                    else
                        b.Draw(Game1.staminaRect, imgRect, new Color(60, 60, 60));

                    // Rounded label background
                    b.Draw(Game1.staminaRect, labelRect, new Color(239, 159, 80));

                    // Album name text
                    int photoCount = GetAlbumPhotoCount(name, type);
                    string displayName = name;
                    if (type == "favourites")
                        displayName = ModEntry.SHelper.Translation.Get("ui.photo.favourites");
                    else if (type == "imported")
                        displayName = ModEntry.SHelper.Translation.Get("ui.photo.imported");
                    string label = ModEntry.SHelper.Translation.Get("ui.photo.album_count_format", new { name = displayName, count = photoCount });

                    float nameScale = GetPhoneTextScale(0.75f) * phoneUiScale;
                    int textPad = ScaleUiValue(PhotoAlbumTileNamePadBase);
                    int maxLabelW = labelRect.Width - textPad * 2;
                    Vector2 labelPos = new Vector2(labelRect.X + textPad, labelRect.Y + textPad);

                    DrawLoopingText(b, label, Game1.smallFont, labelPos, maxLabelW, Color.White, nameScale);
                }
                y += tileH + tileGap;
            }
            return y;
        }

        // ── Overlays ─────────────────────────────────────────────────

        private void DrawPhotoOverlays(SpriteBatch b, Rectangle content)
        {
            if (photoActionDropdownOpen)
                DrawPhotoActionDropdown(b, content);

            if (photoDeleteConfirmOpen)
                DrawPhotoDeleteConfirm(b, content);

            if (photoAlbumPickerOpen)
                DrawPhotoAlbumPicker(b, content);

            if (photoAlbumCreationOpen)
                DrawPhotoAlbumCreationDialog(b, content);

            if (photoAlbumDeleteConfirmOpen)
                DrawAlbumDeleteConfirm(b, content);

            if (photoSelectMode && !photoActionDropdownOpen)
                DrawPhotoSelectModeBar(b, content);
        }

        private void DrawPhotoActionDropdown(SpriteBatch b, Rectangle content)
        {
            int itemH = ScaleUiValue(PhotoDropdownItemHBase);
            int dropW = ScaleUiValue(PhotoDropdownWidthBase);

            var items = new List<string>();
            if (photoDetailIndex >= 0)
            {
                items.Add("Delete");
                if (photoFilterType == "album")
                {
                    items.Add("Drop from album");
                }
                items.AddRange(new[] { "Add to Album", "Set portrait BG", "Set landscape BG" });
                var socialApi = ModEntry.SHelper.ModRegistry.GetApi<IStardewSocialApi>("d5a1lamdtd.Smartphone-AppStardewSocial");
                if (socialApi != null)
                {
                    items.Add("Share");
                }
            }
            else
            {
                items.Add("Delete");
                if (photoFilterType == "album")
                {
                    items.Add("Drop from album");
                }
                items.AddRange(new[] { "Select all", "Favourite", "Add to Album" });
            }

            photoDropdownItems.Clear();
            photoDropdownItemBounds.Clear();
            photoDropdownItems.AddRange(items);

            int dropH = items.Count * itemH;
            int dropX = content.Right - dropW - ScaleUiValue(8);
            int dropY = content.Y + ScaleUiValue(4);

            // If in detail view, pop the menu UP from the bottom bar
            if (photoDetailIndex >= 0)
            {
                int barH = ScaleUiValue(PhotoDetailBarHeightBase);
                dropY = content.Bottom - barH - dropH - ScaleUiValue(8);
            }

            Textures.DrawCard(
                b,
                dropX,
                dropY,
                dropW,
                dropH,
                Color.White,
                1f,
                false
            );

            for (int i = 0; i < items.Count; i++)
            {
                Rectangle r = new(dropX, dropY + i * itemH, dropW, itemH);
                photoDropdownItemBounds.Add(r);
                if (i > 0)
                    b.Draw(Game1.staminaRect, new Rectangle(dropX, r.Y, dropW, 1), new Color(80, 80, 80));
                float scale = GetPhoneTextScale(0.8f) * phoneUiScale;
                string displayLabel = items[i] switch
                {
                    "Delete" => ModEntry.SHelper.Translation.Get("ui.photo.delete"),
                    "Add to Album" => ModEntry.SHelper.Translation.Get("ui.photo.add_to_album"),
                    "Drop from album" => ModEntry.SHelper.Translation.Get("ui.photo.remove_from_album"),
                    "Set portrait BG" => ModEntry.SHelper.Translation.Get("ui.photo.set_portrait_bg"),
                    "Set landscape BG" => ModEntry.SHelper.Translation.Get("ui.photo.set_landscape_bg"),
                    "Share" => ModEntry.SHelper.Translation.Get("ui.photo.share"),
                    "Select all" => ModEntry.SHelper.Translation.Get("ui.photo.select_all"),
                    "Favourite" => ModEntry.SHelper.Translation.Get("ui.photo.favourite"),
                    _ => items[i]
                };
                Vector2 tSize = Game1.smallFont.MeasureString(displayLabel) * scale;
                b.DrawString(Game1.smallFont, displayLabel,
                    new Vector2(r.X + ScaleUiValue(12), r.Center.Y - tSize.Y / 2f),
                    items[i] == "Delete" ? new Color(255, 80, 80) : Color.Black,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
            }
        }

        private void DrawPhotoDeleteConfirm(SpriteBatch b, Rectangle content)
        {
            // Dimmed overlay
            b.Draw(Game1.staminaRect, content, new Color(0, 0, 0, 160));

            int mW = ScaleUiValue(PhotoModalWidthBase);
            int pad = ScaleUiValue(PhotoModalPadBase);
            int btnH = ScaleUiValue(PhotoModalBtnHeightBase);
            int btnGap = ScaleUiValue(PhotoModalBtnGapBase);
            float titleScale = GetPhoneTextScale(0.75f) * phoneUiScale;
            float bodyScale = GetPhoneTextScale(0.72f) * phoneUiScale;
            float btnScale = GetPhoneTextScale(0.7f) * phoneUiScale;

            string title = photoSelectedIndices.Count > 1
                ? ModEntry.SHelper.Translation.Get("ui.photo.delete_multiple_confirm", new { count = photoSelectedIndices.Count })
                : ModEntry.SHelper.Translation.Get("ui.photo.delete_single_confirm");
            string body = ModEntry.SHelper.Translation.Get("ui.photo.delete_warning");

            Vector2 titleSz = Game1.smallFont.MeasureString(title) * titleScale;
            Vector2 bodySz = Game1.smallFont.MeasureString(body) * bodyScale;

            int mH = pad + (int)titleSz.Y + pad + (int)bodySz.Y + pad + btnH + pad;
            int mX = content.Center.X - mW / 2;
            int mY = content.Center.Y - mH / 2;
            Rectangle modal = new(mX, mY, mW, mH);
            Textures.DrawCard(
                b,
                modal.X,
                modal.Y,
                modal.Width,
                modal.Height,
                Color.White,
                1f,
                false
            );

            int cy = mY + pad;
            b.DrawString(Game1.smallFont, title, new Vector2(mX + pad, cy),
                Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 1f);
            cy += (int)titleSz.Y + pad;
            b.DrawString(Game1.smallFont, body, new Vector2(mX + pad, cy),
                Color.Black, 0f, Vector2.Zero, bodyScale, SpriteEffects.None, 1f);
            cy += (int)bodySz.Y + pad;

            int halfW = (mW - pad * 3) / 2;
            photoConfirmNoBounds = new(mX + pad, cy, halfW, btnH);
            photoConfirmYesBounds = new(mX + pad * 2 + halfW, cy, halfW, btnH);

            DrawPhoneRoundButton(b, photoConfirmNoBounds, ModEntry.SHelper.Translation.Get("ui.button.cancel"), Color.Black, Color.White);
            DrawPhoneRoundButton(b, photoConfirmYesBounds, ModEntry.SHelper.Translation.Get("ui.photo.delete"), Color.White, new Color(200, 40, 40, 220));
        }

        private void DrawAlbumDeleteConfirm(SpriteBatch b, Rectangle content)
        {
            b.Draw(Game1.staminaRect, content, new Color(0, 0, 0, 160));

            int mW = ScaleUiValue(PhotoModalWidthBase);
            int pad = ScaleUiValue(PhotoModalPadBase);
            int btnH = ScaleUiValue(PhotoModalBtnHeightBase);
            float titleScale = GetPhoneTextScale(0.85f) * phoneUiScale;
            float bodyScale = GetPhoneTextScale(0.72f) * phoneUiScale;

            string title = ModEntry.SHelper.Translation.Get("ui.photo.delete_album_confirm");
            string body = ModEntry.SHelper.Translation.Get("ui.photo.delete_album_warning", new { albumName = photoAlbumToDelete });

            Vector2 titleSz = Game1.smallFont.MeasureString(title) * titleScale;
            Vector2 bodySz = Game1.smallFont.MeasureString(body) * bodyScale;

            int mH = pad + (int)titleSz.Y + pad + (int)bodySz.Y + pad + btnH + pad;
            int mX = content.Center.X - mW / 2;
            int mY = content.Center.Y - mH / 2;
            Rectangle modal = new(mX, mY, mW, mH);

            Textures.DrawCard(
                b,
                modal.X,
                modal.Y,
                modal.Width,
                modal.Height,
                Color.White,
                1f,
                false
            );

            int cy = mY + pad;
            b.DrawString(Game1.smallFont, title, new Vector2(mX + pad, cy),
                Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 1f);
            cy += (int)titleSz.Y + pad;

            b.DrawString(Game1.smallFont, Game1.parseText(body, Game1.smallFont, mW - pad * 2), new Vector2(mX + pad, cy),
                Color.Black, 0f, Vector2.Zero, bodyScale, SpriteEffects.None, 1f);

            cy = modal.Bottom - btnH - pad;

            int halfW = (mW - pad * 3) / 2;
            photoAlbumConfirmNoBounds = new(mX + pad, cy, halfW, btnH);
            photoAlbumConfirmYesBounds = new(mX + pad * 2 + halfW, cy, halfW, btnH);

            DrawPhoneRoundButton(b, photoAlbumConfirmNoBounds, ModEntry.SHelper.Translation.Get("ui.button.cancel"), Color.Black, Color.White);
            DrawPhoneRoundButton(b, photoAlbumConfirmYesBounds, ModEntry.SHelper.Translation.Get("ui.photo.delete"), Color.White, new Color(200, 40, 40, 220));
        }

        private void DrawPhotoAlbumPicker(SpriteBatch b, Rectangle content)
        {
            b.Draw(Game1.staminaRect, content, new Color(0, 0, 0, 160));

            int mW = ScaleUiValue(PhotoModalWidthBase);
            int pad = ScaleUiValue(PhotoModalPadBase);
            int itemH = ScaleUiValue(PhotoDropdownItemHBase);
            float scale = GetPhoneTextScale(0.7f) * phoneUiScale;
            float titleScale = GetPhoneTextScale(0.85f) * phoneUiScale;

            photoAlbumPickerBounds.Clear();
            photoAlbumPickerKeys.Clear();

            var albums = photoAlbumStore.Albums.Select(a => a.Name).ToList();
            albums.Insert(0, "+ New Album");

            int mH = pad + ScaleUiValue(36) + pad + albums.Count * itemH + pad;
            int mX = content.Center.X - mW / 2;
            int mY = content.Center.Y - mH / 2;

            Textures.DrawCard(
                b,
                mX,
                mY,
                mW,
                mH,
                Color.White,
                1f,
                false
            );

            // Title
            string titleText = ModEntry.SHelper.Translation.Get("ui.photo.add_to_album_title");
            b.DrawString(Game1.smallFont, titleText,
                new Vector2(mX + pad, mY + pad),
                Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 1f);

            int iy = mY + pad + ScaleUiValue(36) + pad;
            foreach (string albumName in albums)
            {
                Rectangle r = new(mX, iy, mW, itemH);
                photoAlbumPickerBounds.Add(r);
                photoAlbumPickerKeys.Add(albumName);
                b.Draw(Game1.staminaRect, new Rectangle(mX + pad, iy, mW - pad * 2, 1), Color.Black * 0.2f);
                string displayAlbumName = albumName == "+ New Album"
                    ? ModEntry.SHelper.Translation.Get("ui.photo.new_album_option")
                    : albumName;
                b.DrawString(Game1.smallFont, displayAlbumName,
                    new Vector2(mX + pad, iy + itemH / 2f - Game1.smallFont.MeasureString(displayAlbumName).Y * scale / 2f),
                    albumName == "+ New Album" ? new Color(0, 96, 200) : Color.Black,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
                iy += itemH;
            }
        }

        private void DrawPhotoAlbumCreationDialog(SpriteBatch b, Rectangle content)
        {
            b.Draw(Game1.staminaRect, content, new Color(0, 0, 0, 160));

            int mW = ScaleUiValue(PhotoModalWidthBase);
            int pad = ScaleUiValue(PhotoModalPadBase);
            int btnH = ScaleUiValue(PhotoModalBtnHeightBase);
            int inputH = ScaleUiValue(50);
            float titleScale = GetPhoneTextScale(0.85f) * phoneUiScale;
            float btnScale = GetPhoneTextScale(0.72f) * phoneUiScale;
            float inputScale = GetPhoneTextScale(0.72f) * phoneUiScale;

            string titleText = ModEntry.SHelper.Translation.Get("ui.photo.new_album_title");
            Vector2 titleSz = Game1.smallFont.MeasureString(titleText) * titleScale;

            int mH = pad + (int)titleSz.Y + pad + inputH + pad + btnH + pad;
            int mX = content.Center.X - mW / 2;
            int mY = content.Center.Y - mH / 2;
            Textures.DrawCard(
                Game1.spriteBatch,
                mX,
                mY,
                mW,
                mH,
                Color.White, // Use Color.White to keep the default brown menu colors
                1f,
                false
            );

            int cy = mY + pad;
            b.DrawString(Game1.smallFont, titleText, new Vector2(mX + pad, cy),
                Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 1f);
            cy += (int)titleSz.Y + pad;

            // Input area
            Rectangle inputRect = new(mX + pad, cy, mW - pad * 2, inputH);
            photoAlbumInputAreaBounds = inputRect;
            b.Draw(Game1.staminaRect, inputRect, new Color(260, 260, 260));

            // Draw text + cursor
            string displayText = photoNewAlbumName;
            string pre = displayText[..Math.Min(photoNewAlbumNameCursorIndex, displayText.Length)];
            Vector2 preSize = Game1.smallFont.MeasureString(pre == "" ? " " : pre) * inputScale;
            Vector2 textPos = new(inputRect.X + ScaleUiValue(8), inputRect.Center.Y - preSize.Y / 2f);
            b.DrawString(Game1.smallFont, displayText, textPos,
                Color.Black, 0f, Vector2.Zero, inputScale, SpriteEffects.None, 1f);

            // Blinking cursor
            bool cursorVisible = ((int)(textCursorBlinkElapsedSeconds * 2)) % 2 == 0;
            if (cursorVisible)
            {
                int cursorX = inputRect.X + ScaleUiValue(8) + (int)preSize.X;
                b.Draw(Game1.staminaRect, new Rectangle(cursorX, (int)textPos.Y, 2, (int)preSize.Y), Color.Black);
            }

            cy += inputH + pad;
            int halfW = (mW - pad * 3) / 2;
            photoAlbumInputCancelBounds = new(mX + pad, cy, halfW, btnH);
            photoAlbumInputConfirmBounds = new(mX + pad * 2 + halfW, cy, halfW, btnH);
            DrawPhoneRoundButton(b, photoAlbumInputCancelBounds, ModEntry.SHelper.Translation.Get("ui.button.cancel"), Color.Black, Color.White);
            DrawPhoneRoundButton(b, photoAlbumInputConfirmBounds, ModEntry.SHelper.Translation.Get("ui.button.create"), Color.Black, Color.White);
        }

        private void DrawPhotoSelectModeBar(SpriteBatch b, Rectangle content)
        {
            if (photoSelectionApiMode)
            {
                int barH = ScaleUiValue(PhotoSelectBarHeightBase);
                Rectangle bar = new(content.X, content.Bottom - barH, content.Width, barH);
                b.Draw(Game1.staminaRect, bar, new Color(0, 0, 0, 210));
                float scale = GetPhoneTextScale(0.7f) * phoneUiScale;
                string label = ModEntry.SHelper.Translation.Get("ui.photo.selected_limit_format", new { count = photoSelectedIndices.Count, limit = photoSelectionApiLimit });
                if (photoSelectionApiSquareOnly)
                {
                    label = ModEntry.SHelper.Translation.Get("ui.photo.square_only") + " " + label;
                }
                Vector2 sz = Game1.smallFont.MeasureString(label) * scale;
                b.DrawString(Game1.smallFont, label,
                    new Vector2(bar.Center.X - sz.X / 2f, bar.Center.Y - sz.Y / 2f),
                    Color.LightGray, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
                return;
            }

            if (!photoSelectMode || photoSelectedIndices.Count == 0) return;
            int barHNormal = ScaleUiValue(PhotoSelectBarHeightBase);
            Rectangle barNormal = new(content.X, content.Bottom - barHNormal, content.Width, barHNormal);
            b.Draw(Game1.staminaRect, barNormal, new Color(0, 0, 0, 210));
            float scaleNormal = GetPhoneTextScale(0.7f) * phoneUiScale;
            string labelNormal = ModEntry.SHelper.Translation.Get("ui.photo.selected_count", new { count = photoSelectedIndices.Count });
            Vector2 szNormal = Game1.smallFont.MeasureString(labelNormal) * scaleNormal;
            b.DrawString(Game1.smallFont, labelNormal,
                new Vector2(barNormal.Center.X - szNormal.X / 2f, barNormal.Center.Y - szNormal.Y / 2f),
                Color.LightGray, 0f, Vector2.Zero, scaleNormal, SpriteEffects.None, 1f);
        }

        // ── Detail view ───────────────────────────────────────────────
        private void DrawPhotoDetailView(SpriteBatch b, Rectangle content)
        {
            // Full-screen black background within phone content
            b.Draw(Game1.staminaRect, content, Color.Black);

            // Load detail texture if needed
            EnsurePhotoDetailTexture();

            if (photoDetailTexture != null && !photoDetailTexture.IsDisposed)
            {
                if (photoDetailTexture.Width >= photoDetailTexture.Height)
                {
                    // Landscape or Square mode: max the possible width and keep original aspect ratio (centered vertically)
                    float scale = (float)content.Width / photoDetailTexture.Width;
                    int destW = content.Width;
                    int destH = (int)Math.Round(photoDetailTexture.Height * scale);
                    int destX = content.X;
                    int destY = content.Y + (content.Height - destH) / 2;
                    b.Draw(photoDetailTexture, new Rectangle(destX, destY, destW, destH), Color.White);
                }
                else
                {
                    // Portrait mode: fill the screen as before
                    b.Draw(photoDetailTexture, content, Color.White);
                }
            }

            int btnPad = ScaleUiValue(PhotoDetailBtnPadBase);
            int navSize = ScaleUiValue(PhotoDetailNavSizeBase);
            int favSize = ScaleUiValue(PhotoDetailFavSizeBase);
            int barH = ScaleUiValue(PhotoDetailBarHeightBase);

            // Bottom toolbar
            Rectangle bottomBar = new(content.X, content.Bottom - barH, content.Width, barH);
            b.Draw(Game1.staminaRect, bottomBar, new Color(0, 0, 0, 100));

            // Prev / Next navigation (Centered vertically on screen height)
            photoDetailPrevBounds = new(content.X + btnPad, content.Center.Y - navSize / 2, navSize, navSize);
            photoDetailNextBounds = new(content.Right - btnPad - navSize, content.Center.Y - navSize / 2, navSize, navSize);

            // Can go prev?
            List<int> filteredIndices = GetFilteredPhotoIndices();
            int currentFilteredIdx = filteredIndices.IndexOf(photoDetailIndex);
            bool hasPrev = currentFilteredIdx > 0;
            bool hasNext = currentFilteredIdx >= 0 && currentFilteredIdx < filteredIndices.Count - 1;

            DrawRedesignedNavButton(b, photoDetailPrevBounds, isNext: false, enabled: hasPrev);
            DrawRedesignedNavButton(b, photoDetailNextBounds, isNext: true, enabled: hasNext);

            // Heart / Favourite button (Centered in bottom bar)
            bool isFav = IsPhotoFavourite(capturedImages[photoDetailIndex]);
            photoDetailFavBounds = new(
                bottomBar.Center.X - favSize / 2,
                bottomBar.Center.Y - favSize / 2,
                favSize, favSize);
            DrawRedesignedFavButton(b, photoDetailFavBounds, isFav);

            // Action button (Moved to bottom right)
            int actionW = ScaleUiValue(90);
            int actionH = ScaleUiValue(50);
            photoDetailActionBounds = new Rectangle(content.Right - btnPad - actionW, bottomBar.Center.Y - actionH / 2, actionW, actionH);
            DrawPhoneRoundButton(b, photoDetailActionBounds, ModEntry.SHelper.Translation.Get("ui.photo.action"), Color.Black, Color.White);

            // Info button (Moved to bottom left)
            int infoW = ScaleUiValue(50);
            int infoH = ScaleUiValue(50);
            photoDetailInfoBounds = new Rectangle(content.X + btnPad, bottomBar.Center.Y - infoH / 2, infoW, infoH);
            DrawPhoneRoundButton(b, photoDetailInfoBounds, "i", Color.Black, Color.White);

            // Hover metadata tooltip for Info button
            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            if (photoDetailInfoBounds.Contains(mouseX, mouseY) && photoDetailIndex >= 0 && photoDetailIndex < capturedImages.Count)
            {
                string photoPath = capturedImages[photoDetailIndex];
                string filename = Path.GetFileName(photoPath);
                string loc = ModEntry.SHelper.Translation.Get("ui.photo.unknown_location");
                string ts = "";

                if (ModEntry.ImageMetadataStore.TryGetValue(filename, out var metadata) && metadata != null)
                {
                    loc = string.IsNullOrWhiteSpace(metadata.Location) ? ModEntry.SHelper.Translation.Get("ui.photo.unknown_location") : metadata.Location;
                    ts = string.IsNullOrWhiteSpace(metadata.TimeString) ? "" : metadata.TimeString;
                }

                string hoverText = string.IsNullOrEmpty(ts) ? loc : $"{loc}\n{ts}";

                float tipScale = GetPhoneTextScale(0.7f) * phoneUiScale;
                Vector2 tipSize = Game1.smallFont.MeasureString(hoverText) * tipScale;
                int tipPad = ScaleUiValue(12);

                int tipX = photoDetailInfoBounds.X;
                int tipY = photoDetailInfoBounds.Y - (int)tipSize.Y - tipPad * 2 - ScaleUiValue(8);

                // Draw tooltip background
                Textures.DrawCard(
                    b,
                    tipX,
                    tipY,
                    (int)tipSize.X + tipPad * 2,
                    (int)tipSize.Y + tipPad * 2,
                    Color.White,
                    1f,
                    false
                );

                // Draw tooltip text
                b.DrawString(Game1.smallFont, hoverText, new Vector2(tipX + tipPad, tipY + tipPad), Color.Black, 0f, Vector2.Zero, tipScale, SpriteEffects.None, 1f);
            }
        }
        // ─────────────────────────────────────────────────────────────
        //  Input Handling
        // ─────────────────────────────────────────────────────────────

        private void ReceiveLeftClickPhotoApp(int x, int y)
        {
            // Reset the "press consumed" flag; set it before every handled return below.
            photoClickHandledOnPress = false;

            Rectangle content = GetPhoneContentBounds();
            Rectangle navBar = new(content.X, content.Y - ScaleUiValue(55), content.Width, PhotoNavBarHeight);
            if (!content.Contains(x, y) && !navBar.Contains(x, y))
            {
                return;
            }

            // ── Close action dropdown on click outside ────────────────
            if (photoActionDropdownOpen)
            {
                bool hitDropdown = photoDropdownItemBounds.Any(r => r.Contains(x, y));
                if (!hitDropdown)
                {
                    photoActionDropdownOpen = false;
                    photoClickHandledOnPress = true;
                    return;
                }
            }

            // ── Delete confirmation dialog ────────────────────────────
            if (photoDeleteConfirmOpen)
            {
                if (photoConfirmYesBounds.Contains(x, y))
                    ExecuteDeleteSelectedPhotos();
                else if (photoConfirmNoBounds.Contains(x, y))
                    photoDeleteConfirmOpen = false;
                photoClickHandledOnPress = true;
                return;
            }

            // ── Album creation dialog ─────────────────────────────────
            if (photoAlbumCreationOpen)
            {
                if (photoAlbumInputConfirmBounds.Contains(x, y))
                    ConfirmAlbumCreation();
                else if (photoAlbumInputCancelBounds.Contains(x, y))
                    CancelAlbumCreation();
                photoClickHandledOnPress = true;
                return;
            }

            // ── Album delete confirmation dialog ──────────────────────
            if (photoAlbumDeleteConfirmOpen)
            {
                if (photoAlbumConfirmYesBounds.Contains(x, y))
                    ExecuteDeleteAlbum();
                else if (photoAlbumConfirmNoBounds.Contains(x, y))
                    photoAlbumDeleteConfirmOpen = false;
                photoClickHandledOnPress = true;
                return;
            }

            // ── Album picker ──────────────────────────────────────────
            if (photoAlbumPickerOpen)
            {
                for (int i = 0; i < photoAlbumPickerBounds.Count; i++)
                {
                    if (!photoAlbumPickerBounds[i].Contains(x, y)) continue;
                    string key = photoAlbumPickerKeys[i];
                    if (key == "+ New Album")
                        OpenAlbumCreationDialog();
                    else
                        AddSelectedPhotosToAlbum(key);
                    photoClickHandledOnPress = true;
                    return;
                }
                photoAlbumPickerOpen = false;
                photoClickHandledOnPress = true;
                return;
            }

            // ── Action dropdown items ─────────────────────────────────
            if (photoActionDropdownOpen)
            {
                for (int i = 0; i < photoDropdownItemBounds.Count; i++)
                {
                    if (!photoDropdownItemBounds[i].Contains(x, y)) continue;
                    HandlePhotoDropdownAction(photoDropdownItems[i]);
                    photoActionDropdownOpen = false;
                    photoClickHandledOnPress = true;
                    return;
                }
                photoActionDropdownOpen = false;
                photoClickHandledOnPress = true;
                return;
            }

            // ── Detail view ───────────────────────────────────────────
            if (photoDetailIndex >= 0)
            {
                if (photoDetailActionBounds.Contains(x, y))
                {
                    photoActionDropdownOpen = true;
                    Game1.playSound("smallSelect");
                    photoClickHandledOnPress = true;
                    return;
                }
                if (photoDetailFavBounds.Contains(x, y))
                {
                    TogglePhotoFavourite(capturedImages[photoDetailIndex]);
                    photoClickHandledOnPress = true;
                    return;
                }

                List<int> filteredIndices = GetFilteredPhotoIndices();
                int currentFilteredIdx = filteredIndices.IndexOf(photoDetailIndex);

                if (photoDetailPrevBounds.Contains(x, y) && currentFilteredIdx > 0)
                {
                    photoDetailIndex = filteredIndices[currentFilteredIdx - 1];
                    DisposePhotoDetailTexture();
                    photoClickHandledOnPress = true;
                    return;
                }
                if (photoDetailNextBounds.Contains(x, y) && currentFilteredIdx >= 0 && currentFilteredIdx < filteredIndices.Count - 1)
                {
                    photoDetailIndex = filteredIndices[currentFilteredIdx + 1];
                    DisposePhotoDetailTexture();
                    photoClickHandledOnPress = true;
                    return;
                }
                // Tap elsewhere in detail → do nothing, but mark click as handled
                photoClickHandledOnPress = true;
                return;
            }

            // ── Nav bar buttons ───────────────────────────────────────
            if (photoSelectionApiMode)
            {
                if (photoBtnApiCancelBounds.Contains(x, y))
                {
                    TriggerPhotoSelectionApiCallback(null);
                    ClosePhotoApp();
                    currentApp = null;
                    Game1.playSound("bigDeSelect");
                    photoClickHandledOnPress = true;
                    return;
                }
                if (photoBtnApiDoneBounds.Contains(x, y))
                {
                    var results = new List<SelectedPhotoResult>();
                    foreach (int idx in photoSelectedIndices)
                    {
                        if (idx >= 0 && idx < capturedImages.Count)
                        {
                            string path = capturedImages[idx];
                            string fname = Path.GetFileName(path);
                            var result = new SelectedPhotoResult
                            {
                                AbsolutePath = path,
                                FileName = fname
                            };
                            if (photoSelectionApiGetMetadata)
                            {
                                ModEntry.ImageMetadataStore.TryGetValue(fname, out var m);
                                result.Location = m?.Location ?? string.Empty;
                                result.Tag = m?.Tag ?? string.Empty;
                                string ts = m?.TimeString ?? string.Empty;
                                if (string.IsNullOrEmpty(ts))
                                {
                                    try { ts = File.GetCreationTime(path).ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
                                }
                                result.Timestamp = ts;
                            }
                            if (photoSelectionApiGetTexture)
                            {
                                try
                                {
                                    result.TextureData = File.ReadAllBytes(path);
                                }
                                catch (Exception ex)
                                {
                                    ModEntry.SMonitor.Log($"Failed to read photo bytes: {ex.Message}", LogLevel.Warn);
                                }
                            }
                            results.Add(result);
                        }
                    }
                    TriggerPhotoSelectionApiCallback(results);
                    ClosePhotoApp();
                    currentApp = null;
                    Game1.playSound("coin");
                    photoClickHandledOnPress = true;
                    return;
                }
            }

            if (photoBtnSelectBounds.Contains(x, y))
            {
                photoAlbumDeleteMode = false;
                photoSelectMode = true;
                photoSelectedIndices.Clear();
                Game1.playSound("smallSelect");
                photoClickHandledOnPress = true;
                return;
            }
            if (photoBtnDoneBounds.Contains(x, y))
            {
                photoAlbumDeleteMode = false;
                photoSelectMode = false;
                photoSelectedIndices.Clear();
                photoActionDropdownOpen = false;
                Game1.playSound("smallSelect");
                photoClickHandledOnPress = true;
                return;
            }
            if (photoBtnActionBounds.Contains(x, y))
            {
                photoAlbumDeleteMode = false;
                photoActionDropdownOpen = !photoActionDropdownOpen;
                Game1.playSound("smallSelect");
                photoClickHandledOnPress = true;
                return;
            }
            if (photoBtnBackFilterBounds.Contains(x, y))
            {
                photoAlbumDeleteMode = false;
                ClearPhotoFilter();
                photoClickHandledOnPress = true;
                return;
            }
        }

        /// <summary>Called from releaseLeftClick when !hasTouchScrolled, handles grid and album taps.</summary>
        internal void ReleaseLeftClickPhotoApp(int x, int y)
        {
            // If the matching press was already consumed by an overlay or button, do nothing.
            if (photoClickHandledOnPress) return;

            if (photoDetailIndex >= 0 && !photoDeleteConfirmOpen) return;    // detail taps handled in receiveLeftClick
            if (photoDeleteConfirmOpen) return;
            if (photoAlbumCreationOpen) return;
            if (photoAlbumPickerOpen) return;
            if (photoActionDropdownOpen) return;

            // Compute the scrollable viewport bounds so we can reject clicks that land on
            // the nav bar area or outside the phone content.
            Rectangle content = GetPhoneContentBounds();
            Rectangle scrollViewport = content;

            // Only process taps that are inside the visible scrollable viewport.
            if (!scrollViewport.Contains(x, y)) return;

            // Section collapse toggle check
            if (photoBtnPeopleToggleBounds.Contains(x, y) && scrollViewport.Intersects(photoBtnPeopleToggleBounds))
            {
                peopleSectionCollapsed = !peopleSectionCollapsed;
                photoScrollTarget = Math.Min(photoScrollTarget, GetPhotoMaxScroll());
                photoScrollOffset = Math.Min(photoScrollOffset, GetPhotoMaxScroll());
                Game1.playSound("smallSelect");
                return;
            }
            if (photoBtnLocationToggleBounds.Contains(x, y) && scrollViewport.Intersects(photoBtnLocationToggleBounds))
            {
                locationSectionCollapsed = !locationSectionCollapsed;
                photoScrollTarget = Math.Min(photoScrollTarget, GetPhotoMaxScroll());
                photoScrollOffset = Math.Min(photoScrollOffset, GetPhotoMaxScroll());
                Game1.playSound("smallSelect");
                return;
            }

            // Grid cell taps – also verify the cell itself is within the viewport (not
            // scrolled off behind the nav bar or off the bottom edge).
            for (int i = 0; i < photoGridCellBounds.Count; i++)
            {
                Rectangle cell = photoGridCellBounds[i];
                if (!cell.Contains(x, y)) continue;
                // Reject if the cell is not actually visible in the viewport.
                if (!scrollViewport.Intersects(cell)) continue;

                if (photoAlbumDeleteMode)
                {
                    photoAlbumDeleteMode = false;
                    return;
                }

                int photoIdx = photoGridCellPhotoIndices[i];

                if (photoSelectMode)
                {
                    if (photoSelectedIndices.Contains(photoIdx))
                    {
                        photoSelectedIndices.Remove(photoIdx);
                        Game1.playSound("smallSelect");
                    }
                    else
                    {
                        if (photoSelectionApiMode && photoSelectedIndices.Count >= photoSelectionApiLimit)
                        {
                            Game1.playSound("cancel");
                        }
                        else
                        {
                            photoSelectedIndices.Add(photoIdx);
                            Game1.playSound("smallSelect");
                        }
                    }
                }
                else
                {
                    photoDetailIndex = photoIdx;
                    DisposePhotoDetailTexture();
                    Game1.playSound("smallSelect");
                }
                return;
            }

            // Delete toggle click
            if (photoBtnAlbumDeleteToggleBounds.Contains(x, y) && scrollViewport.Intersects(photoBtnAlbumDeleteToggleBounds))
            {
                photoAlbumDeleteMode = !photoAlbumDeleteMode;
                Game1.playSound("smallSelect");
                return;
            }

            // Album / People / Location tile taps.
            for (int i = 0; i < photoAlbumTileBounds.Count; i++)
            {
                Rectangle tile = photoAlbumTileBounds[i];
                if (!tile.Contains(x, y)) continue;
                if (!scrollViewport.Intersects(tile)) continue;

                string key = photoAlbumTileKeys[i];
                string type = photoAlbumTileTypes[i];

                if (photoAlbumDeleteMode)
                {
                    if (type == "album")
                    {
                        OpenAlbumDeleteConfirm(key);
                    }
                    else
                    {
                        photoAlbumDeleteMode = false;
                    }
                    Game1.playSound("smallSelect");
                    return;
                }

                SetPhotoFilter(key, type);
                Game1.playSound("smallSelect");
                return;
            }

            if (photoAlbumDeleteMode)
            {
                photoAlbumDeleteMode = false;
            }
        }

        /// <summary>Called from receiveKeyPress to handle album-name input.</summary>
        internal bool HandlePhotoAlbumNameKeyPress(Keys key)
        {
            if (!photoAlbumCreationOpen) return false;

            if (key == Keys.Enter)
            {
                ConfirmAlbumCreation();
                return true;
            }

            if (key == Keys.Back && photoNewAlbumName.Length > 0)
            {
                photoNewAlbumName = photoNewAlbumName.Substring(0, photoNewAlbumName.Length - 1);
                photoNewAlbumNameCursorIndex = Math.Max(0, photoNewAlbumNameCursorIndex - 1);
                photoNewAlbumNameSelectionAnchorIndex = photoNewAlbumNameCursorIndex;
                Game1.playSound("hammer");
                return true;
            }

            return false;
        }

        // ─────────────────────────────────────────────────────────────
        //  Scroll
        // ─────────────────────────────────────────────────────────────

        internal void HandlePhotoScrollWheel(int direction)
        {
            if (photoDetailIndex >= 0)
            {
                List<int> filteredIndices = GetFilteredPhotoIndices();
                int currentFilteredIdx = filteredIndices.IndexOf(photoDetailIndex);
                if (direction > 0) // Scroll up -> previous photo
                {
                    if (currentFilteredIdx > 0)
                    {
                        photoDetailIndex = filteredIndices[currentFilteredIdx - 1];
                        DisposePhotoDetailTexture();
                        Game1.playSound("smallSelect");
                    }
                }
                else if (direction < 0) // Scroll down -> next photo
                {
                    if (currentFilteredIdx >= 0 && currentFilteredIdx < filteredIndices.Count - 1)
                    {
                        photoDetailIndex = filteredIndices[currentFilteredIdx + 1];
                        DisposePhotoDetailTexture();
                        Game1.playSound("smallSelect");
                    }
                }
                return;
            }
            float delta = -(direction / 120f) * ScaleUiValue(PhotoScrollSpeed * 8);
            photoScrollTarget = Math.Clamp(photoScrollTarget + delta, 0f, GetPhotoMaxScroll());
        }

        internal void ApplyPhotoTouchScrollDelta(int pixelDelta)
        {
            if (photoDetailIndex >= 0) return;
            photoScrollTarget = Math.Clamp(photoScrollTarget + pixelDelta, 0f, GetPhotoMaxScroll());
        }

        internal void UpdatePhotoScroll(float lerpAmount)
        {
            if (currentApp != "appPhoto" || photoDetailIndex >= 0) return;
            float maxS = GetPhotoMaxScroll();
            photoScrollTarget = Math.Clamp(photoScrollTarget, 0f, maxS);
            photoScrollOffset = MathHelper.Lerp(photoScrollOffset, photoScrollTarget, lerpAmount);
            if (Math.Abs(photoScrollOffset - photoScrollTarget) <= 0.5f)
                photoScrollOffset = photoScrollTarget;
        }

        private float GetPhotoMaxScroll()
        {
            List<int> indices = GetFilteredPhotoIndices();
            int rows = (int)Math.Ceiling(indices.Count / (double)PhotoCellColsCount);
            int gridH = rows * PhotoCellHeight;
            int sectionsH = (photoFilterType == "all" && !photoSelectionApiMode) ? GetPhotoSectionsEstimatedHeight() : 0;
            int totalH = gridH + sectionsH;
            int viewportH = PhotoScrollViewportH;
            return Math.Max(0f, totalH - viewportH);
        }

        private int GetPhotoSectionsEstimatedHeight()
        {
            int sectionPad = ScaleUiValue(PhotoAlbumSectionPadBase);
            int titleH = ScaleUiValue(PhotoAlbumTitleHeightBase);
            int tileGap = ScaleUiValue(PhotoAlbumTileGapBase);
            int tileW = (GetPhoneContentBounds().Width - tileGap * (PhotoAlbumTileCols + 1)) / PhotoAlbumTileCols;
            int tileH = tileW + ScaleUiValue(PhotoAlbumTileTextHeight);

            int allAlbumCount = 2 + photoAlbumStore.Albums.Count;  // Favourites + Imported + user
            int albumRows = (int)Math.Ceiling(allAlbumCount / (double)PhotoAlbumTileCols);
            int total = sectionPad + titleH + albumRows * (tileH + tileGap) + sectionPad;

            int peopleCount = GetPeopleGroups().Count;
            if (peopleCount > 0)
            {
                total += sectionPad + titleH;
                if (!peopleSectionCollapsed)
                {
                    int pRows = (int)Math.Ceiling(peopleCount / (double)PhotoAlbumTileCols);
                    total += pRows * (tileH + tileGap);
                }
                total += sectionPad;
            }

            int locCount = GetLocationGroups().Count;
            if (locCount > 0)
            {
                total += sectionPad + titleH;
                if (!locationSectionCollapsed)
                {
                    int lRows = (int)Math.Ceiling(locCount / (double)PhotoAlbumTileCols);
                    total += lRows * (tileH + tileGap);
                }
                total += sectionPad;
            }

            return total;
        }

        /// <summary>Handle back button within photo app – returns true if handled internally.</summary>
        internal bool HandlePhotoAppBackButton()
        {
            if (photoAlbumCreationOpen) { CancelAlbumCreation(); return true; }
            if (photoAlbumPickerOpen) { photoAlbumPickerOpen = false; return true; }
            if (photoAlbumDeleteConfirmOpen) { photoAlbumDeleteConfirmOpen = false; return true; }
            if (photoDeleteConfirmOpen) { photoDeleteConfirmOpen = false; return true; }
            if (photoActionDropdownOpen) { photoActionDropdownOpen = false; return true; }
            if (photoDetailIndex >= 0) { photoDetailIndex = -1; DisposePhotoDetailTexture(); return true; }
            if (photoSelectionApiMode)
            {
                TriggerPhotoSelectionApiCallback(null);
                ClosePhotoApp();
                currentApp = null;
                Game1.playSound("bigDeSelect");
                return true;
            }
            if (photoSelectMode) { photoSelectMode = false; photoSelectedIndices.Clear(); return true; }
            if (photoFilterType != "all") { ClearPhotoFilter(); return true; }
            if (photoAlbumDeleteMode) { photoAlbumDeleteMode = false; return true; }
            return false;
        }

        // ─────────────────────────────────────────────────────────────
        //  Filter helpers
        // ─────────────────────────────────────────────────────────────

        private void SetPhotoFilter(string key, string type)
        {
            if (photoFilterType == "all")
            {
                landingPageScrollOffset = photoScrollTarget;
            }
            photoCurrentFilter = key;
            photoFilterType = type;
            photoScrollTarget = 0f;
            photoScrollOffset = 0f;
            photoSelectMode = false;
            photoSelectedIndices.Clear();
        }

        private void ClearPhotoFilter()
        {
            photoCurrentFilter = null;
            photoFilterType = "all";
            photoScrollTarget = landingPageScrollOffset;
            photoScrollOffset = landingPageScrollOffset;
            photoSelectMode = false;
            photoSelectedIndices.Clear();
        }

        private List<int> GetFilteredPhotoIndices()
        {
            var indices = GetFilteredPhotoIndicesRaw();
            if (photoSelectionApiMode && photoSelectionApiSquareOnly)
            {
                indices = indices.Where(idx =>
                {
                    if (idx < 0 || idx >= capturedImages.Count) return false;
                    float ratio = GetPhotoAspectRatio(capturedImages[idx]);
                    return ratio > 0.95f && ratio < 1.05f;
                }).ToList();
            }
            return indices;
        }

        private float GetPhotoAspectRatio(string path)
        {
            if (photoAspectRatioCache.TryGetValue(path, out float ratio))
                return ratio;

            try
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using (Texture2D temp = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream))
                {
                    float r = temp.Width / (float)temp.Height;
                    photoAspectRatioCache[path] = r;
                    return r;
                }
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        private List<int> GetFilteredPhotoIndicesRaw()
        {
            if (photoFilterType == "all")
                return Enumerable.Range(0, capturedImages.Count).ToList();

            if (photoFilterType == "favourites")
                return capturedImages
                    .Select((path, idx) => (path, idx))
                    .Where(t => IsPhotoFavourite(t.path))
                    .Select(t => t.idx)
                    .ToList();

            if (photoFilterType == "imported")
            {
                string importedFolderPath = Path.GetFullPath(GetCaptureFolderPath("photo_imported"));
                return capturedImages
                    .Select((path, idx) => (path, idx))
                    .Where(t =>
                    {
                        try
                        {
                            string dir = Path.GetFullPath(Path.GetDirectoryName(t.path) ?? "");
                            return string.Equals(dir, importedFolderPath, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .Select(t => t.idx)
                    .ToList();
            }

            if (photoFilterType == "album" && photoCurrentFilter != null)
            {
                var album = photoAlbumStore.Albums
                    .FirstOrDefault(a => string.Equals(a.Name, photoCurrentFilter, StringComparison.OrdinalIgnoreCase));
                if (album == null) return new();
                return capturedImages
                    .Select((path, idx) => (path, idx))
                    .Where(t => album.PhotoFileNames.Contains(Path.GetFileName(t.path), StringComparer.OrdinalIgnoreCase))
                    .Select(t => t.idx)
                    .ToList();
            }

            if (photoFilterType == "people" && photoCurrentFilter != null)
            {
                return capturedImages
                    .Select((path, idx) => (path, idx))
                    .Where(t =>
                    {
                        string fname = Path.GetFileName(t.path);
                        if (ModEntry.ImageMetadataStore.TryGetValue(fname, out var m) && m != null && !string.IsNullOrWhiteSpace(m.Tag))
                        {
                            var tags = m.Tag.Split(';', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var tag in tags)
                            {
                                string trimmed = tag.Trim();
                                if (trimmed.StartsWith("#"))
                                {
                                    string name = trimmed.Substring(1).Trim();
                                    if (name.StartsWith("Player ", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string playerName = name.Substring(7).Trim();
                                        if (string.Equals(playerName, photoCurrentFilter, StringComparison.OrdinalIgnoreCase))
                                            return true;
                                    }
                                    if (string.Equals(name, photoCurrentFilter, StringComparison.OrdinalIgnoreCase))
                                        return true;
                                }
                            }
                        }
                        return false;
                    })
                    .Select(t => t.idx)
                    .ToList();
            }

            if (photoFilterType == "location" && photoCurrentFilter != null)
            {
                return capturedImages
                    .Select((path, idx) => (path, idx))
                    .Where(t =>
                    {
                        string fname = Path.GetFileName(t.path);
                        return ModEntry.ImageMetadataStore.TryGetValue(fname, out var m) && m != null
                            && string.Equals(m.Location, photoCurrentFilter, StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(t => t.idx)
                    .ToList();
            }

            return Enumerable.Range(0, capturedImages.Count).ToList();
        }

        private List<string> GetPeopleGroups()
        {
            var npcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var character in Utility.getAllCharacters())
                {
                    if (character != null && !string.IsNullOrWhiteSpace(character.displayName))
                        npcNames.Add(character.displayName);
                }
            }
            catch { }
            if (Game1.player != null && !string.IsNullOrWhiteSpace(Game1.player.displayName))
                npcNames.Add(Game1.player.displayName);

            var people = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in ModEntry.ImageMetadataStore.Values)
            {
                if (m == null || string.IsNullOrWhiteSpace(m.Tag)) continue;
                var tags = m.Tag.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tag in tags)
                {
                    string trimmed = tag.Trim();
                    if (trimmed.StartsWith("#"))
                    {
                        string name = trimmed.Substring(1).Trim();
                        if (name.StartsWith("Player ", StringComparison.OrdinalIgnoreCase))
                        {
                            string playerName = name.Substring(7).Trim();
                            if (npcNames.Contains(playerName))
                            {
                                people.Add(playerName);
                                continue;
                            }
                        }
                        if (npcNames.Contains(name))
                        {
                            people.Add(name);
                        }
                    }
                }
            }
            string playerDisplayName = Game1.player?.displayName;
            var orderedList = people.OrderBy(p => p).ToList();
            if (!string.IsNullOrEmpty(playerDisplayName) && orderedList.Contains(playerDisplayName))
            {
                orderedList.Remove(playerDisplayName);
                orderedList.Insert(0, playerDisplayName);
            }
            return orderedList;
        }

        private List<string> GetLocationGroups()
        {
            return ModEntry.ImageMetadataStore.Values
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Location))
                .Select(m => m.Location)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l)
                .ToList();
        }

        // ─────────────────────────────────────────────────────────────
        //  Album actions
        // ─────────────────────────────────────────────────────────────

        private void HandlePhotoDropdownAction(string action)
        {
            switch (action)
            {
                case "Delete":
                    if (photoDetailIndex >= 0)
                    {
                        photoSelectedIndices.Clear();
                        photoSelectedIndices.Add(photoDetailIndex);
                        photoDeleteConfirmOpen = true;
                    }
                    else if (photoSelectedIndices.Count > 0)
                    {
                        photoDeleteConfirmOpen = true;
                    }
                    break;

                case "Drop from album":
                    if (photoFilterType == "album" && !string.IsNullOrEmpty(photoCurrentFilter))
                    {
                        var targetAlbum = photoAlbumStore.Albums
                            .FirstOrDefault(a => string.Equals(a.Name, photoCurrentFilter, StringComparison.OrdinalIgnoreCase));
                        if (targetAlbum != null)
                        {
                            if (photoDetailIndex >= 0 && photoDetailIndex < capturedImages.Count)
                            {
                                string fname = Path.GetFileName(capturedImages[photoDetailIndex]);
                                targetAlbum.PhotoFileNames.RemoveAll(f => string.Equals(f, fname, StringComparison.OrdinalIgnoreCase));
                                photoDetailIndex = -1;
                                DisposePhotoDetailTexture();
                            }
                            else if (photoSelectedIndices.Count > 0)
                            {
                                foreach (int idx in photoSelectedIndices)
                                {
                                    if (idx >= 0 && idx < capturedImages.Count)
                                    {
                                        string fname = Path.GetFileName(capturedImages[idx]);
                                        targetAlbum.PhotoFileNames.RemoveAll(f => string.Equals(f, fname, StringComparison.OrdinalIgnoreCase));
                                    }
                                }
                                photoSelectedIndices.Clear();
                                photoSelectMode = false;
                            }
                            SavePhotoAlbumStore();
                            photoScrollTarget = Math.Min(photoScrollTarget, GetPhotoMaxScroll());
                            photoScrollOffset = Math.Min(photoScrollOffset, GetPhotoMaxScroll());
                            Game1.playSound("coin");
                        }
                    }
                    break;

                case "Select all":
                    {
                        var filtered = GetFilteredPhotoIndices();
                        foreach (int idx in filtered)
                        {
                            photoSelectedIndices.Add(idx);
                        }
                        Game1.playSound("bigSelect");
                    }
                    break;

                case "Favourite":
                    foreach (int idx in photoSelectedIndices)
                    {
                        if (idx < capturedImages.Count)
                        {
                            string path = capturedImages[idx];
                            string fname = Path.GetFileName(path);
                            if (!photoAlbumStore.FavouriteFileNames.Any(f => string.Equals(f, fname, StringComparison.OrdinalIgnoreCase)))
                            {
                                photoAlbumStore.FavouriteFileNames.Add(fname);
                            }
                        }
                    }
                    SavePhotoAlbumStore();
                    Game1.playSound("coin");
                    break;

                case "Add to Album":
                    if (photoDetailIndex >= 0)
                    {
                        // Auto-select the active photo so it gets added to the album
                        photoSelectedIndices.Clear();
                        photoSelectedIndices.Add(photoDetailIndex);
                    }
                    photoAlbumPickerOpen = true;
                    break;

                case "Set portrait BG":
                    if (photoDetailIndex >= 0 && photoDetailIndex < capturedImages.Count)
                    {
                        string path = capturedImages[photoDetailIndex];
                        ApplyPhoneBackground(path, isLandscape: false);
                        Game1.playSound("coin");
                    }
                    break;

                case "Set landscape BG":
                    if (photoDetailIndex >= 0 && photoDetailIndex < capturedImages.Count)
                    {
                        string path = capturedImages[photoDetailIndex];
                        ApplyPhoneBackground(path, isLandscape: true);
                        Game1.playSound("coin");
                    }
                    break;

                case "Share":
                    if (photoDetailIndex >= 0 && photoDetailIndex < capturedImages.Count)
                    {
                        string path = capturedImages[photoDetailIndex];
                        string fname = Path.GetFileName(path);
                        string tag = "";
                        if (ModEntry.ImageMetadataStore.TryGetValue(fname, out var m) && m != null)
                        {
                            tag = m.Tag ?? "";
                        }
                        var socialApi = ModEntry.SHelper.ModRegistry.GetApi<IStardewSocialApi>("d5a1lamdtd.Smartphone-AppStardewSocial");
                        if (socialApi != null)
                        {
                            photoActionDropdownOpen = false;
                            socialApi.CreateDraftPost(text: null, taggedNpc: null, imagePath: path, postTags: tag);
                        }
                    }
                    break;
            }
        }

        private void OpenAlbumDeleteConfirm(string albumName)
        {
            photoAlbumToDelete = albumName;
            photoAlbumDeleteConfirmOpen = true;
        }

        private void ExecuteDeleteAlbum()
        {
            var album = photoAlbumStore.Albums.FirstOrDefault(a => string.Equals(a.Name, photoAlbumToDelete, StringComparison.OrdinalIgnoreCase));
            if (album != null)
            {
                photoAlbumStore.Albums.Remove(album);
                SavePhotoAlbumStore();
            }
            photoAlbumDeleteConfirmOpen = false;
            photoAlbumDeleteMode = false;
            Game1.playSound("trashcan");
        }

        private void ExecuteDeleteSelectedPhotos()
        {
            bool deletedBackground = false;
            bool deletedLandscapeBackground = false;
            var indices = photoSelectedIndices.OrderByDescending(i => i).ToList();

            foreach (int idx in indices)
            {
                if (idx < 0 || idx >= capturedImages.Count) continue;
                string path = capturedImages[idx];
                string fname = Path.GetFileName(path);

                if (IsSameFilePath(ModEntry.currentPhoneBackground, path))
                    deletedBackground = true;
                if (IsSameFilePath(ModEntry.currentPhoneLandscapeBackground, path))
                    deletedLandscapeBackground = true;

                try { File.Delete(path); }
                catch (Exception ex) { ModEntry.SMonitor.Log($"Failed to delete photo: {ex.Message}", LogLevel.Warn); }

                capturedImages.RemoveAt(idx);
                ModEntry.RemoveImageTags(fname);

                // Drop from album store
                foreach (var album in photoAlbumStore.Albums)
                    album.PhotoFileNames.Remove(fname);
                photoAlbumStore.FavouriteFileNames.Remove(fname);

                // Remove cached thumbnail
                if (photoThumbnailCache.TryGetValue(idx, out var tex))
                {
                    tex?.Dispose();
                    photoThumbnailCache.Remove(idx);
                }
            }

            // Shift cache keys above removed indices
            RebuildThumbnailCacheKeys();

            if (deletedBackground)
                ResetPhoneBackgroundToDefault(isLandscape: false);
            if (deletedLandscapeBackground)
                ResetPhoneBackgroundToDefault(isLandscape: true);

            SavePhotoAlbumStore();
            photoSelectedIndices.Clear();
            photoDeleteConfirmOpen = false;
            photoSelectMode = false;
            photoDetailIndex = -1;
            DisposePhotoDetailTexture();

            // Recalculate scroll bounds
            photoScrollTarget = Math.Min(photoScrollTarget, GetPhotoMaxScroll());
            photoScrollOffset = Math.Min(photoScrollOffset, GetPhotoMaxScroll());

            Game1.playSound("trashcan");
        }

        private void AddSelectedPhotosToAlbum(string albumName)
        {
            var album = photoAlbumStore.Albums
                .FirstOrDefault(a => string.Equals(a.Name, albumName, StringComparison.OrdinalIgnoreCase));
            if (album == null)
            {
                album = new Smartphone.Data.PhotoAlbumEntry { Name = albumName };
                photoAlbumStore.Albums.Add(album);
            }

            foreach (int idx in photoSelectedIndices)
            {
                if (idx >= capturedImages.Count) continue;
                string fname = Path.GetFileName(capturedImages[idx]);
                if (!album.PhotoFileNames.Contains(fname, StringComparer.OrdinalIgnoreCase))
                    album.PhotoFileNames.Add(fname);
            }

            SavePhotoAlbumStore();
            photoAlbumPickerOpen = false;
            Game1.playSound("coin");
        }

        private void OpenAlbumCreationDialog()
        {
            photoAlbumPickerOpen = false;
            photoAlbumCreationOpen = true;
            photoNewAlbumName = string.Empty;
            photoNewAlbumNameCursorIndex = 0;
            photoNewAlbumNameSelectionAnchorIndex = 0;
            photoAlbumNameUndoHistory.Clear();
            SetPhoneTextInputFocus(true);
        }

        private void ConfirmAlbumCreation()
        {
            string name = photoNewAlbumName.Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (!photoAlbumStore.Albums.Any(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)))
                    photoAlbumStore.Albums.Add(new Smartphone.Data.PhotoAlbumEntry { Name = name });

                AddSelectedPhotosToAlbum(name);
            }
            CancelAlbumCreation();
        }

        private void CancelAlbumCreation()
        {
            photoAlbumCreationOpen = false;
            photoNewAlbumName = string.Empty;
            photoAlbumNameUndoHistory.Clear();
            SetPhoneTextInputFocus(false);
        }

        // ─────────────────────────────────────────────────────────────
        //  Favourites
        // ─────────────────────────────────────────────────────────────

        private bool IsPhotoFavourite(string path)
        {
            string fname = Path.GetFileName(path);
            return photoAlbumStore.FavouriteFileNames
                .Any(f => string.Equals(f, fname, StringComparison.OrdinalIgnoreCase));
        }

        private void TogglePhotoFavourite(string path)
        {
            string fname = Path.GetFileName(path);
            int existing = photoAlbumStore.FavouriteFileNames
                .FindIndex(f => string.Equals(f, fname, StringComparison.OrdinalIgnoreCase));

            if (existing >= 0)
                photoAlbumStore.FavouriteFileNames.RemoveAt(existing);
            else
                photoAlbumStore.FavouriteFileNames.Add(fname);

            SavePhotoAlbumStore();
            Game1.playSound("coin");
        }

        // ─────────────────────────────────────────────────────────────
        //  Album count helpers
        // ─────────────────────────────────────────────────────────────

        private int GetAlbumPhotoCount(string name, string type)
        {
            return GetAlbumPhotoIndices(name, type).Count;
        }

        private List<int> GetAlbumPhotoIndices(string name, string type)
        {
            string savedFilter = photoCurrentFilter;
            string savedType = photoFilterType;
            photoCurrentFilter = name;
            photoFilterType = type;
            var result = GetFilteredPhotoIndices();
            photoCurrentFilter = savedFilter;
            photoFilterType = savedType;
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        //  Thumbnail / texture management
        // ─────────────────────────────────────────────────────────────

        private Texture2D? GetOrLoadThumbnail(int index)
        {
            if (photoThumbnailCache.TryGetValue(index, out var cached))
                return cached.IsDisposed ? null : cached;

            if (index < 0 || index >= capturedImages.Count)
                return null;

            try
            {
                string path = capturedImages[index];
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Texture2D source = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                // Scale to thumbnail size based on PhotoPreviewQuality config (Low=1x, Medium=2x, High=3x)
                int qualityMultiplier = ModEntry.Config?.PhotoPreviewQuality switch
                {
                    "Medium" => 2,
                    "High" => 3,
                    _ => 1
                };
                int maxW = Math.Max(1, PhotoCellWidth * qualityMultiplier);
                int maxH = Math.Max(1, PhotoCellHeight * qualityMultiplier);
                Texture2D thumb = CreateScaledThumbnail(source, maxW, maxH);
                source.Dispose();
                photoThumbnailCache[index] = thumb;
                return thumb;
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to load photo thumbnail[{index}]: {ex.Message}", LogLevel.Trace);
                return null;
            }
        }

        private Texture2D? GetAlbumCoverThumbnail(string name, string type)
        {
            List<int> indices = GetAlbumPhotoIndices(name, type);
            if (indices.Count == 0) return null;
            // Use last photo (newest) as cover
            return GetOrLoadThumbnail(indices[^1]);
        }

        private void EnsurePhotoDetailTexture()
        {
            if (photoDetailIndex < 0 || photoDetailIndex >= capturedImages.Count)
            {
                DisposePhotoDetailTexture();
                return;
            }

            if (photoDetailTextureIndex == photoDetailIndex
                && photoDetailTexture != null
                && !photoDetailTexture.IsDisposed)
                return;

            DisposePhotoDetailTexture();

            try
            {
                string path = capturedImages[photoDetailIndex];
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                photoDetailTexture = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                photoDetailTextureIndex = photoDetailIndex;
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to load detail photo: {ex.Message}", LogLevel.Trace);
            }
        }

        private void DisposePhotoDetailTexture()
        {
            if (photoDetailTexture != null && !photoDetailTexture.IsDisposed)
                photoDetailTexture.Dispose();
            photoDetailTexture = null;
            photoDetailTextureIndex = -1;
        }

        private void DisposeThumbnailCache()
        {
            foreach (var tex in photoThumbnailCache.Values)
                if (tex != null && !tex.IsDisposed)
                    tex.Dispose();
            photoThumbnailCache.Clear();
            photoAspectRatioCache.Clear();
        }

        private void RebuildThumbnailCacheKeys()
        {
            // After deletion the indices shift; safest approach is to clear cache
            // and let lazy loading rebuild it.
            DisposeThumbnailCache();
        }

        /// <summary>Creates a nearest-neighbour scaled thumbnail that fits within maxW×maxH.</summary>
        private static Texture2D CreateScaledThumbnail(Texture2D source, int maxW, int maxH)
        {
            int srcW = Math.Max(1, source.Width);
            int srcH = Math.Max(1, source.Height);

            float scale = Math.Min(maxW / (float)srcW, maxH / (float)srcH);
            int destW = Math.Max(1, (int)Math.Round(srcW * scale));
            int destH = Math.Max(1, (int)Math.Round(srcH * scale));

            Color[] src = new Color[srcW * srcH];
            Color[] dest = new Color[destW * destH];
            source.GetData(src);

            for (int dy = 0; dy < destH; dy++)
            {
                int sy = Math.Min(srcH - 1, (int)(dy / (float)destH * srcH));
                for (int dx = 0; dx < destW; dx++)
                {
                    int sx = Math.Min(srcW - 1, (int)(dx / (float)destW * srcW));
                    dest[dy * destW + dx] = src[sy * srcW + sx];
                }
            }

            Texture2D thumb = new(Game1.graphics.GraphicsDevice, destW, destH);
            thumb.SetData(dest);
            return thumb;
        }

        // ─────────────────────────────────────────────────────────────
        //  Album data persistence
        // ─────────────────────────────────────────────────────────────

        private static string GetPhotoAlbumDataPath()
        {
            return System.IO.Path.Combine("userdata", GetCurrentSaveFolderName(), PhotoAlbumDataPath);
        }

        private void LoadPhotoAlbumStore()
        {
            try
            {
                photoAlbumStore =
                    ModEntry.SHelper.Data.ReadJsonFile<Smartphone.Data.PhotoAlbumStore>(GetPhotoAlbumDataPath())
                    ?? new Smartphone.Data.PhotoAlbumStore();
            }
            catch
            {
                photoAlbumStore = new Smartphone.Data.PhotoAlbumStore();
            }
        }

        private void SavePhotoAlbumStore()
        {
            try
            {
                ModEntry.SHelper.Data.WriteJsonFile(GetPhotoAlbumDataPath(), photoAlbumStore);
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to save photo album store: {ex.Message}", LogLevel.Warn);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Utility drawing helpers
        // ─────────────────────────────────────────────────────────────

        private void DrawPhoneRoundButton(SpriteBatch b, Rectangle bounds, string label, Color textColor, Color bgColor)
        {
            // Replace flat rectangle with the textured menu box
            // Note: I've passed Color.White so the texture renders with its original brown colors,
            // but you can change it to bgColor if you want to tint the Stardew box!
            Textures.DrawCard(
                b,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                bgColor,
                1f,
                false
            );

            // Text drawing and centering logic remains the same
            float scale = GetPhoneTextScale(0.75f) * phoneUiScale;
            Vector2 sz = Game1.smallFont.MeasureString(label) * scale;

            b.DrawString(
                Game1.smallFont,
                label,
                new Vector2(bounds.Center.X - sz.X / 2f, bounds.Center.Y - sz.Y / 2f),
                textColor,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                1f
            );
        }

        private void DrawPhoneIconButton(SpriteBatch b, Rectangle bounds, string icon, Color color)
        {
            float scale = GetPhoneTextScale(0.7f) * phoneUiScale;
            Vector2 sz = Game1.smallFont.MeasureString(icon) * scale;
            b.DrawString(Game1.smallFont, icon,
                new Vector2(bounds.Center.X - sz.X / 2f, bounds.Center.Y - sz.Y / 2f),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
        }

        // ─────────────────────────────────────────────────────────────
        //  Display name (kept from original for compatibility)
        // ─────────────────────────────────────────────────────────────

        private string GetPhotoDisplayName(string photoPath)
        {
            string filename = Path.GetFileName(photoPath);
            if (ModEntry.ImageMetadataStore.TryGetValue(filename, out var metadata) && metadata != null)
            {
                string loc = string.IsNullOrWhiteSpace(metadata.Location) ? ModEntry.SHelper.Translation.Get("ui.photo.unknown_location") : metadata.Location;
                string ts = string.IsNullOrWhiteSpace(metadata.TimeString) ? "" : metadata.TimeString;
                return string.IsNullOrEmpty(ts) ? loc : ModEntry.SHelper.Translation.Get("ui.photo.location_timestamp_format", new { location = loc, timestamp = ts });
            }

            string rawName = Path.GetFileNameWithoutExtension(photoPath);
            int underscoreIndex = rawName.IndexOf('_');
            string displayName = underscoreIndex >= 0 ? rawName[..underscoreIndex] : rawName;
            return displayName.Replace("-", " ");
        }

        private void DrawLoopingText(SpriteBatch b, string text, SpriteFont font, Vector2 position, float viewportWidth, Color color, float textScale)
        {
            string safeText = text ?? "";
            if (string.IsNullOrEmpty(safeText))
                return;

            if ((font.MeasureString(safeText).X * textScale) <= viewportWidth)
            {
                b.DrawString(font, safeText, position, color, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);
                return;
            }

            string marqueeSource = safeText + new string(' ', AppLabelTrailingSpaces);
            Rectangle clipRect = new Rectangle(
                (int)Math.Floor(position.X),
                (int)Math.Floor(position.Y),
                Math.Max(1, (int)Math.Ceiling(viewportWidth)),
                Math.Max(1, (int)Math.Ceiling(font.LineSpacing * textScale + 2f)));

            Rectangle viewportBounds = Game1.graphics.GraphicsDevice.ScissorRectangle;
            clipRect = Rectangle.Intersect(clipRect, viewportBounds);
            if (clipRect.Width <= 0 || clipRect.Height <= 0)
                return;

            float marqueeWidth = font.MeasureString(marqueeSource).X * textScale;
            if (marqueeWidth <= 0f)
            {
                b.DrawString(font, safeText, position, color, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);
                return;
            }

            float scrollOffset = (float)((appLabelMarqueeElapsedSeconds * AppLabelMarqueePixelsPerSecond) % marqueeWidth);
            Vector2 drawPos = new Vector2(position.X - scrollOffset, position.Y);

            b.End();

            Rectangle previousScissorRect = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, appLabelScissorRasterizer);

            b.DrawString(font, marqueeSource, drawPos, color, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);
            b.DrawString(font, marqueeSource, new Vector2(drawPos.X + marqueeWidth, drawPos.Y), color, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissorRect;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, appLabelScissorRasterizer);
        }

        private void DrawRedesignedNavButton(SpriteBatch b, Rectangle bounds, bool isNext, bool enabled)
        {
            Rectangle source = isNext
                ? Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33)
                : Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44);

            Color color = enabled ? Color.White : Color.Gray * 0.5f;

            // Subtle drop shadow so the arrow is visible against bright photos
            b.Draw(
                Game1.mouseCursors,
                new Rectangle(bounds.X + ScaleUiValue(8) + 2, bounds.Y + ScaleUiValue(8) + 2, bounds.Width - ScaleUiValue(16), bounds.Height - ScaleUiValue(16)),
                source,
                Color.Black * 0.4f);

            // Draw Arrow
            b.Draw(
                Game1.mouseCursors,
                new Rectangle(bounds.X + ScaleUiValue(8), bounds.Y + ScaleUiValue(8), bounds.Width - ScaleUiValue(16), bounds.Height - ScaleUiValue(16)),
                source,
                color);
        }

        private void DrawRedesignedFavButton(SpriteBatch b, Rectangle bounds, bool isFav)
        {
            Textures.DrawCard(
                b,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                new Color(0, 0, 0, 140),
                1f,
                false);

            Rectangle source = isFav
                ? new Rectangle(211, 428, 7, 7)
                : new Rectangle(218, 428, 7, 7);

            float heartScale = ScaleUiValue(4f);
            int heartW = (int)(7 * heartScale);
            int heartH = (int)(7 * heartScale);
            Rectangle heartDest = new Rectangle(
                bounds.Center.X - heartW / 2,
                bounds.Center.Y - heartH / 2,
                heartW,
                heartH);

            b.Draw(
                Game1.mouseCursors,
                heartDest,
                source,
                isFav ? new Color(255, 80, 80) : Color.White);
        }
    }
}
