using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Logic.UI.ViewModels
{
    public class AddChannelWindowViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private string _channelName;
        private ChannelType _channelType = ChannelType.Public; // Default value
        private bool _isCreating;
        private string _errorMessage;

        // Expose enum values for the ComboBox
        public IEnumerable<ChannelType> AvailableChannelTypes => Enum.GetValues(typeof(ChannelType)).Cast<ChannelType>();

        public string ChannelName
        {
            get => _channelName;
            set
            {
                _channelName = value;
                OnPropertyChanged(nameof(ChannelName));
                // Update command can execute state
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ChannelType ChannelType
        {
            get => _channelType;
            set
            {
                _channelType = value;
                OnPropertyChanged(nameof(ChannelType));
            }
        }

        public bool IsCreating
        {
            get => _isCreating;
            set
            {
                _isCreating = value;
                OnPropertyChanged(nameof(IsCreating));
                // Update command can execute state
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

        public RelayCommand CreateChannelCommand { get; }

        // Event to notify the view when channel creation is successful
        public event EventHandler<Channel> ChannelCreated;

        // Event to close the window after operation
        public event EventHandler RequestClose;

        public AddChannelWindowViewModel()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5186/") };
            CreateChannelCommand = new RelayCommand(
                execute: CreateChannel,
                canExecute: () => !string.IsNullOrWhiteSpace(ChannelName) && !IsCreating
            );
        }

        private void CreateChannel()
        {
            // Start the async operation but don't wait for it
            _ = CreateChannelAsync();
        }

        private async Task CreateChannelAsync()
        {
            try
            {
                IsCreating = true;
                ErrorMessage = string.Empty;

                if (string.IsNullOrEmpty(TokenStorage.JwtToken))
                {
                    ErrorMessage = "Not logged in. Please log in first.";
                    return;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

                var request = new CreateChannelRequest
                {
                    Name = ChannelName,
                    Type = ChannelType
                };

                var response = await _httpClient.PostAsJsonAsync("api/channels", request);

                if (response.IsSuccessStatusCode)
                {
                    var createdChannel = await response.Content.ReadFromJsonAsync<Channel>();
                    Console.WriteLine($"DEBUG: Channel created successfully: {createdChannel.Name}");

                    // Notify subscribers about the new channel
                    ChannelCreated?.Invoke(this, createdChannel);

                    // Request window to close
                    RequestClose?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    ErrorMessage = $"Failed to create channel. Status: {response.StatusCode}. {errorContent}";
                    Console.WriteLine($"DEBUG: {ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                Console.WriteLine($"DEBUG: Exception creating channel: {ex.Message}");
            }
            finally
            {
                IsCreating = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}