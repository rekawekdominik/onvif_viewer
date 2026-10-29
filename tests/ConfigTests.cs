using System;
using System.IO;
using System.Linq;
using Kamery;

internal static class ConfigTests
{
    private static int checks;
    private static void ExpectUrl(Camera camera, string expected)
    {
        if (camera.PlaybackUrl() != expected) throw new Exception("Credential URL construction failed.");
        checks++;
    }
    private static void Reject(Action<Config> change, string example)
    {
        var config = Config.Load(example);
        change(config);
        try { config.Validate(); }
        catch (Exception) { checks++; return; }
        throw new Exception("Invalid configuration was accepted.");
    }

    private static int Main(string[] args)
    {
        try
        {
            string example = args[0];
            Config.Load(example); checks++;
            Reject(c => c.cameras[1].id = c.cameras[0].id, example);
            Reject(c => { c.cameras[0].onvifUrl = null; c.cameras[0].url = "https://example.com"; }, example);
            Reject(c => c.cameras[0].onvifUrl = "rtsp://host/live", example);
            Reject(c => c.cameras[0].onvifUrl = "http://user:password@host/onvif/device_service", example);
            Reject(c => c.sets[0].cameras = new[] { "missing" }, example);
            Reject(c => c.sets[0].cameras = new[] { "front", "front" }, example);
            Reject(c => c.sets[0].cameras = new string[0], example);
            Reject(c => c.sets[0].cameras = Enumerable.Repeat("front", 11).ToArray(), example);
            Reject(c => c.sets[0].columns = -1, example);
            Reject(c => c.defaultSet = "missing", example);
            Reject(c => c.networkCachingMs = 0, example);
            Reject(c => c.reconnectSeconds = 0, example);
            Reject(c => c.cameras = null, example);
            Reject(c => c.sets = null, example);
            Reject(c => c.cameras[0].password = "without-user", example);
            Reject(c => c.cameras[0].username = "bad\0user", example);
            Reject(c => { c.cameras[0].username = "viewer"; c.cameras[0].password = "bad\0password"; }, example);
            ExpectUrl(new Camera { url = "rtsp://host/live?token=a%2Fb" }, "rtsp://host/live?token=a%2Fb");
            ExpectUrl(new Camera { url = "rtsp://old:pass@host/live", username = "", password = "" }, "rtsp://old:pass@host/live");
            ExpectUrl(new Camera { url = "rtsp://host:554/live?token=a%2Fb", username = "view@er", password = "p:a/s?#% " },
                "rtsp://view%40er:p%3Aa%2Fs%3F%23%25%20@host:554/live?token=a%2Fb");
            ExpectUrl(new Camera { url = "rtsp://old:pass@[::1]:554/live", username = "new", password = "fresh" }, "rtsp://new:fresh@[::1]:554/live");
            ExpectUrl(new Camera { url = "rtsps://host", username = "viewer" }, "rtsps://viewer:@host");
            ExpectUrl(new Camera { url = "rtsp://host/live", username = "\u7528\u6237", password = "\u00e9" }, "rtsp://%E7%94%A8%E6%88%B7:%C3%A9@host/live");
            var ten = Config.Load(example);
            ten.cameras = Enumerable.Range(1, 10).Select(i => new Camera { id = "c" + i, name = "Camera " + i, url = "rtsp://127.0.0.1/" + i }).ToArray();
            ten.sets = new[] { new CameraSet { name = "Ten", cameras = ten.cameras.Select(c => c.id).ToArray() } };
            ten.defaultSet = "Ten"; ten.Validate(); checks++;
            var unicode = Config.Load(example);
            unicode.cameras[0].name = "Wejście 庭"; unicode.Validate(); checks++;
            string temporary = Path.GetTempFileName();
            try
            {
                File.WriteAllText(temporary, "{broken");
                bool rejected = false;
                try { Config.Load(temporary); } catch (Exception) { rejected = true; }
                if (!rejected) throw new Exception("Malformed JSON was accepted.");
                checks++;
                File.WriteAllText(temporary, "{\"cameras\":[{\"url\":\"rtsp://secret:password@host/live\"}],\"networkCachingMs\":\"invalid\"}");
                rejected = false;
                try { Config.Load(temporary); }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("secret") || ex.Message.Contains("password")) throw new Exception("Error exposed credentials.");
                    rejected = true;
                }
                if (!rejected) throw new Exception("Invalid field type was accepted.");
                checks++;
            }
            finally { File.Delete(temporary); }
            checks += OnvifTests.Run();
            Console.WriteLine("Passed {0} configuration and ONVIF checks.", checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
