using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;
using Microsoft.Win32;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace SqueakPlayer
{
    public partial class MainWindow : Window
    {
        private LibVLC _libVLC = null!;
        private MediaPlayer _mp = null!;
        private Media? _currentMedia;

        private readonly DispatcherTimer _hideTimer;
        private DispatcherTimer _restoreTimer = null!;
        private WindowState _lastWindowState = WindowState.Normal;
        private bool _suppressControls;   // hide UI during the restore-from-minimize animation
        private bool _controlsVisible = true;
        private bool _isDraggingSeek;
        private bool _scrubbing;
        private bool _updatingSeekFromPlayer;
        private long _lengthMs;

        private int _volume = 100;   // 0..100, the user's chosen level
        private bool _muted;
        private bool _isPlaying;     // cached so WndProc never has to call into libvlc

        private readonly Settings _settings = Settings.Load();
        private string? _currentPath;
        private long _resumeMs;       // pending resume position for the file being opened
        private bool _resumeApplied;

        private bool _pinned;                  // user's "always on top" choice
        private bool _isFullscreen;
        private WindowState _prevState = WindowState.Normal;
        private Rect _prevBounds;

        // The LibVLCSharp video overlay doesn't reliably forward passive mouse
        // messages to WPF, so we poll the cursor ourselves for hover + clicks.
        private readonly DispatcherTimer _inputPoll;
        private POINT _lastCursor;
        private bool _rbDown;
        private bool _lbDown;
        private POINT _lbDownPt;
        private bool _lbDownOnControls;
        private bool _menuOpen;
        private DateTime _lastMenuClose;
        private DateTime _lastToggle;
        private DateTime _lastScrub;

        private static readonly string[] VideoExt =
            { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".ts" };

        private const int WM_ERASEBKGND = 0x0014;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int VK_LBUTTON = 0x01;
        private const int VK_RBUTTON = 0x02;
        private const int GCLP_HBRBACKGROUND = -10;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        // Hit-test codes for starting a native window resize.
        private const int HTTOPLEFT = 13, HTTOPRIGHT = 14, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        // Window styles — re-added so a borderless window still animates on minimize.
        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const double ResizeMargin = 14;
        private int _cornerCode;   // current corner under the cursor (0 = none)

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT p);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

        [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
        private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hwnd, EnumChildProc proc, IntPtr lParam);

        private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateSolidBrush(uint color);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public MainWindow()
        {
            InitializeComponent();

            Core.Initialize(); // finds libvlc from the VideoLAN.LibVLC.Windows package
            _libVLC = new LibVLC();
            _mp = new MediaPlayer(_libVLC);
            Video.MediaPlayer = _mp;
            Video.Background = Brushes.Black;

            // Let VLC's native video window stop swallowing input, so the WPF
            // overlay actually receives MouseMove (hover) and we drive keys ourselves.
            _mp.EnableMouseInput = false;
            _mp.EnableKeyInput = false;

            _mp.TimeChanged += Mp_TimeChanged;
            _mp.LengthChanged += (_, e) => Dispatcher.BeginInvoke(() =>
            {
                _lengthMs = e.Length;
                TryApplyResume();
            });
            _mp.Playing += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                _isPlaying = true;
                PlayPauseBtn.Content = "❚❚";
                DropHint.Visibility = Visibility.Collapsed;
                Backdrop.Visibility = Visibility.Collapsed; // let the picture show
                ApplyVolume();
                MakeBackgroundBlack();
            });
            _mp.Paused  += (_, _) => Dispatcher.BeginInvoke(() => { _isPlaying = false; PlayPauseBtn.Content = "▶"; });
            _mp.EndReached += (_, _) => Dispatcher.BeginInvoke(() =>
            {
                _isPlaying = false;
                PlayPauseBtn.Content = "▶";
                if (_currentPath != null) _settings.Positions.Remove(_currentPath);

                // Autoplay: roll into the next file in the folder (deferred via
                // BeginInvoke so we're not calling Play() inside the VLC event).
                var files = FolderVideos(out int idx);
                bool hasNext = idx >= 0 && idx < files.Length - 1;
                if (_settings.AutoplayNext && hasNext)
                    OpenFile(files[idx + 1]);
                else
                    Backdrop.Visibility = Visibility.Visible; // cover the (white) surface again
            });

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _hideTimer.Tick += (_, _) => { _hideTimer.Stop(); HideControls(); };

            _inputPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _inputPoll.Tick += InputPoll_Tick;
            _inputPoll.Start();

            _restoreTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _restoreTimer.Tick += (_, _) => { _restoreTimer.Stop(); _suppressControls = false; };

            // Restore remembered preferences.
            _volume = _settings.Volume;
            _muted = _settings.Muted;
            _pinned = _settings.AlwaysOnTop;
            Loc.Cur = Loc.Resolve(_settings.Language);
            ApplyTopmost();
            ApplyLanguage();
            ApplyVolume();

            Loaded += (_, _) => MakeBackgroundBlack();

        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            // Restoring from minimize: hide the controls so they aren't visible mid-air
            // during the scale-up animation, and keep them hidden until it finishes.
            if (_lastWindowState == WindowState.Minimized && WindowState != WindowState.Minimized)
            {
                HideControlsInstant();
                _suppressControls = true;
                _restoreTimer.Stop();
                _restoreTimer.Start();
            }
            _lastWindowState = WindowState;
        }

        private void HideControlsInstant()
        {
            _controlsVisible = false;
            ControlBar.BeginAnimation(OpacityProperty, null);
            TopBar.BeginAnimation(OpacityProperty, null);
            BarSlide.BeginAnimation(TranslateTransform.YProperty, null);
            TopSlide.BeginAnimation(TranslateTransform.YProperty, null);
            ControlBar.Opacity = 0;
            TopBar.Opacity = 0;
            BarSlide.Y = 20;
            TopSlide.Y = -20;
            ControlBar.IsHitTestVisible = false;
            TopBar.IsHitTestVisible = false;
            SkipIntroBtn.Visibility = Visibility.Collapsed;
            SeekPreview.IsOpen = false;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            _inputPoll.Stop();
            _hideTimer.Stop();

            SaveCurrentPosition();
            _settings.Volume = _volume;
            _settings.Muted = _muted;
            _settings.AlwaysOnTop = _pinned;
            _settings.Save();

            // libvlc's native shutdown intermittently raises an access violation
            // (0xC0000005) as the process tears down — and calling Stop()/Dispose()
            // can trigger it too. Everything is already persisted, so terminate the
            // process immediately; the OS reclaims all resources cleanly.
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }

        // ---------- Window message hook (stop white flicker on resize) ----------

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is HwndSource src)
            {
                src.AddHook(WndProc);

                // WindowStyle=None drops the minimize/restore animation. Re-adding the
                // caption + min/max box styles brings it back; WindowChrome still hides
                // the actual title bar, so it stays borderless.
                int style = GetWindowLong(src.Handle, GWL_STYLE);
                SetWindowLong(src.Handle, GWL_STYLE, style | WS_CAPTION | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            }
            MakeBackgroundBlack();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Swallow the default (white) background erase so exposed edges stay black
            // while the window is being resized.
            if (msg == WM_ERASEBKGND)
            {
                handled = true;
                return (IntPtr)1;
            }
            return IntPtr.Zero;
        }

        // Repaint the native window (and VLC's child video window) with a black brush
        // instead of the default white one, killing the flash while resizing.
        private IntPtr _blackBrush;

        private void MakeBackgroundBlack()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (_blackBrush == IntPtr.Zero) _blackBrush = CreateSolidBrush(0x00000000);
            SetClassLongPtr(hwnd, GCLP_HBRBACKGROUND, _blackBrush);
            EnumChildWindows(hwnd, (h, _) =>
            {
                SetClassLongPtr(h, GCLP_HBRBACKGROUND, _blackBrush);
                return true;
            }, IntPtr.Zero);
        }

        // ---------- Cursor polling: hover + right-click ----------

        private void InputPoll_Tick(object? sender, EventArgs e)
        {
            if (!GetCursorPos(out var pt)) return;

            // Hover: any real cursor movement inside the window re-shows the controls.
            if ((pt.X != _lastCursor.X || pt.Y != _lastCursor.Y) && CursorInsideWindow(pt))
            {
                ShowControls();
                _hideTimer.Stop();
                _hideTimer.Start();
            }
            _lastCursor = pt;

            // Corner resize: the airspace overlay swallows WindowChrome's corner
            // hit-test, so we detect corners ourselves, show the diagonal cursor,
            // and kick off a native resize on press. Only touch the cursor when the
            // corner actually changes (setting it every tick causes flicker/lag).
            int corner = (WindowState == WindowState.Normal && !_isFullscreen) ? GetCorner(pt) : 0;
            if (corner != _cornerCode)
            {
                _cornerCode = corner;
                Mouse.OverrideCursor = corner == 0 ? null
                    : (corner == HTTOPLEFT || corner == HTBOTTOMRIGHT)
                        ? Cursors.SizeNWSE : Cursors.SizeNESW;
            }

            // Left-click on the video area toggles play/pause (a click, not a drag,
            // and not over the control bars). TogglePause() is debounced so if the
            // WPF handler also fires for the same click nothing double-toggles.
            bool lb = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
            if (lb && !_lbDown)
            {
                _lbDown = true;
                _lbDownPt = pt;
                _lbDownOnControls = OverControls(pt);

                if (corner != 0)
                {
                    _lbDown = false; // the native resize loop consumes this press
                    var hwnd = new WindowInteropHelper(this).Handle;
                    ReleaseCapture();
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, (IntPtr)corner, IntPtr.Zero);
                    return;
                }

                // Press anywhere on the seek bar starts a scrub that follows the cursor.
                if (_controlsVisible && _lengthMs > 0 && OverElement(pt, SeekBar))
                {
                    _scrubbing = true;
                    _isDraggingSeek = true;
                    ScrubToScreen(pt);
                    return;
                }
            }
            else if (lb && _lbDown && _scrubbing)
            {
                ScrubToScreen(pt); // follow the cursor while the button is held
            }
            else if (!lb && _lbDown)
            {
                _lbDown = false;
                if (_scrubbing)
                {
                    _scrubbing = false;
                    _isDraggingSeek = false;
                    SeekTo(SeekBar.Value);   // final, exact seek
                    SeekPreview.IsOpen = false;
                }
                else
                {
                    bool moved = Math.Abs(pt.X - _lbDownPt.X) > 4 || Math.Abs(pt.Y - _lbDownPt.Y) > 4;
                    bool menuNearby = _menuOpen || (DateTime.UtcNow - _lastMenuClose).TotalMilliseconds < 250;
                    if (IsActive && CursorInsideWindow(pt) && !moved && !_lbDownOnControls && !menuNearby)
                        TogglePause();
                }
            }

            // Right-click: fire on the button-release edge, at the cursor.
            bool rb = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;
            if (rb && !_rbDown)
            {
                _rbDown = true;
            }
            else if (!rb && _rbDown)
            {
                _rbDown = false;
                if (IsActive && CursorInsideWindow(pt))
                    ShowContextMenuAt(pt);
            }
        }

        private bool OverControls(POINT s)
        {
            if (_controlsVisible && (OverElement(s, ControlBar) || OverElement(s, TopBar))) return true;
            if (SkipIntroBtn.Visibility == Visibility.Visible && OverElement(s, SkipIntroBtn)) return true;
            return false;
        }

        private bool OverElement(POINT screen, FrameworkElement el)
        {
            if (el == null || !el.IsVisible) return false;
            try
            {
                var p = el.PointFromScreen(new Point(screen.X, screen.Y));
                return p.X >= 0 && p.Y >= 0 && p.X <= el.ActualWidth && p.Y <= el.ActualHeight;
            }
            catch { return false; }
        }

        private bool CursorInsideWindow(POINT screen)
        {
            if (WindowState == WindowState.Minimized) return false;
            try
            {
                var p = PointFromScreen(new Point(screen.X, screen.Y));
                return p.X >= 0 && p.Y >= 0 && p.X <= ActualWidth && p.Y <= ActualHeight;
            }
            catch { return false; }
        }

        // Returns the HT* code if the cursor sits in one of the four corners, else 0.
        private int GetCorner(POINT screen)
        {
            Point p;
            try { p = PointFromScreen(new Point(screen.X, screen.Y)); }
            catch { return 0; }

            double w = ActualWidth, h = ActualHeight;
            bool left = p.X >= 0 && p.X <= ResizeMargin;
            bool right = p.X <= w && p.X >= w - ResizeMargin;
            bool top = p.Y >= 0 && p.Y <= ResizeMargin;
            bool bottom = p.Y <= h && p.Y >= h - ResizeMargin;

            if (top && left) return HTTOPLEFT;
            if (top && right) return HTTOPRIGHT;
            if (bottom && left) return HTBOTTOMLEFT;
            if (bottom && right) return HTBOTTOMRIGHT;
            return 0;
        }

        // ---------- Playback ----------

        private void OpenFile(string path)
        {
            SaveCurrentPosition(); // remember where we were in the previous file

            _currentPath = path;
            _resumeMs = _settings.Positions.TryGetValue(path, out var t) ? t : 0;
            _resumeApplied = false;
            _lengthMs = 0;

            // Keep a reference to the playing media and dispose the *previous* one only
            // after the new one is handed to the player — disposing the media that's
            // being switched from mid-play can crash libvlc natively.
            Backdrop.Visibility = Visibility.Visible; // black while the new file loads

            var media = new Media(_libVLC, new Uri(path));
            var old = _currentMedia;
            _currentMedia = media;
            _mp.Play(media);
            // Dispose the previous media off the UI thread so a slow native teardown
            // can never freeze the switch.
            if (old != null) System.Threading.Tasks.Task.Run(() => old.Dispose());

            TitleLabel.Text = System.IO.Path.GetFileName(path);
            Title = "Squeak — " + System.IO.Path.GetFileName(path);
            ShowControls();
        }

        private void TryApplyResume()
        {
            if (_resumeApplied || _resumeMs <= 0 || _lengthMs <= 0) return;
            _resumeApplied = true;
            if (_resumeMs < _lengthMs - 10000)
                _mp.Time = _resumeMs;
        }

        private void SaveCurrentPosition()
        {
            if (_currentPath != null && _lengthMs > 0)
                _settings.RememberPosition(_currentPath, _mp.Time, _lengthMs);
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e) => ShowOpenDialog();

        private void ShowOpenDialog()
        {
            var dlg = new OpenFileDialog { Filter = Loc.FileFilter };
            if (dlg.ShowDialog() == true)
                OpenFile(dlg.FileName);
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                OpenFile(files[0]);
        }

        private void TogglePause()
        {
            // Debounce: WPF's click handler and the cursor poll can both fire for one click.
            if ((DateTime.UtcNow - _lastToggle).TotalMilliseconds < 150) return;
            _lastToggle = DateTime.UtcNow;

            if (_mp.Media == null) { ShowOpenDialog(); return; }
            bool playing = _mp.IsPlaying;
            if (playing) _mp.Pause(); else _mp.Play();
            ShowCenterFlash(playing ? "❚❚" : "▶");
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e) => TogglePause();
        private void VideoSurface_Click(object sender, MouseButtonEventArgs e) => TogglePause();

        // ---------- Previous / next file in the same folder ----------

        private void Prev_Click(object sender, RoutedEventArgs e) => PlayNeighbor(-1);
        private void Next_Click(object sender, RoutedEventArgs e) => PlayNeighbor(+1);

        private string[] FolderVideos(out int currentIndex)
        {
            currentIndex = -1;
            if (_currentPath == null) return Array.Empty<string>();
            var dir = Path.GetDirectoryName(_currentPath);
            if (dir == null) return Array.Empty<string>();

            string[] files;
            try
            {
                files = Directory.GetFiles(dir)
                    .Where(f => VideoExt.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch { return Array.Empty<string>(); }

            currentIndex = Array.FindIndex(files,
                f => string.Equals(f, _currentPath, StringComparison.OrdinalIgnoreCase));
            return files;
        }

        private void PlayNeighbor(int direction)
        {
            var files = FolderVideos(out int idx);
            if (idx < 0) return;
            int next = idx + direction;
            if (next >= 0 && next < files.Length)
                OpenFile(files[next]);
        }

        // ---------- Time / seek bar ----------

        private void Mp_TimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (!_isDraggingSeek && _lengthMs > 0)
                {
                    _updatingSeekFromPlayer = true;
                    SeekBar.Value = (double)e.Time / _lengthMs * 1000.0;
                    _updatingSeekFromPlayer = false;
                }
                TimeLabel.Text = $"{Fmt(e.Time)} / {Fmt(_lengthMs)}";
                UpdateChapterUi();
            });
        }

        private static string Fmt(long ms)
        {
            var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
            return t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }

        // Scrubbing is driven from the cursor poll (WPF mouse events don't reach the
        // seek bar reliably over the video overlay). See InputPoll_Tick.
        private void ScrubToScreen(POINT screen)
        {
            double w = SeekBar.ActualWidth;
            if (w <= 0 || _lengthMs <= 0) return;
            try
            {
                double x = Math.Clamp(SeekBar.PointFromScreen(new Point(screen.X, screen.Y)).X, 0, w);
                SeekBar.Value = x / w * SeekBar.Maximum; // triggers SeekBar_ValueChanged -> live seek

                SeekPreviewText.Text = Fmt((long)(x / w * _lengthMs));
                SeekPreview.HorizontalOffset = x - 22;
                SeekPreview.VerticalOffset = -34;
                SeekPreview.IsOpen = true;
            }
            catch { /* PointFromScreen can throw during teardown */ }
        }

        private void SeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingSeekFromPlayer) return;
            if (_isDraggingSeek)
            {
                if (_lengthMs > 0)
                {
                    long target = (long)(e.NewValue / 1000.0 * _lengthMs);
                    TimeLabel.Text = $"{Fmt(target)} / {Fmt(_lengthMs)}";

                    // Live scrub: move the video as you drag (throttled so we don't
                    // flood libvlc with seeks on every value change).
                    if ((DateTime.UtcNow - _lastScrub).TotalMilliseconds >= 40)
                    {
                        _lastScrub = DateTime.UtcNow;
                        _mp.Time = target;
                    }
                }
            }
            else
            {
                // click-to-seek (IsMoveToPointEnabled)
                SeekTo(e.NewValue);
            }
        }

        private void SeekTo(double sliderValue)
        {
            if (_lengthMs <= 0) return;
            _mp.Time = (long)(sliderValue / 1000.0 * _lengthMs);
        }

        private void SeekRelative(long deltaMs)
        {
            if (_lengthMs <= 0) return;
            long target = Math.Clamp(_mp.Time + deltaMs, 0, _lengthMs);
            _mp.Time = target;
            ShowOsd($"{(deltaMs >= 0 ? "»" : "«")}  {Fmt(target)} / {Fmt(_lengthMs)}");
        }

        // Hover-preview of the time under the cursor on the seek bar.
        private void SeekBar_MouseMove(object sender, MouseEventArgs e)
        {
            double w = SeekBar.ActualWidth;
            if (_lengthMs <= 0 || w <= 0) { SeekPreview.IsOpen = false; return; }

            if (_scrubbing) return; // the poll drives preview + seek while scrubbing

            double x = Math.Clamp(e.GetPosition(SeekBar).X, 0, w);
            SeekPreviewText.Text = Fmt((long)(x / w * _lengthMs));
            SeekPreview.HorizontalOffset = x - 22;
            SeekPreview.VerticalOffset = -34;
            SeekPreview.IsOpen = true;
        }

        private void SeekBar_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_scrubbing) SeekPreview.IsOpen = false;
        }

        // ---------- Volume ----------

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _volume = (int)Math.Round(e.NewValue);
            if (_volume > 0) _muted = false; // dragging off zero unmutes
            ApplyVolume();
            ShowVolumeOsd();
        }

        private void Mute_Click(object sender, RoutedEventArgs e) => ToggleMute();

        private void ToggleMute()
        {
            _muted = !_muted;
            if (!_muted && _volume == 0) SetVolume(20); // unmuting from silence
            ApplyVolume();
            ShowVolumeOsd();
        }

        private void SetVolume(int volume)
        {
            _volume = Math.Clamp(volume, 0, 100);
            if (_volume > 0) _muted = false;
            ApplyVolume();
        }

        private void AdjustVolume(int delta)
        {
            SetVolume(_volume + delta);
            ShowVolumeOsd();
        }

        private void ShowVolumeOsd()
            => ShowOsd(_muted || _volume == 0 ? Loc.Muted : $"🔊  {_volume}%");

        private void ApplyVolume()
        {
            int target = _muted ? 0 : _volume;

            // Keep the slider in sync without re-entering the ValueChanged handler.
            if ((int)Math.Round(VolumeSlider.Value) != _volume)
            {
                VolumeSlider.ValueChanged -= VolumeSlider_ValueChanged;
                VolumeSlider.Value = _volume;
                VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
            }

            // MediaPlayer.Volume only sticks once there is output, hence we also
            // call this from the Playing event.
            if (_mp != null) _mp.Volume = target;

            MuteBtn.Content = target == 0 ? "🔇" : target < 50 ? "🔉" : "🔊";
        }

        // ---------- Chapters / Skip intro ----------

        private void UpdateChapterUi()
        {
            int count = _mp.ChapterCount;
            int current = _mp.Chapter;

            bool showSkip = count > 1 && current == 0;
            SkipIntroBtn.Visibility = showSkip && _controlsVisible
                ? Visibility.Visible : Visibility.Collapsed;

            ChapterLabel.Text = count > 0 ? Loc.Chapter(current + 1, count) : "";
        }

        private void SkipIntro_Click(object sender, RoutedEventArgs e)
        {
            if (_mp.ChapterCount > 1)
                _mp.Chapter = 1;
        }

        // ---------- Right-click menu: audio / subtitle tracks ----------

        private void ShowContextMenuAt(POINT screen)
        {
            Point p;
            try { p = PointFromScreen(new Point(screen.X, screen.Y)); }
            catch { return; }

            var menu = BuildContextMenu();
            menu.PlacementTarget = this;
            menu.Placement = PlacementMode.Relative;
            menu.HorizontalOffset = p.X;
            menu.VerticalOffset = p.Y;
            menu.Closed += (_, _) => { _menuOpen = false; _lastMenuClose = DateTime.UtcNow; };
            _menuOpen = true;
            menu.IsOpen = true;
        }

        private ContextMenu BuildContextMenu()
        {
            var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };
            // Apply the dark item/separator styles to every (sub)menu item in this menu.
            menu.Resources[typeof(MenuItem)] = FindResource("DarkMenuItem");
            menu.Resources[typeof(Separator)] = FindResource("DarkSeparator");

            var files = FolderVideos(out int idx);
            var prev = new MenuItem { Header = "❚◀  " + Loc.MenuPrev, IsEnabled = idx > 0 };
            prev.Click += (_, _) => PlayNeighbor(-1);
            menu.Items.Add(prev);

            var next = new MenuItem { Header = "▶❚  " + Loc.MenuNext, IsEnabled = idx >= 0 && idx < files.Length - 1 };
            next.Click += (_, _) => PlayNeighbor(+1);
            menu.Items.Add(next);

            menu.Items.Add(new Separator());

            menu.Items.Add(BuildTrackMenu(
                Loc.MenuAudio,
                SafeTracks(() => _mp.AudioTrackDescription),
                _mp.AudioTrack,
                id => _mp.SetAudioTrack(id)));

            menu.Items.Add(BuildTrackMenu(
                Loc.MenuSubs,
                SafeTracks(() => _mp.SpuDescription),
                _mp.Spu,
                id => _mp.SetSpu(id)));

            menu.Items.Add(new Separator());

            // View / behaviour toggles
            var full = new MenuItem { Header = Loc.MenuFull };
            full.Click += (_, _) => ToggleFullscreen();
            menu.Items.Add(full);

            var pin = new MenuItem { Header = Loc.MenuOnTop, IsCheckable = true, IsChecked = Topmost };
            pin.Click += (_, _) => TogglePin();
            menu.Items.Add(pin);

            var autoplay = new MenuItem { Header = Loc.MenuAutoplay, IsCheckable = true, IsChecked = _settings.AutoplayNext };
            autoplay.Click += (_, _) => _settings.AutoplayNext = !_settings.AutoplayNext;
            menu.Items.Add(autoplay);

            menu.Items.Add(new Separator());

            // Settings-ish
            var lang = new MenuItem { Header = Loc.MenuLang };
            lang.Items.Add(LangItem(Loc.LangAuto, "auto"));
            lang.Items.Add(LangItem("English", "en"));
            lang.Items.Add(LangItem("Polski", "pl"));
            menu.Items.Add(lang);

            var open = new MenuItem { Header = Loc.MenuOpen };
            open.Click += (_, _) => ShowOpenDialog();
            menu.Items.Add(open);

            return menu;
        }

        private MenuItem LangItem(string header, string code)
        {
            var mi = new MenuItem { Header = header, IsCheckable = true, IsChecked = _settings.Language == code };
            mi.Click += (_, _) => SetLanguage(code);
            return mi;
        }

        private static MenuItem BuildTrackMenu(string header, TrackDescription[] tracks, int currentId, Action<int> select)
        {
            var item = new MenuItem { Header = header };
            if (tracks.Length == 0)
            {
                item.IsEnabled = false;
                return item;
            }

            foreach (var td in tracks)
            {
                int id = td.Id;
                var mi = new MenuItem
                {
                    Header = td.Name ?? $"#{id}",
                    IsCheckable = true,
                    IsChecked = id == currentId
                };
                mi.Click += (_, _) => select(id);
                item.Items.Add(mi);
            }
            return item;
        }

        private static TrackDescription[] SafeTracks(Func<TrackDescription[]> get)
        {
            try { return get() ?? Array.Empty<TrackDescription>(); }
            catch { return Array.Empty<TrackDescription>(); }
        }

        // ---------- Hover show/hide ----------

        private void Overlay_MouseMove(object sender, MouseEventArgs e)
        {
            ShowControls();
            _hideTimer.Stop();
            _hideTimer.Start();
        }

        private void ShowControls()
        {
            if (_suppressControls) { return; }
            if (_controlsVisible) { return; }
            _controlsVisible = true;
            Mouse.OverrideCursor = null;
            AnimateBars(toOpacity: 1, toY: 0);
            UpdateChapterUi();
        }

        private void HideControls()
        {
            if (!_controlsVisible || _isDraggingSeek
                || ControlBar.IsMouseOver || TopBar.IsMouseOver) return;
            _controlsVisible = false;
            if (_isPlaying) Mouse.OverrideCursor = Cursors.None;
            AnimateBars(toOpacity: 0, toY: 20);
            SkipIntroBtn.Visibility = Visibility.Collapsed;
            SeekPreview.IsOpen = false;
        }

        private void AnimateBars(double toOpacity, double toY)
        {
            var dur = TimeSpan.FromMilliseconds(200);
            var ease = new QuadraticEase();

            ControlBar.BeginAnimation(OpacityProperty, new DoubleAnimation(toOpacity, dur) { EasingFunction = ease });
            BarSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(toY, dur) { EasingFunction = ease });

            TopBar.BeginAnimation(OpacityProperty, new DoubleAnimation(toOpacity, dur) { EasingFunction = ease });
            TopSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-toY, dur) { EasingFunction = ease });

            ControlBar.IsHitTestVisible = toOpacity > 0;
            TopBar.IsHitTestVisible = toOpacity > 0;
        }

        // ---------- On-screen feedback ----------

        private void ShowCenterFlash(string glyph)
        {
            CenterFlashIcon.Text = glyph;
            var dur = TimeSpan.FromMilliseconds(450);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            CenterFlash.BeginAnimation(OpacityProperty, new DoubleAnimation(0.9, 0, dur) { EasingFunction = ease });
            CenterFlashScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 1.6, dur) { EasingFunction = ease });
            CenterFlashScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 1.6, dur) { EasingFunction = ease });
        }

        private void ShowOsd(string text)
        {
            OsdText.Text = text;
            // Hold at full opacity ~0.9s, then fade out.
            var fade = new DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900))));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1300))));
            Osd.BeginAnimation(OpacityProperty, fade);
        }

        // ---------- Window chrome: drag / minimize / close / pin ----------

        private void TopBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { ToggleFullscreen(); return; }
            if (e.ButtonState == MouseButtonState.Pressed && WindowState == WindowState.Normal && !_isFullscreen)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Pin_Click(object sender, RoutedEventArgs e) => TogglePin();

        private void TogglePin()
        {
            _pinned = !_pinned;
            ApplyTopmost();
            UpdatePinVisual();
        }

        // Topmost is the OR of the user's pin choice and the temporary fullscreen state.
        private void ApplyTopmost() => Topmost = _pinned || _isFullscreen;

        private void UpdatePinVisual()
        {
            PinBtn.Foreground = _pinned ? (Brush)FindResource("AccentBrush") : Brushes.White;
            PinBtn.ToolTip = _pinned ? Loc.TipUnpin : Loc.TipPin;
        }

        // ---------- Localization ----------

        private void ApplyLanguage()
        {
            DropHint.Text = Loc.DropHint;
            SkipIntroBtn.Content = Loc.SkipIntro + "  ▶❚";
            MuteBtn.ToolTip = Loc.TipMute;
            PrevBtn.ToolTip = Loc.TipPrev;
            NextBtn.ToolTip = Loc.TipNext;
            OpenBtn.ToolTip = Loc.TipOpen;
            FullscreenBtn.ToolTip = Loc.TipFull;
            MinBtn.ToolTip = Loc.TipMin;
            CloseBtn.ToolTip = Loc.TipClose;
            UpdatePinVisual();
            UpdateChapterUi();
        }

        private void SetLanguage(string code)
        {
            _settings.Language = code;
            Loc.Cur = Loc.Resolve(code);
            ApplyLanguage();
        }

        // ---------- Keyboard / fullscreen ----------

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space: TogglePause(); break;
                case Key.Left: SeekRelative(-5000); break;
                case Key.Right: SeekRelative(5000); break;
                case Key.Up: AdjustVolume(+5); break;
                case Key.Down: AdjustVolume(-5); break;
                case Key.M: ToggleMute(); break;
                case Key.T: TogglePin(); break;
                case Key.F: ToggleFullscreen(); break;
                case Key.Escape when _isFullscreen: ToggleFullscreen(); break;
                case Key.O when Keyboard.Modifiers == ModifierKeys.Control: ShowOpenDialog(); break;
                default: return;
            }
            ShowControls();
            _hideTimer.Stop();
            _hideTimer.Start();
            e.Handled = true;
        }

        private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

        private void ToggleFullscreen()
        {
            if (!_isFullscreen)
                EnterFullscreen();
            else
                ExitFullscreen();
        }

        private void EnterFullscreen()
        {
            _prevState = WindowState;
            _prevBounds = RestoreBounds; // normal bounds even if currently maximized

            _isFullscreen = true;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.NoResize;

            // Cover the whole monitor (not just the work area) so the taskbar hides.
            var hwnd = new WindowInteropHelper(this).Handle;
            var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(mon, ref mi)
                && PresentationSource.FromVisual(this) is HwndSource src)
            {
                var m = src.CompositionTarget.TransformFromDevice; // device px -> DIP
                var tl = m.Transform(new Point(mi.rcMonitor.Left, mi.rcMonitor.Top));
                var br = m.Transform(new Point(mi.rcMonitor.Right, mi.rcMonitor.Bottom));
                Left = tl.X; Top = tl.Y; Width = br.X - tl.X; Height = br.Y - tl.Y;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }

            ApplyTopmost();
            Activate();
        }

        private void ExitFullscreen()
        {
            _isFullscreen = false;
            ResizeMode = ResizeMode.CanResize;
            ApplyTopmost();

            WindowState = _prevState;
            if (_prevState == WindowState.Normal)
            {
                Left = _prevBounds.Left; Top = _prevBounds.Top;
                Width = _prevBounds.Width; Height = _prevBounds.Height;
            }
        }
    }
}
