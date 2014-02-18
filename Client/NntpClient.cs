using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Globalization;
using System.Collections.ObjectModel;

namespace McNNTP.Client
{
    public class NntpClient : TcpClient
    {
        public bool CanPost { get; private set; }

        public int Port { get; set; }

        public NntpClient()
        {
            Port = 119;
        }

        #region Connections
        public void Connect(string hostName)
        {
            Connect(hostName, Port);
        }

        public new void Connect(string hostName, int port)
        {
            base.Connect(hostName, port);
            var response = Response();

            switch (response.Substring(0, 3))
            {
                case "200":
                    CanPost = true;
                    return;
                case "201":
                    CanPost = false;
                    return;
                default:
                    throw new NntpException(response);
            }
        }
        public void Disconnect()
        {
            const string message = "QUIT\r\n";
            Write(message);
            var response = Response();
            if (response.Substring(0, 3) != "205")
                throw new NntpException(response);
        }
        #endregion

        #region IO
        public string Response()
        {
            var enc = new System.Text.ASCIIEncoding();
            var serverbuff = new Byte[1024];
            var stream = GetStream();
            var count = stream.Read(serverbuff, 0, 1024);
            return count == 0 ? string.Empty : enc.GetString(serverbuff, 0, count);
        }
        public void Write(string message)
        {
            var en = new System.Text.ASCIIEncoding();
            var writeBuffer = en.GetBytes(message);
            var stream = GetStream();
            stream.Write(writeBuffer, 0, writeBuffer.Length);
        }
        #endregion

        public ReadOnlyCollection<string> GetCapabilities()
        {
            Write("CAPABILITIES\r\n");
            var response = Response();
            if (response.Substring(0, 3) != "101")
                throw new NntpException(response);

            return new ReadOnlyCollection<string>(response.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Where(x => x != ".")
                .ToList());
        }

        public ReadOnlyCollection<string> GetNewsgroups()
        {
            Write("LIST\r\n");
            var response = Response();
            if (response.Substring(0, 3) != "215")
                throw new NntpException(response);

            var retval = new List<string>();
            while (true)
            {
                response = Response();
                if (response == ".\r\n" ||
                    response == ".\n")
                    return new ReadOnlyCollection<string>(retval);
                var values = response.Split(' ');
                retval.Add(values[0]);
            }
        }
        public ReadOnlyCollection<string> GetNews(string newsgroup)
        {
            var topics = new List<string>();
            var message = "GROUP " + newsgroup + "\r\n";
            Write(message);
            var response = Response();
            if (response.Substring(0, 3) != "211")
            {
                throw new NntpException(response);
            }

            char[] seps = { ' ' };
            var values = response.Split(seps);

            long start = Int32.Parse(values[2], CultureInfo.InvariantCulture);
            long end = Int32.Parse(values[3], CultureInfo.InvariantCulture);

            if (start + 100 < end && end > 100)
            {
                start = end - 100;
            }

            for (var i = start; i < end; i++)
            {
                message = "ARTICLE " + i + "\r\n";
                Write(message);
                response = Response();
                if (response.Substring(0, 3) == "423")
                {
                    continue;
                }
                if (response.Substring(0, 3) != "220")
                {
                    throw new NntpException(response);
                }

                var article = "";
                while (true)
                {
                    response = Response();
                    if (response == ".\r\n")
                        break;

                    if (response == ".\n")
                        break;

                    if (article.Length < 1024)
                        article += response;
                }

                topics.Add(article);
            }

            return new ReadOnlyCollection<string>(topics);
        }
        public void Post(string newsgroup, string subject, string from, string content)
        {
            var message = "POST\r\n";
            Write(message);
            var response = Response();
            if (response.Substring(0, 3) != "340")
            {
                throw new NntpException(response);
            }

            message = "From: " + from + "\r\n"
                + "Newsgroups: " + newsgroup + "\r\n"
                + "Subject: " + subject + "\r\n\r\n"
                + content + "\r\n.\r\n";
            Write(message);
            response = Response();
            if (response.Substring(0, 3) != "240")
            {
                throw new NntpException(response);
            }
        }
    }
}
