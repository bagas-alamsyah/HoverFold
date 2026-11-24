using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AuraFolders
{
    public struct ThemePalette
    {
        public Color TopGrad;
        public Color BottomGrad;
        public Color BorderColor;
        public Color AccentGlow;
        public Color TileWellBg;
        public Color TileHoverBg;
        public Color TileHoverBorder;

        public static ThemePalette Get(string preset, double opacity)
        {
            byte alphaMain = (byte)Math.Max(110, Math.Min(252, (int)Math.Round(opacity * 255)));
            byte alphaTop = (byte)Math.Min(255, alphaMain + 6);

            if (string.Equals(preset, "Frosted", StringComparison.OrdinalIgnoreCase))
            {
                return new ThemePalette
                {
                    TopGrad = Color.FromArgb(alphaTop, 34, 35, 44),
                    BottomGrad = Color.FromArgb(alphaMain, 18, 19, 25),
                    BorderColor = Color.FromArgb(115, 255, 255, 255),
                    AccentGlow = Color.FromArgb(180, 240, 244, 255),
                    TileWellBg = Color.FromArgb(24, 255, 255, 255),
                    TileHoverBg = Color.FromArgb(46, 255, 255, 255),
                    TileHoverBorder = Color.FromArgb(95, 255, 255, 255)
                };
            }
            if (string.Equals(preset, "Campfire", StringComparison.OrdinalIgnoreCase))
            {
                return new ThemePalette
                {
                    TopGrad = Color.FromArgb(alphaTop, 32, 21, 28),
                    BottomGrad = Color.FromArgb(alphaMain, 14, 10, 15),
                    BorderColor = Color.FromArgb(110, 255, 190, 145),
                    AccentGlow = Color.FromArgb(210, 255, 165, 105),
                    TileWellBg = Color.FromArgb(22, 255, 200, 170),
                    TileHoverBg = Color.FromArgb(45, 255, 175, 120),
                    TileHoverBorder = Color.FromArgb(105, 255, 185, 135)
                };
            }
            if (string.Equals(preset, "Midnight", StringComparison.OrdinalIgnoreCase))
            {
                return new ThemePalette
                {
                    TopGrad = Color.FromArgb(alphaTop, 22, 28, 42),
                    BottomGrad = Color.FromArgb(alphaMain, 10, 13, 21),
                    BorderColor = Color.FromArgb(105, 175, 210, 255),
                    AccentGlow = Color.FromArgb(200, 140, 190, 255),
                    TileWellBg = Color.FromArgb(22, 190, 220, 255),
                    TileHoverBg = Color.FromArgb(44, 160, 205, 255),
                    TileHoverBorder = Color.FromArgb(100, 175, 215, 255)
                };
            }

            return new ThemePalette
            {
                TopGrad = Color.FromArgb(alphaTop, 24, 24, 31),
                BottomGrad = Color.FromArgb(alphaMain, 10, 10, 14),
                BorderColor = Color.FromArgb(92, 255, 255, 255),
                AccentGlow = Color.FromArgb(210, 245, 245, 250),
                TileWellBg = Color.FromArgb(18, 255, 255, 255),
                TileHoverBg = Color.FromArgb(38, 255, 255, 255),
                TileHoverBorder = Color.FromArgb(82, 255, 255, 255)
            };
        }
    }

    public class FolderWidgetWindow : Window
    {
        private readonly FolderData _folder;
        private readonly FolderManagerController _controller;

        private Grid _rootGrid;
        private Border _idleContainer;
        private ScaleTransform _idleScaleTransform;
        private Border _idleFolderBox;
        private Border _idleCountBadge;
        private Grid _miniIconGrid;
        private TextBlock _idleTitleText;
        private TextBlock _idleCountText;

        private Border _expandedCard;
        private ScaleTransform _expandedScale;
        private TextBlock _expandedTitleText;
        private TextBlock _expandedCountText;
        private ScrollViewer _appsScrollViewer;
        private WrapPanel _appsWrapPanel;

        private DispatcherTimer _hoverOpenTimer;
        private DispatcherTimer _hoverCloseTimer;
        private DispatcherTimer _holdGrabTimer;
        private bool _isExpanded = false;
        private bool _isPointerDown = false;
        private bool _isDraggingWindow = false;
        private bool _startedDragFromExpanded = false;
        private Point _pointerDownScreenDip;
        private double _dragStartFolderX;
        private double _dragStartFolderY;
        private UIElement _capturedDragElement;
        private Point _appDragStartPoint;
        private bool _isDialogOpen = false;
        private bool _currentlyTopmost = false;

        public const double CanvasWidth = 370;
        public const double CanvasHeight = 390;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const uint SWP_HIDEWINDOW = 0x0080;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        public FolderData Folder { get { return _folder; } }
        public bool IsExpanded { get { return _isExpanded; } }
        public bool IsDraggingOrDialog { get { return _isDraggingWindow || _isPointerDown || _isDialogOpen; } }
        public IntPtr Hwnd { get; private set; }

        public FolderWidgetWindow(FolderData folder, FolderManagerController controller)
        {
            _folder = folder;
            _controller = controller;

            Title = "AuraFolder - " + _folder.Title;
            Width = CanvasWidth;
            Height = CanvasHeight;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Topmost = false;
            AllowDrop = true;

            Left = _folder.X - (CanvasWidth / 2.0);
            Top = _folder.Y - (CanvasHeight / 2.0);

            int hoverMs = _controller.Config != null ? _controller.Config.HoverDelayMs : 80;
            _hoverOpenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(hoverMs) };
            _hoverOpenTimer.Tick += (s, e) =>
            {
                _hoverOpenTimer.Stop();
                if (!_isDraggingWindow && !_isPointerDown)
                {
                    ExpandFolder();
                }
            };

            _hoverCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
            _hoverCloseTimer.Tick += (s, e) =>
            {
                _hoverCloseTimer.Stop();
                if (_isDialogOpen || _isDraggingWindow || _isPointerDown) return;
                if (!_expandedCard.IsMouseOver && !_idleContainer.IsMouseOver)
                {
                    CollapseFolder();
                }
            };

            // Hold timer: holding mouse down for 150ms immediately activates semi-transparent Pick-Up Mode
            _holdGrabTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _holdGrabTimer.Tick += (s, e) =>
            {
                _holdGrabTimer.Stop();
                if (_isPointerDown && !_isDraggingWindow)
                {
                    EngageDragPickUpMode();
                }
            };

            BuildUI();
            ApplyThemeAndSettings();
            RefreshFolderContent();

            Loaded += OnWindowLoaded;
            Drop += OnWindowDrop;
            DragOver += OnWindowDragOver;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(Hwnd, GWL_EXSTYLE);
            SetWindowLong(Hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

            HwndSource src = HwndSource.FromHwnd(Hwnd);
            if (src != null)
            {
                src.AddHook(WndProc);
            }

            DesktopNativeHelper.ShowWindow(Hwnd, DesktopNativeHelper.SW_SHOWNOACTIVATE);
            AssertOnTopOfDesktop(true);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SYSCOMMAND)
            {
                int cmd = wParam.ToInt32() & 0xFFF0;
                if (cmd == SC_MINIMIZE)
                {
                    handled = true;
                    return IntPtr.Zero;
                }
            }
            else if (msg == WM_WINDOWPOSCHANGING && lParam != IntPtr.Zero)
            {
                WINDOWPOS wp = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                if ((wp.flags & SWP_HIDEWINDOW) != 0)
                {
                    wp.flags &= ~SWP_HIDEWINDOW;
                    Marshal.StructureToPtr(wp, lParam, false);
                }
            }
            return IntPtr.Zero;
        }

        public void AssertOnTopOfDesktop(bool forceReband)
        {
            if (Hwnd == IntPtr.Zero) return;
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            if (!DesktopNativeHelper.IsWindowVisible(Hwnd))
            {
                DesktopNativeHelper.ShowWindow(Hwnd, DesktopNativeHelper.SW_SHOWNOACTIVATE);
            }

            if (forceReband || !_currentlyTopmost)
            {
                DesktopNativeHelper.SetWindowPos(
                    Hwnd,
                    DesktopNativeHelper.HWND_NOTOPMOST,
                    0, 0, 0, 0,
                    DesktopNativeHelper.SWP_NOMOVE | DesktopNativeHelper.SWP_NOSIZE | DesktopNativeHelper.SWP_NOACTIVATE);

                DesktopNativeHelper.SetWindowPos(
                    Hwnd,
                    DesktopNativeHelper.HWND_TOPMOST,
                    0, 0, 0, 0,
                    DesktopNativeHelper.SWP_NOMOVE | DesktopNativeHelper.SWP_NOSIZE | DesktopNativeHelper.SWP_NOACTIVATE | DesktopNativeHelper.SWP_SHOWWINDOW);

                _currentlyTopmost = true;
            }
        }

        public void DropBehindNormalApps(IntPtr coveringAppHwnd)
        {
            if (Hwnd == IntPtr.Zero) return;
            if (_currentlyTopmost)
            {
                DesktopNativeHelper.SetWindowPos(
                    Hwnd,
                    DesktopNativeHelper.HWND_NOTOPMOST,
                    0, 0, 0, 0,
                    DesktopNativeHelper.SWP_NOMOVE | DesktopNativeHelper.SWP_NOSIZE | DesktopNativeHelper.SWP_NOACTIVATE);
                _currentlyTopmost = false;
            }

            if (coveringAppHwnd != IntPtr.Zero)
            {
                DesktopNativeHelper.SetWindowPos(
                    Hwnd,
                    coveringAppHwnd,
                    0, 0, 0, 0,
                    DesktopNativeHelper.SWP_NOMOVE | DesktopNativeHelper.SWP_NOSIZE | DesktopNativeHelper.SWP_NOACTIVATE);
            }
        }

        private void BuildUI()
        {
            _rootGrid = new Grid
            {
                Width = CanvasWidth,
                Height = CanvasHeight,
                Background = Brushes.Transparent
            };

            BuildIdleTile();
            BuildExpandedCard();

            _rootGrid.Children.Add(_idleContainer);
            _rootGrid.Children.Add(_expandedCard);

            Content = _rootGrid;
        }

        private void BuildIdleTile()
        {
            _idleScaleTransform = new ScaleTransform(1.0, 1.0);

            _idleContainer = new Border
            {
                Width = 104,
                Height = 120,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _idleScaleTransform,
                Cursor = Cursors.Hand
            };

            StackPanel stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid boxWrapper = new Grid
            {
                Width = 84,
                Height = 82,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _idleFolderBox = new Border
            {
                Width = 76,
                Height = 76,
                CornerRadius = new CornerRadius(21),
                BorderThickness = new Thickness(1.2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 22,
                    ShadowDepth = 6,
                    Opacity = 0.85
                }
            };

            _miniIconGrid = new Grid();
            _miniIconGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _miniIconGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _miniIconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _miniIconGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _idleFolderBox.Child = _miniIconGrid;
            boxWrapper.Children.Add(_idleFolderBox);

            _idleCountBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 24, 24, 31)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(115, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(6, 1, 6, 1.5),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 1, 0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 2,
                    Opacity = 0.7
                }
            };

            _idleCountText = new TextBlock
            {
                Text = "0",
                Foreground = new SolidColorBrush(Color.FromArgb(240, 245, 245, 250)),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = 9.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _idleCountBadge.Child = _idleCountText;
            boxWrapper.Children.Add(_idleCountBadge);

            _idleTitleText = new TextBlock
            {
                Text = _folder.Title,
                Foreground = new SolidColorBrush(Color.FromArgb(248, 246, 246, 252)),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = 11.5,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 100,
                Margin = new Thickness(0, 6, 0, 0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 8,
                    ShadowDepth = 1.5,
                    Opacity = 0.95
                }
            };

            stack.Children.Add(boxWrapper);
            stack.Children.Add(_idleTitleText);
            _idleContainer.Child = stack;

            _idleContainer.MouseEnter += (s, e) =>
            {
                _hoverCloseTimer.Stop();
                if (!_isExpanded && !_isDraggingWindow && !_isPointerDown)
                {
                    _hoverOpenTimer.Start();
                }
            };

            _idleContainer.MouseLeave += (s, e) =>
            {
                _hoverOpenTimer.Stop();
                if (_isExpanded && !_isDraggingWindow && !_isPointerDown)
                {
                    _hoverCloseTimer.Start();
                }
            };

            _idleContainer.MouseLeftButtonDown += (s, e) => StartPointerHold( _idleContainer, e, false );
            _idleContainer.MouseMove += OnFolderMouseMove;
            _idleContainer.MouseLeftButtonUp += OnFolderMouseLeftButtonUp;
            _idleContainer.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenFolderContextMenu();
            };
        }

        private void BuildExpandedCard()
        {
            _expandedScale = new ScaleTransform(0.50, 0.50);

            _expandedCard = new Border
            {
                Width = 284,
                MaxHeight = 344,
                CornerRadius = new CornerRadius(24),
                BorderThickness = new Thickness(1.2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = _expandedScale,
                Opacity = 0,
                Visibility = Visibility.Collapsed,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 32,
                    ShadowDepth = 10,
                    Opacity = 0.88
                }
            };

            // Allowing hold-to-drag from anywhere on the expanded card background too!
            _expandedCard.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) return;
                StartPointerHold(_expandedCard, e, true);
            };
            _expandedCard.MouseMove += OnFolderMouseMove;
            _expandedCard.MouseLeftButtonUp += OnFolderMouseLeftButtonUp;

            Grid cardLayout = new Grid();
            cardLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
            cardLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1) });
            cardLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Border headerBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)),
                CornerRadius = new CornerRadius(24, 24, 0, 0),
                Padding = new Thickness(16, 0, 12, 0),
                Cursor = Cursors.SizeAll,
                ToolTip = "Tahan & geser untuk memindahkan folder (Canva Smart Snap) • Klik 2x judul untuk ganti nama"
            };
            headerBar.MouseLeftButtonDown += OnHeaderMouseLeftButtonDown;
            headerBar.MouseMove += OnFolderMouseMove;
            headerBar.MouseLeftButtonUp += OnFolderMouseLeftButtonUp;
            headerBar.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenFolderContextMenu();
            };

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _expandedTitleText = new TextBlock
            {
                Text = _folder.Title,
                Foreground = new SolidColorBrush(Color.FromArgb(252, 248, 248, 252)),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = 13.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 155
            };

            Border headerCountPill = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(6, 1, 6, 1.5),
                Margin = new Thickness(8, 1, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            _expandedCountText = new TextBlock
            {
                Text = "0",
                Foreground = new SolidColorBrush(Color.FromArgb(170, 225, 225, 235)),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = 10
            };
            headerCountPill.Child = _expandedCountText;

            titleStack.Children.Add(_expandedTitleText);
            titleStack.Children.Add(headerCountPill);

            StackPanel actionButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            Button addAppBtn = CreateIconPillButton("＋", "Tambah aplikasi / shortcut ke folder ini");
            addAppBtn.Click += (s, e) => PromptAddApplication();

            Button settingsBtn = CreateIconPillButton("⚙", "Pengaturan Tampilan & Tema (Settings)");
            settingsBtn.Click += (s, e) => _controller.OpenSettingsWindow();

            Button menuBtn = CreateIconPillButton("⋯", "Menu Folder / Tutup Aplikasi");
            menuBtn.Click += (s, e) => OpenFolderContextMenu();

            actionButtons.Children.Add(addAppBtn);
            actionButtons.Children.Add(settingsBtn);
            actionButtons.Children.Add(menuBtn);

            Grid.SetColumn(titleStack, 0);
            Grid.SetColumn(actionButtons, 1);
            headerGrid.Children.Add(titleStack);
            headerGrid.Children.Add(actionButtons);
            headerBar.Child = headerGrid;

            Border divider = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Height = 1
            };

            _appsScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(12, 10, 12, 14),
                BorderThickness = new Thickness(0)
            };
            _appsScrollViewer.PreviewMouseWheel += (s, e) =>
            {
                _appsScrollViewer.ScrollToVerticalOffset(_appsScrollViewer.VerticalOffset - (e.Delta / 2.5));
                e.Handled = true;
            };

            _appsWrapPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _appsScrollViewer.Content = _appsWrapPanel;

            Grid.SetRow(headerBar, 0);
            Grid.SetRow(divider, 1);
            Grid.SetRow(_appsScrollViewer, 2);

            cardLayout.Children.Add(headerBar);
            cardLayout.Children.Add(divider);
            cardLayout.Children.Add(_appsScrollViewer);

            _expandedCard.Child = cardLayout;

            _expandedCard.MouseEnter += (s, e) =>
            {
                _hoverCloseTimer.Stop();
            };

            _expandedCard.MouseLeave += (s, e) =>
            {
                if (!_isDraggingWindow && !_isPointerDown && !_isDialogOpen)
                {
                    _hoverCloseTimer.Start();
                }
            };
        }

        private Button CreateIconPillButton(string iconText, string tooltip)
        {
            Button btn = new Button
            {
                Content = iconText,
                ToolTip = tooltip,
                Cursor = Cursors.Hand,
                Width = 26,
                Height = 26,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 240, 240, 248)),
                Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = 11.5,
                Margin = new Thickness(4, 0, 0, 0)
            };

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "btnBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);

            template.VisualTree = border;

            Trigger hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)), "btnBorder"));
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), "btnBorder"));
            hoverTrigger.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            template.Triggers.Add(hoverTrigger);

            btn.Template = template;
            return btn;
        }

        public void ApplyThemeAndSettings()
        {
            AppConfig cfg = _controller.Config ?? new AppConfig();
            cfg.EnsureDefaults();

            ThemePalette pal = ThemePalette.Get(cfg.ThemePreset, cfg.CardOpacity);

            LinearGradientBrush glassBg = new LinearGradientBrush(
                pal.TopGrad,
                pal.BottomGrad,
                new Point(0, 0),
                new Point(1, 1));

            SolidColorBrush borderBrush = new SolidColorBrush(pal.BorderColor);

            _idleFolderBox.Background = glassBg;
            _idleFolderBox.BorderBrush = borderBrush;

            _expandedCard.Background = glassBg;
            _expandedCard.BorderBrush = borderBrush;

            _idleScaleTransform.ScaleX = cfg.IdleTileScale;
            _idleScaleTransform.ScaleY = cfg.IdleTileScale;
            _idleCountBadge.Visibility = cfg.ShowCountBadge ? Visibility.Visible : Visibility.Collapsed;

            int cols = cfg.GridColumns;
            if (cols == 4)
            {
                _expandedCard.Width = 342;
                _expandedCard.MaxHeight = 352;
            }
            else
            {
                _expandedCard.Width = 282;
                _expandedCard.MaxHeight = 338;
            }

            _hoverOpenTimer.Interval = TimeSpan.FromMilliseconds(cfg.HoverDelayMs);
        }

        public void RefreshFolderContent()
        {
            ApplyThemeAndSettings();

            AppConfig cfg = _controller.Config ?? new AppConfig();
            ThemePalette pal = ThemePalette.Get(cfg.ThemePreset, cfg.CardOpacity);

            _idleTitleText.Text = _folder.Title;
            _expandedTitleText.Text = _folder.Title;

            int count = _folder.Apps != null ? _folder.Apps.Count : 0;
            _idleCountText.Text = count.ToString();
            _expandedCountText.Text = count.ToString();

            _miniIconGrid.Children.Clear();
            for (int i = 0; i < 4; i++)
            {
                int row = i / 2;
                int col = i % 2;

                Border cell = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(7.5),
                    Background = new SolidColorBrush(pal.TileWellBg),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                if (_folder.Apps != null && i < _folder.Apps.Count)
                {
                    AppItemData app = _folder.Apps[i];
                    ImageSource icon = IconExtractor.GetIconForApp(app);
                    if (icon != null)
                    {
                        Image img = new Image
                        {
                            Source = icon,
                            Width = 21,
                            Height = 21,
                            Stretch = Stretch.Uniform
                        };
                        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                        cell.Child = img;
                    }
                    else
                    {
                        cell.Child = CreateMonogramFallback(app.Name, 10.5);
                    }
                }

                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, col);
                _miniIconGrid.Children.Add(cell);
            }

            _appsWrapPanel.Children.Clear();
            if (_folder.Apps == null || _folder.Apps.Count == 0)
            {
                Border emptyBox = new Border
                {
                    Width = 240,
                    Height = 150,
                    Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                StackPanel emptyStack = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                TextBlock emptyTitle = new TextBlock
                {
                    Text = "Folder Kosong",
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 230, 230, 240)),
                    FontFamily = new FontFamily("Segoe UI Semibold"),
                    FontSize = 12.5,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                TextBlock emptySub = new TextBlock
                {
                    Text = "Klik tombol '＋' di kanan atas atau drag shortcut ke sini",
                    Foreground = new SolidColorBrush(Color.FromArgb(115, 195, 195, 208)),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10.5,
                    Margin = new Thickness(0, 5, 0, 0),
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 210
                };

                emptyStack.Children.Add(emptyTitle);
                emptyStack.Children.Add(emptySub);
                emptyBox.Child = emptyStack;
                _appsWrapPanel.Children.Add(emptyBox);
                return;
            }

            int cols = cfg.GridColumns == 4 ? 4 : 3;
            double tileWidth = cols == 4 ? 78 : 84;

            foreach (AppItemData app in _folder.Apps)
            {
                UIElement tile = CreateAppTileElement(app, tileWidth, pal);
                _appsWrapPanel.Children.Add(tile);
            }
        }

        private UIElement CreateAppTileElement(AppItemData app, double tileWidth, ThemePalette pal)
        {
            ScaleTransform tileScale = new ScaleTransform(1.0, 1.0);

            Border tileBorder = new Border
            {
                Width = tileWidth,
                Height = 84,
                CornerRadius = new CornerRadius(15),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 7, 4, 5),
                Cursor = Cursors.Hand,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = tileScale,
                ToolTip = app.Name
            };

            StackPanel stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Border iconHolder = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(pal.TileWellBg),
                BorderBrush = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            ImageSource icon = IconExtractor.GetIconForApp(app);
            if (icon != null)
            {
                Image img = new Image
                {
                    Source = icon,
                    Width = 30,
                    Height = 30,
                    Stretch = Stretch.Uniform
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                iconHolder.Child = img;
            }
            else
            {
                iconHolder.Child = CreateMonogramFallback(app.Name, 15);
            }

            TextBlock nameText = new TextBlock
            {
                Text = app.Name,
                Foreground = new SolidColorBrush(Color.FromArgb(232, 238, 238, 245)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10.5,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = tileWidth - 10,
                Margin = new Thickness(0, 6, 0, 0)
            };

            stack.Children.Add(iconHolder);
            stack.Children.Add(nameText);
            tileBorder.Child = stack;

            tileBorder.MouseEnter += (s, e) =>
            {
                tileBorder.Background = new SolidColorBrush(pal.TileHoverBg);
                tileBorder.BorderBrush = new SolidColorBrush(pal.TileHoverBorder);
                DoubleAnimation anim = new DoubleAnimation(1.06, TimeSpan.FromMilliseconds(110));
                tileScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                tileScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };

            tileBorder.MouseLeave += (s, e) =>
            {
                tileBorder.Background = Brushes.Transparent;
                tileBorder.BorderBrush = Brushes.Transparent;
                DoubleAnimation anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(110));
                tileScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                tileScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };

            tileBorder.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                _appDragStartPoint = e.GetPosition(this);
            };

            tileBorder.MouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    Point current = e.GetPosition(this);
                    if (Math.Abs(current.X - _appDragStartPoint.X) > 10 || Math.Abs(current.Y - _appDragStartPoint.Y) > 10)
                    {
                        DataObject data = new DataObject("AuraFolderAppItem", app.Id + "|" + _folder.Id);
                        DragDrop.DoDragDrop(tileBorder, data, DragDropEffects.Move);
                    }
                }
            };

            tileBorder.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                LaunchApp(app);
            };

            tileBorder.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                OpenAppItemContextMenu(app);
            };

            return tileBorder;
        }

        private UIElement CreateMonogramFallback(string name, double fontSize)
        {
            string letter = "?";
            if (!string.IsNullOrWhiteSpace(name))
            {
                letter = name.Trim().Substring(0, 1).ToUpperInvariant();
            }
            return new TextBlock
            {
                Text = letter,
                Foreground = new SolidColorBrush(Color.FromArgb(225, 235, 235, 245)),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = fontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        public void ExpandFolder()
        {
            if (_isExpanded || _isDraggingWindow) return;
            _isExpanded = true;

            _controller.NotifyFolderExpanding(this);
            Activate();

            _expandedCard.Visibility = Visibility.Visible;
            _idleContainer.BeginAnimation(UIElement.OpacityProperty, null);
            _idleContainer.Opacity = 0;
            _idleContainer.IsHitTestVisible = false;

            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            DoubleAnimation scaleAnim = new DoubleAnimation(0.50, 1.0, TimeSpan.FromMilliseconds(175))
            {
                EasingFunction = ease
            };
            DoubleAnimation fadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(140));

            _expandedScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _expandedScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            _expandedCard.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        public void CollapseFolder()
        {
            if (!_isExpanded) return;
            _isExpanded = false;

            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseIn };
            DoubleAnimation scaleAnim = new DoubleAnimation(1.0, 0.50, TimeSpan.FromMilliseconds(135))
            {
                EasingFunction = ease
            };
            DoubleAnimation fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(120));

            fadeAnim.Completed += (s, e) =>
            {
                if (!_isExpanded)
                {
                    _expandedCard.Visibility = Visibility.Collapsed;
                }
            };

            _expandedScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _expandedScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            _expandedCard.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            _idleContainer.IsHitTestVisible = true;
            DoubleAnimation idleFade = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(135));
            _idleContainer.BeginAnimation(UIElement.OpacityProperty, idleFade);
        }

        private void LaunchApp(AppItemData app)
        {
            if (app == null || string.IsNullOrEmpty(app.TargetPath)) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(app.TargetPath)
                {
                    UseShellExecute = true
                };
                Process.Start(psi);
                CollapseFolder();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal membuka aplikasi:\n" + ex.Message, "AuraFolders", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Convert physical screen point to exact WPF Device-Independent Pixels (1:1 cursor tracking at any DPI!)
        private Point GetCursorScreenDip(MouseEventArgs e)
        {
            Point physPt = PointToScreen(e.GetPosition(this));
            PresentationSource src = PresentationSource.FromVisual(this);
            if (src != null && src.CompositionTarget != null)
            {
                return src.CompositionTarget.TransformFromDevice.Transform(physPt);
            }
            return physPt;
        }

        // Hold-to-Drag Pick-Up Mode (Semi-transparent 1x1 Folder + Canva Alignment Guides)
        private void StartPointerHold(UIElement captureElement, MouseButtonEventArgs e, bool fromExpanded)
        {
            _hoverOpenTimer.Stop();
            _hoverCloseTimer.Stop();

            _isPointerDown = true;
            _isDraggingWindow = false;
            _startedDragFromExpanded = fromExpanded;
            _capturedDragElement = captureElement;
            _pointerDownScreenDip = GetCursorScreenDip(e);
            _dragStartFolderX = _folder.X;
            _dragStartFolderY = _folder.Y;

            captureElement.CaptureMouse();
            _holdGrabTimer.Start();
            e.Handled = true;
        }

        private void OnHeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _holdGrabTimer.Stop();
                _isPointerDown = false;
                e.Handled = true;
                PromptRenameFolder();
                return;
            }
            StartPointerHold((UIElement)sender, e, true);
        }

        private void EngageDragPickUpMode()
        {
            if (_isDraggingWindow) return;
            _isDraggingWindow = true;
            _hoverOpenTimer.Stop();
            _hoverCloseTimer.Stop();

            // If we started holding while the folder was expanded, collapse it into the compact 1x1 folder icon
            // and center it smoothly under the cursor so you can see exactly where the 1x1 folder lines up!
            if (_isExpanded)
            {
                _isExpanded = false;
                _expandedCard.BeginAnimation(UIElement.OpacityProperty, null);
                _expandedCard.Opacity = 0;
                _expandedCard.Visibility = Visibility.Collapsed;
                _idleContainer.IsHitTestVisible = true;
            }

            if (_startedDragFromExpanded)
            {
                // Snap the 1x1 folder center to the initial grab point so it feels picked up right under the cursor
                _dragStartFolderX = _pointerDownScreenDip.X;
                _dragStartFolderY = _pointerDownScreenDip.Y;
                _folder.X = _dragStartFolderX;
                _folder.Y = _dragStartFolderY;
                Left = _folder.X - (CanvasWidth / 2.0);
                Top = _folder.Y - (CanvasHeight / 2.0);
            }

            // Visual "Picked Up & Semi-Transparent" feedback (like iOS/Tab folder drag)
            double baseScale = _controller.Config != null ? _controller.Config.IdleTileScale : 1.0;
            _idleContainer.BeginAnimation(UIElement.OpacityProperty, null);
            _idleContainer.Opacity = 0.62;
            _idleScaleTransform.ScaleX = baseScale * 1.08;
            _idleScaleTransform.ScaleY = baseScale * 1.08;
            _idleFolderBox.BorderBrush = new SolidColorBrush(Color.FromArgb(230, 56, 189, 248)); // Cyan/Canva active drag rim

            double initSnapX;
            double initSnapY;
            _controller.UpdateCanvaAlignmentGuides(_folder, _folder.X, _folder.Y, out initSnapX, out initSnapY);
            _folder.X = initSnapX;
            _folder.Y = initSnapY;
            Left = _folder.X - (CanvasWidth / 2.0);
            Top = _folder.Y - (CanvasHeight / 2.0);
        }

        private void OnFolderMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPointerDown) return;

            Point currentDip = GetCursorScreenDip(e);
            double dx = currentDip.X - _pointerDownScreenDip.X;
            double dy = currentDip.Y - _pointerDownScreenDip.Y;
            double dist = Math.Sqrt((dx * dx) + (dy * dy));

            if (!_isDraggingWindow)
            {
                if (dist > 4.0)
                {
                    _holdGrabTimer.Stop();
                    EngageDragPickUpMode();
                }
                else
                {
                    return;
                }
            }

            double rawTargetX = _dragStartFolderX + dx;
            double rawTargetY = _dragStartFolderY + dy;

            double snappedX;
            double snappedY;
            _controller.UpdateCanvaAlignmentGuides(_folder, rawTargetX, rawTargetY, out snappedX, out snappedY);

            _folder.X = snappedX;
            _folder.Y = snappedY;

            Left = _folder.X - (CanvasWidth / 2.0);
            Top = _folder.Y - (CanvasHeight / 2.0);
        }

        private void OnFolderMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPointerDown) return;

            _holdGrabTimer.Stop();
            bool wasDragging = _isDraggingWindow;
            _isPointerDown = false;
            _isDraggingWindow = false;

            if (_capturedDragElement != null)
            {
                _capturedDragElement.ReleaseMouseCapture();
                _capturedDragElement = null;
            }

            _controller.HideCanvaAlignmentGuides();

            // Restore normal opacity, scale, and border from Pick-Up Mode
            _idleContainer.BeginAnimation(UIElement.OpacityProperty, null);
            _idleContainer.Opacity = _isExpanded ? 0.0 : 1.0;
            ApplyThemeAndSettings();

            if (wasDragging)
            {
                _controller.SaveConfig();
            }
            else if (!_isExpanded)
            {
                ExpandFolder();
            }

            e.Handled = true;
        }

        private void OnWindowDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent("AuraFolderAppItem"))
            {
                e.Effects = DragDropEffects.Copy | DragDropEffects.Move;
                if (!_isExpanded)
                {
                    ExpandFolder();
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnWindowDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent("AuraFolderAppItem"))
                {
                    string payload = e.Data.GetData("AuraFolderAppItem") as string;
                    if (!string.IsNullOrEmpty(payload))
                    {
                        string[] parts = payload.Split('|');
                        if (parts.Length == 2)
                        {
                            _controller.MoveAppBetweenFolders(parts[0], parts[1], _folder.Id);
                        }
                    }
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
                    if (files != null && files.Length > 0)
                    {
                        foreach (string file in files)
                        {
                            AddAppFromFilePath(file);
                        }
                        RefreshFolderContent();
                        _controller.SaveConfig();
                    }
                }
            }
            catch { }
            e.Handled = true;
        }

        private void AddAppFromFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            if (!File.Exists(filePath) && !Directory.Exists(filePath)) return;

            string name = System.IO.Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(name)) name = System.IO.Path.GetFileName(filePath);

            AppItemData item = new AppItemData
            {
                Name = name,
                TargetPath = filePath
            };
            _folder.Apps.Add(item);
        }

        public void PromptAddApplication()
        {
            _isDialogOpen = true;
            try
            {
                OpenFileDialog dlg = new OpenFileDialog
                {
                    Title = "Pilih Aplikasi atau Shortcut untuk Folder '" + _folder.Title + "'",
                    Filter = "Aplikasi & Shortcut (*.lnk;*.exe;*.url)|*.lnk;*.exe;*.url|Semua File (*.*)|*.*",
                    Multiselect = true,
                    DereferenceLinks = false
                };

                string desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (Directory.Exists(desktopDir))
                {
                    dlg.InitialDirectory = desktopDir;
                }

                if (dlg.ShowDialog() == true)
                {
                    foreach (string file in dlg.FileNames)
                    {
                        AddAppFromFilePath(file);
                    }
                    RefreshFolderContent();
                    _controller.SaveConfig();
                }
            }
            finally
            {
                _isDialogOpen = false;
            }
        }

        public void PromptRenameFolder()
        {
            _isDialogOpen = true;
            try
            {
                string newName = MonochromeInputDialog.Show("Ganti Nama Folder", "Masukkan nama folder:", _folder.Title);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    _folder.Title = newName.Trim();
                    RefreshFolderContent();
                    _controller.SaveConfig();
                }
            }
            finally
            {
                _isDialogOpen = false;
            }
        }

        private void OpenFolderContextMenu()
        {
            _isDialogOpen = true;
            ContextMenu menu = CreateDarkContextMenu();

            MenuItem settingsItem = CreateMenuItem("⚙ Buka Pengaturan (Settings & Tema)", () => _controller.OpenSettingsWindow());
            MenuItem addAppItem = CreateMenuItem("＋ Tambah Aplikasi ke Folder Ini...", () => PromptAddApplication());
            MenuItem renameItem = CreateMenuItem("✎ Ganti Nama Folder...", () => PromptRenameFolder());
            MenuItem newFolderItem = CreateMenuItem("⊞ Buat Folder 1x1 Baru di Desktop", () => _controller.CreateNewFolderNear(_folder));

            bool nativeIconsVisible = DesktopNativeHelper.AreNativeDesktopIconsVisible();
            string toggleIconsLabel = nativeIconsVisible
                ? "👁 Sembunyikan Icon Desktop Bawaan Windows"
                : "👁 Tampilkan Kembali Icon Desktop Bawaan Windows";
            MenuItem toggleIconsItem = CreateMenuItem(toggleIconsLabel, () => DesktopNativeHelper.ToggleNativeDesktopIcons());

            MenuItem rescanItem = CreateMenuItem("🔄 Scan Ulang & Kelompokkan Otomatis Shortcut Desktop", () => _controller.RescanDesktopShortcuts());
            MenuItem deleteFolderItem = CreateMenuItem("🗑 Hapus Folder Ini", () => _controller.DeleteFolder(_folder));
            MenuItem exitAppItem = CreateMenuItem("✖ Tutup / Matikan AuraFolders (Close App)", () => Application.Current.Shutdown());

            menu.Items.Add(settingsItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(addAppItem);
            menu.Items.Add(renameItem);
            menu.Items.Add(newFolderItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(toggleIconsItem);
            menu.Items.Add(rescanItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(deleteFolderItem);
            menu.Items.Add(exitAppItem);

            menu.Closed += (s, e) =>
            {
                _isDialogOpen = false;
                _hoverCloseTimer.Start();
            };

            menu.IsOpen = true;
        }

        private void OpenAppItemContextMenu(AppItemData app)
        {
            _isDialogOpen = true;
            ContextMenu menu = CreateDarkContextMenu();

            MenuItem openItem = CreateMenuItem("▶ Buka " + app.Name, () => LaunchApp(app));
            MenuItem renameAppItem = CreateMenuItem("✎ Ganti Nama Tampilan...", () =>
            {
                string updated = MonochromeInputDialog.Show("Ganti Nama Aplikasi", "Nama tampilan:", app.Name);
                if (!string.IsNullOrWhiteSpace(updated))
                {
                    app.Name = updated.Trim();
                    RefreshFolderContent();
                    _controller.SaveConfig();
                }
            });

            MenuItem moveMenu = new MenuItem
            {
                Header = "➔ Pindahkan ke Folder Lain",
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(16, 16, 21))
            };

            foreach (FolderData targetFolder in _controller.Config.Folders)
            {
                if (targetFolder.Id == _folder.Id) continue;
                FolderData capturedTarget = targetFolder;
                MenuItem sub = CreateMenuItem("📁 " + capturedTarget.Title, () =>
                {
                    _controller.MoveAppBetweenFolders(app.Id, _folder.Id, capturedTarget.Id);
                });
                moveMenu.Items.Add(sub);
            }

            MenuItem removeAppItem = CreateMenuItem("🗑 Hapus dari Folder Ini", () =>
            {
                _folder.Apps.Remove(app);
                RefreshFolderContent();
                _controller.SaveConfig();
            });

            menu.Items.Add(openItem);
            menu.Items.Add(renameAppItem);
            if (moveMenu.Items.Count > 0)
            {
                menu.Items.Add(moveMenu);
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(removeAppItem);

            menu.Closed += (s, e) =>
            {
                _isDialogOpen = false;
            };

            menu.IsOpen = true;
        }

        private ContextMenu CreateDarkContextMenu()
        {
            return new ContextMenu
            {
                Background = new SolidColorBrush(Color.FromRgb(16, 16, 21)),
                Foreground = new SolidColorBrush(Color.FromRgb(242, 242, 247)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };
        }

        private MenuItem CreateMenuItem(string header, Action onClick)
        {
            MenuItem item = new MenuItem
            {
                Header = header,
                Foreground = new SolidColorBrush(Color.FromRgb(242, 242, 247)),
                Background = new SolidColorBrush(Color.FromRgb(16, 16, 21)),
                Padding = new Thickness(6, 4, 6, 4)
            };
            item.Click += (s, e) => onClick();
            return item;
        }
    }

    public static class MonochromeInputDialog
    {
        public static string Show(string title, string prompt, string defaultValue)
        {
            Window win = new Window
            {
                Title = title,
                Width = 340,
                Height = 165,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Topmost = true,
                ShowInTaskbar = false
            };

            Border card = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromArgb(250, 15, 15, 20)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(85, 255, 255, 255)),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(18),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 22,
                    Opacity = 0.85
                }
            };

            StackPanel stack = new StackPanel();

            TextBlock titleText = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = 13.5,
                Margin = new Thickness(0, 0, 0, 10)
            };

            TextBox inputBox = new TextBox
            {
                Text = defaultValue ?? "",
                Background = new SolidColorBrush(Color.FromRgb(25, 25, 33)),
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                Padding = new Thickness(8, 6, 8, 6)
            };

            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            string result = null;

            Button cancelBtn = new Button
            {
                Content = "Batal",
                Width = 72,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 40)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Cursor = Cursors.Hand
            };
            cancelBtn.Click += (s, e) => win.Close();

            Button okBtn = new Button
            {
                Content = "Simpan",
                Width = 78,
                Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 248)),
                Foreground = new SolidColorBrush(Color.FromRgb(12, 12, 16)),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsDefault = true
            };
            okBtn.Click += (s, e) =>
            {
                result = inputBox.Text;
                win.Close();
            };

            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(okBtn);

            stack.Children.Add(titleText);
            stack.Children.Add(inputBox);
            stack.Children.Add(btnRow);
            card.Child = stack;
            win.Content = card;

            win.Loaded += (s, e) =>
            {
                inputBox.Focus();
                inputBox.SelectAll();
            };

            win.ShowDialog();
            return result;
        }
    }
}
