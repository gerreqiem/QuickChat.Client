using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using QuickChat.Client.Models;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Threading;

namespace QuickChat.Client.Services
{
    public class SignalRService
    {
        private HubConnection _hubConnection;
        private readonly ObservableCollection<User> _users;
        private readonly ObservableCollection<Message> _messages;
        private readonly ObservableCollection<Chat> _chats;
        private readonly Dispatcher _dispatcher;
        private readonly ChatService _chatService;
        private readonly ComboBox _chatIdCombo;

        public SignalRService(ObservableCollection<User> users, ObservableCollection<Message> messages, ObservableCollection<Chat> chats, Dispatcher dispatcher, ChatService chatService, ComboBox chatIdCombo)
        {
            _users = users;
            _messages = messages;
            _chats = chats;
            _dispatcher = dispatcher;
            _chatService = chatService;
            _chatIdCombo = chatIdCombo;
        }

        public async Task ConnectAsync(string token, Guid currentUserId)
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl("https://localhost:5001/chathub", options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .WithAutomaticReconnect()
                .Build();

            _hubConnection.On<Guid, Guid, Guid, string, DateTime, bool>("ReceiveMessage", (messageId, senderId, chatId, message, sentAt, isRead) =>
            {
                _dispatcher.Invoke(async () =>
                {
                    var isSentByMe = senderId == currentUserId;
                    var senderName = _users.FirstOrDefault(u => u.Id == senderId)?.Username ?? "Unknown";
                    if (_chatIdCombo.SelectedValue != null && (Guid)_chatIdCombo.SelectedValue == chatId)
                    {
                        _messages.Add(new Message
                        {
                            Id = messageId,
                            ChatId = chatId,
                            SenderId = senderId,
                            SenderName = senderName,
                            Text = message,
                            SentAt = sentAt,
                            IsRead = isRead,
                            IsSentByMe = isSentByMe
                        });
                        UpdateReadStatus(currentUserId);
                    }
                    await _chatService.LoadChatsAsync(_chats, currentUserId);
                });
            });

            _hubConnection.On<Guid>("UserOnline", userId =>
            {
                _dispatcher.Invoke(() =>
                {
                    var user = _users.FirstOrDefault(u => u.Id == userId);
                    if (user != null) user.IsOnline = true;
                });
            });

            _hubConnection.On<Guid>("UserOffline", userId =>
            {
                _dispatcher.Invoke(() =>
                {
                    var user = _users.FirstOrDefault(u => u.Id == userId);
                    if (user != null) user.IsOnline = false;
                });
            });

            _hubConnection.On<Guid[]>("MessagesUpdated", messageIds =>
            {
                _dispatcher.Invoke(() =>
                {
                    foreach (var messageId in messageIds)
                    {
                        var message = _messages.FirstOrDefault(m => m.Id == messageId);
                        if (message != null)
                        {
                            message.IsRead = true;
                        }
                    }
                    _chatIdCombo.Items.Refresh();
                });
            });

            await _hubConnection.StartAsync();
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                _hubConnection = null;
            }
        }

        public async Task JoinChatAsync(Guid chatId)
        {
            if (_hubConnection != null)
            {
                await _hubConnection.InvokeAsync("JoinChat", chatId);
            }
        }

        public void UpdateReadStatus(Guid currentUserId)
        {
            var currentChatId = (Guid?)_chatIdCombo.SelectedValue;
            if (currentChatId.HasValue)
            {
                var chat = _chats.FirstOrDefault(c => c.Id == currentChatId);
                if (chat != null && chat.UserIds.Length == 2)
                {
                    var otherUserId = chat.UserIds.FirstOrDefault(id => id != currentUserId);
                    var otherUser = _users.FirstOrDefault(u => u.Id == otherUserId);
                    foreach (var message in _messages)
                    {
                        if (message.SenderId != currentUserId && !message.IsRead && otherUser?.IsOnline == true)
                        {
                            message.IsRead = true;
                        }
                    }
                    _chatIdCombo.Items.Refresh();
                }
            }
        }
    }
}