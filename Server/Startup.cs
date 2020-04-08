using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System;

namespace UseBlazorSpaSample.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            // app.UseHttpsRedirection(); // commented so can browse on port http 5000 and 5001 for demo without being redirected to https 5001
            // app.UseBlazorFrameworkFiles();
            //  app.UseStaticFiles();
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

            app.MapWhen((a) => a.Request.Host.Port == 5001,
               (app) =>
               {
                   // Shows another way
                   var fileProvider = env.CreateStaticAssetsFileProvider(".private/spa2", Configuration, "/");
                   app.UseBlazorSpa("/", fileProvider);                 

                   app.UseRouting();
                   app.UseEndpoints(endpoints =>
                   {
                       endpoints.MapControllers();
                       endpoints.MapFallbackToFile("index.html",
                           new StaticFileOptions() { FileProvider = fileProvider });
                   });
               });          
        }   }
}
