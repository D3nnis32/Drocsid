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
        private ObservableCollection<AvailablePluginInfo> _availablePlugins = new ObservableCollection<AvailablePluginInfo>();
        private AvailablePluginInfo _selectedAvailablePlugin;
        private bool _isLoading;
        private string _statusMessage;

        public ObservableCollection<AvailablePluginInfo> AvailablePlugins
        {
            get => _availablePlugins;
            set
            {
                _availablePlugins = value;
                OnPropertyChanged();
            }
        }

        public AvailablePluginInfo SelectedAvailablePlugin
        {
            get => _selectedAvailablePlugin;
            set
            {
                _selectedAvailablePlugin = value;
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
                canExecute: () => SelectedAvailablePlugin != null && !SelectedAvailablePlugin.IsLoaded
            );

            CloseWindowCommand = new RelayCommand(
                execute: CloseWindow
            );

            // Load plugins when created
            LoadAvailablePlugins();
        }

        private async void LoadAvailablePlugins()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading plugins...";

                var response = await _httpClient.GetAsync("api/plugins/available");
                
                if (response.IsSuccessStatusCode)
                {
                    var plugins = await response.Content.ReadFromJsonAsync<AvailablePluginInfo[]>();
                    
                    Application.Current.Dispatcher.Invoke(() => {
                        AvailablePlugins.Clear();
                        foreach (var plugin in plugins)
                        {
                            AvailablePlugins.Add(plugin);
                        }
                    });
                    
                    StatusMessage = $"Found {plugins.Length} plugins";
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
            if (SelectedAvailablePlugin == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading {SelectedAvailablePlugin.Name}...";

                // Load the plugin by file path
                var response = await _httpClient.PostAsync(
                    $"api/plugins/load-by-path?pluginPath={Uri.EscapeDataString(SelectedAvailablePlugin.FilePath)}", 
                    null);
                
                if (response.IsSuccessStatusCode)
                {
                    var loadedPlugin = await response.Content.ReadFromJsonAsync<LoadedPluginInfo>();
                    StatusMessage = $"Loaded {SelectedAvailablePlugin.Name}";
                    
                    // Update the plugin's status in our collection
                    var plugin = AvailablePlugins.FirstOrDefault(p => p.FilePath == SelectedAvailablePlugin.FilePath);
                    if (plugin != null)
                    {
                        plugin.IsLoaded = true;
                        // Trigger a refresh of the UI
                        SelectedAvailablePlugin = null;
                        SelectedAvailablePlugin = plugin;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    StatusMessage = $"Failed: {response.StatusCode}";
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

    /// <summary>
    /// Model for an available plugin
    /// </summary>
    public class AvailablePluginInfo
    {
        public string FileName { get; set; }
        public string Name { get; set; }
        public string FilePath { get; set; }
        public bool IsLoaded { get; set; }
        public string Type { get; set; }
    }

    /// <summary>
    /// Model for a loaded plugin
    /// </summary>
    public class LoadedPluginInfo
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