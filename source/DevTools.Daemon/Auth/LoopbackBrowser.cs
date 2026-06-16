using System.Diagnostics;
using System.Net;
using Duende.IdentityModel.OidcClient.Browser;
namespace DevTools.Daemon.Auth;

public sealed class LoopbackBrowser(AuthOptions authOptions) : IBrowser
{
    public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken ct = default)
    {
        HttpListener? listener = null;
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add(authOptions.UriPrefix);
            listener.Start();

            Process.Start(new ProcessStartInfo(options.StartUrl) { UseShellExecute = true });

            var result = await Task.Run(() => WaitForCallbackAsync(listener, ct), ct).ConfigureAwait(false);
            return new BrowserResult { Response = result, ResultType = BrowserResultType.Success };
        }
        catch (OperationCanceledException)
        {
            return new BrowserResult { ResultType = BrowserResultType.Timeout };
        }
        catch (Exception ex)
        {
            return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = ex.Message };
        }
        finally
        {
            listener?.Close();
        }
    }

    private static async Task<string> WaitForCallbackAsync(HttpListener listener, CancellationToken ct)
    {
        // ReSharper disable once UseAwaitUsing
        using var reg = ct.Register(listener.Close);
        var context = await listener.GetContextAsync().ConfigureAwait(false);

        var callbackUrl = context.Request.Url!.ToString();
        var hasError = context.Request.QueryString["error"] is not null;
        var body = hasError ? ErrorResponseBody : ResponseBody;

        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body, ct).ConfigureAwait(false);
        context.Response.Close();

        return callbackUrl;
    }

    private static readonly byte[] ErrorResponseBody = """
                                                      <html>
                                                      <head>
                                                        <meta charset="utf-8">
                                                        <meta name="viewport" content="width=device-width, initial-scale=1">
                                                        <title>DevTools — Sign in denied</title>
                                                        <style>
                                                          * { box-sizing: border-box; margin: 0; padding: 0; }
                                                          body {
                                                            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
                                                            background: #f5f5f5;
                                                            min-height: 100vh;
                                                            display: flex;
                                                            align-items: center;
                                                            justify-content: center;
                                                            padding: 2rem;
                                                          }
                                                          .card {
                                                            background: #fff;
                                                            border: 1px solid #e5e5e5;
                                                            border-radius: 12px;
                                                            padding: 2.5rem 3rem;
                                                            max-width: 380px;
                                                            width: 100%;
                                                            text-align: center;
                                                          }
                                                          .icon {
                                                            width: 60px;
                                                            height: 60px;
                                                            border-radius: 50%;
                                                            background: #fee2e2;
                                                            display: flex;
                                                            align-items: center;
                                                            justify-content: center;
                                                            margin: 0 auto 1.25rem;
                                                          }
                                                          .icon svg { width: 26px; height: 26px; stroke: #dc2626; }
                                                          h1 { font-size: 19px; font-weight: 500; color: #111; margin-bottom: 0.5rem; }
                                                          p { font-size: 14px; color: #888; line-height: 1.6; }
                                                        </style>
                                                      </head>
                                                      <body>
                                                        <div class="card">
                                                          <div class="icon">
                                                            <svg fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor">
                                                              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12"/>
                                                            </svg>
                                                          </div>
                                                          <h1>Sign in denied</h1>
                                                          <p>Authentication was cancelled or denied. You can close this tab and try again.</p>
                                                        </div>
                                                      </body>
                                                      </html>
                                                      """u8.ToArray();

    private static readonly byte[] ResponseBody= """
                                                 <html>
                                                 <head>
                                                   <meta charset="utf-8">
                                                   <meta name="viewport" content="width=device-width, initial-scale=1">
                                                   <title>DevTools — Authentication</title>
                                                   <style>
                                                     * { box-sizing: border-box; margin: 0; padding: 0; }
                                                     body {
                                                       font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
                                                       background: #f5f5f5;
                                                       min-height: 100vh;
                                                       display: flex;
                                                       align-items: center;
                                                       justify-content: center;
                                                       padding: 2rem;
                                                     }
                                                     .card {
                                                       background: #fff;
                                                       border: 1px solid #e5e5e5;
                                                       border-radius: 12px;
                                                       padding: 2.5rem 3rem;
                                                       max-width: 380px;
                                                       width: 100%;
                                                       text-align: center;
                                                     }
                                                     .icon {
                                                       width: 60px;
                                                       height: 60px;
                                                       border-radius: 50%;
                                                       background: #e0f2fe;
                                                       display: flex;
                                                       align-items: center;
                                                       justify-content: center;
                                                       margin: 0 auto 1.25rem;
                                                     }
                                                     .icon svg { width: 26px; height: 26px; stroke: #16a34a; }
                                                     h1 { font-size: 19px; font-weight: 500; color: #111; margin-bottom: 0.5rem; }
                                                     p { font-size: 14px; color: #888; line-height: 1.6; }
                                                   </style>
                                                 </head>
                                                 <body>
                                                   <div class="card">
                                                     <div class="icon">
                                                       <svg fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor">
                                                         <path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7"/>
                                                       </svg>
                                                     </div>
                                                     <h1>Signed in successfully</h1>
                                                     <p>You can close this tab.</p>
                                                   </div>
                                                 </body>
                                                 </html>
                                                 """u8.ToArray();
}
