namespace QuickChat.Client.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; }
        public bool IsOnline { get; set; }
    }
}