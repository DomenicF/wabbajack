﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Alphaleonis.Win32.Filesystem;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Configuration;
using Nancy.Diagnostics;
using Nancy.Hosting.Self;
using Nancy.TinyIoc;
using Wabbajack.CacheServer.ServerConfig;
using Wabbajack.Common;

namespace Wabbajack.CacheServer
{
    public class Server : IDisposable
    {
        private NancyHost _server;
        private HostConfiguration _config;
        public static BuildServerConfig Config;

        public Server(string address)
        {
            Address = address;
            _config = new HostConfiguration {MaximumConnectionCount = 24, RewriteLocalhost = true};
            //_config.UrlReservations.CreateAutomatically = true;
            _server = new NancyHost(_config, new Uri(address));

            Config = File.ReadAllText("config.yaml").FromYaml<BuildServerConfig>();
        }

        public string Address { get; }

        public void Start()
        {
            _server.Start();
        }

        public void Dispose()
        {
            _server?.Dispose();
        }
    }

    public class CachingBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            pipelines.AfterRequest.AddItemToEndOfPipeline(ctx =>
            {
                ctx.Response.WithHeader("Access-Control-Allow-Origin", "*")
                    .WithHeader("Access-Control-Allow-Methods", "POST, GET")
                    .WithHeader("Access-Control-Allow-Headers", "Accept, Origin, Content-type");
            });
        }

        public override void Configure(INancyEnvironment environment)
        {
            environment.Tracing(
                enabled: true,
                displayErrorTraces: true);
        }
    }
}
