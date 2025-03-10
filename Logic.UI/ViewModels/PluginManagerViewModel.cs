using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Drocsid.HenrikDennis2025.Core.Models;

namespace Logic.UI.ViewModels
{
    public class PluginManagerViewModel : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient;
        private readonly Window _window;
        private ObservableCollection<PluginInfo> _availablePlugins = new ObservableCollection<PluginInfo>();
        private PluginInfo _selectedPlugin;
        private bool _isLoading;
        private string _statusMessage;

        public ObservableCollection<PluginInfo> AvailablePlugins
        {
            get => _availablePlugins;
            set
            {
                _availablePlugins = value;
                OnPropertyChanged();
            }
        }

        public PluginInfo SelectedPlugin
        {
            get => _selectedPlugin;
            set
            {
                _selectedPlugin = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadPluginCommand { get; }
        public ICommand CloseWindowCommand { get; }

        public PluginManagerViewModel(Window window)
        {
            _window = window;
            _httpClient = new HttpClient();
            
            // Use the same API endpoint as in the ChatViewModel
            var baseUrl = TokenStorage.CurrentNodeEndpoint ?? "http://localhost:5186";
            _httpClient.BaseAddress = new Uri(baseUrl);
            
            // Add the auth token
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", TokenStorage.JwtToken);

            // Initialize commands
            LoadPluginCommand = new RelayCommand(
                execute: LoadSelectedPlugin,
                canExecute: () => SelectedPlugin != null && SelectedPlugin.State != "Running"
            );

            CloseWindowCommand = new RelayCommand(
                execute: CloseWindow
            );

            // Load plugins when created
            LoadPlugins();
        }

        private async void LoadPlugins()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading plugins...";

                var response = await _httpClient.GetAsync("api/plugins");
                
                if (response.IsSuccessStatusCode)
                {
                    var plugins = await response.Content.ReadFromJsonAsync<PluginInfo[]>();
                    
                    Application.Current.Dispatcher.Invoke(() => {
                        AvailablePlugins.Clear();
                        foreach (var plugin in plugins)
                        {
                            AvailablePlugins.Add(plugin);
                        }
                    });
                    
                    StatusMessage = $"Loaded {plugins.Length} plugins";
                }
                else
                {
                    StatusMessage = $"Error: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void LoadSelectedPlugin()
        {
            if (SelectedPlugin == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading plugin: {SelectedPlugin.Name}...";

                var response = await _httpClient.PostAsync(
                    $"api/plugins/load?pluginName={Uri.EscapeDataString(SelectedPlugin.Name)}", 
                    null);
                
                if (response.IsSuccessStatusCode)
                {
                    var loadedPlugin = await response.Content.ReadFromJsonAsync<PluginInfo>();
                    StatusMessage = $"Successfully loaded plugin: {SelectedPlugin.Name}";
                    
                    // Refresh the plugin list
                    LoadPlugins();
                }
                else
                {
                    StatusMessage = $"Failed to load plugin: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CloseWindow()
        {
            _window.Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PluginInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string State { get; set; }
        public string Type { get; set; }
    }
}