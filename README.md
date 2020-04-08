## Blazor - Select different wasms to serve on different paths

This example shows multiple Blazor wasm projects referenced from the host, each client project
has it's own `StaticWebAssetBasePath` to prevent conflicts, e.g:

```
 <StaticWebAssetBasePath>.private/spa2</StaticWebAssetBasePath>
```

Then the host, gets to serve the one it chooses thanks to some new extensions methods that I produced via taking a lot of the existing blazor extension methods and setup and refactoring it slightly and adding a couple of new extension methods:

Startup.cs:


```
  // tenant A browse on port 5000
  app.MapWhen((a) => a.Request.Host.Port == 5000,
                (app) =>
                {          
                    var files = app.UseBlazorSpa("/", ".private/spa1", Configuration);                   

                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapFallbackToFile("index.html",
                            new StaticFileOptions() { FileProvider = files });
                    });
                });

 // tenant B browse on port 5001
  app.MapWhen((a) => a.Request.Host.Port == 5001,
               (app) =>
               {
                   var files = app.UseBlazorSpa("/", ".private/spa2", Configuration);

                   app.UseRouting();
                   app.UseEndpoints(endpoints =>
                   {
                       endpoints.MapControllers();
                       endpoints.MapFallbackToFile("index.html",
                           new StaticFileOptions() { FileProvider = files });
                   });
               });     

```

`app.UseBlazorSpa` does the magic, and there are a couple of overloads, the one shown above creates the file provider to source the spa static files, and makes those files available on the path you choose - in this sample that is the root "/".

Note: see https://github.com/dotnet/aspnetcore/issues/20642 and https://github.com/dotnet/aspnetcore/issues/20605








