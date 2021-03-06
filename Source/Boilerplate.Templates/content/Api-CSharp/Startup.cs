﻿namespace ApiTemplate
{
    using System.IO.Compression;
    using System.Linq;
#if (CORS)
    using ApiTemplate.Constants;
#endif
    using ApiTemplate.Settings;
    using Boilerplate.AspNetCore;
    using Boilerplate.AspNetCore.Filters;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Formatters;
    using Microsoft.AspNetCore.Mvc.Infrastructure;
    using Microsoft.AspNetCore.Mvc.Routing;
    using Microsoft.AspNetCore.ResponseCompression;
#if (HttpsEverywhere)
    using Microsoft.AspNetCore.Rewrite;
#endif
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// The main start-up class for the application.
    /// </summary>
    public partial class Startup
    {
        /// <summary>
        /// Gets or sets the application configuration, where key value pair settings are stored. See
        /// http://docs.asp.net/en/latest/fundamentals/configuration.html
        /// </summary>
        private readonly IConfigurationRoot configuration;

        /// <summary>
        /// The environment the application is running under. This can be Development, Staging or Production by default.
        /// To set the hosting environment on Windows:
        /// 1. On your server, right click 'Computer' or 'My Computer' and click on 'Properties'.
        /// 2. Go to 'Advanced System Settings'.
        /// 3. Click on 'Environment Variables' in the Advanced tab.
        /// 4. Add a new System Variable with the name 'ASPNETCORE_ENVIRONMENT' and value of Production, Staging or
        /// whatever you want. See http://docs.asp.net/en/latest/fundamentals/environments.html
        /// </summary>
        private readonly IHostingEnvironment hostingEnvironment;
#if (HttpsEverywhere)

        /// <summary>
        /// Gets or sets the port to use for HTTPS. Only used in the development environment.
        /// </summary>
        private readonly int? sslPort;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="Startup"/> class.
        /// </summary>
        /// <param name="hostingEnvironment">The environment the application is running under. This can be Development,
        /// Staging or Production by default.</param>
        public Startup(IHostingEnvironment hostingEnvironment)
        {
            this.hostingEnvironment = hostingEnvironment;

            this.configuration = new ConfigurationBuilder()
                .SetBasePath(this.hostingEnvironment.ContentRootPath)
                // Add configuration from the config.json file.
                .AddJsonFile("config.json")
                // Add configuration from an optional config.development.json, config.staging.json or
                // config.production.json file, depending on the environment. These settings override the ones in the
                // config.json file.
                .AddJsonFile($"config.{this.hostingEnvironment.EnvironmentName}.json", optional: true)
                // This reads the configuration keys from the secret store. This allows you to store connection strings
                // and other sensitive settings, so you don't have to check them into your source control provider.
                // Only use this in Development, it is not intended for Production use. See
                // http://docs.asp.net/en/latest/security/app-secrets.html
                .AddIf(
                    this.hostingEnvironment.IsDevelopment(),
                    x => x.AddUserSecrets())
                // Add configuration specific to the Development, Staging or Production environments. This config can
                // be stored on the machine being deployed to or if you are using Azure, in the cloud. These settings
                // override the ones in all of the above config files.
                // Note: To set environment variables for debugging navigate to:
                // Project Properties -> Debug Tab -> Environment Variables
                // Note: To get environment variables for the machine use the following command in PowerShell:
                // [System.Environment]::GetEnvironmentVariable("[VARIABLE_NAME]", [System.EnvironmentVariableTarget]::Machine)
                // Note: To set environment variables for the machine use the following command in PowerShell:
                // [System.Environment]::SetEnvironmentVariable("[VARIABLE_NAME]", "[VARIABLE_VALUE]", [System.EnvironmentVariableTarget]::Machine)
                // Note: Environment variables use a colon separator e.g. You can override the site title by creating a
                // variable named AppSettings:SiteTitle. See http://docs.asp.net/en/latest/security/app-secrets.html
                .AddEnvironmentVariables()
#if (ApplicationInsights)
                // Push telemetry data through the Azure Application Insights pipeline faster in the development and
                // staging environments, allowing you to view results immediately.
                .AddApplicationInsightsSettings(developerMode: !this.hostingEnvironment.IsProduction())
#endif
                .Build();
#if (HttpsEverywhere)

            if (this.hostingEnvironment.IsDevelopment())
            {
                var launchConfiguration = new ConfigurationBuilder()
                    .SetBasePath(this.hostingEnvironment.ContentRootPath)
                    .AddJsonFile(@"Properties\launchSettings.json")
                    .Build();
                this.sslPort = launchConfiguration.GetValue<int>("iisSettings:iisExpress:sslPort");
            }
#endif
        }

        /// <summary>
        /// Configures the services to add to the ASP.NET MVC 6 Injection of Control (IoC) container. This method gets
        /// called by the ASP.NET runtime. See
        /// http://blogs.msdn.com/b/webdev/archive/2014/06/17/dependency-injection-in-asp-net-vnext.aspx
        /// </summary>
        /// <param name="services">The services collection or IoC container.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services
#if (ApplicationInsights)
                // Add Azure Application Insights data collection services to the services container.
                .AddApplicationInsightsTelemetry(this.configuration)
#endif
                .AddCaching()
                .AddOptions(this.configuration)
                .AddRouting(
                    options =>
                    {
                        // Improve SEO by stopping duplicate URL's due to case differences or trailing slashes.
                        // See http://googlewebmastercentral.blogspot.co.uk/2010/04/to-slash-or-not-to-slash.html
                        // All generated URL's should append a trailing slash.
                        options.AppendTrailingSlash = true;
                        // All generated URL's should be lower-case.
                        options.LowercaseUrls = true;
                    })
#if (CORS)
                // Add cross-origin resource sharing (CORS) services. See https://docs.asp.net/en/latest/security/cors.html
                .AddCors(
                    options =>
                    {
                        // Create named CORS policies here which you can consume using
                        // application.UseCors("PolicyName") or a [EnableCors("PolicyName")] attribute on your
                        // controller or action.
                        options.AddPolicy(
                            CorsPolicyName.AllowAny,
                            builder => builder
                                .AllowAnyOrigin()
                                .AllowAnyMethod()
                                .AllowAnyHeader());
                    })
#endif
                .AddResponseCaching()
                // Add response compression to enable GZIP compression.
                .AddResponseCompression(
                    options =>
                    {
#if (HttpsEverywhere)
                        options.EnableForHttps = true;
#endif
                        // Add additional MIME types (other than the built in defaults) to enable GZIP compression for.
                        var responseCompressionSettings = configuration.GetSection<ResponseCompressionSettings>(
                            nameof(ResponseCompressionSettings));
                        options.MimeTypes = ResponseCompressionDefaults
                            .MimeTypes
                            .Concat(responseCompressionSettings.MimeTypes);
                    })
                .Configure<GzipCompressionProviderOptions>(
                    options => options.Level = CompressionLevel.Optimal)
                // Add useful interface for accessing the ActionContext outside a controller.
                .AddSingleton<IActionContextAccessor, ActionContextAccessor>()
                // Add useful interface for accessing the HttpContext outside a controller.
                .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                // Add useful interface for accessing the IUrlHelper outside a controller.
                .AddScoped<IUrlHelper>(x => x
                    .GetRequiredService<IUrlHelperFactory>()
                    .GetUrlHelper(x.GetRequiredService<IActionContextAccessor>().ActionContext))
                // Add many MVC services to the services container.
                .AddMvcCore(
                    options =>
                    {
                        // Controls how controller actions cache content from the config.json file.
                        var cacheProfileSettings = this.configuration.GetSection<CacheProfileSettings>();
                        foreach (var keyValuePair in cacheProfileSettings.CacheProfiles)
                        {
                            options.CacheProfiles.Add(keyValuePair);
                        }

                        if (this.hostingEnvironment.IsDevelopment())
                        {
                            // Lets you pass a format parameter into the query string to set the response type:
                            // e.g. ?format=application/json. Good for debugging.
                            options.Filters.Add(new FormatFilterAttribute());
                        }

                        // Check model state for null or invalid models and automatically return a 400 Bad Request.
                        options.Filters.Add(new ValidateModelStateAttribute());

                        options.OutputFormatters.RemoveType<StreamOutputFormatter>();
                        options.OutputFormatters.RemoveType<StringOutputFormatter>();

                        options.ReturnHttpNotAcceptable = true;
                    })
                .AddApiExplorer()
                .AddAuthorization()
                .AddFormatterMappings()
                .AddDataAnnotations()
                .AddJsonFormatters()
                .AddJsonOptions(
                    options =>
                    {
                        options.SerializerSettings.DateParseHandling = DateParseHandling.DateTimeOffset;
                        options.SerializerSettings.Converters.Add(new StringEnumConverter());
                    })
#if (CORS)
                .AddCors()
#endif
#if (DataContractSerializer)
                // Adds the XML input and output formatter using the DataContractSerializer.
                .AddXmlDataContractSerializerFormatters()
#elif (XmlSerializer)
                // Adds the XML input and output formatter using the XmlSerializer.
                .AddXmlSerializerFormatters()
#endif
                .Services
#if (Swagger)
                .AddSwagger()
#endif
                .AddCommands()
                .AddRepositories()
                .AddTranslators();
        }

        /// <summary>
        /// Configures the application and HTTP request pipeline. Configure is called after ConfigureServices is
        /// called by the ASP.NET runtime.
        /// </summary>
        /// <param name="application">The application.</param>
        /// <param name="loggerfactory">The logger factory.</param>
        public void Configure(IApplicationBuilder application, ILoggerFactory loggerfactory)
        {
            // Configure application logging. See http://docs.asp.net/en/latest/fundamentals/logging.html
            loggerfactory
                // Log to Serilog (A great logging framework). See https://github.com/serilog/serilog-framework-logging.
                // Add the Serilog package to project.json before uncommenting the line below.
                // .AddSerilog()
                .AddIf(
                    hostingEnvironment.IsDevelopment(),
                    x => x
                        .AddConsole(configuration.GetSection("Logging"))
                        .AddDebug());

            application
                // Removes the Server HTTP header from the HTTP response for marginally better security and performance.
                .UseNoServerHttpHeader()
#if (HttpsEverywhere)
                // Require HTTPS to be used across the whole site. Also set a custom port to use for SSL in
                // Development. The port number to use is taken from the launchSettings.json file which Visual
                // Studio uses to start the application.
                .UseRewriter(
                    new RewriteOptions().AddRedirectToHttps(StatusCodes.Status301MovedPermanently, this.sslPort))
#endif
#if (CORS)
                .UseCors(CorsPolicyName.AllowAny)
#endif
                .UseResponseCaching()
                .UseResponseCompression()
                .UseStaticFilesWithCacheControl(this.configuration)
                .UseIf(
                    this.hostingEnvironment.IsDevelopment(),
                    x => x
                        .UseDebugging()
                        .UseDeveloperErrorPages())
#if (HttpsEverywhere)
                .UseStrictTransportSecurityHttpHeader()
                .UsePublicKeyPinsHttpHeader()
#endif
            // Add MVC to the request pipeline.
#if (Swagger)
                .UseMvc()
                // Add Swagger to the request pipeline.
                .UseSwagger()
                .UseSwaggerUi();
#else
                .UseMvc();
#endif
        }
    }
}