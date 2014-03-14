// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   The client console application that allows a user to connect to a remote NNTP server host via a command line interface
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Client.Console
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    using McNNTP.Core.Client;

    /// <summary>
    /// The client console application that allows a user to connect to a remote NNTP server host via a command line interface
    /// </summary>
    public class Program
    {
        /// <summary>
        /// A dictionary of commands and function pointers to command handlers for console commands
        /// </summary>
        private static readonly Dictionary<string, Func<NntpClient, string, Task<bool>>> CommandDirectory =
            new Dictionary<string, Func<NntpClient, string, Task<bool>>>
                {
                    { "?", (c, s) => Help() },
                    { "CONNECT", Connect },
                    { "GROUP", (c, s) => Group(c,s) },
                    { "HELP", (c, s) => Help() },
                    { "EXIT", (c, s) => Quit() },
                    { "QUIT", (c, s) => Quit() }
                };

        /// <summary>
        /// The NNTP client object instance
        /// </summary>
        [CanBeNull]
        private static NntpClient client;

        public static int Main(string[] args)
        {
            var version = Assembly.GetEntryAssembly().GetName().Version;
            Console.WriteLine("McNNTP Client Console Harness v{0}", version);

            client = new NntpClient();

            while (true)
            {
                Console.Write("\r\n> ");
                var input = Console.ReadLine();
                if (input == null || !CommandDirectory.ContainsKey(input.Split(' ')[0].ToUpperInvariant()))
                {
                    Console.WriteLine("Unrecongized command.  Type HELP for a list of available commands.");
                    continue;
                }

                if (!CommandDirectory[input.Split(' ')[0].ToUpperInvariant()].Invoke(client, input).Result) continue;

                return 0;
            }

        }

        #region Commands
        /// <summary>
        /// Connects the client to a remote news server host
        /// </summary>
        /// <param name="client">The <see cref="NntpClient"/> object that will be used to connect to the remote host</param>
        /// <param name="input">The command and arguments for the console 'CONNECT' command</param>
        /// <returns>Returns false to indicate the program should not exit.</returns>
        private static async Task<bool> Connect([NotNull] NntpClient client, [NotNull] string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                Console.WriteLine("At least one parameter is required.");
                return false;
            }

            int port;
            port = parts.Length >= 3 ? int.TryParse(parts[2], out port) ? port : 119 : 119;

            client.Port = port;
            await client.Connect(parts[1]);

            Console.WriteLine("Connected!");

            return false;
        }

        private static async Task<bool> Group(NntpClient client, string input)
        {
            await client.SetCurrentGroup(input);
            return false;
        }

        /// <summary>
        /// The Help command handler, which shows a help banner on the console
        /// </summary>
        /// <returns>A value indicating whether the server should terminate</returns>
        private static async Task<bool> Help()
        {
            Console.WriteLine("CONNECT <host> <port>           : Connects to the specified server");
            Console.WriteLine("QUIT                            : Exit the program");
            return false;
        }

        /// <summary>
        /// The Quit command handler, which causes the server process to stop and a value to be returned as 'true',
        /// indicating to the caller the server should terminate.
        /// </summary>
        /// <returns><c>true</c></returns>
        private static async Task<bool> Quit()
        {
            //if (client != null)
            //    client.Close();
            return true;
        }
        #endregion
    }
}
