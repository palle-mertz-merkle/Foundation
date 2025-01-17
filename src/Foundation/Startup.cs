﻿using EPiServer.Data;
using EPiServer.Framework.Web.Resources;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using EPiServer.Web.Routing;
using Foundation.Features.Checkout.Payments;
using Foundation.Infrastructure;
using Foundation.Infrastructure.Cms.ModelBinders;
using Foundation.Infrastructure.Cms.Users;
using Foundation.Infrastructure.Display;
using Mediachase.Commerce.Anonymous;
using Mediachase.Commerce.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Foundation
{
    public class Startup
    {
        private readonly IWebHostEnvironment _webHostingEnvironment;
        private readonly IConfiguration _configuration;

        public Startup(IWebHostEnvironment webHostingEnvironment, IConfiguration configuration)
        {
            _webHostingEnvironment = webHostingEnvironment;
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<DataAccessOptions>(o =>
            {
                o.ConnectionStrings.Add(new ConnectionStringOptions
                {
                    ConnectionString = _configuration.GetConnectionString("EcfSqlConnection"),
                    Name = "EcfSqlConnection"
                });
            });

            services.AddCmsAspNetIdentity<SiteUser>(o =>
            {
                if (string.IsNullOrEmpty(o.ConnectionStringOptions?.ConnectionString))
                {
                    o.ConnectionStringOptions = new ConnectionStringOptions
                    {
                        Name = "EcfSqlConnection",
                        ConnectionString = _configuration.GetConnectionString("EcfSqlConnection")
                    };
                }
            });

            //UI
            if (_webHostingEnvironment.IsDevelopment())
            {
                services.Configure<ClientResourceOptions>(uiOptions =>
                {
                    uiOptions.Debug = true;
                });
            }

            services.AddMvc(o =>
            {
                o.Conventions.Add(new FeatureConvention());
                o.ModelBinderProviders.Insert(0, new DecimalModelBinderProvider());
                o.ModelBinderProviders.Insert(0, new PaymentModelBinderProvider());
            })
            .AddRazorOptions(ro => ro.ViewLocationExpanders.Add(new FeatureViewLocationExpander()));

            services.AddCommerce();
            services.AddFind();
            services.AddDisplay();
            services.TryAddEnumerable(Microsoft.Extensions.DependencyInjection.ServiceDescriptor.Singleton(typeof(IFirstRequestInitializer), typeof(ContentInstaller)));
            services.AddDetection();

            //site specific
            services.AddEmbeddedLocalization<Startup>();
            services.Configure<OrderOptions>(o => o.DisableOrderDataLocalization = true);

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAnonymousId();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(name: "Default", pattern: "{controller}/{action}/{id?}");
                endpoints.MapControllers();
                endpoints.MapContent();
            });
        }
    }
}
