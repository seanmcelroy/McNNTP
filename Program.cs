using System;
using System.Collections.Generic;
using System.Linq;
using McNNTP.Server;
using System.Threading.Tasks;

namespace McNNTP
{
    class Program
    {
        private static readonly Dictionary<string, Func<string, bool>> _commandDirectory = new Dictionary<string, Func<string, bool>>
        {
            { "DUMPBUFS", s => DumpBufs()},
            { "HELP", s => Help()},
            { "MAKEGROUP", MakeGroup},
            { "SHOWCONN", s => Help()},
            { "TOGBYTES", s => TogBytes()},
            { "TOGCMD", s => TogCommands()},
            { "TOGDATA", s => TogData()},
            { "QUIT", s => Quit()}
        };

        private static NntpServer _server;

        private static int Main(string[] args)
        {

            try
            {
                _server = new NntpServer
                {
                    AllowPosting = true
                };

                if (!_server.VerifyDatabase())
                {
                    Console.WriteLine("Unable to verify a database.  Would you like to create and initialize a database?");
                    _server.InitializeDatabase();
                }

                var listenerTask = Task.Factory.StartNew(() => _server.StartListening(119));

                while (true)
                {
                    var input = Console.ReadLine();
                    if (input == null || !_commandDirectory.ContainsKey(input.Split(' ')[0].ToUpperInvariant()))
                        continue;
                    if (!_commandDirectory[input.Split(' ')[0].ToUpperInvariant()].Invoke(input))
                        continue;
                    listenerTask.Wait(1); // Kill me.
                    return 0;
                }
            }
            catch (AggregateException)
            {
                return -2;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return -1;
            }
        }

        #region Commands
        private static bool DumpBufs()
        {
            foreach (var kvp in _server.GetAllBuffs())
                Console.WriteLine("{0}:{1} |=> {2}", kvp.Key.Address, kvp.Key.Port, kvp.Value);
            Console.Write("[DUMPBUFS: DONE]");
            return false;
        }
        private static bool Help()
        {
            Console.WriteLine("DUMPBUFS                : Show current receiver buffers on all connections");
            Console.WriteLine("MAKEGROUP <name> <desc> : Creates a new news group on the server");
            Console.WriteLine("SHOWCONN                : Show active connections");
            Console.WriteLine("TOGBYTES                : Toggle showing bytes and destinations");
            Console.WriteLine("TOGCMD                  : Toggle showing commands and responses");
            Console.WriteLine("TOGDATA                 : Toggle showing all data in and out");
            Console.WriteLine("QUIT                    : Exit the program, klling all connections");
            return false;
        }

        private static bool MakeGroup(string input)
        {
            var parts = input.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                Console.WriteLine("Two parameters are required.");
                return false;
            }

            var name = parts[1].ToLowerInvariant();

            if (!name.Contains('.'))
            {
                Console.WriteLine("The <name> parameter must contain a '.' to enforce a news heirarchy");
                return false;
            }

            var desc = parts.Skip(2).Aggregate((c, n) => c + " " + n);
            _server.ConsoleCreateGroup(name, desc);
            return false;
        }

        private static bool TogBytes()
        {
            _server.ShowBytes = !_server.ShowBytes;
            Console.Write("[TOGBYTES: ");
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = _server.ShowBytes ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write(_server.ShowBytes ? "ON" : "OFF");
            Console.ForegroundColor = orig;
            Console.Write("]");
            return false;
        }

        private static bool TogCommands()
        {
            _server.ShowCommands = !_server.ShowCommands;
            Console.Write("[TOGCMD: ");
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = _server.ShowCommands ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write(_server.ShowCommands ? "ON" : "OFF");
            Console.ForegroundColor = orig;
            Console.Write("]");
            return false;
        }

        private static bool TogData()
        {
            _server.ShowData = !_server.ShowData;
            Console.Write("[TOGDATA: ");
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = _server.ShowData ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write(_server.ShowData ? "ON" : "OFF");
            Console.ForegroundColor = orig;
            Console.Write("]");
            return false;
        }
        private static bool Quit()
        {
            return true;
        }
        #endregion
    }
}