// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Provides infrastructure for debugging Blazor WebAssembly applications.
    /// </summary>
    public static class WebAssemblyNetDebugProxyAppBuilderExtensions
    {
        /// <summary>
        /// Adds middleware needed for debugging Blazor WebAssembly applications
        /// inside Chromium dev tools.
        /// </summary>
        public static void UseWebAssemblyDebugging(this IApplicationBuilder app)
        {
            Process debugProxyProcess = null;
            var lifetime = app.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
            lifetime.ApplicationStopping.Register(() => {
                if (debugProxyProcess != null) {
                    Console.WriteLine($"Killing process with PID: {debugProxyProcess.Id}");
                    debugProxyProcess.Kill();
                }
            });

            app.Map("/_framework/debug", app =>
            {
                app.Use(async (context, next) =>
                {
                    var queryParams = HttpUtility.ParseQueryString(context.Request.QueryString.Value);
                    var browserParam = queryParams.Get("browser");
                    Uri browserUrl = null;
                    var devToolsHost = "http://localhost:9222";
                    if (browserParam != null)
                    {
                        browserUrl = new Uri(browserParam);
                        devToolsHost = $"http://{browserUrl.Host}:{browserUrl.Port}";
                    }

                    var (debugProxyBaseUrl, debugProxyProcess) = await DebugProxyLauncher.EnsureLaunchedAndGetUrl(context.RequestServices, devToolsHost);
                    var requestPath = context.Request.Path.ToString();
                    if (requestPath == string.Empty)
                    {
                        requestPath = "/";
                    }

                    switch (requestPath)
                    {
                        case "/":
                            var targetPickerUi = new TargetPickerUi(debugProxyBaseUrl, devToolsHost);
                            await targetPickerUi.Display(context);
                            break;
                        case "/ws-proxy":
                            context.Response.Redirect($"{debugProxyBaseUrl}{browserUrl.PathAndQuery}");
                            break;
                        default:
                            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                            break;
                    }
                });
            });
        }
    }
}
