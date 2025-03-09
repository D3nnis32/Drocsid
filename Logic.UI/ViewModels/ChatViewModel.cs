using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace Logic.UI.ViewModels
{
    public class ChatViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly Channel _channel;
        private string _messageText;
        private bool _isSending;
        private string _errorMessage;
        private System.Windows.Threading.DispatcherTimer _refreshTimer;

        public ObservableCollection<Message> Messages { get; } = new ObservableCollection<Message>();

        public string ChannelName => _channel?.Name ?? "Unknown Channel";

        public string MessageText
        {
            get => _messageText;
            set
            {
                _messageText = value;
                OnPropertyChanged(nameof(MessageText));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsSending
        {
            get => _isSending;
            set
            {
                _isSending = value;
                OnPropertyChanged(nameof(IsSending));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        public RelayCommand SendMessageCommand { get; }
        public RelayCommand RefreshMessagesCommand { get; }

        public ChatViewModel(Channel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5186/") };

            SendMessageCommand = new RelayCommand(
                execute: SendMessage,
                canExecute: () => !string.IsNullOrWhiteSpace(MessageText) && !IsSending
            );

            RefreshMessagesCommand = new RelayCommand(
                execute: () => Task.Run(async () => await LoadMessagesAsync())
            );

            // Initialize timer on UI thread to avoid threading issues
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                // Set up a timer to refresh messages
                _refreshTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10)
                };
                _refreshTimer.Tick += (s, e) => Task.Run(async () => await LoadMessagesAsync());
                _refreshTimer.Start();
            });

            // Load initial messages
            Task.Run(async () => await LoadMessagesAsync());
        }

        private void SendMessage()
        {
            // Start the async operation
            _ = SendMessageAsync();
        }

        private async Task SendMessageAsync()
        {
            try
            {
                IsSending = true;
                ErrorMessage = string.Empty;

                if (string.IsNullOrEmpty(TokenStorage.JwtToken))
                {
                    ErrorMessage = "Not logged in. Please log in first.";
                    return;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

                var request = new CreateMessageRequest
                {
                    Content = MessageText
                };

                var response = await _httpClient.PostAsJsonAsync($"api/channels/{_channel.Id}/messages", request);

                if (response.IsSuccessStatusCode)
                {
                    var message = await response.Content.ReadFromJsonAsync<Message>();
                    Messages.Add(message);
                    MessageText = string.Empty; // Clear the input
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to send message: {response.StatusCode}. {errorContent}";
                    Console.WriteLine($"DEBUG: {ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                Console.WriteLine($"DEBUG: Exception sending message: {ex.Message}");
            }
            finally
            {
                IsSending = false;
            }
        }

        public async Task LoadMessagesAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(TokenStorage.JwtToken))
                {
                    Console.WriteLine("DEBUG: No JWT Token found. Cannot fetch messages.");
                    return;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

                var response = await _httpClient.GetAsync($"api/channels/{_channel.Id}/messages");

                if (response.IsSuccessStatusCode)
                {
                    var messages = await response.Content.ReadFromJsonAsync<Message[]>();

                    // We need to update on the UI thread
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        Messages.Clear();
                        foreach (var message in messages)
                        {
                            Messages.Add(message);
                        }
                    });

                    Console.WriteLine($"DEBUG: Loaded {messages.Length} messages.");
                }
                else
                {
                    Console.WriteLine($"DEBUG: Failed to fetch messages. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception fetching messages: {ex.Message}");
            }
        }

        // Make sure to clean up resources
        public void Dispose()
        {
            _refreshTimer.Stop();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}