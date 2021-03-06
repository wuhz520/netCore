﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Exceptionless;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Filter;
using Web.Model;
using Web.Server;

namespace Web
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
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            var model = Configuration.GetSection("ManageUser").Get<ManageUser>();

            //获取数据库连接字符串
            //var npgsSqlConnectionString = Configuration.GetConnectionString("NpgsSqlContext");
            //添加数据上下文
            //services.AddDbContext<NpgsSqlContext>(options =>
            //    options.UseNpgsql(npgsSqlConnectionString)
            //);

            //获取数据库连接字符串
            var sqLiteConnectionString = Configuration.GetConnectionString("SqliteContext");
            services.AddDbContext<SqliteContext>(options =>
                options.UseSqlite(sqLiteConnectionString)
            );



            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
            services.AddMvc(options =>
            {
                options.Filters.Add<HttpGlobalExceptionFilter>();
            });

            services.AddTransient<IStartupFilter, RequestSetOptionsStartupFilter>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseMiddleware<RequestSetOptionsMiddleware>();

            ExceptionlessClient.Default.Configuration.ApiKey = Configuration.GetSection("Exceptionless:ApiKey").Value;
            ExceptionlessClient.Default.Configuration.ServerUrl = Configuration.GetSection("Exceptionless:ServerUrl").Value;
            app.UseExceptionless();



            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();


            app.UseMvc(routes =>
            {
                routes.MapRoute(
                  name: "areas",
                  template: "{area:exists}/{controller=Home}/{action=Index}/{id?}"
                );

                routes.MapRoute(
                  name: "areasdefault",
                  template: "{area:exists}/{controller=default}/{action=Index}/{id?}"
                );

                routes.MapRoute(
                  name: "areasid",
                  template: "{area:exists}/{sid}/{controller=default}/{action=Index}/{id?}"
                );

                routes.MapRoute(
                    name: "home",
                    template: "{controller=Home}/{action=Index}/{id?}");

            });
        }
    }

    public class RequestSetOptionsMiddleware
    {
        private readonly RequestDelegate _next;
        private IOptions<AppOptions> _injectedOptions;

        public RequestSetOptionsMiddleware(
            RequestDelegate next, IOptions<AppOptions> injectedOptions)
        {
            _next = next;
            _injectedOptions = injectedOptions;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            Console.WriteLine("RequestSetOptionsMiddleware.Invoke");

            var option = httpContext.Request.Query["option"];

            if (!string.IsNullOrWhiteSpace(option))
            {
                _injectedOptions.Value.Option = WebUtility.HtmlEncode(option);
            }

            await _next(httpContext);
        }
    }

    public class RequestSetOptionsStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                builder.UseMiddleware<RequestSetOptionsMiddleware>();
                next(builder);
            };
        }
    }

    public class AppOptions
    {
        public string Option { get; set; }
    }
}
