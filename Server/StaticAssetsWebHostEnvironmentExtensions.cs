using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

public static class StaticAssetsWebHostEnvironmentExtensions
{
    private static Lazy<List<StaticWebAssetsReader.ContentRootMapping>> _contentRootMappings = null;
    private static object _lock = new object();

    public static IFileProvider CreateStaticAssetsFileProvider(this IWebHostEnvironment environment, string staticAssetBasePath, IConfiguration configuration, string requestBasePath = null)
    {
        if (environment.IsDevelopment())
        {
            // mappings won't change at runtime in development so lazy initialising on first call..
            // using locking just in case called concurrently we don't init mappings more than once.
            if (_contentRootMappings == null)
            {
                lock (_lock)
                {
                    if (_contentRootMappings == null)
                    {
                        _contentRootMappings = new Lazy<List<StaticWebAssetsReader.ContentRootMapping>>(() => GetStaticWebAssetsManifest(environment, configuration));
                    }
                }
            }
        }
                
        if (string.IsNullOrWhiteSpace(requestBasePath))
        {
            requestBasePath = staticAssetBasePath;
        }

        var mappings = _contentRootMappings?.Value;
        var fp = GetWebRootFileProvider(environment.WebRootPath, staticAssetBasePath, requestBasePath, mappings);
        return fp;
    }

    internal static List<StaticWebAssetsReader.ContentRootMapping> GetStaticWebAssetsManifest(IWebHostEnvironment environment, IConfiguration configuration)
    {
        using (var manifest = StaticWebAssetsLoader.ResolveManifest(environment, configuration))
        {
            if (manifest != null)
            {
                var mappings = StaticWebAssetsReader.Parse(manifest).ToList();
                return mappings;
            }
            return null;
        }
    }

    private static IFileProvider GetWebRootFileProvider(string webRootPath, string staticAssetBasePath, string requestBasePath, List<StaticWebAssetsReader.ContentRootMapping> contentMappings = null)
    {
        var contentRoots = GetSpaContentRootPaths(webRootPath, staticAssetBasePath, contentMappings).ToArray();
        if (!contentRoots.Any())
        {
            return new NullFileProvider();
        }

        if (contentRoots.Length == 1)
        {
            IFileProvider fp = CreateFileProvider(contentRoots[0], requestBasePath);
            return fp;
        }

        var composite = new CompositeFileProvider(contentRoots.Select(c => CreateFileProvider(c, requestBasePath)));
        return composite;
    }


    private static IEnumerable<string> GetSpaContentRootPaths(string webRootPath, string staticAssetPath, List<StaticWebAssetsReader.ContentRootMapping> contentMappings = null)
    {
        // if no content mappings, then expect spa content
        // to be published under the WebRootPath directory of the host app.
        string spaContentPath = string.Empty;
        if (contentMappings == null)
        {
            spaContentPath = Path.Combine(webRootPath, staticAssetPath);
            yield return spaContentPath;
        }
        else
        {
            var appMappings = contentMappings.FindAll(a => a.BasePath == staticAssetPath);
            foreach (var mapping in appMappings)
            {
                if (string.IsNullOrWhiteSpace(mapping.Path))
                {
                    throw new Exception($"Unable to locate static asset mapping for static asset path: {staticAssetPath}");
                }

                yield return mapping.Path;
            }
        }
    }

    private static IFileProvider CreateFileProvider(string contentRoot, string basePath)
    {
        return new StaticWebAssetsFileProvider(basePath, contentRoot);
    }
}



