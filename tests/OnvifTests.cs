using System;
using System.Xml.Linq;
using Kamery;

internal static class OnvifTests
{
    internal static int Run()
    {
        const string media = "http://www.onvif.org/ver10/media/wsdl";
        const string schema = "http://www.onvif.org/ver10/schema";
        var camera = new Camera { onvifUrl = "http://camera/onvif/device_service", username = "viewer", password = "p@ss", profileToken = "sub&2" };
        int calls = 0;
        Func<string, string, string, XDocument> transport = (endpoint, action, body) =>
        {
            calls++;
            if (action.EndsWith("/GetCapabilities"))
            {
                if (endpoint != camera.onvifUrl) throw new Exception("Wrong device endpoint.");
                return XDocument.Parse("<Capabilities xmlns:t='" + schema + "'><t:Media><t:XAddr>http://camera/media</t:XAddr></t:Media></Capabilities>");
            }
            if (endpoint != "http://camera/media") throw new Exception("Media endpoint was not discovered.");
            if (action.EndsWith("/GetProfiles"))
                return XDocument.Parse("<m:GetProfilesResponse xmlns:m='" + media + "' xmlns:t='" + schema + "'>" +
                    "<m:Profiles token='metadata'/><m:Profiles token='main'><t:VideoEncoderConfiguration/></m:Profiles>" +
                    "<m:Profiles token='sub&amp;2'><t:VideoEncoderConfiguration/></m:Profiles></m:GetProfilesResponse>");
            var request = XDocument.Parse(body);
            if ((string)request.Root.Element(XName.Get("ProfileToken", media)) != (camera.profileToken ?? "main"))
                throw new Exception("Incorrect profile selection.");
            return XDocument.Parse("<MediaUri xmlns:t='" + schema + "'><t:Uri>rtsp://camera:554/live?x=1&amp;y=2</t:Uri></MediaUri>");
        };
        var resolved = new Onvif(camera, transport).Resolve();
        if (calls != 3 || resolved.PlaybackUrl() != "rtsp://viewer:p%40ss@camera:554/live?x=1&y=2")
            throw new Exception("ONVIF stream resolution failed.");
        camera.profileToken = null;
        new Onvif(camera, transport).Resolve();
        camera.profileToken = "missing";
        bool rejected = false;
        try { new Onvif(camera, transport).Resolve(); }
        catch (Exception) { rejected = true; }
        if (!rejected) throw new Exception("Missing profile was accepted.");
        rejected = false;
        try
        {
            new Onvif(camera, (e, a, b) => { throw new Exception("secret-password"); }).Resolve();
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("secret-password")) throw new Exception("ONVIF leaked response details.");
            rejected = true;
        }
        if (!rejected) throw new Exception("Expected ONVIF failure.");
        var direct = new Camera { url = "rtsp://host/live" };
        if (!Object.ReferenceEquals(new Onvif(direct).Resolve(), direct)) throw new Exception("Legacy RTSP support failed.");
        return 5;
    }
}
