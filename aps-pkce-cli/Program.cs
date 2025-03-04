﻿
using System.Net;
using System.Text.RegularExpressions;
using System.Text;
using System;
using System.Web;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

static class Global
{
    private static string _codeVerifier = "";

    public static string codeVerifier
    {
        get { return _codeVerifier; }
        set { _codeVerifier = value; }
    }

    private static string _accessToken = "";

    public static string AccessToken
    {
        get { return _accessToken; }
        set { _accessToken = value; }
    }

    private static string _refreshToken = "";

    public static string RefreshToken
    {
        get { return _refreshToken; }
        set { _refreshToken = value; }
    }

    private static string _clientId = "";

    public static string ClientId
    {
        get { return _clientId; }
        set { _clientId = value; }
    }

    private static string _callbackUrl = "";

    public static string CallbackURL
    {
        get { return _callbackUrl; }
        set { _callbackUrl = value; }
    }

    private static string _scopes = "";

    public static string Scopes
    {
        get { return _scopes; }
        set { _scopes = value; }
    }
}
internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Type your client id!");
        Global.ClientId = Console.ReadLine()?.ToString();
        Console.WriteLine("Type your callback url!");
        Global.CallbackURL = Console.ReadLine()?.ToString();
        string codeVerifier = RandomString(64);
        string codeChallenge = GenerateCodeChallenge(codeVerifier);
        Global.codeVerifier = codeVerifier;
        Global.Scopes = "data:read";
        redirectToLogin(codeChallenge);
        Console.WriteLine("Press ESC to stop the workflow!");
        do
        {
            while (!Console.KeyAvailable)
            {
                if (Console.ReadKey(true).Key == ConsoleKey.R)
                {
                    refreshToken();
                }
            }
        } while (Console.ReadKey(true).Key != ConsoleKey.Escape);
    }

    private static void redirectToLogin(string codeChallenge)
    {
        string[] prefixes =
        {
                Global.CallbackURL
        };
        var response = new HttpResponseMessage(HttpStatusCode.Redirect);
        //response.Headers.Location = new Uri($"https://developer.api.autodesk.com/authentication/v2/authorize?response_type=code&client_id={Global.ClientId}&redirect_uri={HttpUtility.UrlEncode(Global.CallbackURL)}&scope={Global.Scopes}&prompt=login&code_challenge={codeChallenge}&code_challenge_method=S256");
        Process.Start(new ProcessStartInfo($"https://developer.api.autodesk.com/authentication/v2/authorize?response_type=code&client_id={Global.ClientId}&redirect_uri={HttpUtility.UrlEncode(Global.CallbackURL)}&scope={Global.Scopes}&prompt=login&code_challenge={codeChallenge}&code_challenge_method=S256") { UseShellExecute = true });
        SimpleListenerExample(prefixes);
    }

    private static async void refreshToken()
    {
        try
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://developer.api.autodesk.com/authentication/v2/token"),
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                            { "scope", "data:read" },
                            { "grant_type", "refresh_token" },
                            { "refresh_token", Global.RefreshToken },
                            { "client_id", Global.ClientId }
                    }),
            };
            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                string bodystring = await response.Content.ReadAsStringAsync();
                JObject bodyjson = JObject.Parse(bodystring);
                Console.WriteLine("You can find your new token below");
                Global.AccessToken = bodyjson["access_token"].Value<string>();
                Global.RefreshToken = bodyjson["refresh_token"].Value<string>();
                Console.WriteLine(Global.AccessToken);
                Console.WriteLine("You can find your refresh token below");
                Console.WriteLine(Global.RefreshToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred!");
            Console.WriteLine(ex.Message);
        }
    }

    private static async Task SimpleListenerExample(string[] prefixes)
    {
        if (!HttpListener.IsSupported)
        {
            throw new NotSupportedException("HttpListener is not supported in this context!");
        }
        // URI prefixes are required,
        // for example "http://contoso.com:8080/index/".
        if (prefixes == null || prefixes.Length == 0)
            throw new ArgumentException("prefixes");

        // Create a listener.
        HttpListener listener = new HttpListener();
        // Add the prefixes.
        foreach (string s in prefixes)
        {
            listener.Prefixes.Add(s);
        }
        listener.Start();
        //Console.WriteLine("Listening...");
        // Note: The GetContext method blocks while waiting for a request.
        HttpListenerContext context = listener.GetContext();
        HttpListenerRequest request = context.Request;
        // Obtain a response object.
        HttpListenerResponse response = context.Response;

        try
        {
            string authCode = request.Url.Query.ToString().Split('=')[1];
            await GetPKCEToken(authCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred!");
            Console.WriteLine(ex.ToString());
        }

        // Construct a response.
        string responseString = "<HTML><BODY> You can move to the form!</BODY></HTML>";
        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
        // Get a response stream and write the response to it.
        response.ContentLength64 = buffer.Length;
        System.IO.Stream output = response.OutputStream;
        output.Write(buffer, 0, buffer.Length);
        // You must close the output stream.
        output.Close();
        listener.Stop();
    }

    private static string RandomString(int length)
    {
        Random random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());

        //Note: The use of the Random class makes this unsuitable for anything security related, such as creating passwords or tokens.Use the RNGCryptoServiceProvider class if you need a strong random number generator
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var b64Hash = Convert.ToBase64String(hash);
        var code = Regex.Replace(b64Hash, "\\+", "-");
        code = Regex.Replace(code, "\\/", "_");
        code = Regex.Replace(code, "=+$", "");
        return code;
    }

    private static async Task GetPKCEToken(string authCode)
    {
        try
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://developer.api.autodesk.com/authentication/v2/token"),
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                            { "client_id", Global.ClientId },
                            { "code_verifier", Global.codeVerifier },
                            { "code", authCode},
                            { "scope", Global.Scopes },
                            { "grant_type", "authorization_code" },
                            { "redirect_uri", Global.CallbackURL }
                    }),
            };

            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                string bodystring = await response.Content.ReadAsStringAsync();
                JObject bodyjson = JObject.Parse(bodystring);
                Console.WriteLine("You can find your token below");
                Global.AccessToken = bodyjson["access_token"].Value<string>();
                Global.RefreshToken = bodyjson["refresh_token"].Value<string>();
                Console.WriteLine(Global.AccessToken);
                Console.WriteLine("You can find your refresh token below");
                Console.WriteLine(Global.RefreshToken);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred!");
            Console.WriteLine(ex.ToString());
        }
    }
}