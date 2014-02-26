using JetBrains.Annotations;
using McNNTP.Server.Data;
using MoreLinq;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Linq;
using NHibernate.Tool.hbm2ddl;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace McNNTP.Server
{
    public class NntpServer
    {
        internal readonly Func<ISession> _sessionProvider;

        private readonly List<System.Tuple<Thread, NntpListener>> _listeners = new List<System.Tuple<Thread, NntpListener>>();

        internal readonly ConcurrentBag<Connection> _connections = new ConcurrentBag<Connection>();

        public bool AllowPosting { get; set; }
        public bool AllowStartTLS { get; set; }
        public int[] ClearPorts { get; set; }
        public int[] ExplicitTLSPorts { get; set; }
        public int[] ImplicitTLSPorts { get; set; }
        public string ServerPath { get; set; }

        public NntpServer([NotNull] Func<ISession> sessionProvider)
        {
            _sessionProvider = sessionProvider;

            // TODO: Put this in a custom config section
            ServerPath = "freenews.localhost";

            AllowStartTLS = true;
            ShowData = true;
        }

        #region Connection and IO
        public void Start()
        {
            _listeners.Clear();

            foreach (var clearPort in ClearPorts)
            {
                // Establish the local endpoint for the socket.
                var localEndPoint = new IPEndPoint(IPAddress.Any, clearPort);

                // Create a TCP/IP socket.
                var listener = new NntpListener(this, localEndPoint)
                {
                    PortType = PortClass.ClearText
                };

                _listeners.Add(new System.Tuple<Thread, NntpListener>(new Thread(listener.StartAccepting), listener));
            }

            foreach (var thread in _listeners)
                thread.Item1.Start();
        }

        public void Stop()
        {
            foreach (var listener in _listeners)
                listener.Item2.Stop();

            foreach (var connection in _connections)
                connection.Shutdown();

            foreach (var thread in _listeners)
                thread.Item1.Abort();
        }
        #endregion
        
        #region Interactivity
        public bool ShowBytes { get; set; }
        public bool ShowCommands { get; set; }
        public bool ShowData { get; set; }

        #endregion

        public void InitializeDatabase()
        {
            var configuration = new Configuration();
            configuration.AddAssembly(typeof(Newsgroup).Assembly);
            configuration.Configure();

            using (var connection = new SQLiteConnection(configuration.GetProperty("connection.connection_string")))
            {
                connection.Open();
                try
                {
                    var update = new SchemaUpdate(configuration);
                    update.Execute(false, true);

                    // Update failed..  recreate it.
                    if (!VerifyDatabase())
                    {
                        var export = new SchemaExport(configuration);
                        export.Execute(false, true, false, connection, null);

                        using (var session = _sessionProvider.Invoke())
                        {
                            session.Save(new Newsgroup
                            {
                                CreateDate = DateTime.UtcNow,
                                Description = "Control group for the repository",
                                Name = "freenews.config"
                            });
                            session.Close();
                        }
                    }
                    else
                    {
                        // Ensure placeholder data is there.
                        using (var session = _sessionProvider.Invoke())
                        {
                            var newsgroupCount = session.Query<Newsgroup>().Count(n => n.Name != null);
                            if (newsgroupCount > 0)
                                session.Save(new Newsgroup
                                {
                                    CreateDate = DateTime.UtcNow,
                                    Description = "Control group for the repository",
                                    Name = "freenews.config"
                                });
                        }
                    }
                }
                finally
                {
                    connection.Close();
                }
            }
        }

        public bool VerifyDatabase()
        {
            try
            {
                using (var session = _sessionProvider.Invoke())
                {
                    var newsgroupCount = session.Query<Newsgroup>().Count(n => n.Name != null);
                    Console.WriteLine("Verified database has {0} newsgroups", newsgroupCount);

                    var articleCount = session.Query<Article>().Count(a => a.Headers != null);
                    Console.WriteLine("Verified database has {0} articles", articleCount);

                    var adminCount = session.Query<Administrator>().Count(a => a.CanInject);
                    Console.WriteLine("Verified database has {0} local admins", adminCount);

                    session.Close();

                    return newsgroupCount > 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}