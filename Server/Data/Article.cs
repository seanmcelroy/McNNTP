namespace McNNTP.Server.Data
{
    public class Article
    {
        public virtual long Id { get; set; }
        public virtual Newsgroup Newsgroup { get; set; }
        public virtual string Subject { get; set; }
        public virtual string Author { get; set; }
        public virtual string Date { get; set; }
        public virtual string References { get; set; }
        public virtual string MessageId { get; set; }
        public virtual string Body { get; set; }
    }
}
