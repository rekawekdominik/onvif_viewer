using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Kamery
{
    internal sealed class Onvif
    {
        private const string Device = "http://www.onvif.org/ver10/device/wsdl";
        private const string Media = "http://www.onvif.org/ver10/media/wsdl";
        private const string Schema = "http://www.onvif.org/ver10/schema";
        private readonly Camera camera;
        private readonly Func<string, string, string, XDocument> send;

        internal Onvif(Camera camera, Func<string, string, string, XDocument> transport = null)
        {
            this.camera = camera;
            send = transport ?? Send;
        }

        internal Camera Resolve()
        {
            if (String.IsNullOrWhiteSpace(camera.onvifUrl)) return camera;
            try
            {
                var caps = send(camera.onvifUrl, Device + "/GetCapabilities",
                    "<d:GetCapabilities xmlns:d=\"" + Device + "\"><d:Category>Media</d:Category></d:GetCapabilities>");
                XNamespace schema = Schema;
                var media = caps.Descendants(schema + "Media").FirstOrDefault();
                string endpoint = media == null ? null : (string)media.Element(schema + "XAddr");
                Uri address;
                if (!Uri.TryCreate(endpoint, UriKind.Absolute, out address) ||
                    (address.Scheme != "http" && address.Scheme != "https") || !String.IsNullOrEmpty(address.UserInfo))
                    throw new Exception();
                var profiles = send(endpoint, Media + "/GetProfiles", "<m:GetProfiles xmlns:m=\"" + Media + "\"/>");
                XNamespace m = Media;
                var videoProfiles = profiles.Descendants(m + "Profiles")
                    .Where(p => p.Element(schema + "VideoEncoderConfiguration") != null).ToArray();
                var selected = String.IsNullOrEmpty(camera.profileToken) ? videoProfiles.FirstOrDefault() :
                    videoProfiles.FirstOrDefault(p => (string)p.Attribute("token") == camera.profileToken);
                if (selected == null) throw new Exception();
                string token = (string)selected.Attribute("token");
                if (String.IsNullOrEmpty(token)) throw new Exception();
                var stream = send(endpoint, Media + "/GetStreamUri",
                    "<m:GetStreamUri xmlns:m=\"" + Media + "\" xmlns:t=\"" + Schema + "\">" +
                    "<m:StreamSetup><t:Stream>RTP-Unicast</t:Stream><t:Transport><t:Protocol>RTSP</t:Protocol>" +
                    "</t:Transport></m:StreamSetup><m:ProfileToken>" + SecurityElement.Escape(token) + "</m:ProfileToken></m:GetStreamUri>");
                string url = (string)stream.Descendants(schema + "Uri").FirstOrDefault();
                Uri uri;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || (uri.Scheme != "rtsp" && uri.Scheme != "rtsps") || String.IsNullOrEmpty(uri.Host))
                    throw new Exception();
                return new Camera { id = camera.id, name = camera.name, url = url, username = camera.username, password = camera.password };
            }
            catch (Exception)
            {
                // Never surface response bodies or request URLs: they can contain secrets.
                throw new Exception("ONVIF connection failed. Check the endpoint, credentials, profile token and camera clock, then click Reconnect.");
            }
        }

        private XDocument Send(string endpoint, string action, string body)
        {
            string header = "";
            if (!String.IsNullOrEmpty(camera.username))
            {
                var nonce = new byte[20];
                using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(nonce);
                string created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
                byte[] input = nonce.Concat(Encoding.UTF8.GetBytes(created + (camera.password ?? ""))).ToArray();
                string digest;
                using (var sha = SHA1.Create()) digest = Convert.ToBase64String(sha.ComputeHash(input));
                const string secext = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
                const string utility = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
                const string profile = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0";
                header = "<w:Security s:mustUnderstand=\"1\" xmlns:w=\"" + secext + "\" xmlns:u=\"" + utility + "\">" +
                    "<w:UsernameToken><w:Username>" + SecurityElement.Escape(camera.username) + "</w:Username>" +
                    "<w:Password Type=\"" + profile + "#PasswordDigest\">" + digest + "</w:Password>" +
                    "<w:Nonce EncodingType=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary\">" +
                    Convert.ToBase64String(nonce) + "</w:Nonce><u:Created>" + created + "</u:Created></w:UsernameToken></w:Security>";
            }
            byte[] data = Encoding.UTF8.GetBytes("<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\"><s:Header>" +
                header + "</s:Header><s:Body>" + body + "</s:Body></s:Envelope>");
            var request = (HttpWebRequest)WebRequest.Create(endpoint);
            request.Method = "POST";
            request.ContentType = "application/soap+xml; charset=utf-8; action=\"" + action + "\"";
            request.Timeout = 8000; request.ReadWriteTimeout = 8000;
            request.AllowAutoRedirect = false;
            request.Proxy = null;
            request.Credentials = new NetworkCredential(camera.username ?? "", camera.password ?? "");
            request.ContentLength = data.Length;
            using (var output = request.GetRequestStream()) output.Write(data, 0, data.Length);
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var input = response.GetResponseStream())
            using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2097152 }))
            {
                var document = XDocument.Load(reader);
                if (document.Descendants(XName.Get("Fault", "http://www.w3.org/2003/05/soap-envelope")).Any()) throw new Exception();
                return document;
            }
        }
    }
}
