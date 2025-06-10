using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuickChat.Client.Models;
using System.Collections.ObjectModel;

namespace QuickChat.Client.Services
{
    public class ChatService
    {
        private readonly HttpClient _httpClient;
        private readonly ObservableCollection<User> _users;
        private readonly ObservableCollection<Message> _messages;

        public ChatService(HttpClient httpClient, ObservableCollection<User> users, ObservableCollection<Message> messages)
        {
            _httpClient = httpClient;
            _users = users;
            _messages = messages;
        }

        public async Task<(bool Success, User[] TopThreeUsers, string Message)> LoadUsersAsync(Guid currentUserId)
        {
            var response = await _httpClient.GetAsync("api/users");
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, "Failed to load users.");
            }

            var users = JsonConvert.DeserializeObject<User[]>(await response.Content.ReadAsStringAsync());
            return (true, users.Where(u => u.Id != currentUserId).Take(3).ToArray(), "");
        }

        public async Task<(bool Success, string Message)> LoadChatsAsync(ObservableCollection<Chat> chats, Guid currentUserId)
        {
            var response = await _httpClient.GetAsync("api/chats");
            if (!response.IsSuccessStatusCode)
            {
                return (false, "Failed to load chats.");
            }

            var chatArray = JsonConvert.DeserializeObject<Chat[]>(await response.Content.ReadAsStringAsync());
            chats.Clear();
            foreach (var chat in chatArray)
            {
                if (!chat.IsGroup && chat.UserIds.Length == 2)
                {
                    var otherUser = _users.FirstOrDefault(u => u.Id == chat.UserIds.FirstOrDefault(id => id != currentUserId));
                    chat.UserName = otherUser?.Username ?? "Unknown";
                    chat.LastMessage = _messages.Where(m => m.ChatId == chat.Id).OrderByDescending(m => m.SentAt).FirstOrDefault()?.Text ?? "No messages yet";
                    chat.LastTime = _messages.Where(m => m.ChatId == chat.Id).OrderByDescending(m => m.SentAt).FirstOrDefault()?.SentAt ?? DateTime.Now;
                }
                else
                {
                    chat.UserName = chat.Name ?? "Group Chat";
                    chat.LastMessage = _messages.Where(m => m.ChatId == chat.Id).OrderByDescending(m => m.SentAt).FirstOrDefault()?.Text ?? "No messages yet";
                    chat.LastTime = _messages.Where(m => m.ChatId == chat.Id).OrderByDescending(m => m.SentAt).FirstOrDefault()?.SentAt ?? DateTime.Now;
                }
                chats.Add(chat);
            }
            return (true, "");
        }

        public async Task<(bool Success, string Message)> CreateChatAsync(string chatName, bool isGroup, Guid? otherUserId)
        {
            var newChat = new { Name = chatName, IsGroup = isGroup, OtherUserId = otherUserId };
            var content = new StringContent(JsonConvert.SerializeObject(newChat), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/chats", content);
            return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Chat created successfully!" : "Failed to create chat.");
        }

        public async Task<(bool Success, string Message)> LoadMessagesAsync(Guid chatId, int page, int pageSize, ObservableCollection<Message> messages, Guid currentUserId)
        {
            var response = await _httpClient.GetAsync($"api/messages/{chatId}?page={page}&size={pageSize}");
            if (!response.IsSuccessStatusCode)
            {
                return (false, "Failed to load messages.");
            }

            var messageArray = JsonConvert.DeserializeObject<Message[]>(await response.Content.ReadAsStringAsync());
            messages.Clear();
            foreach (var message in messageArray)
            {
                message.IsSentByMe = message.SenderId == currentUserId;
                message.SenderName = _users.FirstOrDefault(u => u.Id == message.SenderId)?.Username ?? "Unknown";
                messages.Add(message);
            }
            return (true, "");
        }

        public async Task<(bool Success, string Message)> SendMessageAsync(Guid? chatId, string messageText, Guid currentUserId, ObservableCollection<Chat> chats, ObservableCollection<User> users)
        {
            if (chatId == null)
            {
                return (false, "Please select a chat.");
            }
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return (false, "Message cannot be empty.");
            }

            var chat = chats.FirstOrDefault(c => c.Id == chatId);
            if (chat == null)
            {
                return (false, "Chat not found.");
            }

            var otherUserId = chat.UserIds.FirstOrDefault(id => id != currentUserId);
            var otherUser = users.FirstOrDefault(u => u.Id == otherUserId);
            var isRead = otherUser?.IsOnline ?? false;
            var newMessage = new { ChatId = chatId, SenderId = currentUserId, Text = messageText, IsRead = isRead };
            var content = new StringContent(JsonConvert.SerializeObject(newMessage), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/messages", content);
            return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "" : "Failed to send message.");
        }

        public async Task SimulateResponseAsync(Guid chatId, Guid responderId, string receivedMessage, ObservableCollection<User> users)
        {
            var responder = users.FirstOrDefault(u => u.Id == responderId);
            if (responder == null) return;

            string responseMessage;
            switch (responder.Username.ToLower())
            {
                case "alice":
                    responseMessage = $"Ответ от Alice: {receivedMessage} - Okay!";
                    break;
                case "bob":
                    responseMessage = $"Ответ от Bob: {receivedMessage}.";
                    break;
                case "charlie":
                    responseMessage = $"Ответ от Charlie: {receivedMessage} - Understood!";
                    break;
                default:
                    responseMessage = $"Ответ от {responder.Username}: {receivedMessage} - Понял!";
                    break;
            }

            var newMessage = new { ChatId = chatId, SenderId = responderId, Text = responseMessage, IsRead = false };
            var content = new StringContent(JsonConvert.SerializeObject(newMessage), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/messages", content);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Simulated response sent from {responderId} ({responder.Username}): {responseMessage}");
            }
            else
            {
                Console.WriteLine("Failed to simulate response.");
            }
        }

        public Guid GetOtherUserId(Guid chatId, ObservableCollection<Chat> chats, Guid currentUserId)
        {
            var chat = chats.FirstOrDefault(c => c.Id == chatId);
            if (chat != null && chat.UserIds.Length == 2)
                return chat.UserIds.FirstOrDefault(id => id != currentUserId);
            return Guid.Empty;
        }
    }
}