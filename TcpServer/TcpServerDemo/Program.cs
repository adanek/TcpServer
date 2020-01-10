using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TcpServerDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new TcpServer(7);
            var cts = new CancellationTokenSource(5000);
            try
            {
                server.RunAsync(cts.Token).GetAwaiter().GetResult();
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
        private List<Task<TcpClient>> tasks;
        private CancellationTokenSource cts;
        private Task cleanUpTask;

        public TcpServer(int port)
        {
            this.port = port;
            this.tasks = new List<Task<TcpClient>>();
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

            Console.WriteLine($"Client {client.Client.RemoteEndPoint} disconnected.");
            client.Dispose();

            this.cleanUpTask = this.CleanUp(ct);
        }

        private async Task<TcpClient> HandleClient(TcpClient client, CancellationToken ct)
        {
            await Task.Delay(1000, ct).ConfigureAwait(false);
            return client;
        }

        private Task AddTask(TcpClient client, CancellationToken ct)
        {
            Console.WriteLine($"Client {client.Client.RemoteEndPoint} connected.");
            var task = this.HandleClient(client, ct);
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
}
