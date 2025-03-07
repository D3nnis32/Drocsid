using Drocsid.HenrikDennis2025.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace Logic.UI.ViewModels
{
    public class ChatInterfaceUserControlViewModel:INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;

        public ObservableCollection<Channel> Channels { get; set; } = new();
        private Channel _selectedChannel;

        

        public Channel SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                _selectedChannel = value;
                OnPropertyChanged(nameof(SelectedChannel));
            }
        }

        public RelayCommand LoadChannelsCommand { get; }

        public ChatInterfaceUserControlViewModel()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5186/") };
            LoadChannelsCommand = new RelayCommand(async (_) => await LoadChannelsAsync());

            Task.Run(async () => LoadChannelsAsync());
        }
        private async Task LoadChannelsAsync()
        {
            try
            {
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
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
