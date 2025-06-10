namespace QuickChat.Client.Models
{
    public class Chat
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public bool IsGroup { get; set; }
        public Guid[] UserIds { get; set; }
        public string UserName { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastTime { get; set; }
    }
}