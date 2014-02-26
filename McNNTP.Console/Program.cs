using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using McNNTP.Server;
using McNNTP.Server.Data;
using NHibernate;
using NHibernate.Cfg;

namespace McNNTP.Console
{
    public static class Program
    {
        private static NntpServer _server;
        private static readonly object _sessionFactoryLock = new object();
        private static ISessionFactory _sessionFactory;
        
        private static readonly Dictionary<string, Func<string, bool>> _commandDirectory = new Dictionary
            <string, Func<string, bool>>
        {
            {"HELP", s => Help()},
            {"MAKEADMIN", MakeAdmin},
            {"MAKEGROUP", MakeGroup},
            {"SHOWCONN", s => Help()},
            {"TOGBYTES", s => TogBytes()},
            {"TOGCMD", s => TogCommands()},
            {"TOGDATA", s => TogData()},
            {"QUIT", s => Quit()}
        };

        public static int Main(string[] args)
        {
            try
            {
                _server = new NntpServer(OpenSession)
                {
                    AllowPosting = true,
                    ClearPorts = new [] { 119 }
                };

                if (!_server.VerifyDatabase())
                {
                    System.Console.WriteLine("Unable to verify a database.  Would you like to create and initialize a database?");
                    _server.InitializeDatabase();
                }

                var listenerTask = Task.Factory.StartNew(() => _server.Start());

                System.Console.WriteLine("Type QUIT and press Enter to end the server.");

                while (true)
                {
                    System.Console.Write("\r\n> ");
                    var input = System.Console.ReadLine();
                    if (input == null || !_commandDirectory.ContainsKey(input.Split(' ')[0].ToUpperInvariant()))
                        continue;
                    if (!_commandDirectory[input.Split(' ')[0].ToUpperInvariant()].Invoke(input))
                        continue;
                    listenerTask.Wait(1); // Kill me.
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

            
            var saltBytes = new byte[64];
            var rng = RandomNumberGenerator.Create();
            rng.GetNonZeroBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);

            var bstr = Marshal.SecureStringToBSTR(pass);
            try
            {
                using (var session = OpenSession())
                {
                    session.Save(new Administrator
                    {
                        Username = name,
                        PasswordHash = Convert.ToBase64String(new SHA512CryptoServiceProvider().ComputeHash(Encoding.UTF8.GetBytes(string.Concat(salt, Marshal.PtrToStringBSTR(bstr))))),
                        PasswordSalt = salt,
                        CanApproveGroups = "*",
                        CanCancel = true,
                        CanCheckGroups = true,
                        CanCreateGroup = true,
                        CanDeleteGroup = true,
                        CanInject = false
                    });
                    session.Close();
                }
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
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

            using (var session = OpenSession())
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


        #region Database
        private static ISession OpenSession()
        {
            lock (_sessionFactoryLock)
            {
                if (_sessionFactory == null)
                {
                    var configuration = new Configuration();
                    configuration.AddAssembly(typeof(Newsgroup).Assembly);
                    _sessionFactory = configuration.BuildSessionFactory();
                }
            }
            return _sessionFactory.OpenSession();
        }
        #endregion
    }
}
