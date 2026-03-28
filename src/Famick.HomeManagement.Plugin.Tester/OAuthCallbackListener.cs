using System.Net;
using System.Web;

namespace Famick.HomeManagement.Plugin.Tester;

/// <summary>
/// Lightweight HTTP listener that captures OAuth callback codes on localhost.
/// Starts a temporary server, waits for the redirect, extracts the authorization
/// code, and returns a success page to the browser.
/// </summary>
internal static class OAuthCallbackListener
{
    public const string RedirectUri = "http://localhost:8080/callback";

    /// <summary>
    /// Starts an HTTP listener on localhost:8080/callback and waits for the OAuth
    /// redirect containing the authorization code.
    /// </summary>
    /// <returns>The authorization code from the callback query string.</returns>
    public static async Task<string> WaitForCallbackAsync(CancellationToken ct)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:8080/callback/");
        listener.Start();

        try
        {
            // Wait for the callback request (or cancellation)
            var contextTask = listener.GetContextAsync();
            using var reg = ct.Register(() => listener.Stop());

            HttpListenerContext context;
            try
            {
                context = await contextTask;
            }
            catch (ObjectDisposedException) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            var query = context.Request.Url?.Query ?? "";
            var queryParams = HttpUtility.ParseQueryString(query);
            var code = queryParams["code"];
            var error = queryParams["error"];

            // Send response to the browser
            string responseHtml;
            int statusCode;

            if (!string.IsNullOrEmpty(error))
            {
                statusCode = 400;
                var errorDesc = queryParams["error_description"] ?? error;
                responseHtml = $"""
                    <html><body style="font-family:sans-serif;text-align:center;padding:40px">
                    <h2 style="color:#c00">Authentication Failed</h2>
                    <p>{WebUtility.HtmlEncode(errorDesc)}</p>
                    <p style="color:#666">You can close this tab.</p>
                    </body></html>
                    """;
            }
            else if (string.IsNullOrEmpty(code))
            {
                statusCode = 400;
                responseHtml = """
                    <html><body style="font-family:sans-serif;text-align:center;padding:40px">
                    <h2 style="color:#c00">Missing Authorization Code</h2>
                    <p>No 'code' parameter found in the callback.</p>
                    <p style="color:#666">You can close this tab.</p>
                    </body></html>
                    """;
            }
            else
            {
                statusCode = 200;
                responseHtml = """
                    <html><body style="font-family:sans-serif;text-align:center;padding:40px">
                    <h2 style="color:#080">Authentication Successful</h2>
                    <p>Authorization code received. You can close this tab and return to the CLI.</p>
                    </body></html>
                    """;
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/html";
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, ct);
            context.Response.Close();

            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"OAuth error: {queryParams["error_description"] ?? error}");

            if (string.IsNullOrEmpty(code))
                throw new InvalidOperationException("No authorization code received in callback.");

            return code;
        }
        finally
        {
            listener.Stop();
        }
    }
}
