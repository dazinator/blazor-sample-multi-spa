using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

public static class BlazorAppBuilderExtensions
{
    public static IFileProvider UseBlazorSpa(this IApplicationBuilder appBuilder,
        PathString requestpath,
        string staticAssetPath,
        IConfiguration configuration,
        PathString? fallbackFilePath = null)
    {
        var env = appBuilder.ApplicationServices.GetRequiredService<IWebHostEnvironment>();
        var fileProvider = env.CreateStaticAssetsFileProvider(staticAssetPath, configuration, requestpath);
        UseBlazorSpa(appBuilder, requestpath, fileProvider, fallbackFilePath);
        return fileProvider;
    }

    public static void UseBlazorSpa(this IApplicationBuilder appBuilder,
        PathString requestpath,
        IFileProvider fileProvider,
        PathString? fallbackFilePath = null)
    {
        // We have to serve blazor _framework static content up with special options.
        appBuilder.MapWhen(ctx => IsBlazorFrameworkFileRequest(requestpath, ctx), subBuilder =>
        {
            StaticFileOptions options = GetBlazorFrameworkStaticFileOptions(fileProvider);
            subBuilder.UseMiddleware<ContentEncodingNegotiator>(fileProvider);
            subBuilder.UseStaticFiles(options);
        });

        appBuilder.MapWhen(ctx => IsBlazorAppRequest(requestpath, ctx, out var remainder), subBuilder =>
        {
            // indicator that the SPA owns this URL space so safe to use DefaultFiles. See comment below.
            if (fallbackFilePath != null)
            {
                appBuilder.UseDefaultFiles(new DefaultFilesOptions() { FileProvider = fileProvider });
            }

            var spaFileOptions = new StaticFileOptions() { FileProvider = fileProvider };
            appBuilder.UseStaticFiles(spaFileOptions);

            // If we didn't match any SPA files, and if a fallbackFilePath is enforced, 
            // (i.e we aren't leaving it up to endpoint routing to decide)
            // then we need to enforce the fallback now. 
            // This maybe desirable in some apps where the SPA owns a certain URL space and the host isnt meant to handle request within that space.
            if (fallbackFilePath != null)
            {
                appBuilder.Use(next => context =>
                {
                    if (context.Response.StatusCode == (int)HttpStatusCode.NotFound)
                    {
                        context.Request.Path = fallbackFilePath.Value;
                            // Set endpoint to null so the static files middleware will handle the request.
                            context.SetEndpoint(null);
                    }

                    return next(context);

                });

                // try static files again this time it should resolve fallback file.
                // assuming that file exists.. We could verify that with the IFileProvider somewhere to catch
                // problems ahead of time?
                appBuilder.UseStaticFiles(spaFileOptions);

            }
        });



    }

    private static bool IsBlazorFrameworkFileRequest(PathString basePathPrefix, HttpContext ctx)
    {
        if (!IsBlazorAppRequest(basePathPrefix, ctx, out var remainder))
        {
            return false;
        }

        if (!remainder.StartsWithSegments("/_framework", out var remaining))
        {
            return false;
        }

        if (remaining.StartsWithSegments("/blazor.server.js"))
        {
            return false;
        }

        return true;
    }

    private static bool IsBlazorAppRequest(PathString basePathPrefix, HttpContext ctx, out PathString remainder)
    {
        remainder = ctx.Request.Path;
        if (basePathPrefix != null && basePathPrefix != "/")
        {
            if (!ctx.Request.Path.StartsWithSegments(basePathPrefix, out remainder))
            {
                return false;
            }
        }

        return true;
    }


    private static StaticFileOptions GetBlazorFrameworkStaticFileOptions(IFileProvider fileProvider)
    {
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        AddMapping(contentTypeProvider, ".dll", MediaTypeNames.Application.Octet);
        // We unconditionally map pdbs as there will be no pdbs in the output folder for
        // release builds unless BlazorEnableDebugging is explicitly set to true.
        AddMapping(contentTypeProvider, ".pdb", MediaTypeNames.Application.Octet);
        AddMapping(contentTypeProvider, ".br", MediaTypeNames.Application.Octet);
        var options = new StaticFileOptions()
        {
            FileProvider = fileProvider,
            ContentTypeProvider = contentTypeProvider,
        };

        // Static files middleware will try to use application/x-gzip as the content
        // type when serving a file with a gz extension. We need to correct that before
        // sending the file.
        options.OnPrepareResponse = fileContext =>
        {
            // At this point we mapped something from the /_framework
            fileContext.Context.Response.Headers.Append(HeaderNames.CacheControl, "no-cache");

            var requestPath = fileContext.Context.Request.Path;
            var fileExtension = Path.GetExtension(requestPath.Value);
            if (string.Equals(fileExtension, ".gz") || string.Equals(fileExtension, ".br"))
            {
                // When we are serving framework files (under _framework/ we perform content negotiation
                // on the accept encoding and replace the path with <<original>>.gz|br if we can serve gzip or brotli content
                // respectively.
                // Here we simply calculate the original content type by removing the extension and apply it
                // again.
                // When we revisit this, we should consider calculating the original content type and storing it
                // in the request along with the original target path so that we don't have to calculate it here.
                var originalPath = Path.GetFileNameWithoutExtension(requestPath.Value);
                if (contentTypeProvider.TryGetContentType(originalPath, out var originalContentType))
                {
                    fileContext.Context.Response.ContentType = originalContentType;
                }
            }
        };

        return options;
    }

    private static void AddMapping(FileExtensionContentTypeProvider provider, string name, string mimeType)
    {
        if (!provider.Mappings.ContainsKey(name))
        {
            provider.Mappings.Add(name, mimeType);
        }
    }

}



