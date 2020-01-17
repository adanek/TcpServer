// Copyright (c) World-Direct eBusiness solutions GmbH. All rights reserved.
// Licensed under the The Unlicense license. See LICENSE file in the project root for full license information.

using System;

namespace TcpTestClient
{
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Program
    {
        public static async Task Main(string[] args)
        {
           await ConnectAsync("localhost", 7).ConfigureAwait(false);
        }

        private static async Task ConnectAsync(string ip, int port)
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource();

            await client.ConnectAsync(ip, port).ConfigureAwait(false);
            Console.ReadKey();
        }
    }
}
