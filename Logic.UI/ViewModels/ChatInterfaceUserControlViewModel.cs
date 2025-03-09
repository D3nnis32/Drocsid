using Drocsid.HenrikDennis2025.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Logic.UI.ViewModels
{
    public class ChatInterfaceUserControlViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        public ObservableCollection<Channel> Channels { get; set; } = new();
        private Channel _selectedChannel;
        private ChatViewModel _currentChatViewModel;
        private string _searchQuery;
        private bool _isLoading;

        public Channel SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                _selectedChannel = value;
                OnPropertyChanged(nameof(SelectedChannel));

                // Load the chat view for the selected channel
                if (_selectedChannel != null)
                {
                    CurrentChatViewModel = new ChatViewModel(_selectedChannel);
                    OnPropertyChanged(nameof(CurrentView));
                }
            }
        }

        public object CurrentView => _currentChatViewModel ?? (object)"No channel selected. Select a channel from the list or create a new one.";

        public ChatViewModel CurrentChatViewModel
        {
            get => _currentChatViewModel;
            set
            {
                _currentChatViewModel = value;
                OnPropertyChanged(nameof(CurrentChatViewModel));
                OnPropertyChanged(nameof(CurrentView));
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged(nameof(SearchQuery));
                FilterChannels();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public RelayCommand LoadChannelsCommand { get; }
        public RelayCommand OpenNewChannelWindowCommand { get; }

        public ChatInterfaceUserControlViewModel()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5186/") };
            LoadChannelsCommand = new RelayCommand(async (_) => await LoadChannelsAsync());
            OpenNewChannelWindowCommand = new RelayCommand(() => OpenNewChannelWindow());
            Task.Run(async () => await LoadChannelsAsync());
        }

        // Define event args class to pass the window
        public class OpenNewChannelWindowEventArgs : EventArgs
        {
            public Action OnWindowClosed { get; set; }
        }

        // Update the event type
        public event EventHandler<OpenNewChannelWindowEventArgs> RequestOpenNewChannelWindow;

        private void OpenNewChannelWindow()
        {
            // Create event args with a callback to refresh channels
            var args = new OpenNewChannelWindowEventArgs
            {
                OnWindowClosed = async () => await LoadChannelsAsync()
            };

            // Raise the event with the callback
            RequestOpenNewChannelWindow?.Invoke(this, args);
        }

        private void FilterChannels()
        {
            // If you want to implement search functionality
            // You could filter the channels based on the search query
        }

        public async Task LoadChannelsAsync()
        {
            try
            {
                IsLoading = true;
                if (string.IsNullOrEmpty(TokenStorage.JwtToken))
                {
                    Console.WriteLine("DEBUG: No JWT Token found. Cannot fetch channels.");
                    return;
                }
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);
                var response = await _httpClient.GetAsync("api/channels");
                Console.WriteLine($"DEBUG: Fetching Channels - Status: {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    var channels = await response.Content.ReadFromJsonAsync<Channel[]>();
                    Channels.Clear();
                    foreach (var channel in channels)
                    {
                        Channels.Add(channel);
                    }
                    Console.WriteLine($"DEBUG: Loaded {Channels.Count} channels.");

                    // If there are channels but none selected, select the first one
                    if (Channels.Count > 0 && SelectedChannel == null)
                    {
                        SelectedChannel = Channels[0];
                    }
                }
                else
                {
                    Console.WriteLine($"DEBUG: Failed to fetch channels. Status: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception fetching channels: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}