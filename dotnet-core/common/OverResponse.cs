namespace mcnntp.common
{
    public struct OverResponse
    {
        public int ArticleNumber { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public string Date { get; set; }
        public string MessageID { get; set; }
        public string References { get; set; }
        public int Bytes { get; set; }
        public int Lines { get; set; }
    }
}