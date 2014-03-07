using log4net;
using log4net.Config;
using McNNTP.Core.Server;
using McNNTP.Core.Server.Configuration;
using McNNTP.Data;
using NHibernate.Linq;
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
            {"ADMIN", AdminCommand},
            {"DB", DatabaseCommand},
            {"DEBUG", DebugCommand},
            {"GROUP", GroupCommand},
            {"PURGEDB", Core.Database.DatabaseUtility.RebuildSchema},
            {"SHOWCONN", s => ShowConn()},
            {"EXIT", s => Quit()},
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
                if (Core.Database.DatabaseUtility.VerifyDatabase())
                    Core.Database.DatabaseUtility.UpdateSchema();
                else if (Core.Database.DatabaseUtility.UpdateSchema() && !Core.Database.DatabaseUtility.VerifyDatabase(true))
                {
                    System.Console.WriteLine("Unable to verify a database.  Would you like to create and initialize a database?");
                    var resp = System.Console.ReadLine();
                    if (resp != null && new[] {"y", "yes"}.Contains(resp.ToLowerInvariant().Trim()))
                    {
                        Core.Database.DatabaseUtility.RebuildSchema();
                    }
                }

                _server = new NntpServer
                {
                    AllowPosting = true,
                    ClearPorts = mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ClearText").Select(p => p.Port).ToArray(),
                    ExplicitTLSPorts = mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ExplicitTLS").Select(p => p.Port).ToArray(),
                    ImplicitTLSPorts = mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ImplicitTLS").Select(p => p.Port).ToArray(),
                    PathHost = mcnntpConfigurationSection.PathHost,
                    SslGenerateSelfSignedServerCertificate = mcnntpConfigurationSection.SSL == null || mcnntpConfigurationSection.SSL.GenerateSelfSignedServerCertificate,
                    SslServerCertificateThumbprint = mcnntpConfigurationSection.SSL == null ? null : mcnntpConfigurationSection.SSL.ServerCertificateThumbprint
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
            System.Console.WriteLine("ADMIN <name> CREATE <pass>      : Creates a new news administrator on the server");
            System.Console.WriteLine("DB ANNIHILATE                   : Completely wipe and rebuild the news database");
            System.Console.WriteLine("DB UPDATE                       : Updates the database schema integrity to match the code object definitions");
            System.Console.WriteLine("DB VERIFY                       : Verify the database schema integrity against the code object definitions");
            System.Console.WriteLine("DEBUG BYTES <value>             : Toggles showing bytes and destinations");
            System.Console.WriteLine("DEBUG COMMANDS <value>          : Toggles showing commands and responses");
            System.Console.WriteLine("DEBUG DATA <value>              : Toggles showing all data in and out");
            System.Console.WriteLine("GROUP <name> CREATE <desc>      : Creates a new news group on the server");
            System.Console.WriteLine("GROUP <name> CREATOR <value>    : Toggles moderation of a group (value is 'true' or 'false')");
            System.Console.WriteLine("GROUP <name> MODERATION <value> : Toggles moderation of a group (value is 'true' or 'false')");
            System.Console.WriteLine("SHOWCONN                        : Show active connections");
            System.Console.WriteLine("QUIT                            : Exit the program, klling all connections");
            return false;
        }

        private static bool AdminCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                System.Console.WriteLine("Two parameters are required.");
                return false;
            }

            var name = parts[1].ToLowerInvariant();

            switch (parts[2].ToLowerInvariant())
            {
                case "create":
                {
                    var pass = new SecureString();
                    foreach (var c in parts.Skip(3).Aggregate((c, n) => c + " " + n))
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

                    using (var session = Core.Database.SessionUtility.OpenSession())
                    {
                        session.Save(admin);
                        session.Flush();
                        session.Close();
                    }

                    admin.SetPassword(pass);
                    System.Console.WriteLine("Administrator {0} created with all priviledges.", name);
                    break;
                }
                default:
                    System.Console.WriteLine("Unknown parameter {0} specified for Admin command", parts[2]);
                    break;
            }

            return false;
        }
        private static bool GroupCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
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

            switch (parts[2].ToLowerInvariant())
            {
                case "create":
                {
                    var desc = parts.Skip(3).Aggregate((c, n) => c + " " + n);
                    using (var session = Core.Database.SessionUtility.OpenSession())
                    {
                        session.Save(new Newsgroup
                        {
                            Name = name,
                            Description = desc,
                            CreateDate = DateTime.UtcNow
                        });
                        session.Close();
                        System.Console.WriteLine("Newsgroup {0} created.", name);
                    }
                    break;
                }
                case "creator":
                {
                    var creator = parts.Skip(3).Aggregate((c,n) => c + " " + n);

                    using (var session = Core.Database.SessionUtility.OpenSession())
                    {
                        var newsgroup = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == name);
                        if (newsgroup == null)
                            System.Console.WriteLine("No newsgroup named '{0}' exists.", name);
                        else
                        {
                            newsgroup.CreatorEntity = creator;
                            session.SaveOrUpdate(newsgroup);
                            session.Flush();
                            System.Console.WriteLine("Creator entity of newsgroup {0} set to {1}", name, creator);
                        }

                        session.Close();
                    }
                    break;
                }
                case "moderation":
                {
                    var val = parts[3];

                    using (var session = Core.Database.SessionUtility.OpenSession())
                    {
                        var newsgroup = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == name);
                        if (newsgroup == null)
                            System.Console.WriteLine("No newsgroup named '{0}' exists.", name);
                        else if (new[] { "ENABLE", "TRUE", "ON", "YES", "1" }.Contains(val, StringComparer.OrdinalIgnoreCase))
                        {
                            newsgroup.Moderated = true;
                            session.SaveOrUpdate(newsgroup);
                            session.Flush();
                            System.Console.WriteLine("Moderation of newsgroup {0} enabled.", name);
                        }
                        else if (new[] { "DISABLE", "FALSE", "OFF", "NO", "0" }.Contains(val, StringComparer.OrdinalIgnoreCase))
                        {
                            newsgroup.Moderated = false;
                            session.SaveOrUpdate(newsgroup);
                            session.Flush();
                            System.Console.WriteLine("Moderation of newsgroup {0} disabled.", name);
                        }
                        else
                            System.Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                        session.Close();
                    }
                    break;
                }
                default:
                    System.Console.WriteLine("Unknown parameter {0} specified for Group command", parts[2]);
                    break;
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
        private static bool DatabaseCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                System.Console.WriteLine("One parameter is required.");
                return false;
            }

            switch (parts[1].ToLowerInvariant())
            {
                case "annihilate":
                {
                    Core.Database.DatabaseUtility.RebuildSchema();
                    System.Console.WriteLine("Database recreated");
                    break;
                }
                case "update":
                {
                    Core.Database.DatabaseUtility.UpdateSchema();
                    break;
                }
                case "verify":
                {
                    Core.Database.DatabaseUtility.VerifyDatabase();
                    break;
                }
                default:
                    System.Console.WriteLine("Unknown parameter {0} specified for Debug command", parts[1]);
                    break;
            }

            return false;
        }
        private static bool DebugCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                System.Console.WriteLine("One parameter is required.");
                return false;
            }

            switch (parts[1].ToLowerInvariant())
            {
                case "bytes":
                {
                    var val = parts[2];

                    if (new[] { "ENABLE", "TRUE", "ON", "YES", "1" }.Contains(val, StringComparer.OrdinalIgnoreCase))
                        _server.ShowBytes = true;
                    else if (new[] {"DISABLE", "FALSE", "OFF", "NO", "0"}.Contains(val, StringComparer.OrdinalIgnoreCase))
                        _server.ShowBytes = false;
                    else
                        System.Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                    System.Console.Write("[DEBUG BYTES: ");
                    var orig = System.Console.ForegroundColor;
                    System.Console.ForegroundColor = _server.ShowBytes ? ConsoleColor.Green : ConsoleColor.Red;
                    System.Console.Write(_server.ShowBytes ? "ON" : "OFF");
                    System.Console.ForegroundColor = orig;
                    System.Console.Write("]");

                    break;
                }
                case "commands":
                {
                    var val = parts[2];

                    if (new[] { "ENABLE", "TRUE", "ON", "YES", "1" }.Contains(val, StringComparer.OrdinalIgnoreCase))
                        _server.ShowCommands = true;
                    else if (new[] { "DISABLE", "FALSE", "OFF", "NO", "0" }.Contains(val, StringComparer.OrdinalIgnoreCase))
                        _server.ShowCommands = false;
                    else
                        System.Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                    System.Console.Write("[DEBUG COMMANDS: ");
                    var orig = System.Console.ForegroundColor;
                    System.Console.ForegroundColor = _server.ShowCommands ? ConsoleColor.Green : ConsoleColor.Red;
                    System.Console.Write(_server.ShowCommands ? "ON" : "OFF");
                    System.Console.ForegroundColor = orig;
                    System.Console.Write("]");

                    break;
                }
                case "data":
                {
                    var val = parts[2];

                    if (new[] { "ENABLE", "TRUE", "ON", "YES", "1" }.Contains(val, StringComparer.OrdinalIgnoreCase))
                        _server.ShowData = true;
                    else if (new[] { "DISABLE", "FALSE", "OFF", "NO", "0" }.Contains(val, StringComparer.OrdinalIgnoreCase))
                        _server.ShowData = false;
                    else
                        System.Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                    System.Console.Write("[DEBUG DATA: ");
                    var orig = System.Console.ForegroundColor;
                    System.Console.ForegroundColor = _server.ShowData ? ConsoleColor.Green : ConsoleColor.Red;
                    System.Console.Write(_server.ShowData ? "ON" : "OFF");
                    System.Console.ForegroundColor = orig;
                    System.Console.Write("]");

                    break;
                }
                default:
                System.Console.WriteLine("Unknown parameter {0} specified for Debug command", parts[1]);
                   break;
            }

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
