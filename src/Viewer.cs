using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kamery
{
    internal static class Vlc
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr libvlc_get_version();
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr libvlc_new(int argc, IntPtr argv);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern void libvlc_release(IntPtr instance);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr libvlc_media_new_location(IntPtr instance, byte[] url);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern void libvlc_media_add_option(IntPtr media, byte[] option);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern void libvlc_media_release(IntPtr media);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr libvlc_media_player_new_from_media(IntPtr media);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern void libvlc_media_player_set_hwnd(IntPtr player, IntPtr hwnd);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern int libvlc_media_player_play(IntPtr player);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern void libvlc_media_player_stop(IntPtr player);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern void libvlc_media_player_release(IntPtr player);
        [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl)] internal static extern int libvlc_media_player_get_state(IntPtr player);
        internal static byte[] Utf8(string text) { return Encoding.UTF8.GetBytes(text + "\0"); }

        internal static IntPtr Initialize(Config config, string configPath)
        {
            string directory = String.IsNullOrWhiteSpace(config.vlcDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VideoLAN", "VLC")
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(configPath), config.vlcDirectory));
            if (!File.Exists(Path.Combine(directory, "libvlc.dll")) || !Directory.Exists(Path.Combine(directory, "plugins")))
                throw new Exception("Install 64-bit VLC 3 from https://www.videolan.org/vlc/ or set vlcDirectory to its installation folder.");
            if (!SetDllDirectory(directory)) throw new Exception("Cannot load the configured VLC directory.");
            string version = Marshal.PtrToStringAnsi(libvlc_get_version());
            if (version == null || !version.StartsWith("3.", StringComparison.Ordinal))
                throw new Exception("This viewer requires VLC 3. Install the 64-bit VLC 3 release.");
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", Path.Combine(directory, "plugins"));
            IntPtr instance = libvlc_new(0, IntPtr.Zero);
            if (instance == IntPtr.Zero) throw new Exception("VLC initialization failed. Use 64-bit VLC 3 with its plugins folder.");
            return instance;
        }
    }

    internal sealed class CameraTile : Panel
    {
        private readonly object gate = new object();
        private readonly Label status;
        private readonly Timer timer = new Timer();
        private IntPtr player;
        private bool closing;
        private bool checking;
        private DateTime lastAttempt = DateTime.UtcNow;
        private readonly int retrySeconds;

        internal CameraTile(IntPtr instance, Camera camera, Config config)
        {
            Dock = DockStyle.Fill;
            Margin = new Padding(4);
            BackColor = Color.Black;
            var title = new Label { Text = camera.name, Dock = DockStyle.Top, Height = 30,
                Padding = new Padding(8, 6, 0, 0), BackColor = Color.FromArgb(38, 43, 51), ForeColor = Color.White };
            status = new Label { Text = "Connecting...", Dock = DockStyle.Bottom, Height = 24,
                Padding = new Padding(8, 3, 0, 0), ForeColor = Color.Silver, BackColor = Color.FromArgb(28, 32, 38) };
            var video = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            Controls.Add(video);
            Controls.Add(title);
            Controls.Add(status);
            retrySeconds = config.reconnectSeconds;
            IntPtr media = Vlc.libvlc_media_new_location(instance, Vlc.Utf8(camera.PlaybackUrl()));
            if (media == IntPtr.Zero) throw new Exception("Could not create camera media.");
            try
            {
                Vlc.libvlc_media_add_option(media, Vlc.Utf8(":network-caching=" + config.networkCachingMs));
                Vlc.libvlc_media_add_option(media, Vlc.Utf8(":no-audio"));
                Vlc.libvlc_media_add_option(media, Vlc.Utf8(":no-video-title-show"));
                if (config.rtspTcp) Vlc.libvlc_media_add_option(media, Vlc.Utf8(":rtsp-tcp"));
                player = Vlc.libvlc_media_player_new_from_media(media);
            }
            finally { Vlc.libvlc_media_release(media); }
            if (player == IntPtr.Zero) throw new Exception("Could not create camera player.");
            Vlc.libvlc_media_player_set_hwnd(player, video.Handle);
            if (Vlc.libvlc_media_player_play(player) < 0) status.Text = "Unavailable - retrying automatically";
            timer.Interval = 1000;
            timer.Tick += async delegate { await CheckAsync(); };
            timer.Start();
        }

        private async Task CheckAsync()
        {
            if (closing || checking) return;
            checking = true;
            try
            {
                string message = await Task.Run(() =>
                {
                    lock (gate)
                    {
                        if (closing || player == IntPtr.Zero) return "";
                        int state = Vlc.libvlc_media_player_get_state(player);
                        if (state == 3) return "Live";
                        if ((DateTime.UtcNow - lastAttempt).TotalSeconds >= retrySeconds)
                        {
                            Vlc.libvlc_media_player_stop(player);
                            Vlc.libvlc_media_player_play(player);
                            lastAttempt = DateTime.UtcNow;
                            return "Reconnecting...";
                        }
                        return state == 6 || state == 7 ? "Unavailable - retrying automatically" : "Connecting...";
                    }
                });
                if (!closing) { status.Text = message; status.ForeColor = message == "Live" ? Color.LightGreen : Color.Silver; }
            }
            finally { checking = false; }
        }

        internal async Task StopAsync()
        {
            closing = true;
            timer.Stop();
            timer.Dispose();
            await Task.Run(() =>
            {
                lock (gate)
                {
                    if (player == IntPtr.Zero) return;
                    Vlc.libvlc_media_player_stop(player);
                    Vlc.libvlc_media_player_release(player);
                    player = IntPtr.Zero;
                }
            });
        }
    }

    internal sealed class Viewer : Form
    {
        private Config config;
        private readonly string configPath;
        private readonly IntPtr instance;
        private readonly ComboBox sets = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        private readonly FlowLayoutPanel toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8) };
        private readonly TableLayoutPanel grid = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(18, 21, 26) };
        private readonly List<CameraTile> tiles = new List<CameraTile>();
        private bool busy;
        private bool allowClose;
        private bool closeRequested;
        private bool fullscreen;
        private FormWindowState previousState;
        private Rectangle previousBounds;

        internal Viewer(Config initialConfig, string path, IntPtr vlc)
        {
            config = initialConfig; configPath = path; instance = vlc;
            Text = "Kamery - RTSP camera viewer";
            Size = new Size(1200, 780); MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9);
            toolbar.BackColor = Color.FromArgb(235, 237, 240);
            toolbar.Controls.Add(new Label { Text = "Camera set", AutoSize = true, Padding = new Padding(0, 5, 4, 0) });
            toolbar.Controls.Add(sets);
            AddButton("Reconnect", async delegate { await SwitchAsync(); });
            AddButton("Edit config", delegate { Process.Start("notepad.exe", "\"" + configPath + "\""); });
            AddButton("Reload config", async delegate { await ReloadAsync(); });
            AddButton("Fullscreen (F11)", delegate { ToggleFullscreen(); });
            Controls.Add(grid); Controls.Add(toolbar);
            PopulateSets(config.defaultSet);
            sets.SelectedIndexChanged += async delegate { await SwitchAsync(); };
            Shown += async delegate { await SwitchAsync(); };
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.F11 || (e.KeyCode == Keys.Escape && fullscreen)) { ToggleFullscreen(); e.Handled = true; }
            };
            FormClosing += async delegate(object sender, FormClosingEventArgs e)
            {
                if (allowClose) return;
                e.Cancel = true;
                if (busy) { closeRequested = true; return; }
                busy = true; toolbar.Enabled = false;
                await ClearAsync();
                allowClose = true; Close();
            };
        }

        private void AddButton(string text, EventHandler action)
        {
            var button = new Button { Text = text, AutoSize = true, Height = 28 };
            button.Click += action; toolbar.Controls.Add(button);
        }

        private void PopulateSets(string preferred)
        {
            sets.Items.Clear(); sets.Items.AddRange(config.sets);
            sets.SelectedItem = config.sets.FirstOrDefault(s => s.name == preferred) ?? config.sets[0];
        }

        private async Task ReloadAsync()
        {
            Config updated;
            try
            {
                updated = Config.Load(configPath);
                if (updated.vlcDirectory != config.vlcDirectory)
                    throw new Exception("Restart the viewer to change vlcDirectory.");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Configuration error", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string preferred = ((CameraSet)sets.SelectedItem).name;
            busy = true;
            try { config = updated; PopulateSets(preferred); }
            finally { busy = false; }
            await SwitchAsync();
        }

        private async Task ClearAsync()
        {
            await Task.WhenAll(tiles.Select(t => t.StopAsync()));
            foreach (Control control in grid.Controls.Cast<Control>().ToArray()) control.Dispose();
            tiles.Clear(); grid.Controls.Clear();
        }

        private async Task SwitchAsync()
        {
            if (busy || sets.SelectedItem == null) return;
            busy = true; toolbar.Enabled = false;
            try
            {
                await ClearAsync();
                var set = (CameraSet)sets.SelectedItem;
                Text = "Kamery - connecting via ONVIF...";
                var cameras = set.cameras.Select(id => config.cameras.First(c => c.id == id)).ToArray();
                var resolved = new Camera[cameras.Length];
                var errors = new string[cameras.Length];
                await Task.WhenAll(cameras.Select((camera, index) => Task.Run(() =>
                {
                    try { resolved[index] = new Onvif(camera).Resolve(); }
                    catch (Exception ex) { errors[index] = ex.Message; }
                })));
                int columns = set.columns == 0 ? (int)Math.Ceiling(Math.Sqrt(set.cameras.Length)) : Math.Min(set.columns, set.cameras.Length);
                int rows = (int)Math.Ceiling((double)set.cameras.Length / columns);
                grid.SuspendLayout();
                try
                {
                    grid.ColumnStyles.Clear(); grid.RowStyles.Clear();
                    grid.ColumnCount = columns; grid.RowCount = rows;
                    for (int i = 0; i < columns; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
                    for (int i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
                    for (int i = 0; i < set.cameras.Length; i++)
                    {
                        if (resolved[i] == null)
                        {
                            grid.Controls.Add(new Label { Text = cameras[i].name + "\n\n" + errors[i],
                                Dock = DockStyle.Fill, ForeColor = Color.Salmon, BackColor = Color.FromArgb(28, 32, 38),
                                Padding = new Padding(14), TextAlign = ContentAlignment.MiddleCenter }, i % columns, i / columns);
                        }
                        else
                        {
                            var tile = new CameraTile(instance, resolved[i], config);
                            tiles.Add(tile); grid.Controls.Add(tile, i % columns, i / columns);
                        }
                    }
                }
                finally { grid.ResumeLayout(); }
                Text = "Kamery - " + set.name + " (" + tiles.Count + "/" + cameras.Length + " cameras connected)";
            }
            catch (Exception) { MessageBox.Show(this, "Could not start camera playback. Check your VLC installation and reload the set.", "Playback error"); }
            finally
            {
                toolbar.Enabled = true; busy = false;
                if (closeRequested) BeginInvoke(new Action(Close));
            }
        }

        private void ToggleFullscreen()
        {
            if (!fullscreen)
            {
                previousState = WindowState; previousBounds = Bounds;
                WindowState = FormWindowState.Normal; FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds;
            }
            else { FormBorderStyle = FormBorderStyle.Sizable; Bounds = previousBounds; WindowState = previousState; }
            fullscreen = !fullscreen;
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            IntPtr instance = IntPtr.Zero;
            try
            {
                string path = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cameras.json"));
                if (!File.Exists(path) && args.Length == 0)
                    File.Copy(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cameras.example.json"), path, false);
                var config = Config.Load(path);
                instance = Vlc.Initialize(config, path);
                Application.Run(new Viewer(config, path, instance));
            }
            catch (DllNotFoundException) { MessageBox.Show("Cannot load VLC. Install 64-bit VLC 3, including its plugins.", "Kamery"); }
            catch (BadImageFormatException) { MessageBox.Show("VLC architecture mismatch. This viewer requires 64-bit VLC 3.", "Kamery"); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Kamery", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { if (instance != IntPtr.Zero) Vlc.libvlc_release(instance); }
        }
    }
}
