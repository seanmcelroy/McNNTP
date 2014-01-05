using System;
using System.Collections.Generic;
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
            { "SHOWCONN", s => Help()},
            { "TOGFULL", s => TogFull()},
            { "TOGSUM", s => TogSum()},
            { "QUIT", s => Quit()}
        };

        private static NntpServer _server;

        private static void Main()
        {

            try
            {
                _server = new NntpServer();
                var listenerTask = Task.Factory.StartNew(() => _server.StartListening(119));

                while (true)
                {
                    var input = Console.ReadLine();
                    if (input != null && _commandDirectory.ContainsKey(input.Split(' ')[0].ToUpperInvariant()))
                    {
                        if (_commandDirectory[input.Split(' ')[0].ToUpperInvariant()].Invoke(input))
                        {
                            listenerTask.Wait(1); // Kill me.
                            return;
                        }
                    }
                }
            }
            catch (AggregateException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());


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
            Console.WriteLine("DUMPBUFS: Show current receiver buffers on all connections");
            Console.WriteLine("SHOWCONN: Show active connections");
            Console.WriteLine("TOGFULL : Toggle showing all data in and out");
            Console.WriteLine("TOGSUM  : Toggle showing summary data in and out");
            Console.WriteLine("QUIT    : Exit the program, klling all connections");
            return false;
        }
        private static bool TogFull()
        {
            _server.ShowDetail = !_server.ShowDetail;
            Console.Write("[TOGFULL: ");
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = _server.ShowDetail ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write(_server.ShowDetail ? "ON" : "OFF");
            Console.ForegroundColor = orig;
            Console.Write("]");
            return false;
        }
        private static bool TogSum()
        {
            _server.ShowSummaries = !_server.ShowSummaries;
            Console.Write("[TOGSUM: ");
            var orig = Console.ForegroundColor;
            Console.ForegroundColor = _server.ShowSummaries ? ConsoleColor.Green : ConsoleColor.Red;
            Console.Write(_server.ShowSummaries ? "ON" : "OFF");
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