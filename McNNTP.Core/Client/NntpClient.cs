namespace McNNTP.Core.Client
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    [UsedImplicitly]
    public class NntpClient : TcpClient
    {
        public bool CanPost { get; private set; }

        public int Port { get; set; }

        public NntpClient()
        {
            Port = 119;
        }

        #region Connections
        public Task ConnectAsync(string hostName)
        {
            return ConnectAsync(hostName, Port);
        }

        public new async Task Connect(string hostName, int port)
        {
            await ConnectAsync(hostName, port);
            var response = await Response();

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
        public async Task Disconnect()
        {
            const string Message = "QUIT\r\n";
            await this.WriteAsync(Message);
            var response = await Response();
            if (response.Substring(0, 3) != "205")
                throw new NntpException(response);
        }
        #endregion

        #region IO
        public async Task<string> Response()
        {
            var enc = new System.Text.ASCIIEncoding();
            var serverbuff = new byte[1024];
            var stream = GetStream();
            var count = await stream.ReadAsync(serverbuff, 0, 1024);
            return count == 0 ? string.Empty : enc.GetString(serverbuff, 0, count);
        }
        public async Task WriteAsync(string message)
        {
            var en = new System.Text.ASCIIEncoding();
            var writeBuffer = en.GetBytes(message);
            var stream = GetStream();
            await stream.WriteAsync(writeBuffer, 0, writeBuffer.Length);
        }
        #endregion

        public async Task<ReadOnlyCollection<string>> GetCapabilities()
        {
            await this.WriteAsync("CAPABILITIES\r\n");
            var response = await Response();
            if (response.Substring(0, 3) != "101")
                throw new NntpException(response);

            return new ReadOnlyCollection<string>(response.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Where(x => x != ".")
                .ToList());
        }

        public async Task<ReadOnlyCollection<string>> GetNewsgroups()
        {
            await this.WriteAsync("LIST\r\n");
            var response = await Response();
            if (response.Substring(0, 3) != "215")
                throw new NntpException(response);

            var retval = new List<string>();
            while (true)
            {
                response = await Response();
                if (response == ".\r\n" ||
                    response == ".\n")
                    return new ReadOnlyCollection<string>(retval);
                var values = response.Split(' ');
                retval.Add(values[0]);
            }
        }
        public async Task<ReadOnlyCollection<string>> GetNews(string newsgroup)
        {
            var topics = new List<string>();
            var message = "GROUP " + newsgroup + "\r\n";
            await this.WriteAsync(message);
            var response = await Response();
            if (response.Substring(0, 3) != "211")
            {
                throw new NntpException(response);
            }

            char[] seps = { ' ' };
            var values = response.Split(seps);

            var start = int.Parse(values[2], CultureInfo.InvariantCulture);
            var end = int.Parse(values[3], CultureInfo.InvariantCulture);

            if (start + 100 < end && end > 100)
            {
                start = end - 100;
            }

            for (var i = start; i < end; i++)
            {
                message = "ARTICLE " + i + "\r\n";
                await this.WriteAsync(message);
                response = await Response();
                if (response.Substring(0, 3) == "423")
                    continue;

                if (response.Substring(0, 3) != "220")
                    throw new NntpException(response);

                var article = string.Empty;
                while (true)
                {
                    response = await Response();
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

        public async Task Post(string newsgroup, string subject, string from, string content)
        {
            var message = "POST\r\n";
            await this.WriteAsync(message);
            var response = await Response();
            if (response.Substring(0, 3) != "340")
            {
                throw new NntpException(response);
            }

            message = "From: " + from + "\r\n"
                + "Newsgroups: " + newsgroup + "\r\n"
                + "Subject: " + subject + "\r\n\r\n"
                + content + "\r\n.\r\n";
            await this.WriteAsync(message);
            response = await Response();
            if (response.Substring(0, 3) != "240")
            {
                throw new NntpException(response);
            }
        }
    }
}
