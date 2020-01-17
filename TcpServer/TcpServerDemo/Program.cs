// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.
// Licensed under the The Unlicense license. See LICENSE file in the project root for full license information.

using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TcpServerDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var loggerFactory = LoggerFactory.Create(c =>
            {
                c.AddConsole(c1 =>
                {
                    c1.IncludeScopes = true;
                });

                c.SetMinimumLevel(LogLevel.Trace);
            });

            var server = new TcpServer(7, loggerFactory.CreateLogger("A"));
            using var cts = new CancellationTokenSource();
            try
            {
                await server.RunAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Tcp server canceled.");
            }

            Console.ReadKey();
        }
    }

    class TcpServer
    {
        private readonly int port;
        private readonly ILogger logger;
        private List<Task<MyTcpClient>> tasks;
        private CancellationTokenSource cts;
        private Task cleanUpTask;

        public TcpServer(int port, ILogger logger)
        {
            this.port = port;
            this.logger = logger;
            this.tasks = new List<Task<MyTcpClient>>();
            this.cts = new CancellationTokenSource();
        }

        public async Task RunAsync(CancellationToken ct)
        {
            Console.WriteLine($"Tcp server started on port {this.port}");
            this.StartCleanUp();

            var listener = new TcpListener(IPAddress.Any, this.port);

            try
            {
                listener.Start();

                while (!ct.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync().WaitAsync(ct).ConfigureAwait(false);
                    await this.AddTask(client, ct).ConfigureAwait(false);
                }
            }
            finally
            {
                listener.Stop();
                await this.StopCleanUp();
                Console.WriteLine("Tcp server stopped.");
            }
        }

        private async Task CleanUp(CancellationToken ct)
        {
            if (!this.tasks.Any())
            {
                return;
            }

            var finishedTask = await Task.WhenAny(this.tasks).ConfigureAwait(false);
            this.tasks.Remove(finishedTask);

            var client = await finishedTask.ConfigureAwait(false);
            //Console.WriteLine($"Client {client.Client.RemoteEndPoint} disconnected.");
            client.Dispose();

            this.cleanUpTask = this.CleanUp(ct);
        }

        private async Task<MyTcpClient> HandleClient(MyTcpClient client, CancellationToken ct)
        {
            using (var stream = client.GetStream())
            {
                var buffer = new byte[256];
                await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
            }

            return client;
        }

        private Task AddTask(TcpClient client, CancellationToken ct)
        {
            Console.WriteLine($"Client {client.Client.RemoteEndPoint} connected.");

            var c = new MyTcpClient(client, logger);
            var task = this.HandleClient(c, ct);
            this.tasks.Add(task);
            return this.ResetCleanUp();
        }

        private void StartCleanUp()
        {
            this.cts?.Dispose();
            this.cts = new CancellationTokenSource();
            this.cleanUpTask = this.CleanUp(this.cts.Token);
        }

        private async Task StopCleanUp()
        {
            this.cts.Cancel();

            try
            {
                await this.cleanUpTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                this.cts.Dispose();
                this.cts = null;
            }
        }

        private async Task ResetCleanUp()
        {
            await this.StopCleanUp();
            this.StartCleanUp();
        }
    }

    public class MyTcpClient : IDisposable
    {
        private readonly TcpClient client;
        private readonly ILogger logger;
        private readonly IDisposable loggingScope;

        public MyTcpClient(TcpClient client, ILogger logger)
        {
            this.client = client;
            this.logger = logger;

            this.loggingScope = this.logger.BeginScope(this.client.Client.RemoteEndPoint);
            this.logger.LogTrace("connected");
        }

        public NetworkStream GetStream()
        {
            return this.client.GetStream();
        }

        public void Dispose()
        {
            this.logger.LogTrace("disconnected");
            this.client.Dispose();
            this.loggingScope.Dispose();
        }
    }
}
