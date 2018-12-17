using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Cerith;
using Consul;
using Microsoft.Extensions.Primitives;
using Winton.Extensions.Configuration.Consul;

namespace cerithapi
{
    public class Startup
    {
        private readonly CancellationTokenSource _consulCancellationSource = new CancellationTokenSource();

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            var consulUrl = Environment.GetEnvironmentVariable("CONSUL_URL");
            if (string.IsNullOrEmpty(consulUrl)) consulUrl = "http://host.docker.internal:8501/";

            var cerithConfigKey = Environment.GetEnvironmentVariable("CERITH_CONFIG_KEY");
            if (string.IsNullOrEmpty(cerithConfigKey))
            {
                Console.WriteLine("No environment variable CERITH_CONFIG_KEY found");
                builder
                    .AddJsonFile("cerith.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"cerith.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
            }
            else
            {
                Console.WriteLine($"Setting Cerith config from {cerithConfigKey} found");
                builder.AddConsul(
                    $"cerith/{cerithConfigKey}",
                    _consulCancellationSource.Token,
                    options =>
                    {
                        options.ReloadOnChange = true;
                        options.ConsulConfigurationOptions = configuration =>
                            configuration.Address = new Uri(consulUrl);
                    });
            }

            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            services.AddCerith(Configuration);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime appLifetime)
        {
            var config = app.ApplicationServices.GetService<IOptions<CerithConfiguration>>();
            if (config != null && config.Value.Collections.Any())
            {
                Console.WriteLine("Found collections");
            }
            else
            {
                Console.WriteLine("Collections missing");
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }


            app.UseHttpsRedirection();

            app.UseCerith();

            app.UseMvc();

            appLifetime.ApplicationStopping.Register(_consulCancellationSource.Cancel);
            
            ChangeToken.OnChange(
                Configuration.GetReloadToken,
                () =>
                {
                    var monitor = app.ApplicationServices.GetService<IOptionsMonitor<CerithConfiguration>>();
                    Console.WriteLine($"New config loaded : {Newtonsoft.Json.JsonConvert.SerializeObject(monitor.CurrentValue)}"); 
                }
            );

        }
    }
}
