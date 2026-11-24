using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Forms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using DrawingColor = System.Drawing.Color;

namespace AuraFolders
{
    /// <summary>
    /// Full-screen click-through overlay window that renders Canva-style smart alignment lines,
    /// desktop center/margin guides, and distance badges while the user drags a folder.
    /// </summary>
    public class CanvaGuideOverlayWindow : Window
    {
        private Canvas _canvas;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        public CanvaGuideOverlayWindow()
        {
            Title = "AuraFolders Canva Guides";
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = MediaBrushes.Transparent;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            IsHitTestVisible = false;

            _canvas = new Canvas
            {
                Width = Width,
                Height = Height,
                Background = MediaBrushes.Transparent,
                IsHitTestVisible = false
            };
            Content = _canvas;

            Loaded += (s, e) =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int ex = DesktopNativeHelper.GetWindowLong(hwnd, GWL_EXSTYLE);
                DesktopNativeHelper.SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            };
        }

        public void ClearAndHide()
        {
            _canvas.Children.Clear();
            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Collapsed;
            }
        }

        public void RenderGuides(
            FolderData dragged,
            List<FolderData> allFolders,
            double rawX,
            double rawY,
            out double snappedX,
            out double snappedY)
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;
            Width = screenW;
            Height = screenH;
            _canvas.Width = screenW;
            _canvas.Height = screenH;
            _canvas.Children.Clear();

            snappedX = rawX;
            snappedY = rawY;

            const double snapTolerance = 8.0;
            const double halfBox = 38.0; // Half of 76x76 1x1 folder box

            bool snappedToX = false;
            bool snappedToY = false;

            // Canva Colors:
            // - Desktop Center / Margin Guides: Crisp Magenta/Purple (#FFE879F9) & Cyan (#FF38BDF8)
            // - Folder-to-Folder Alignment: Canva Pink/Rose (#FFF43F5E)
            SolidColorBrush centerBrush = new SolidColorBrush(MediaColor.FromArgb(230, 232, 121, 249));
            SolidColorBrush marginBrush = new SolidColorBrush(MediaColor.FromArgb(175, 56, 189, 248));
            SolidColorBrush folderAlignBrush = new SolidColorBrush(MediaColor.FromArgb(240, 244, 63, 94));
            SolidColorBrush folderEdgeBrush = new SolidColorBrush(MediaColor.FromArgb(145, 244, 63, 94));

            // 1. Desktop Safe Margin Box (Kiri, Kanan, Atas, Bawah Desktop)
            double marginLeft = 130;
            double marginRight = screenW - 130;
            double marginTop = 110;
            double marginBottom = screenH - 120;

            // Check Desktop Horizontal Center (Sejajar Tengah Kiri-Kanan Desktop)
            double screenCenterX = screenW / 2.0;
            if (Math.Abs(rawX - screenCenterX) <= snapTolerance)
            {
                snappedX = screenCenterX;
                snappedToX = true;
                AddGuideLine(screenCenterX, 0, screenCenterX, screenH, centerBrush, 1.4, false);
                AddGuideBadge(screenCenterX + 8, 36, "Tengah Desktop (X)", centerBrush);
            }

            // Check Desktop Vertical Center (Sejajar Tengah Atas-Bawah Desktop)
            double screenCenterY = screenH / 2.0;
            if (Math.Abs(rawY - screenCenterY) <= snapTolerance)
            {
                snappedY = screenCenterY;
                snappedToY = true;
                AddGuideLine(0, screenCenterY, screenW, screenCenterY, centerBrush, 1.4, false);
                AddGuideBadge(36, screenCenterY - 26, "Tengah Desktop (Y)", centerBrush);
            }

            // Check Desktop Left / Right / Top / Bottom Margin Alignment
            bool showMarginFrame = false;
            if (!snappedToX && Math.Abs(rawX - marginLeft) <= snapTolerance)
            {
                snappedX = marginLeft;
                snappedToX = true;
                showMarginFrame = true;
                AddGuideBadge(marginLeft + 8, rawY - 58, "Batas Kiri Desktop", marginBrush);
            }
            else if (!snappedToX && Math.Abs(rawX - marginRight) <= snapTolerance)
            {
                snappedX = marginRight;
                snappedToX = true;
                showMarginFrame = true;
                AddGuideBadge(marginRight - 130, rawY - 58, "Batas Kanan Desktop", marginBrush);
            }

            if (!snappedToY && Math.Abs(rawY - marginTop) <= snapTolerance)
            {
                snappedY = marginTop;
                snappedToY = true;
                showMarginFrame = true;
                AddGuideBadge(rawX + 48, marginTop - 12, "Batas Atas Desktop", marginBrush);
            }
            else if (!snappedToY && Math.Abs(rawY - marginBottom) <= snapTolerance)
            {
                snappedY = marginBottom;
                snappedToY = true;
                showMarginFrame = true;
                AddGuideBadge(rawX + 48, marginBottom - 12, "Batas Bawah Desktop", marginBrush);
            }

            if (showMarginFrame)
            {
                // Draw Canva-style dashed desktop margin frame
                System.Windows.Shapes.Rectangle frameRect = new System.Windows.Shapes.Rectangle
                {
                    Width = marginRight - marginLeft,
                    Height = marginBottom - marginTop,
                    Stroke = marginBrush,
                    StrokeThickness = 1.2,
                    StrokeDashArray = new DoubleCollection(new double[] { 5, 4 })
                };
                Canvas.SetLeft(frameRect, marginLeft);
                Canvas.SetTop(frameRect, marginTop);
                _canvas.Children.Add(frameRect);
            }

            // 2. Folder-to-Folder Alignment & Magnetic Spacing (Sejajar dengan Folder Samping / Atas-Bawah)
            const double standardGridSpacing = 115.0; // Standard center-to-center distance between 1x1 folders

            foreach (FolderData other in allFolders)
            {
                if (other.Id == dragged.Id) continue;

                // A. Horizontal Row Alignment (Sejajar Kiri-Kanan dengan Folder Sampingnya)
                if (Math.Abs(snappedY - other.Y) <= snapTolerance)
                {
                    if (!snappedToY)
                    {
                        snappedY = other.Y;
                        snappedToY = true;
                    }

                    // Also check if X is near standard 115px grid spacing next to this aligned folder!
                    if (!snappedToX)
                    {
                        if (Math.Abs((rawX - other.X) - standardGridSpacing) <= snapTolerance)
                        {
                            snappedX = other.X + standardGridSpacing;
                            snappedToX = true;
                        }
                        else if (Math.Abs((other.X - rawX) - standardGridSpacing) <= snapTolerance)
                        {
                            snappedX = other.X - standardGridSpacing;
                            snappedToX = true;
                        }
                    }

                    double minX = Math.Min(snappedX, other.X) - 65;
                    double maxX = Math.Max(snappedX, other.X) + 65;

                    // Center line + Top & Bottom bounding box edge lines (Canva style!)
                    AddGuideLine(minX, other.Y, maxX, other.Y, folderAlignBrush, 1.4, false);
                    AddGuideLine(minX, other.Y - halfBox, maxX, other.Y - halfBox, folderEdgeBrush, 1.0, true);
                    AddGuideLine(minX, other.Y + halfBox, maxX, other.Y + halfBox, folderEdgeBrush, 1.0, true);

                    // Draw Canva distance indicator pill between the two folders if they are neighbors
                    double centerDist = Math.Abs(snappedX - other.X);
                    double edgeGap = centerDist - (halfBox * 2);
                    if (edgeGap > 4 && edgeGap < 260)
                    {
                        double gapStartX = Math.Min(snappedX, other.X) + halfBox;
                        double gapEndX = Math.Max(snappedX, other.X) - halfBox;
                        double midX = (gapStartX + gapEndX) / 2.0;
                        AddDistanceIndicatorHorizontal(gapStartX, gapEndX, other.Y + halfBox + 18, (int)Math.Round(edgeGap));
                        AddGuideBadge(midX - 22, other.Y - halfBox - 26, ((int)Math.Round(edgeGap)) + " px", folderAlignBrush);
                    }
                }

                // B. Vertical Column Alignment (Sejajar Atas-Bawah dengan Folder Lain)
                if (Math.Abs(snappedX - other.X) <= snapTolerance)
                {
                    if (!snappedToX)
                    {
                        snappedX = other.X;
                        snappedToX = true;
                    }

                    if (!snappedToY)
                    {
                        if (Math.Abs((rawY - other.Y) - standardGridSpacing) <= snapTolerance)
                        {
                            snappedY = other.Y + standardGridSpacing;
                            snappedToY = true;
                        }
                        else if (Math.Abs((other.Y - rawY) - standardGridSpacing) <= snapTolerance)
                        {
                            snappedY = other.Y - standardGridSpacing;
                            snappedToY = true;
                        }
                    }

                    double minY = Math.Min(snappedY, other.Y) - 65;
                    double maxY = Math.Max(snappedY, other.Y) + 65;

                    // Center vertical line + Left & Right bounding box edge lines (Canva style!)
                    AddGuideLine(other.X, minY, other.X, maxY, folderAlignBrush, 1.4, false);
                    AddGuideLine(other.X - halfBox, minY, other.X - halfBox, maxY, folderEdgeBrush, 1.0, true);
                    AddGuideLine(other.X + halfBox, minY, other.X + halfBox, maxY, folderEdgeBrush, 1.0, true);

                    double centerDistY = Math.Abs(snappedY - other.Y);
                    double edgeGapY = centerDistY - (halfBox * 2);
                    if (edgeGapY > 4 && edgeGapY < 260)
                    {
                        double midY = (snappedY + other.Y) / 2.0;
                        AddGuideBadge(other.X + halfBox + 10, midY - 10, ((int)Math.Round(edgeGapY)) + " px", folderAlignBrush);
                    }
                }
            }

            // Draw subtle target bounding box around the snapped 1x1 folder position
            System.Windows.Shapes.Rectangle activeBox = new System.Windows.Shapes.Rectangle
            {
                Width = halfBox * 2,
                Height = halfBox * 2,
                RadiusX = 20,
                RadiusY = 20,
                Stroke = (snappedToX || snappedToY) ? folderAlignBrush : marginBrush,
                StrokeThickness = 1.4,
                StrokeDashArray = new DoubleCollection(new double[] { 3, 2 })
            };
            Canvas.SetLeft(activeBox, snappedX - halfBox);
            Canvas.SetTop(activeBox, snappedY - halfBox);
            _canvas.Children.Add(activeBox);

            if (Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
            }
        }

        private void AddGuideLine(double x1, double y1, double x2, double y2, System.Windows.Media.Brush stroke, double thickness, bool dashed)
        {
            Line line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = stroke,
                StrokeThickness = thickness,
                SnapsToDevicePixels = true
            };
            if (dashed)
            {
                line.StrokeDashArray = new DoubleCollection(new double[] { 4, 3 });
            }
            _canvas.Children.Add(line);
        }

        private void AddDistanceIndicatorHorizontal(double x1, double x2, double y, int gapPx)
        {
            SolidColorBrush br = new SolidColorBrush(MediaColor.FromArgb(220, 244, 63, 94));
            AddGuideLine(x1, y, x2, y, br, 1.2, false);
            AddGuideLine(x1, y - 4, x1, y + 4, br, 1.2, false);
            AddGuideLine(x2, y - 4, x2, y + 4, br, 1.2, false);
        }

        private void AddGuideBadge(double x, double y, string text, System.Windows.Media.Brush accentBrush)
        {
            Border pill = new Border
            {
                Background = new SolidColorBrush(MediaColor.FromArgb(235, 14, 14, 19)),
                BorderBrush = accentBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 2, 6, 2.5)
            };

            TextBlock tb = new TextBlock
            {
                Text = text,
                Foreground = MediaBrushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                FontSize = 10
            };
            pill.Child = tb;

            Canvas.SetLeft(pill, x);
            Canvas.SetTop(pill, y);
            _canvas.Children.Add(pill);
        }
    }

    public class SettingsWindow : Window
    {
        private readonly FolderManagerController _controller;

        public SettingsWindow(FolderManagerController controller)
        {
            _controller = controller;

            Title = "AuraFolders Settings";
            Width = 420;
            Height = 520;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = MediaBrushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = true;

            BuildUI();
        }

        private void BuildUI()
        {
            AppConfig cfg = _controller.Config;
            cfg.EnsureDefaults();

            Border outerCard = new Border
            {
                CornerRadius = new CornerRadius(22),
                Background = new System.Windows.Media.LinearGradientBrush(
                    MediaColor.FromArgb(250, 22, 22, 29),
                    MediaColor.FromArgb(252, 11, 11, 15),
                    new System.Windows.Point(0, 0),
                    new System.Windows.Point(1, 1)),
                BorderBrush = new SolidColorBrush(MediaColor.FromArgb(95, 255, 255, 255)),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(14),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 26,
                    ShadowDepth = 8,
                    Opacity = 0.85
                }
            };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });

            Border header = new Border
            {
                Background = new SolidColorBrush(MediaColor.FromArgb(14, 255, 255, 255)),
                CornerRadius = new CornerRadius(22, 22, 0, 0),
                Padding = new Thickness(20, 0, 14, 0),
                Cursor = Cursors.SizeAll
            };
            header.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed) DragMove();
            };

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock titleText = new TextBlock
            {
                Text = "⚙  Pengaturan HoverFold",
                Foreground = MediaBrushes.White,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button closeWinBtn = CreatePillButton("✕", 28, 28, false);
            closeWinBtn.ToolTip = "Tutup jendela pengaturan";
            closeWinBtn.Click += (s, e) => Close();

            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(closeWinBtn, 1);
            headerGrid.Children.Add(titleText);
            headerGrid.Children.Add(closeWinBtn);
            header.Child = headerGrid;

            Border div = new Border
            {
                Background = new SolidColorBrush(MediaColor.FromArgb(24, 255, 255, 255)),
                Height = 1
            };

            ScrollViewer bodyScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                Padding = new Thickness(20, 14, 20, 10)
            };

            StackPanel body = new StackPanel();

            body.Children.Add(CreateSectionLabel("TEMA VISUAL (AESTHETIC PRESET)"));
            WrapPanel themeRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            string[] themes = new[] { "Obsidian", "Frosted", "Campfire", "Midnight" };
            string[] themeLabels = new[] { "Obsidian Dark", "Frosted Glass", "Campfire Warm", "Midnight Slate" };

            for (int i = 0; i < themes.Length; i++)
            {
                string tKey = themes[i];
                bool active = string.Equals(cfg.ThemePreset, tKey, StringComparison.OrdinalIgnoreCase);
                Button tBtn = CreatePillButton(themeLabels[i], 168, 32, active);
                tBtn.Margin = new Thickness(0, 0, 8, 8);
                tBtn.Click += (s, e) =>
                {
                    cfg.ThemePreset = tKey;
                    _controller.ApplySettingsAndRefreshAll();
                    BuildUI();
                };
                themeRow.Children.Add(tBtn);
            }
            body.Children.Add(themeRow);

            TextBlock opacityLabel = CreateSectionLabel("TRANSPARANSI BACKGROUND (" + (int)Math.Round(cfg.CardOpacity * 100) + "%)");
            body.Children.Add(opacityLabel);

            Slider opacitySlider = new Slider
            {
                Minimum = 0.50,
                Maximum = 1.00,
                Value = cfg.CardOpacity,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = false,
                Margin = new Thickness(0, 2, 0, 16),
                Cursor = Cursors.Hand
            };
            opacitySlider.ValueChanged += (s, e) =>
            {
                cfg.CardOpacity = opacitySlider.Value;
                opacityLabel.Text = "TRANSPARANSI BACKGROUND (" + (int)Math.Round(cfg.CardOpacity * 100) + "%)";
                _controller.ApplySettingsAndRefreshAll();
            };
            body.Children.Add(opacitySlider);

            body.Children.Add(CreateSectionLabel("LAYOUT GRID SAAT MEKAR (HOVER)"));
            StackPanel colRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };

            Button col3Btn = CreatePillButton("3 Kolom (Compact)", 168, 30, cfg.GridColumns == 3);
            col3Btn.Margin = new Thickness(0, 0, 8, 0);
            col3Btn.Click += (s, e) =>
            {
                cfg.GridColumns = 3;
                _controller.ApplySettingsAndRefreshAll();
                BuildUI();
            };

            Button col4Btn = CreatePillButton("4 Kolom (Wide)", 168, 30, cfg.GridColumns == 4);
            col4Btn.Click += (s, e) =>
            {
                cfg.GridColumns = 4;
                _controller.ApplySettingsAndRefreshAll();
                BuildUI();
            };

            colRow.Children.Add(col3Btn);
            colRow.Children.Add(col4Btn);
            body.Children.Add(colRow);

            body.Children.Add(CreateSectionLabel("UKURAN KOTAK FOLDER 1x1 DI DESKTOP"));
            StackPanel scaleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            double[] scales = new[] { 0.88, 1.0, 1.15 };
            string[] scaleNames = new[] { "Kecil", "Normal (1x1)", "Besar" };

            for (int i = 0; i < scales.Length; i++)
            {
                double sc = scales[i];
                bool active = Math.Abs(cfg.IdleTileScale - sc) < 0.05;
                Button sBtn = CreatePillButton(scaleNames[i], 110, 30, active);
                sBtn.Margin = new Thickness(0, 0, 8, 0);
                sBtn.Click += (s, e) =>
                {
                    cfg.IdleTileScale = sc;
                    _controller.ApplySettingsAndRefreshAll();
                    BuildUI();
                };
                scaleRow.Children.Add(sBtn);
            }
            body.Children.Add(scaleRow);

            body.Children.Add(CreateSectionLabel("OPSI DESKTOP & SHORTCUT"));
            StackPanel quickRow = new StackPanel { Orientation = Orientation.Vertical };

            string badgeText = cfg.ShowCountBadge ? "✓  Tampilkan Badge Angka Jumlah App" : "○  Tampilkan Badge Angka Jumlah App";
            Button badgeBtn = CreatePillButton(badgeText, 344, 32, cfg.ShowCountBadge);
            badgeBtn.Margin = new Thickness(0, 0, 0, 8);
            badgeBtn.Click += (s, e) =>
            {
                cfg.ShowCountBadge = !cfg.ShowCountBadge;
                _controller.ApplySettingsAndRefreshAll();
                BuildUI();
            };

            Button toggleDesktopIconsBtn = CreatePillButton("👁  Sembunyikan / Tampilkan Icon Bawaan Windows", 344, 32, false);
            toggleDesktopIconsBtn.Margin = new Thickness(0, 0, 0, 8);
            toggleDesktopIconsBtn.Click += (s, e) => DesktopNativeHelper.ToggleNativeDesktopIcons();

            StackPanel manageRow = new StackPanel { Orientation = Orientation.Horizontal };
            Button addFolderBtn = CreatePillButton("＋ Folder Baru", 168, 30, false);
            addFolderBtn.Margin = new Thickness(0, 0, 8, 0);
            addFolderBtn.Click += (s, e) => _controller.CreateNewFolderNear(null);

            Button centerAllBtn = CreatePillButton("⌖ Rapikan ke Tengah", 168, 30, false);
            centerAllBtn.Click += (s, e) => _controller.CenterAllFoldersOnDesktop();

            manageRow.Children.Add(addFolderBtn);
            manageRow.Children.Add(centerAllBtn);

            quickRow.Children.Add(badgeBtn);
            quickRow.Children.Add(toggleDesktopIconsBtn);
            quickRow.Children.Add(manageRow);
            body.Children.Add(quickRow);

            bodyScroll.Content = body;

            Border footer = new Border
            {
                Background = new SolidColorBrush(MediaColor.FromArgb(14, 255, 255, 255)),
                CornerRadius = new CornerRadius(0, 0, 22, 22),
                Padding = new Thickness(20, 0, 20, 0)
            };

            Grid footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button killAppBtn = new Button
            {
                Content = "✖  Matikan Aplikasi (Close App)",
                Height = 34,
                Padding = new Thickness(14, 0, 14, 0),
                Cursor = Cursors.Hand,
                Foreground = new SolidColorBrush(MediaColor.FromRgb(255, 195, 195)),
                Background = new SolidColorBrush(MediaColor.FromArgb(45, 220, 55, 55)),
                BorderBrush = new SolidColorBrush(MediaColor.FromArgb(110, 255, 95, 95)),
                BorderThickness = new Thickness(1),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            ApplyRoundedTemplate(killAppBtn, 10, MediaColor.FromArgb(85, 235, 65, 65));
            killAppBtn.Click += (s, e) =>
            {
                _controller.CleanupTray();
                Application.Current.Shutdown();
            };

            Button doneBtn = CreatePillButton("Selesai", 86, 34, true);
            doneBtn.VerticalAlignment = VerticalAlignment.Center;
            doneBtn.Click += (s, e) => Close();

            Grid.SetColumn(killAppBtn, 0);
            Grid.SetColumn(doneBtn, 1);
            footerGrid.Children.Add(killAppBtn);
            footerGrid.Children.Add(doneBtn);
            footer.Child = footerGrid;

            Grid.SetRow(header, 0);
            Grid.SetRow(div, 1);
            Grid.SetRow(bodyScroll, 2);
            Grid.SetRow(footer, 3);

            layout.Children.Add(header);
            layout.Children.Add(div);
            layout.Children.Add(bodyScroll);
            layout.Children.Add(footer);

            outerCard.Child = layout;
            Content = outerCard;
        }

        private TextBlock CreateSectionLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(MediaColor.FromArgb(150, 210, 210, 225)),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                FontSize = 10,
                Margin = new Thickness(2, 0, 0, 6)
            };
        }

        private Button CreatePillButton(string text, double width, double height, bool isActive)
        {
            Button btn = new Button
            {
                Content = text,
                Width = width,
                Height = height,
                Cursor = Cursors.Hand,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Semibold"),
                FontSize = 11.5,
                Foreground = isActive
                    ? new SolidColorBrush(MediaColor.FromRgb(12, 12, 16))
                    : new SolidColorBrush(MediaColor.FromArgb(230, 235, 235, 245)),
                Background = isActive
                    ? new SolidColorBrush(MediaColor.FromRgb(242, 242, 248))
                    : new SolidColorBrush(MediaColor.FromArgb(22, 255, 255, 255)),
                BorderBrush = isActive
                    ? new SolidColorBrush(MediaColor.FromRgb(255, 255, 255))
                    : new SolidColorBrush(MediaColor.FromArgb(45, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };

            MediaColor hoverCol = isActive
                ? MediaColor.FromRgb(255, 255, 255)
                : MediaColor.FromArgb(48, 255, 255, 255);
            ApplyRoundedTemplate(btn, 10, hoverCol);
            return btn;
        }

        private void ApplyRoundedTemplate(Button btn, double radius, MediaColor hoverBg)
        {
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "b";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;

            Trigger tr = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            tr.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverBg), "b"));
            template.Triggers.Add(tr);

            btn.Template = template;
        }
    }

    public class FolderManagerController
    {
        public AppConfig Config { get; private set; }
        private readonly List<FolderWidgetWindow> _windows = new List<FolderWidgetWindow>();
        private Forms.NotifyIcon _trayIcon;
        private System.Windows.Threading.DispatcherTimer _zOrderTimer;
        private IntPtr _lastForegroundHwnd = IntPtr.Zero;
        private SettingsWindow _settingsWindow;
        private CanvaGuideOverlayWindow _canvaOverlay;

        public void Initialize()
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            Config = ConfigManager.LoadOrCreateDefault(screenW, screenH);
            IconExtractor.ClearCache();

            _canvaOverlay = new CanvaGuideOverlayWindow();
            _canvaOverlay.Show();
            _canvaOverlay.ClearAndHide();

            SpawnAllFolderWindows();
            SetupSystemTray();
            StartDesktopZOrderWatchdog();
        }

        public void UpdateCanvaAlignmentGuides(FolderData dragged, double rawX, double rawY, out double snappedX, out double snappedY)
        {
            if (_canvaOverlay == null)
            {
                snappedX = rawX;
                snappedY = rawY;
                return;
            }
            _canvaOverlay.RenderGuides(dragged, Config.Folders, rawX, rawY, out snappedX, out snappedY);
        }

        public void HideCanvaAlignmentGuides()
        {
            if (_canvaOverlay != null)
            {
                _canvaOverlay.ClearAndHide();
            }
        }

        public void OpenSettingsWindow()
        {
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.Activate();
                return;
            }
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        public void ApplySettingsAndRefreshAll()
        {
            SaveConfig();
            foreach (FolderWidgetWindow win in _windows)
            {
                win.RefreshFolderContent();
            }
        }

        private void StartDesktopZOrderWatchdog()
        {
            _zOrderTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(75)
            };
            _zOrderTimer.Tick += (s, e) => UpdateWindowsZOrder();
            _zOrderTimer.Start();
        }

        private void UpdateWindowsZOrder()
        {
            try
            {
                if (_settingsWindow != null && _settingsWindow.IsVisible)
                    return;

                HashSet<IntPtr> ourHwnds = new HashSet<IntPtr>();
                if (_canvaOverlay != null)
                {
                    IntPtr overlayHwnd = new WindowInteropHelper(_canvaOverlay).Handle;
                    if (overlayHwnd != IntPtr.Zero) ourHwnds.Add(overlayHwnd);
                }

                foreach (FolderWidgetWindow w in _windows)
                {
                    if (w.Hwnd != IntPtr.Zero)
                    {
                        ourHwnds.Add(w.Hwnd);
                    }
                }

                IntPtr fg = DesktopNativeHelper.GetForegroundWindow();
                bool fgChanged = (fg != _lastForegroundHwnd);
                _lastForegroundHwnd = fg;

                string fgCls = DesktopNativeHelper.GetWindowClassName(fg);
                bool isDesktopFg = DesktopNativeHelper.IsDesktopClass(fgCls) ||
                                   fgCls == "Shell_TrayWnd" ||
                                   fgCls == "Shell_SecondaryTrayWnd" ||
                                   ourHwnds.Contains(fg);

                foreach (FolderWidgetWindow w in _windows)
                {
                    if (w.IsExpanded || w.IsDraggingOrDialog)
                    {
                        w.AssertOnTopOfDesktop(fgChanged);
                        return;
                    }
                }

                foreach (FolderWidgetWindow win in _windows)
                {
                    if (win.Hwnd == IntPtr.Zero) continue;

                    if (isDesktopFg)
                    {
                        win.AssertOnTopOfDesktop(fgChanged);
                        continue;
                    }

                    DesktopNativeHelper.RECT r;
                    if (!DesktopNativeHelper.GetWindowRect(win.Hwnd, out r)) continue;

                    int cx = (r.Left + r.Right) / 2;

                    IntPtr covTop;
                    IntPtr covBottom;
                    int stateTop = DesktopNativeHelper.CheckPointVisibilityState(cx, r.Top - 12, ourHwnds, out covTop);
                    int stateBottom = DesktopNativeHelper.CheckPointVisibilityState(cx, r.Bottom + 12, ourHwnds, out covBottom);

                    if (stateTop == 1 || stateBottom == 1)
                    {
                        win.AssertOnTopOfDesktop(fgChanged);
                    }
                    else if (stateTop == -1 && stateBottom == -1)
                    {
                        win.DropBehindNormalApps(covTop != IntPtr.Zero ? covTop : covBottom);
                    }
                }
            }
            catch { }
        }

        private void SpawnAllFolderWindows()
        {
            foreach (FolderWidgetWindow win in _windows)
            {
                try { win.Close(); } catch { }
            }
            _windows.Clear();

            foreach (FolderData folder in Config.Folders)
            {
                SpawnFolderWindow(folder);
            }
        }

        private FolderWidgetWindow SpawnFolderWindow(FolderData folder)
        {
            FolderWidgetWindow win = new FolderWidgetWindow(folder, this);
            _windows.Add(win);
            win.Show();
            return win;
        }

        public void NotifyFolderExpanding(FolderWidgetWindow expandingWindow)
        {
            foreach (FolderWidgetWindow win in _windows)
            {
                if (win != expandingWindow)
                {
                    win.Topmost = false;
                    win.CollapseFolder();
                }
            }
            expandingWindow.Topmost = true;
            expandingWindow.Topmost = false;
        }

        public void CreateNewFolderNear(FolderData referenceFolder)
        {
            double newX = SystemParameters.PrimaryScreenWidth / 2.0;
            double newY = SystemParameters.PrimaryScreenHeight / 2.0;

            if (referenceFolder != null)
            {
                newX = Math.Min(SystemParameters.PrimaryScreenWidth - 120, referenceFolder.X + 115);
                newY = referenceFolder.Y;
            }

            FolderData folder = new FolderData
            {
                Title = "New Folder",
                X = newX,
                Y = newY
            };

            Config.Folders.Add(folder);
            SaveConfig();

            FolderWidgetWindow win = SpawnFolderWindow(folder);
            win.PromptRenameFolder();
        }

        public void DeleteFolder(FolderData folder)
        {
            if (folder == null) return;

            if (Config.Folders.Count <= 1)
            {
                MessageBox.Show(
                    "Minimal harus ada 1 folder di desktop.",
                    "HoverFold",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult res = MessageBox.Show(
                "Hapus folder '" + folder.Title + "' dari tampilan desktop?\n(Shortcut asli aplikasi kamu tetap aman)",
                "Hapus Folder",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            Config.Folders.Remove(folder);
            FolderWidgetWindow targetWin = _windows.Find(w => w.Folder.Id == folder.Id);
            if (targetWin != null)
            {
                _windows.Remove(targetWin);
                targetWin.Close();
            }
            SaveConfig();
        }

        public void MoveAppBetweenFolders(string appId, string sourceFolderId, string targetFolderId)
        {
            if (sourceFolderId == targetFolderId) return;

            FolderData source = Config.Folders.Find(f => f.Id == sourceFolderId);
            FolderData target = Config.Folders.Find(f => f.Id == targetFolderId);
            if (source == null || target == null) return;

            AppItemData item = source.Apps.Find(a => a.Id == appId);
            if (item == null) return;

            source.Apps.Remove(item);
            target.Apps.Add(item);
            SaveConfig();

            foreach (FolderWidgetWindow win in _windows)
            {
                if (win.Folder.Id == sourceFolderId || win.Folder.Id == targetFolderId)
                {
                    win.RefreshFolderContent();
                }
            }
        }

        public void RescanDesktopShortcuts()
        {
            MessageBoxResult res = MessageBox.Show(
                "Scan ulang shortcut di Desktop dan susun kembali ke folder kategori otomatis?",
                "Auto-Scan Desktop",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res != MessageBoxResult.Yes) return;

            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;
            AppConfig scanned = ConfigManager.ScanDesktopAndBuildDefault(screenW, screenH);
            scanned.ThemePreset = Config.ThemePreset;
            scanned.CardOpacity = Config.CardOpacity;
            scanned.GridColumns = Config.GridColumns;
            scanned.IdleTileScale = Config.IdleTileScale;
            scanned.ShowCountBadge = Config.ShowCountBadge;

            Config = scanned;
            IconExtractor.ClearCache();
            SaveConfig();
            SpawnAllFolderWindows();
        }

        public void CenterAllFoldersOnDesktop()
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;
            int count = Config.Folders.Count;
            double spacing = 115;
            double totalWidth = (count - 1) * spacing;
            double startX = (screenW / 2.0) - (totalWidth / 2.0);
            double startY = (screenH / 2.0) - 80;

            for (int i = 0; i < count; i++)
            {
                Config.Folders[i].X = startX + (i * spacing);
                Config.Folders[i].Y = startY;
            }
            SaveConfig();
            SpawnAllFolderWindows();
        }

        public void SaveConfig()
        {
            ConfigManager.Save(Config);
        }

        private void SetupSystemTray()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = CreateMonochromeTrayIcon(),
                Text = "HoverFold — Klik kiri untuk Pengaturan, Klik kanan untuk Menu",
                Visible = true
            };

            _trayIcon.MouseClick += (s, e) =>
            {
                if (e.Button == Forms.MouseButtons.Left)
                {
                    OpenSettingsWindow();
                }
            };

            Forms.ContextMenuStrip trayMenu = new Forms.ContextMenuStrip();
            trayMenu.Items.Add("⚙ Buka Pengaturan (Settings & Tema)", null, (s, e) => OpenSettingsWindow());
            trayMenu.Items.Add(new Forms.ToolStripSeparator());
            trayMenu.Items.Add("＋ Buat Folder 1x1 Baru di Desktop", null, (s, e) => CreateNewFolderNear(null));
            trayMenu.Items.Add("⌖ Rapikan Semua Folder ke Tengah Desktop", null, (s, e) => CenterAllFoldersOnDesktop());
            trayMenu.Items.Add("👁 Sembunyikan / Tampilkan Icon Desktop Bawaan", null, (s, e) => DesktopNativeHelper.ToggleNativeDesktopIcons());
            trayMenu.Items.Add("🔄 Scan Ulang Shortcut Desktop", null, (s, e) => RescanDesktopShortcuts());
            trayMenu.Items.Add(new Forms.ToolStripSeparator());
            trayMenu.Items.Add("✖ Matikan / Keluar dari Aplikasi", null, (s, e) =>
            {
                CleanupTray();
                Application.Current.Shutdown();
            });

            _trayIcon.ContextMenuStrip = trayMenu;
        }

        public void CleanupTray()
        {
            if (_canvaOverlay != null)
            {
                try { _canvaOverlay.Close(); } catch { }
                _canvaOverlay = null;
            }
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }
        }

        private Icon CreateMonochromeTrayIcon()
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(DrawingColor.Transparent);

                using (SolidBrush bg = new SolidBrush(DrawingColor.FromArgb(245, 16, 16, 20)))
                using (System.Drawing.Pen border = new System.Drawing.Pen(DrawingColor.FromArgb(200, 230, 230, 240), 2f))
                using (SolidBrush dot = new SolidBrush(DrawingColor.FromArgb(240, 240, 240, 248)))
                {
                    g.FillEllipse(bg, 2, 2, 28, 28);
                    g.DrawEllipse(border, 2, 2, 28, 28);

                    g.FillRectangle(dot, 9, 9, 5, 5);
                    g.FillRectangle(dot, 18, 9, 5, 5);
                    g.FillRectangle(dot, 9, 18, 5, 5);
                    g.FillRectangle(dot, 18, 18, 5, 5);
                }

                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }

    public static class AppProgram
    {
        private static Mutex _mutex;

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                int currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                System.Diagnostics.Process[] existing = System.Diagnostics.Process.GetProcessesByName("AuraFolders");
                foreach (System.Diagnostics.Process p in existing)
                {
                    if (p.Id != currentPid)
                    {
                        try { p.Kill(); p.WaitForExit(1500); } catch { }
                    }
                }
            }
            catch { }

            if (args != null && args.Length > 0 && args[0] == "--stop")
            {
                return;
            }

            bool createdNew;
            _mutex = new Mutex(true, "AuraFolders_SingleInstance_Mutex_V2_" + Environment.UserName, out createdNew);

            Application app = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            FolderManagerController controller = new FolderManagerController();
            app.Startup += (s, e) => controller.Initialize();
            app.Exit += (s, e) => controller.CleanupTray();

            app.Run();
        }
    }
}
