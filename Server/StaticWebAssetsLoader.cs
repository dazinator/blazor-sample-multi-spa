using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

public class StaticWebAssetsLoader
{
    internal const string StaticWebAssetsManifestName = "Microsoft.AspNetCore.StaticWebAssets.xml";
        
    /// <summary>
    /// Configure the <see cref="IWebHostEnvironment"/> to use static web assets.
    /// </summary>
    /// <param name="environment">The application <see cref="IWebHostEnvironment"/>.</param>
    /// <param name="configuration">The host <see cref="IConfiguration"/>.</param>
    public static void UseStaticWebAssets(IWebHostEnvironment environment, IConfiguration configuration)
    {
        using (var manifest = ResolveManifest(environment, configuration))
        {
            if (manifest != null)
            {
                UseStaticWebAssetsCore(environment, manifest);
            }
        }
    }

    internal static void UseStaticWebAssetsCore(IWebHostEnvironment environment, Stream manifest)
    {
        var staticWebAssetsFileProvider = new List<IFileProvider>();
        var webRootFileProvider = environment.WebRootFileProvider;

        var additionalFiles = StaticWebAssetsReader.Parse(manifest)
            .Where(cr => !IsExcludedPath(cr.BasePath))
            .Select(cr => new StaticWebAssetsFileProvider(cr.BasePath, cr.Path))
            .OfType<IFileProvider>() // Upcast so we can insert on the resulting list.
            .ToList();

        if (additionalFiles.Count == 0)
        {
            return;
        }
        else
        {
            additionalFiles.Insert(0, webRootFileProvider);
            environment.WebRootFileProvider = new CompositeFileProvider(additionalFiles);
        }
    }

    private static bool IsExcludedPath(string basePath)
    {
        var split = basePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (split.Any(a => a.StartsWith(".")))
        {
            return true;
        }

        return false;
    }

    internal static Stream ResolveManifest(IWebHostEnvironment environment, IConfiguration configuration)
    {
        try
        {
            var manifestPath = configuration.GetValue<string>(WebHostDefaults.StaticWebAssetsKey);
            var filePath = manifestPath ?? ResolveRelativeToAssembly(environment);

            if (File.Exists(filePath))
            {
                return File.OpenRead(filePath);
            }
            else
            {
                // A missing manifest might simply mean that the feature is not enabled, so we simply
                // return early. Misconfigurations will be uncommon given that the entire process is automated
                // at build time.
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveRelativeToAssembly(IWebHostEnvironment environment)
    {
        var assembly = Assembly.Load(environment.ApplicationName);
        return Path.Combine(Path.GetDirectoryName(GetAssemblyLocation(assembly)), $"{environment.ApplicationName}.StaticWebAssets.xml");
    }

    internal static string GetAssemblyLocation(Assembly assembly)
    {
        if (Uri.TryCreate(assembly.CodeBase, UriKind.Absolute, out var result) &&
            result.IsFile && string.IsNullOrWhiteSpace(result.Fragment))
        {
            return result.LocalPath;
        }

        return assembly.Location;
    }
}



