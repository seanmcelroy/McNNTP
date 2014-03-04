using log4net;
using log4net.Config;
using McNNTP.Server;
using McNNTP.Server.Configuration;
using McNNTP.Server.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security;

namespace McNNTP.Console
{
    public static class Program
    {
        private static NntpServer _server;
        
        private static readonly Dictionary<string, Func<string, bool>> _commandDirectory = new Dictionary
            <string, Func<string, bool>>
        {
            {"?", s => Help()},
            {"HELP", s => Help()},
            {"MAKEADMIN", MakeAdmin},
            {"MAKEGROUP", MakeGroup},
            {"PURGEDB", Database.DatabaseUtility.RebuildSchema},
            {"SHOWCONN", s => ShowConn()},
            {"TOGBYTES", s => TogBytes()},
            {"TOGCMD", s => TogCommands()},
            {"TOGDATA", s => TogData()},
            {"QUIT", s => Quit()}
        };

        public static int Main(string[] args)
        {
            var version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            System.Console.WriteLine("McNNTP Console Harness v{0}", version);
            
            try
            {
                // Setup LOG4NET
                XmlConfigurator.Configure();

                var logger = LogManager.GetLogger(typeof (Program));

                // Load configuration
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var mcnntpConfigurationSection = (McNNTPConfigurationSection)config.GetSection("mcnntp");
                logger.InfoFormat("Loaded configuration from {0}", config.FilePath);

                // Verify Database
                if (Database.DatabaseUtility.VerifyDatabase())
                    Database.DatabaseUtility.UpdateSchema();
                else if (Database.DatabaseUtility.UpdateSchema() && !Database.DatabaseUtility.VerifyDatabase(true))
                {
                    System.Console.WriteLine("Unable to verify a database.  Would you like to create and initialize a database?");
                    var resp = System.Console.ReadLine();
                    if (resp != null && new[] {"y", "yes"}.Contains(resp.ToLowerInvariant().Trim()))
                    {
                        Database.DatabaseUtility.RebuildSchema();
                    }
                }

                _server = new NntpServer
                {
                    AllowPosting = true,
                    ClearPorts = mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ClearText").Select(p => p.Port).ToArray(),
                    ExplicitTLSPorts = mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ExplicitTLS").Select(p => p.Port).ToArray(),
                    ImplicitTLSPorts = mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ImplicitTLS").Select(p => p.Port).ToArray(),
                    PathHost = mcnntpConfigurationSection.PathHost
                };
                
                _server.Start();

                System.Console.WriteLine("Type QUIT and press Enter to end the server.");

                while (true)
                {
                    System.Console.Write("\r\n> ");
                    var input = System.Console.ReadLine();
                    if (input == null || !_commandDirectory.ContainsKey(input.Split(' ')[0].ToUpperInvariant()))
                    {
                        System.Console.WriteLine("Unrecongized command.  Type HELP for a list of available commands.");
                        continue;
                    }
                    if (!_commandDirectory[input.Split(' ')[0].ToUpperInvariant()].Invoke(input))
                        continue;

                    _server.Stop();
                    return 0;
                }
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                    System.Console.WriteLine(ex.ToString());
                System.Console.ReadLine();
                return -2;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                System.Console.ReadLine();
                return -1;
            }
        }

        #region Commands
        private static bool Help()
        {
            System.Console.WriteLine("MAKEADMIN <name> <pass> : Creates a new news administrator on the server");
            System.Console.WriteLine("MAKEGROUP <name> <desc> : Creates a new news group on the server");
            System.Console.WriteLine("SHOWCONN                : Show active connections");
            System.Console.WriteLine("TOGBYTES                : Toggle showing bytes and destinations");
            System.Console.WriteLine("TOGCMD                  : Toggle showing commands and responses");
            System.Console.WriteLine("TOGDATA                 : Toggle showing all data in and out");
            System.Console.WriteLine("QUIT                    : Exit the program, klling all connections");
            return false;
        }

        private static bool MakeAdmin(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                System.Console.WriteLine("Two parameters are required.");
                return false;
            }

            var name = parts[1].ToLowerInvariant();
            var pass = new SecureString();
            foreach (var c in parts.Skip(2).Aggregate((c, n) => c + " " + n))
                pass.AppendChar(c);

            var admin = new Administrator
            {
                Username = name,
                CanApproveAny = true,
                CanCancel = true,
                CanCheckGroups = true,
                CanCreateGroup = true,
                CanDeleteGroup = true,
                CanInject = false
            };

            admin.SetPassword(pass);

            using (var session = Database.SessionUtility.OpenSession())
            {
                session.Save(admin);
                session.Close();
            }

            System.Console.Clear();
            System.Console.WriteLine("Administrator created.");

            return false;
        }

        private static bool MakeGroup(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                System.Console.WriteLine("Two parameters are required.");
                return false;
            }

            var name = parts[1].ToLowerInvariant();

            if (!name.Contains('.'))
            {
                System.Console.WriteLine("The <name> parameter must contain a '.' to enforce a news heirarchy");
                return false;
            }

            var desc = parts.Skip(2).Aggregate((c, n) => c + " " + n);

            using (var session = Database.SessionUtility.OpenSession())
            {
                session.Save(new Newsgroup
                {
                    Name = name,
                    Description = desc,
                    CreateDate = DateTime.UtcNow
                });
                session.Close();
            }

            return false;
        }
        private static bool ShowConn()
        {
            _server.ShowBytes = !_server.ShowBytes;
            System.Console.WriteLine("\r\nConnections ({0})", _server.Connections.Count);
            System.Console.WriteLine("-----------");
            foreach (var connection in _server.Connections)
            {
                if (connection.AuthenticatedUsername == null)
                    System.Console.WriteLine("{0}:{1}", connection.RemoteAddress, connection.RemotePort);
                else
                    System.Console.WriteLine("{0}:{1} ({2})", connection.RemoteAddress, connection.RemotePort, connection.AuthenticatedUsername);
            }

            return false;
        }


        private static bool TogBytes()
        {
            _server.ShowBytes = !_server.ShowBytes;
            System.Console.Write("[TOGBYTES: ");
            var orig = System.Console.ForegroundColor;
            System.Console.ForegroundColor = _server.ShowBytes ? ConsoleColor.Green : ConsoleColor.Red;
            System.Console.Write(_server.ShowBytes ? "ON" : "OFF");
            System.Console.ForegroundColor = orig;
            System.Console.Write("]");
            return false;
        }

        private static bool TogCommands()
        {
            _server.ShowCommands = !_server.ShowCommands;
            System.Console.Write("[TOGCMD: ");
            var orig = System.Console.ForegroundColor;
            System.Console.ForegroundColor = _server.ShowCommands ? ConsoleColor.Green : ConsoleColor.Red;
            System.Console.Write(_server.ShowCommands ? "ON" : "OFF");
            System.Console.ForegroundColor = orig;
            System.Console.Write("]");
            return false;
        }

        private static bool TogData()
        {
            _server.ShowData = !_server.ShowData;
            System.Console.Write("[TOGDATA: ");
            var orig = System.Console.ForegroundColor;
            System.Console.ForegroundColor = _server.ShowData ? ConsoleColor.Green : ConsoleColor.Red;
            System.Console.Write(_server.ShowData ? "ON" : "OFF");
            System.Console.ForegroundColor = orig;
            System.Console.Write("]");
            return false;
        }
        private static bool Quit()
        {
            _server.Stop();
            return true;
        }
        #endregion
    }
}
