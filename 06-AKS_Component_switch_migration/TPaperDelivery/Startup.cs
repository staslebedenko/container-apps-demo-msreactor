using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace TPaperDelivery
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(options =>
            {
                options.AddFilter("TPaperDelivery", LogLevel.Information);
            });

            services.AddOptions<ProjectOptions>()
                .Configure<IConfiguration>((settings, configuration) =>
                {
                    configuration.GetSection("ProjectOptions").Bind(settings);
                });

            string sqlDeliveryString = Environment.GetEnvironmentVariable("SqlDeliveryString");
            string sqlPassword = Environment.GetEnvironmentVariable("SqlPaperPassword");

            string deliveryConnectionString = new SqlConnectionStringBuilder(sqlDeliveryString) { Password = sqlPassword }.ConnectionString;

            services.AddDbContextPool<DeliveryDbContext>(options =>
            {
                if (!string.IsNullOrEmpty(deliveryConnectionString))
                {
                    options.UseSqlServer(deliveryConnectionString, providerOptions => providerOptions.EnableRetryOnFailure());
                }
            });

            services.AddSwaggerDocument();

            DeliveryDbContext.ExecuteMigrations(deliveryConnectionString);
            //services.AddApplicationInsightsTelemetry("e2b3302f-7b57-4165-8185-7b799ab67b89");
            //services.AddApplicationInsightsKubernetesEnricher();
            
            services.AddControllers().AddDapr();
            services.AddHttpClient();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseCloudEvents();

            app.UseOpenApi();
            app.UseSwaggerUi3();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapSubscribeHandler();
                endpoints.MapControllers();
            });
        }
    }
}
