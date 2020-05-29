using System;
using System.Linq;
using mcnntp.common.client;

namespace mcnntp.client
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = args.Length > 0 ? args[0] : "news.fysh.org";
            Console.WriteLine("Hello World!");

            var client = new NntpClient();
            try
            {
                Console.WriteLine($"Connecting to {host}...");
                var connectTask = client.ConnectAsync(host).Result;
                Console.WriteLine($"Connected to {host}");

                var newsgroups = client.GetNewsgroups().Result;

                Console.WriteLine($"\r\nGroups count={newsgroups.Count}");
                foreach (var ng in newsgroups)
                    Console.WriteLine($"\t{ng}");

                Console.WriteLine($"\r\nNews");
                foreach (var ng in newsgroups)
                {
                    Console.WriteLine($"News for {ng}");
                    var news = client.GetNews(ng).Result;
                    Console.WriteLine($"\tCount={news.Count}");
                    foreach (var article in news.Take(2))
                    {
                        Console.WriteLine($"\t\t#{article.ArticleNumber}: {article.Subject}");
                        var body = client.Article(article.ArticleNumber).Result;
                        Console.WriteLine($"\t\t\tBODY: {body.Aggregate((c, n) => c + '\r' + '\n' + n)}");
                    }
                }
            }
            catch (ArgumentException aex)
            {
                Console.WriteLine($"Caught ArgumentException: {aex.Message}");
                return;
            }
            catch (AggregateException aex)
            {
                Console.WriteLine($"Caught AggregateException: {aex.Message}");
                foreach (var ex in aex.InnerExceptions)
                    Console.WriteLine($"...InnerException: {ex.Message}: {ex}");
                return;
            }

            client.Disconnect().GetAwaiter().GetResult();
        }
    }
}
