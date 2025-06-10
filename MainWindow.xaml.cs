using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using QuickChat.Client.Models;
using QuickChat.Client.Services;
using QuickChat.Client.Utilities;

namespace QuickChat.Client
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient = new();
        private readonly AuthService _authService;
        private readonly ChatService _chatService;
        private readonly SignalRService _signalRService;
        private string _token;
        private Guid _currentUserId;
        private User[] _topThreeUsers;
        private int _currentPage = 1;
        private const int _pageSize = 10;
        private DispatcherTimer _messageUpdateTimer;

        public ObservableCollection<User> Users { get; } = new();
        public ObservableCollection<Chat> Chats { get; } = new();
        public ObservableCollection<Message> Messages { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            _httpClient.BaseAddress = new Uri("https://localhost:5001/");
            _authService = new AuthService(_httpClient);
            _chatService = new ChatService(_httpClient, Users, Messages);
            _signalRService = new SignalRService(Users, Messages, Chats, Dispatcher, _chatService, ChatIdCombo);

            UsersList.ItemsSource = Users;
            ChatsList.ItemsSource = Chats;
            MessagesList.ItemsSource = Messages;

            UsersTab.IsEnabled = false;
            ChatsTab.IsEnabled = false;
            MessagesTab.IsEnabled = false;

            MainTabControl.SelectedItem = LoginTab;

            _messageUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _messageUpdateTimer.Tick += async (s, e) => await UpdateMessagesIfNeeded();
            _pageText = PageText;
        }

        private TextBlock _pageText;

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            (bool success, string message) = await _authService.RegisterAsync(RegisterUsername.Text, RegisterPassword.Password);
            RegisterMessage.Text = message;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            (bool success, string token, string message) = await _authService.LoginAsync(LoginUsername.Text, LoginPassword.Password);
            LoginMessage.Text = message;

            if (!success) return;

            _token = token;
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            _currentUserId = Guid.Parse(JwtUtils.GetClaim(_token, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"));

            UsersTab.IsEnabled = true;
            ChatsTab.IsEnabled = true;
            MessagesTab.IsEnabled = true;

            MainTabControl.SelectedItem = null;
            UsersList.IsEnabled = true;
            ChatsList.IsEnabled = true;

            await _signalRService.ConnectAsync(_token, _currentUserId);
            (bool usersSuccess, User[] topThreeUsers, string usersMessage) = await _chatService.LoadUsersAsync(_currentUserId);
            if (usersSuccess)
            {
                Users.Clear();
                foreach (var user in topThreeUsers) Users.Add(user);
                _topThreeUsers = topThreeUsers;
                UsersMessage.Text = usersMessage;
            }
            await LoadChatsAsync();
            _messageUpdateTimer.Start();
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            await _signalRService.DisconnectAsync();
            _token = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
            _currentUserId = Guid.Empty;
            _topThreeUsers = null;
            _currentPage = 1;

            Users.Clear();
            Chats.Clear();
            Messages.Clear();

            UsersTab.IsEnabled = false;
            ChatsTab.IsEnabled = false;
            MessagesTab.IsEnabled = false;
            UsersList.IsEnabled = false;
            ChatsList.IsEnabled = false;

            MainTabControl.SelectedItem = LoginTab;
            ChatsMessage.Text = "Logged out successfully!";
            _messageUpdateTimer.Stop();
        }

        private async Task LoadChatsAsync()
        {
            (bool success, string message) = await _chatService.LoadChatsAsync(Chats, _currentUserId);
            ChatsMessage.Text = message;
            ChatIdCombo.ItemsSource = Chats;
        }

        private async void CreateChatButton_Click(object sender, RoutedEventArgs e)
        {
            Guid? otherUserId = string.IsNullOrWhiteSpace(OtherUserId.Text) ? null : Guid.Parse(OtherUserId.Text);
            (bool success, string message) = await _chatService.CreateChatAsync(ChatName.Text, IsGroup.IsChecked ?? false, otherUserId);
            ChatsMessage.Text = message;
            if (success) await LoadChatsAsync();
        }

        private async void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            var chatId = (Guid?)ChatIdCombo.SelectedValue;
            var messageText = MessageText.Text;
            (bool success, string message) = await _chatService.SendMessageAsync(chatId, messageText, _currentUserId, Chats, Users);
            MessagesMessage.Text = message;

            if (success)
            {
                MessageText.Clear();
                await LoadMessagesAsync(chatId!.Value);

                if (_topThreeUsers != null)
                {
                    var otherUserId = _chatService.GetOtherUserId(chatId!.Value, Chats, _currentUserId);
                    var targetUser = _topThreeUsers.FirstOrDefault(u => u.Id == otherUserId);
                    if (targetUser != null && targetUser.IsOnline)
                    {
                        await _chatService.SimulateResponseAsync(chatId!.Value, targetUser.Id, messageText, Users);
                    }
                }
            }
        }

        private async Task LoadMessagesAsync(Guid chatId)
        {
            (bool success, string message) = await _chatService.LoadMessagesAsync(chatId, _currentPage, _pageSize, Messages, _currentUserId);
            MessagesMessage.Text = message;
            if (success)
            {
                _pageText.Text = $"Страница: {_currentPage}";
                _signalRService.UpdateReadStatus(_currentUserId);
                MessagesList.Items.Refresh();
            }
        }

        private async Task UpdateMessagesIfNeeded()
        {
            if (MainTabControl.SelectedItem == MessagesTab && ChatIdCombo.SelectedValue != null)
            {
                await LoadMessagesAsync((Guid)ChatIdCombo.SelectedValue);
            }
        }

        private async void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                var chatId = (Guid?)ChatIdCombo.SelectedValue;
                if (chatId.HasValue)
                {
                    await LoadMessagesAsync(chatId.Value);
                }
            }
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            var chatId = (Guid?)ChatIdCombo.SelectedValue;
            if (chatId.HasValue)
            {
                await LoadMessagesAsync(chatId.Value);
            }
        }

        private async void UsersList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (UsersList.SelectedItem is User selectedUser)
            {
                var existingChat = Chats.FirstOrDefault(chat =>
                    !chat.IsGroup && chat.UserIds.Contains(selectedUser.Id) && chat.UserIds.Contains(_currentUserId));

                if (existingChat != null)
                {
                    MessageBox.Show($"Chat with {selectedUser.Username} already exists!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    ChatsList.SelectedItem = existingChat;
                    ChatIdCombo.SelectedValue = existingChat.Id;
                    _currentPage = 1;
                    await LoadMessagesAsync(existingChat.Id);
                    await _signalRService.JoinChatAsync(existingChat.Id);
                    MainTabControl.SelectedItem = MessagesTab;
                }
                else
                {
                    (bool success, string message) = await _chatService.CreateChatAsync("", false, selectedUser.Id);
                    ChatsMessage.Text = message;
                    if (success)
                    {
                        await LoadChatsAsync();
                        var createdChat = Chats.FirstOrDefault(c => c.UserIds.Contains(selectedUser.Id) && !c.IsGroup);
                        if (createdChat != null)
                        {
                            ChatsList.SelectedItem = createdChat;
                            ChatIdCombo.SelectedValue = createdChat.Id;
                            _currentPage = 1;
                            await LoadMessagesAsync(createdChat.Id);
                            await _signalRService.JoinChatAsync(createdChat.Id);
                            MainTabControl.SelectedItem = MessagesTab;
                        }
                    }
                }
            }
        }

        private async void ChatsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChatsList.SelectedItem is Chat selectedChat)
            {
                ChatIdCombo.SelectedValue = selectedChat.Id;
                _currentPage = 1;
                await LoadMessagesAsync(selectedChat.Id);
                await _signalRService.JoinChatAsync(selectedChat.Id);
                MainTabControl.SelectedItem = MessagesTab;
            }
        }

        private async void UpdateUsernameButton_Click(object sender, RoutedEventArgs e)
        {
            (bool success, string token, string message) = await _authService.UpdateUsernameAsync(_currentUserId, NewUsernameText.Text);
            UsersMessage.Text = message;

            if (success)
            {
                _token = token;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                _currentUserId = Guid.Parse(JwtUtils.GetClaim(_token, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"));
                LogoutButton_Click(null, null);
                LoginUsername.Text = NewUsernameText.Text;
                MainTabControl.SelectedItem = LoginTab;
            }
            NewUsernameText.Clear();
        }
    }
}