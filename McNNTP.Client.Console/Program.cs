namespace McNNTP.Client.Console
{
    using JetBrains.Annotations;
    using McNNTP.Core.Client;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public class Program
    {
        /// <summary>
        /// A dictionary of commands and function pointers to command handlers for console commands
        /// </summary>
        private static readonly Dictionary<string, Func<string, bool>> CommandDirectory = new Dictionary<string, Func<string, bool>>
        {
            { "?", s => Help() }, 
            { "HELP", s => Help() }, 
            { "EXIT", s => Quit() }, 
            { "QUIT", s => Quit() }
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

            while (true)
            {
                Console.Write("\r\n> ");
                var input = Console.ReadLine();
                if (input == null || !CommandDirectory.ContainsKey(input.Split(' ')[0].ToUpperInvariant()))
                {
                    Console.WriteLine("Unrecongized command.  Type HELP for a list of available commands.");
                    continue;
                }

                if (!CommandDirectory[input.Split(' ')[0].ToUpperInvariant()].Invoke(input))
                    continue;

                return 0;
            }

        }

        #region Commands
        /// <summary>
        /// The Help command handler, which shows a help banner on the console
        /// </summary>
        /// <returns>A value indicating whether the server should terminate</returns>
        private static bool Help()
        {
            Console.WriteLine("QUIT                            : Exit the program");
            return false;
        }

        /// <summary>
        /// The Quit command handler, which causes the server process to stop and a value to be returned as 'true',
        /// indicating to the caller the server should terminate.
        /// </summary>
        /// <returns><c>true</c></returns>
        private static bool Quit()
        {
            if (client != null)
                client.Close();
            return true;
        }
        #endregion
    }
}
