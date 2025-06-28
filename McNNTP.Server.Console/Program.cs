// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Sean McElroy">
//   Copyright Sean McElroy, 2014.  All rights reserved.
// </copyright>
// <summary>
//   A console host for the NNTP server process
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace McNNTP.Server.Console
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security;

    using log4net;
    using log4net.Config;

    using McNNTP.Core.Database;
    using McNNTP.Core.Server.Configuration;
    using McNNTP.Core.Server.NNTP;
    using McNNTP.Data;

    using NHibernate.Linq;

    /// <summary>
    /// A console host for the NNTP nntpServer process
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// A dictionary of commands and function pointers to command handlers for console commands
        /// </summary>
        private static readonly Dictionary<string, Func<string, bool>> _CommandDirectory = new Dictionary<string, Func<string, bool>>
        {
            { "?", s => Help() },
            { "HELP", s => Help() },
            { "DB", DatabaseCommand },
            { "DEBUG", DebugCommand },
            { "GROUP", GroupCommand },
            { "PEER", PeerCommand },
            { "PURGEDB", DatabaseUtility.RebuildSchema },
            { "SHOWCONN", s => ShowConn() },
            { "EXIT", s => Quit() },
            { "QUIT", s => Quit() },
            { "USER", UserCommand }
        };

        /// <summary>
        /// The strings that evaluate to 'true' for a Boolean value
        /// </summary>
        private static readonly string[] _YesStrings = { "ENABLE", "T", "TRUE", "ON", "Y", "YES", "1" };

        /// <summary>
        /// The strings that evaluate to 'false' for a Boolean value
        /// </summary>
        private static readonly string[] _NoStrings = { "DISABLE", "F", "FALSE", "OFF", "N", "NO", "0" };

        /// <summary>
        /// The NNTP server object instance
        /// </summary>
        private static NntpServer? _nntpServer;

        /// <summary>
        /// The main program message loop
        /// </summary>
        /// <returns>An error code, indicating an error condition when the value returned is non-zero.</returns>
        /// <exception cref="FormatException">Thrown when an attempt to construct a format string fails to properly format a finalized message.</exception>
        /// <exception cref="IOException">Thrown when the process is unable to write status to the console window.</exception>
        /// <exception cref="ArgumentNullException">Thrown when a 'null' value is attempted to be written to the console window.</exception>
        /// <exception cref="ConfigurationErrorsException">Thrown when the configuration file for the process cannot be parsed.</exception>
        /// <exception cref="SecurityException">Thrown when the X.509 certificate store cannot be opened or enumerated when constructing SSL ports.</exception>
        public static int Main()
        {
            var version = Assembly.GetEntryAssembly().GetName().Version;
            Console.WriteLine("McNNTP Server Console Harness v{0}", version);

            try
            {
                // Setup LOG4NET
                XmlConfigurator.Configure();

                var logger = LogManager.GetLogger(typeof(Program));

                // Load configuration
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var mcnntpConfigurationSection = (McNNTPConfigurationSection)config.GetSection("mcnntp");
                logger.InfoFormat("Loaded configuration from {0}", config.FilePath);

                // Verify Database
                if (DatabaseUtility.VerifyDatabase()) DatabaseUtility.UpdateSchema();
                else if (DatabaseUtility.UpdateSchema() && !DatabaseUtility.VerifyDatabase(true))
                {
                    Console.WriteLine(
                        "Unable to verify a database.  Would you like to create and initialize a database?");
                    var resp = Console.ReadLine();
                    if (resp != null && new[] { "y", "yes" }.Contains(resp.ToLowerInvariant().Trim()))
                    {
                        DatabaseUtility.RebuildSchema();
                    }
                }

                _nntpServer = new NntpServer
                {
                    AllowPosting = true,
                    IrcClearPorts =
                                     mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ClearText" && p.Protocol == "irc")
                                     .Select(p => p.Port)
                                     .ToArray(),
                    IrcImplicitTLSPorts =
                                     mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ImplicitTLS" && p.Protocol == "irc")
                                     .Select(p => p.Port)
                                     .ToArray(),
                    NntpClearPorts =
                                     mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ClearText" && p.Protocol == "nntp")
                                     .Select(p => p.Port)
                                     .ToArray(),
                    NntpExplicitTLSPorts =
                                     mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ExplicitTLS" && p.Protocol == "nntp")
                                     .Select(p => p.Port)
                                     .ToArray(),
                    NntpImplicitTLSPorts =
                                     mcnntpConfigurationSection.Ports.Where(p => p.Ssl == "ImplicitTLS" && p.Protocol == "nntp")
                                     .Select(p => p.Port)
                                     .ToArray(),
                    PathHost = mcnntpConfigurationSection.PathHost,
                    SslGenerateSelfSignedServerCertificate =
                                     mcnntpConfigurationSection.Ssl == null
                                     || mcnntpConfigurationSection.Ssl.GenerateSelfSignedServerCertificate,
                    SslServerCertificateThumbprint =
                                     mcnntpConfigurationSection.Ssl == null
                                         ? null
                                         : mcnntpConfigurationSection.Ssl.ServerCertificateThumbprint
                };

                _nntpServer.Start();

                Console.WriteLine("Type QUIT and press Enter to end the server.");

                while (true)
                {
                    Console.Write("\r\n> ");
                    var input = Console.ReadLine();
                    if (input == null || !_CommandDirectory.ContainsKey(input.Split(' ')[0].ToUpperInvariant()))
                    {
                        Console.WriteLine("Unrecongized command.  Type HELP for a list of available commands.");
                        continue;
                    }

                    if (!_CommandDirectory[input.Split(' ')[0].ToUpperInvariant()].Invoke(input)) continue;

                    _nntpServer.Stop();
                    return 0;
                }
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions) Console.WriteLine(ex.ToString());
                Console.ReadLine();
                return -2;
            }
            catch (SecurityException sex)
            {
                Console.WriteLine(sex.ToString());
                Console.ReadLine();
                return -3;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
                return -1;
            }
        }

        #region Commands

        /// <summary>
        /// The Help command handler, which shows a help banner on the console
        /// </summary>
        /// <returns>A value indicating whether the nntpServer should terminate</returns>
        private static bool Help()
        {
            Console.WriteLine("DB ANNIHILATE                    : Completely wipe and rebuild the news database");
            Console.WriteLine("DB UPDATE                        : Updates the database schema integrity to\r\n" +
                              "                                   match the code object definitions");
            Console.WriteLine("DB VERIFY                        : Verify the database schema integrity against\r\n" +
                              "                                   the code object definitions");
            Console.WriteLine("DEBUG BYTES <value>              : Toggles showing bytes and destinations");
            Console.WriteLine("DEBUG COMMANDS <value>           : Toggles showing commands and responses");
            Console.WriteLine("DEBUG DATA <value>               : Toggles showing all data in and out");
            Console.WriteLine("GROUP <name> CREATE <desc>       : Creates a new news group on the nntpServer");
            Console.WriteLine("GROUP <name> CREATOR <value>     : Sets the addr-spec (name and email)");
            Console.WriteLine("GROUP <name> DENYLOCAL <value>   : Toggles denial of local postings to a group");
            Console.WriteLine("GROUP <name> DENYPEER <value>    : Toggles denial of peer postings to a group");
            Console.WriteLine("GROUP <name> MODERATION <value>  : Toggles moderation of a group (true or false)");
            Console.WriteLine("PEER <host>[:port] CREATE        : Creates a peer nntpServer for article exchange");
            Console.WriteLine("PEER <host> SUCK [wildmat]       : Configures the active receive (suck)\r\n" +
                              "                                   distribution wildmat.  If the wildmat is\r\n" +
                              "                                   blank, the current wildmat will be displayed");
            Console.WriteLine("SHOWCONN                         : Show active connections");
            Console.WriteLine("QUIT                             : Exit the program, killing all connections");
            Console.WriteLine("USER <name> CREATE <pass>        : Creates a new news administrator");
            return false;
        }

        /// <summary>
        /// The Admin command handler, which handles console commands for administration management
        /// </summary>
        /// <param name="input">The full console command input.</param>
        /// <returns>A value indicating whether the nntpServer should terminat.e</returns>
        private static bool UserCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                Console.WriteLine("Two parameters are required.");
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

                        var admin = new User
                        {
                            Username = name,
                            CanApproveAny = true,
                            CanCancel = true,
                            CanCheckCatalogs = true,
                            CanCreateCatalogs = true,
                            CanDeleteCatalogs = true,
                            CanInject = false
                        };

                        using (var session = SessionUtility.OpenSession())
                        {
                            session.Save(admin);
                            session.Flush();
                            session.Close();
                        }

                        admin.SetPassword(pass);
                        Console.WriteLine("User {0} created with all priviledges.", name);
                        break;
                    }

                default:
                    Console.WriteLine("Unknown parameter {0} specified for Admin command", parts[2]);
                    break;
            }

            return false;
        }

        /// <summary>
        /// The Group command handler, which handles console commands for group management
        /// </summary>
        /// <param name="input">The full console command input</param>
        /// <returns>A value indicating whether the nntpServer should terminate</returns>
        private static bool GroupCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
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

            switch (parts[2].ToLowerInvariant())
            {
                case "create":
                    {
                        var desc = parts.Skip(3).Aggregate((c, n) => c + " " + n);
                        using (var session = SessionUtility.OpenSession())
                        {
                            session.Save(new Newsgroup
                            {
                                Name = name,
                                Description = desc,
                                CreateDate = DateTime.UtcNow
                            });
                            session.Close();
                            Console.WriteLine("Newsgroup {0} created.", name);
                        }

                        break;
                    }

                case "creator":
                    {
                        var creator = parts.Skip(3).Aggregate((c, n) => c + " " + n);

                        using (var session = SessionUtility.OpenSession())
                        {
                            var newsgroup = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == name);
                            if (newsgroup == null)
                                Console.WriteLine("No newsgroup named '{0}' exists.", name);
                            else
                            {
                                newsgroup.CreatorEntity = creator;
                                session.SaveOrUpdate(newsgroup);
                                session.Flush();
                                Console.WriteLine("Creator entity of newsgroup {0} set to {1}", name, creator);
                            }

                            session.Close();
                        }

                        break;
                    }

                case "denylocal":
                    {
                        var val = parts[3];

                        using (var session = SessionUtility.OpenSession())
                        {
                            var newsgroup = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == name);
                            if (newsgroup == null)
                                Console.WriteLine("No newsgroup named '{0}' exists.", name);
                            else if (_YesStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            {
                                newsgroup.DenyLocalPosting = true;
                                session.SaveOrUpdate(newsgroup);
                                session.Flush();
                                Console.WriteLine("Local posting to newsgroup {0} denied.", name);
                            }
                            else if (_NoStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            {
                                newsgroup.DenyLocalPosting = false;
                                session.SaveOrUpdate(newsgroup);
                                session.Flush();
                                Console.WriteLine("Local posting to newsgroup {0} re-enabled.", name);
                            }
                            else
                                Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                            session.Close();
                        }

                        break;
                    }

                case "denypeer":
                    {
                        var val = parts[3];

                        using (var session = SessionUtility.OpenSession())
                        {
                            var newsgroup = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == name);
                            if (newsgroup == null)
                                Console.WriteLine("No newsgroup named '{0}' exists.", name);
                            else if (_YesStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            {
                                newsgroup.DenyPeerPosting = true;
                                session.SaveOrUpdate(newsgroup);
                                session.Flush();
                                Console.WriteLine("Peer posting to newsgroup {0} denied.", name);
                            }
                            else if (_NoStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            {
                                newsgroup.DenyPeerPosting = false;
                                session.SaveOrUpdate(newsgroup);
                                session.Flush();
                                Console.WriteLine("Peer posting to newsgroup {0} re-enabled.", name);
                            }
                            else
                                Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                            session.Close();
                        }

                        break;
                    }

                case "moderation":
                    {
                        var val = parts[3];

                        using (var session = SessionUtility.OpenSession())
                        {
                            var newsgroup = session.Query<Newsgroup>().SingleOrDefault(n => n.Name == name);
                            if (newsgroup == null)
                                Console.WriteLine("No newsgroup named '{0}' exists.", name);
                            else if (_YesStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            {
                                newsgroup.Moderated = true;
                                session.SaveOrUpdate(newsgroup);
                                session.Flush();
                                Console.WriteLine("Moderation of newsgroup {0} enabled.", name);
                            }
                            else if (_NoStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            {
                                newsgroup.Moderated = false;
                                session.SaveOrUpdate(newsgroup);
                                session.Flush();
                                Console.WriteLine("Moderation of newsgroup {0} disabled.", name);
                            }
                            else
                                Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                            session.Close();
                        }

                        break;
                    }

                default:
                    Console.WriteLine("Unknown parameter {0} specified for Group command", parts[2]);
                    break;
            }

            return false;
        }

        /// <summary>
        /// Shows all open connection to the nntpServer process
        /// </summary>
        /// <returns>A value indicating whether the nntpServer should terminate</returns>
        private static bool ShowConn()
        {
            _nntpServer.ShowBytes = !_nntpServer.ShowBytes;
            Console.WriteLine("\r\nConnections ({0})", _nntpServer.Connections.Count);
            Console.WriteLine("-----------");
            foreach (var connection in _nntpServer.Connections)
            {
                if (connection.AuthenticatedUsername == null)
                    Console.WriteLine("{0}:{1} (nntp)", connection.RemoteAddress, connection.RemotePort);
                else
                    Console.WriteLine("{0}:{1} (nntp:{2})", connection.RemoteAddress, connection.RemotePort, connection.AuthenticatedUsername);
            }

            return false;
        }

        /// <summary>
        /// The Database command handler, which handles console commands for database file and schema management
        /// </summary>
        /// <param name="input">The full console command input</param>
        /// <returns>A value indicating whether the nntpServer should terminate</returns>
        private static bool DatabaseCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                Console.WriteLine("One parameter is required.");
                return false;
            }

            switch (parts[1].ToLowerInvariant())
            {
                case "annihilate":
                    {
                        DatabaseUtility.RebuildSchema();
                        Console.WriteLine("Database recreated");
                        break;
                    }

                case "update":
                    {
                        DatabaseUtility.UpdateSchema();
                        break;
                    }

                case "verify":
                    {
                        DatabaseUtility.VerifyDatabase();
                        break;
                    }

                default:
                    Console.WriteLine("Unknown parameter {0} specified for Debug command", parts[1]);
                    break;
            }

            return false;
        }

        /// <summary>
        /// The Debug command handler, which handles console commands for enabling verbose debug log display
        /// </summary>
        /// <param name="input">The full console command input</param>
        /// <returns>A value indicating whether the nntpServer should terminate</returns>
        private static bool DebugCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                Console.WriteLine("One parameter is required.");
                return false;
            }

            switch (parts[1].ToLowerInvariant())
            {
                case "bytes":
                    {
                        var val = parts[2];

                        if (_YesStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            _nntpServer.ShowBytes = true;
                        else if (_NoStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            _nntpServer.ShowBytes = false;
                        else
                            Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                        Console.Write("[DEBUG BYTES: ");
                        var orig = Console.ForegroundColor;
                        Console.ForegroundColor = _nntpServer.ShowBytes ? ConsoleColor.Green : ConsoleColor.Red;
                        Console.Write(_nntpServer.ShowBytes ? "ON" : "OFF");
                        Console.ForegroundColor = orig;
                        Console.Write("]");

                        break;
                    }

                case "commands":
                    {
                        var val = parts[2];

                        if (_YesStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            _nntpServer.ShowCommands = true;
                        else if (_NoStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            _nntpServer.ShowCommands = false;
                        else
                            Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                        Console.Write("[DEBUG COMMANDS: ");
                        var orig = Console.ForegroundColor;
                        Console.ForegroundColor = _nntpServer.ShowCommands ? ConsoleColor.Green : ConsoleColor.Red;
                        Console.Write(_nntpServer.ShowCommands ? "ON" : "OFF");
                        Console.ForegroundColor = orig;
                        Console.Write("]");

                        break;
                    }

                case "data":
                    {
                        var val = parts[2];

                        if (_YesStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            _nntpServer.ShowData = true;
                        else if (_NoStrings.Contains(val, StringComparer.OrdinalIgnoreCase))
                            _nntpServer.ShowData = false;
                        else
                            Console.WriteLine("Unable to parse '{0}' value.  Please use 'on' or 'off' to change this value.", val);

                        Console.Write("[DEBUG DATA: ");
                        var orig = Console.ForegroundColor;
                        Console.ForegroundColor = _nntpServer.ShowData ? ConsoleColor.Green : ConsoleColor.Red;
                        Console.Write(_nntpServer.ShowData ? "ON" : "OFF");
                        Console.ForegroundColor = orig;
                        Console.Write("]");

                        break;
                    }

                default:
                    Console.WriteLine("Unknown parameter {0} specified for Debug command", parts[1]);
                    break;
            }

            return false;
        }

        /// <summary>
        /// The Peer command handler, which handles peer nntpServer management functions
        /// </summary>
        /// <param name="input">The full console command input</param>
        /// <returns>A value indicating whether the nntpServer should terminate</returns>
        private static bool PeerCommand(string input)
        {
            var parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                Console.WriteLine("Three parameters are required.");
                return false;
            }

            var hostname = parts[1].IndexOf(':') > -1
                ? parts[1].Substring(0, parts[1].IndexOf(':'))
                : parts[1];

            switch (parts[2].ToLowerInvariant())
            {
                case "create":
                    {
                        int port;
                        port = parts[1].IndexOf(':') > -1 ? int.TryParse(parts[1].Substring(parts[1].IndexOf(':') + 1), out port) ? port : 119 : 119;

                        using var session = SessionUtility.OpenSession();
                        session.Save(new Peer
                        {
                            Hostname = hostname,
                            Port = port,
                            ActiveReceiveDistribution = null,
                            PassiveReceiveDistribution = null,
                            SendDistribution = null
                        });
                        session.Close();
                        Console.WriteLine("Peer {0}:{1} created.", hostname, port);

                        break;
                    }

                // PEER <host> SUCK [wildmat]
                case "suck":
                    {
                        var wildmat = parts.Skip(3).Aggregate((c, n) => c + " " + n);

                        using (var session = SessionUtility.OpenSession())
                        {
                            var peer = session.Query<Peer>().SingleOrDefault(n => n.Hostname == hostname);
                            if (peer == null)
                                Console.WriteLine("No peer '{0}' exists.", hostname);
                            else
                            {
                                peer.ActiveReceiveDistribution = string.IsNullOrWhiteSpace(wildmat) ? null : wildmat;
                                session.SaveOrUpdate(peer);
                                session.Flush();
                                Console.WriteLine("Peer {0}'s active 'suck' for newsgroups is wildmat: {1}", hostname, wildmat);
                            }

                            session.Close();
                        }

                        break;
                    }

                default:
                    Console.WriteLine("Unknown parameter {0} specified for Group command", parts[2]);
                    break;
            }

            return false;
        }

        /// <summary>
        /// The Quit command handler, which causes the nntpServer process to stop and a value to be returned as 'true',
        /// indicating to the caller the nntpServer should terminate.
        /// </summary>
        /// <returns><c>true</c></returns>
        private static bool Quit()
        {
            _nntpServer?.Stop();
            return true;
        }
        #endregion
    }
}
