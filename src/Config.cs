using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace Kamery
{
    public sealed class Camera
    {
        public string id { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public string onvifUrl { get; set; }
        public string profileToken { get; set; }
        public string username { get; set; }
        public string password { get; set; }

        // Preserve the stream path/query exactly; escape only raw credential fields.
        public string PlaybackUrl()
        {
            if (String.IsNullOrEmpty(username)) return url;
            int start = url.IndexOf("://", StringComparison.Ordinal) + 3;
            int end = url.IndexOfAny(new[] { '/', '?', '#' }, start);
            if (end < 0) end = url.Length;
            string authority = url.Substring(start, end - start);
            int at = authority.LastIndexOf('@');
            if (at >= 0) authority = authority.Substring(at + 1);
            return url.Substring(0, start) + Uri.EscapeDataString(username) + ":" +
                Uri.EscapeDataString(password ?? "") + "@" + authority + url.Substring(end);
        }
    }

    public sealed class CameraSet
    {
        public string name { get; set; }
        public int columns { get; set; }
        public string[] cameras { get; set; }
        public override string ToString() { return name; }
    }

    public sealed class Config
    {
        public string defaultSet { get; set; }
        public int networkCachingMs { get; set; }
        public int reconnectSeconds { get; set; }
        public bool rtspTcp { get; set; }
        public string vlcDirectory { get; set; }
        public Camera[] cameras { get; set; }
        public CameraSet[] sets { get; set; }

        public Config() { networkCachingMs = 500; reconnectSeconds = 10; rtspTcp = true; }

        public static Config Load(string path)
        {
            Config config;
            try { config = new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(path)); }
            catch (IOException) { throw new Exception("Cannot read configuration file: " + path); }
            catch (UnauthorizedAccessException) { throw new Exception("Cannot access configuration file: " + path); }
            catch (Exception) { throw new Exception("Invalid JSON or field types. Check the configuration against cameras.example.json."); }
            if (config == null) throw new Exception("Configuration must be a JSON object.");
            config.Validate();
            return config;
        }

        public void Validate()
        {
            if (networkCachingMs < 100 || networkCachingMs > 30000)
                throw new Exception("networkCachingMs must be between 100 and 30000.");
            if (reconnectSeconds < 3 || reconnectSeconds > 300)
                throw new Exception("reconnectSeconds must be between 3 and 300.");
            if (cameras == null || cameras.Length == 0) throw new Exception("Define at least one camera.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var camera in cameras)
            {
                if (camera == null || String.IsNullOrWhiteSpace(camera.id) || !ids.Add(camera.id))
                    throw new Exception("Each camera needs a unique, nonempty id.");
                if (String.IsNullOrWhiteSpace(camera.name)) throw new Exception("Each camera needs a name.");
                if (!String.IsNullOrEmpty(camera.password) && String.IsNullOrEmpty(camera.username))
                    throw new Exception("Camera '" + camera.id + "' needs a username when a password is supplied.");
                if ((camera.username != null && camera.username.IndexOf('\0') >= 0) ||
                    (camera.password != null && camera.password.IndexOf('\0') >= 0))
                    throw new Exception("Camera credentials cannot contain null characters.");
                Uri uri;
                if (!String.IsNullOrWhiteSpace(camera.onvifUrl))
                {
                    if (!Uri.TryCreate(camera.onvifUrl, UriKind.Absolute, out uri) ||
                        (uri.Scheme != "http" && uri.Scheme != "https") || String.IsNullOrEmpty(uri.Host) ||
                        !String.IsNullOrEmpty(uri.UserInfo))
                        throw new Exception("Camera '" + camera.id + "' needs an HTTP(S) onvifUrl without embedded credentials.");
                }
                else if (!Uri.TryCreate(camera.url, UriKind.Absolute, out uri) ||
                    (uri.Scheme != "rtsp" && uri.Scheme != "rtsps") || String.IsNullOrEmpty(uri.Host))
                    throw new Exception("Camera '" + camera.id + "' needs an absolute rtsp:// or rtsps:// URL.");
            }
            if (sets == null || sets.Length == 0) throw new Exception("Define at least one camera set.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var set in sets)
            {
                if (set == null || String.IsNullOrWhiteSpace(set.name) || !names.Add(set.name))
                    throw new Exception("Each set needs a unique, nonempty name.");
                if (set.columns < 0 || set.columns > 10) throw new Exception("Set columns must be 0 (automatic) or 1 to 10.");
                if (set.cameras == null || set.cameras.Length < 1 || set.cameras.Length > 10)
                    throw new Exception("Each set must contain 1 to 10 camera ids.");
                if (set.cameras.Any(id => id == null || !ids.Contains(id)))
                    throw new Exception("Set '" + set.name + "' references an unknown camera id.");
                if (set.cameras.Distinct().Count() != set.cameras.Length)
                    throw new Exception("A camera can appear only once in each set.");
            }
            if (!String.IsNullOrEmpty(defaultSet) && !names.Contains(defaultSet))
                throw new Exception("defaultSet must match a set name.");
        }
    }
}
