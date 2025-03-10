using Drocsid.HenrikDennis2025.Core.DTO;
using Drocsid.HenrikDennis2025.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Logic.UI.ViewModels
{
    public class AddMembersViewModel
    {
        private readonly HttpClient _httpClient;
        private readonly Guid _channelId;
        public ObservableCollection<User> AvailableUsers { get; } = new();
        public User SelectedUser { get; set; }

        public ICommand AddUserCommand { get; }
        public ICommand CloseWindowCommand { get; }

        public AddMembersViewModel(Guid channelId)
        {
            _channelId = channelId;
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5186/") };

            AddUserCommand = new RelayCommand(async () => await AddUser(), () => SelectedUser != null);
            CloseWindowCommand = new RelayCommand(CloseWindow);

            // Load available users in background
            Task.Run(async () => await LoadAvailableUsersAsync());
        }

        private async Task LoadAvailableUsersAsync()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

                // Adjust the URL as needed for your API; here we assume a GET endpoint for users.
                var response = await _httpClient.GetAsync("api/users");

                if (response.IsSuccessStatusCode)
                {
                    var users = await response.Content.ReadFromJsonAsync<User[]>();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableUsers.Clear();
                        foreach (var user in users)
                        {
                            AvailableUsers.Add(user);
                        }
                    });
                }
                else
                {
                    MessageBox.Show("Failed to load users.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Failed to load users: {ex.Message}");
            }
        }

        private async Task AddUser()
        {
            if (SelectedUser == null)
                return;

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

                var request = new AddMemberRequest { UserId = SelectedUser.Id };

                var response = await _httpClient.PostAsJsonAsync($"api/channels/{_channelId}/members", request);

                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"User {SelectedUser.Username} added successfully!");
                }
                else
                {
                    MessageBox.Show("Failed to add user to channel.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Failed to add user: {ex.Message}");
            }
        }

        private void CloseWindow()
        {
            // Close the current window (assuming it's the topmost)
            if (Application.Current.Windows.Count > 0)
                Application.Current.Windows[Application.Current.Windows.Count - 1].Close();
        }
    }
}
