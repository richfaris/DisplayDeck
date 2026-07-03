using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DisplayDeck.App.Services;
using DisplayDeck.App.ViewModels;
using DisplayDeck.App.Views;
using Wpf.Ui.Appearance;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace DisplayDeck.App;

public partial class MainWindow
{
    private readonly MainViewModel _viewModel = new();
    private HotkeyManager? _hotkey;
    private WinForms.NotifyIcon? _trayIcon;
    private bool _reallyExit;

    private DisplaysView? _displaysView;
    private ProfilesView? _profilesView;
    private ProfilesViewModel? _profilesVm;
    private GettingStartedView? _gettingStartedView;
    private RoadmapView? _roadmapView;
    private AboutView? _aboutView;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        ContentHost.Content = _displaysView ??= new DisplaysView();

        Log.Write("MainWindow constructed.");

        _hotkey = new HotkeyManager(this);
        _hotkey.Pressed += ToggleWindow;

        SetupTrayIcon();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var gesture = _hotkey?.ActiveGesture;
        Log.Write($"Window loaded. Active gesture = {gesture ?? "(none)"}");

        if (_trayIcon is not null)
        {
            _trayIcon.Text = gesture is null
                ? "DisplayDeck"
                : $"DisplayDeck — {gesture}";

            _trayIcon.BalloonTipTitle = "DisplayDeck is running";
            _trayIcon.BalloonTipText = gesture is null
                ? "No global hotkey available — click the tray icon to open."
                : $"Press {gesture} to open, or click the tray icon.";
            _trayIcon.ShowBalloonTip(4000);
        }
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = CreateAppIcon(),
            Text = "DisplayDeck — Ctrl+Alt+D",
            Visible = true,
        };

        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                ToggleWindow();
        };

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("Show DisplayDeck", null, (_, _) => ShowApp());

        var profilesMenu = new WinForms.ToolStripMenuItem("Apply a profile");
        menu.Items.Add(profilesMenu);
        menu.Items.Add("Save current setup", null, (_, _) => SaveCurrentFromTray());

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Refresh displays", null, (_, _) => _viewModel.Refresh());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        // Rebuild the profile list each time the menu opens so it stays current.
        menu.Opening += (_, _) => RebuildProfilesMenu(profilesMenu);

        _trayIcon.ContextMenuStrip = menu;
    }

    private void RebuildProfilesMenu(WinForms.ToolStripMenuItem parent)
    {
        parent.DropDownItems.Clear();
        var profiles = _viewModel.Profiles.LoadAll();

        if (profiles.Count == 0)
        {
            parent.DropDownItems.Add(new WinForms.ToolStripMenuItem("(No profiles saved yet)") { Enabled = false });
            return;
        }

        foreach (var profile in profiles)
        {
            var item = new WinForms.ToolStripMenuItem($"{profile.Name}  —  {profile.Summary}");
            item.Click += (_, _) => ApplyProfileFromTray(profile);
            parent.DropDownItems.Add(item);
        }
    }

    private void ApplyProfileFromTray(DisplayDeck.Core.Models.DisplayProfile profile)
    {
        // Bring the window up so the 15-second confirm bar is visible, then apply.
        ShowApp();
        _viewModel.ApplyProfileWithRevert(profile);
    }

    private void SaveCurrentFromTray()
    {
        var profile = _viewModel.Profiles.CaptureCurrent($"Setup {DateTime.Now:MMM d, h:mm tt}");
        _viewModel.Profiles.Save(profile);
        _profilesVm?.Reload();

        if (_trayIcon is not null)
        {
            _trayIcon.BalloonTipTitle = "Profile saved";
            _trayIcon.BalloonTipText = $"Saved \u201C{profile.Name}\u201D ({profile.Summary}).";
            _trayIcon.ShowBalloonTip(3000);
        }
    }

    private static Drawing.Icon CreateAppIcon()
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new Drawing.Drawing2D.LinearGradientBrush(
                new Drawing.Rectangle(0, 0, 32, 32),
                Drawing.Color.FromArgb(0x2F, 0x6B, 0xFF),
                Drawing.Color.FromArgb(0x8A, 0x4B, 0xFF),
                45f);
            g.FillEllipse(brush, 1, 1, 30, 30);
            using var font = new Drawing.Font("Segoe UI", 15, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel);
            using var fmt = new Drawing.StringFormat
            {
                Alignment = Drawing.StringAlignment.Center,
                LineAlignment = Drawing.StringAlignment.Center,
            };
            g.DrawString("D", font, Drawing.Brushes.White, new Drawing.RectangleF(0, 0, 32, 32), fmt);
        }

        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    private void ToggleWindow()
    {
        Log.Write($"ToggleWindow: IsVisible={IsVisible}, State={WindowState}, IsActive={IsActive}");
        if (IsVisible && WindowState != WindowState.Minimized && IsActive)
            HideApp();
        else
            ShowApp();
    }

    private void ShowApp()
    {
        _viewModel.Refresh();

        Show();
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        CenterOnCursorScreen();

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        Log.Write($"ShowApp done. Left={Left:0}, Top={Top:0}, W={ActualWidth:0}, H={ActualHeight:0}");
    }

    private void HideApp()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private void ExitApp()
    {
        _reallyExit = true;
        Close();
    }

    private void CenterOnCursorScreen()
    {
        // WorkArea is in device-independent units relative to the primary monitor,
        // which guarantees a visible placement regardless of per-monitor DPI.
        var wa = SystemParameters.WorkArea;
        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : Height;

        Left = wa.Left + (wa.Width - w) / 2;
        Top = wa.Top + (wa.Height - h) / 2;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideApp();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_reallyExit)
        {
            // Closing just tucks the app back into the tray so it stays warm.
            e.Cancel = true;
            HideApp();
            return;
        }

        _hotkey?.Dispose();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }

        base.OnClosing(e);
    }

    private void OnNavChanged(object sender, RoutedEventArgs e)
    {
        // ContentHost may not exist yet while XAML is still being parsed.
        if (ContentHost is null || sender is not RadioButton { Tag: string key })
            return;

        Log.Write($"Nav changed -> {key}");
        ContentHost.Content = key switch
        {
            "displays" => _displaysView ??= new DisplaysView(),
            "profiles" => ShowProfiles(),
            "start" => _gettingStartedView ??= new GettingStartedView(),
            "roadmap" => _roadmapView ??= new RoadmapView(),
            "about" => _aboutView ??= new AboutView(),
            _ => ContentHost.Content,
        };
    }

    private ProfilesView ShowProfiles()
    {
        _profilesVm ??= new ProfilesViewModel(_viewModel);
        _profilesView ??= new ProfilesView { DataContext = _profilesVm };
        _profilesVm.Reload(); // reflect anything saved from the tray while we were away
        return _profilesView;
    }

    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        Log.Write("OnToggleTheme fired.");
        var current = ApplicationThemeManager.GetAppTheme();
        ApplicationThemeManager.Apply(
            current == ApplicationTheme.Dark ? ApplicationTheme.Light : ApplicationTheme.Dark);
    }
}
