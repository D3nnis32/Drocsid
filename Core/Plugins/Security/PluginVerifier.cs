using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Drocsid.HenrikDennis2025.PluginContracts.Interfaces;

namespace Drocsid.HenrikDennis2025.Core.Plugins.Security
{
    /// <summary>
    /// Provides plugin verification functionality to ensure plugins are authentic and haven't been tampered with
    /// </summary>
    public class PluginVerifier
    {
        private readonly string _verificationEndpoint;
        private readonly string _trustedKeysPath;
        private readonly IPluginLogger _logger;
        private Dictionary<string, PluginSignatureInfo> _trustedPlugins;
        private bool _offlineMode;

        /// <summary>
        /// Indicates the verification level for the application
        /// </summary>
        public VerificationLevel VerificationLevel { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="verificationEndpoint">URL for online verification</param>
        /// <param name="trustedKeysPath">Path to local trusted keys file</param>
        /// <param name="verificationLevel">Level of verification to enforce</param>
        /// <param name="logger">Logger instance</param>
        public PluginVerifier(
            string verificationEndpoint,
            string trustedKeysPath,
            VerificationLevel verificationLevel,
            IPluginLogger logger)
        {
            _verificationEndpoint = verificationEndpoint;
            _trustedKeysPath = trustedKeysPath;
            VerificationLevel = verificationLevel;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _trustedPlugins = new Dictionary<string, PluginSignatureInfo>();
            _offlineMode = false;

            // Load trusted plugins from local file
            LoadTrustedPlugins();
        }

        /// <summary>
        /// Verify a plugin file by checking its hash against trusted sources
        /// </summary>
        /// <param name="pluginFilePath">Path to the plugin file</param>
        /// <returns>Verification result</returns>
        public async Task<VerificationResult> VerifyPluginAsync(string pluginFilePath)
        {
            try
            {
                // Skip verification if not required
                if (VerificationLevel == VerificationLevel.None)
                {
                    return new VerificationResult 
                    { 
                        IsVerified = true, 
                        Status = VerificationStatus.NotRequired,
                        Message = "Verification not required based on application settings" 
                    };
                }

                if (!File.Exists(pluginFilePath))
                {
                    return new VerificationResult 
                    { 
                        IsVerified = false, 
                        Status = VerificationStatus.FileNotFound,
                        Message = "Plugin file not found" 
                    };
                }

                // Calculate file hash
                string pluginHash = CalculateFileHash(pluginFilePath);
                string pluginId = Path.GetFileNameWithoutExtension(pluginFilePath);

                // Step 1: Check local trusted plugins list
                bool isVerifiedLocally = CheckLocalVerification(pluginId, pluginHash);
                if (isVerifiedLocally)
                {
                    return new VerificationResult 
                    { 
                        IsVerified = true, 
                        Status = VerificationStatus.VerifiedLocally,
                        PluginId = pluginId,
                        Hash = pluginHash,
                        Message = "Plugin verified using local signature database" 
                    };
                }

                // Step 2: Check online verification if not in offline mode
                if (!_offlineMode)
                {
                    bool isVerifiedOnline = await CheckOnlineVerificationAsync(pluginId, pluginHash);
                    if (isVerifiedOnline)
                    {
                        // Update local database with this newly verified plugin
                        UpdateLocalTrustedPlugins(pluginId, pluginHash);

                        return new VerificationResult 
                        { 
                            IsVerified = true, 
                            Status = VerificationStatus.VerifiedOnline,
                            PluginId = pluginId,
                            Hash = pluginHash,
                            Message = "Plugin verified using online signature database" 
                        };
                    }
                }

                // Step 3: If we reached here and verification is required, return failed
                if (VerificationLevel == VerificationLevel.Required)
                {
                    return new VerificationResult 
                    { 
                        IsVerified = false, 
                        Status = VerificationStatus.VerificationFailed,
                        PluginId = pluginId,
                        Hash = pluginHash,
                        Message = "Plugin failed verification and verification is required" 
                    };
                }

                // Step 4: If verification is optional, ask user confirmation
                return new VerificationResult 
                { 
                    IsVerified = false, 
                    Status = VerificationStatus.RequiresUserConfirmation,
                    PluginId = pluginId,
                    Hash = pluginHash,
                    Message = "Plugin could not be verified. User confirmation required." 
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"Error verifying plugin: {ex.Message}", ex);
                
                if (VerificationLevel == VerificationLevel.Required)
                {
                    return new VerificationResult 
                    { 
                        IsVerified = false, 
                        Status = VerificationStatus.VerificationError,
                        Message = $"Error during verification: {ex.Message}" 
                    };
                }
                else
                {
                    return new VerificationResult 
                    { 
                        IsVerified = false, 
                        Status = VerificationStatus.RequiresUserConfirmation,
                        Message = $"Verification error: {ex.Message}. User confirmation required." 
                    };
                }
            }
        }

        /// <summary>
        /// Add a trusted plugin to the local database
        /// </summary>
        /// <param name="pluginId">Plugin ID</param>
        /// <param name="pluginHash">Plugin hash</param>
        /// <param name="publisherName">Publisher name</param>
        /// <param name="timestamp">Timestamp of the verification</param>
        public void AddTrustedPlugin(string pluginId, string pluginHash, string publisherName, DateTime timestamp)
        {
            _trustedPlugins[pluginId] = new PluginSignatureInfo
            {
                PluginId = pluginId,
                Hash = pluginHash,
                PublisherName = publisherName,
                VerificationTimestamp = timestamp
            };

            // Save to file
            SaveTrustedPlugins();
        }

        /// <summary>
        /// Manually verify and trust a plugin (for administrator use)
        /// </summary>
        /// <param name="pluginFilePath">Path to the plugin file</param>
        /// <param name="publisherName">Publisher name</param>
        /// <returns>True if successful</returns>
        public bool ManuallyTrustPlugin(string pluginFilePath, string publisherName)
        {
            try
            {
                if (!File.Exists(pluginFilePath))
                {
                    return false;
                }

                // Calculate file hash
                string pluginHash = CalculateFileHash(pluginFilePath);
                string pluginId = Path.GetFileNameWithoutExtension(pluginFilePath);

                // Add to trusted plugins
                AddTrustedPlugin(pluginId, pluginHash, publisherName, DateTime.UtcNow);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error manually trusting plugin: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Set offline mode (no online verification)
        /// </summary>
        public void SetOfflineMode(bool enabled)
        {
            _offlineMode = enabled;
        }

        /// <summary>
        /// Recalculate and verify all plugins in the plugins directory
        /// </summary>
        /// <param name="pluginsDirectory">Path to plugins directory</param>
        /// <returns>Dictionary of plugin paths and their verification results</returns>
        public async Task<Dictionary<string, VerificationResult>> VerifyAllPluginsAsync(string pluginsDirectory)
        {
            var results = new Dictionary<string, VerificationResult>();

            if (!Directory.Exists(pluginsDirectory))
            {
                return results;
            }

            var pluginFiles = Directory.GetFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories);
            foreach (var pluginFile in pluginFiles)
            {
                var result = await VerifyPluginAsync(pluginFile);
                results[pluginFile] = result;
            }

            return results;
        }

        #region Private Methods

        /// <summary>
        /// Calculate SHA-256 hash for a file
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>Hex-encoded hash</returns>
        private string CalculateFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hashBytes = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Check if a plugin is verified in the local database
        /// </summary>
        /// <param name="pluginId">Plugin ID</param>
        /// <param name="pluginHash">Plugin hash</param>
        /// <returns>True if verified</returns>
        private bool CheckLocalVerification(string pluginId, string pluginHash)
        {
            if (_trustedPlugins.TryGetValue(pluginId, out var info))
            {
                return string.Equals(info.Hash, pluginHash, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        /// <summary>
        /// Check if a plugin is verified with the online service
        /// </summary>
        /// <param name="pluginId">Plugin ID</param>
        /// <param name="pluginHash">Plugin hash</param>
        /// <returns>True if verified</returns>
        private async Task<bool> CheckOnlineVerificationAsync(string pluginId, string pluginHash)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(
                        $"{_verificationEndpoint}?pluginId={Uri.EscapeDataString(pluginId)}&hash={Uri.EscapeDataString(pluginHash)}");

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var result = JsonSerializer.Deserialize<OnlineVerificationResponse>(content);
                        return result?.IsVerified ?? false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking online verification: {ex.Message}", ex);
                _offlineMode = true; // Switch to offline mode after failure
            }

            return false;
        }

        /// <summary>
        /// Load trusted plugins from local file
        /// </summary>
        private void LoadTrustedPlugins()
        {
            try
            {
                if (File.Exists(_trustedKeysPath))
                {
                    var json = File.ReadAllText(_trustedKeysPath);
                    var plugins = JsonSerializer.Deserialize<List<PluginSignatureInfo>>(json);
                    _trustedPlugins = plugins.ToDictionary(p => p.PluginId, p => p);
                    _logger.Info($"Loaded {_trustedPlugins.Count} trusted plugins from local database");
                }
                else
                {
                    _trustedPlugins = new Dictionary<string, PluginSignatureInfo>();
                    _logger.Info("No local trusted plugins database found. Creating new database.");
                    SaveTrustedPlugins(); // Create the file
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading trusted plugins: {ex.Message}", ex);
                _trustedPlugins = new Dictionary<string, PluginSignatureInfo>();
            }
        }

        /// <summary>
        /// Save trusted plugins to local file
        /// </summary>
        private void SaveTrustedPlugins()
        {
            try
            {
                var directory = Path.GetDirectoryName(_trustedKeysPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_trustedPlugins.Values.ToList());
                File.WriteAllText(_trustedKeysPath, json);
                _logger.Info($"Saved {_trustedPlugins.Count} trusted plugins to local database");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error saving trusted plugins: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update local trusted plugins with a new verification
        /// </summary>
        /// <param name="pluginId">Plugin ID</param>
        /// <param name="pluginHash">Plugin hash</param>
        private void UpdateLocalTrustedPlugins(string pluginId, string pluginHash)
        {
            _trustedPlugins[pluginId] = new PluginSignatureInfo
            {
                PluginId = pluginId,
                Hash = pluginHash,
                PublisherName = "Verified Online",
                VerificationTimestamp = DateTime.UtcNow
            };

            SaveTrustedPlugins();
        }

        #endregion
    }

    /// <summary>
    /// Information about a plugin signature
    /// </summary>
    public class PluginSignatureInfo
    {
        /// <summary>
        /// Plugin ID
        /// </summary>
        public string PluginId { get; set; }

        /// <summary>
        /// Plugin hash
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Publisher name
        /// </summary>
        public string PublisherName { get; set; }

        /// <summary>
        /// Timestamp of verification
        /// </summary>
        public DateTime VerificationTimestamp { get; set; }
    }

    /// <summary>
    /// Response from online verification
    /// </summary>
    public class OnlineVerificationResponse
    {
        /// <summary>
        /// Whether the plugin is verified
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Publisher information
        /// </summary>
        public string Publisher { get; set; }

        /// <summary>
        /// Signature timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Result of plugin verification
    /// </summary>
    public class VerificationResult
    {
        /// <summary>
        /// Whether the plugin is verified
        /// </summary>
        public bool IsVerified { get; set; }

        /// <summary>
        /// Verification status
        /// </summary>
        public VerificationStatus Status { get; set; }

        /// <summary>
        /// Plugin ID
        /// </summary>
        public string PluginId { get; set; }

        /// <summary>
        /// Plugin hash
        /// </summary>
        public string Hash { get; set; }

        /// <summary>
        /// Message describing the verification result
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Publisher information (if available)
        /// </summary>
        public string Publisher { get; set; }
    }

    /// <summary>
    /// Status of plugin verification
    /// </summary>
    public enum VerificationStatus
    {
        /// <summary>
        /// Plugin not verified
        /// </summary>
        NotVerified,

        /// <summary>
        /// Plugin verified using local database
        /// </summary>
        VerifiedLocally,

        /// <summary>
        /// Plugin verified using online service
        /// </summary>
        VerifiedOnline,

        /// <summary>
        /// Verification failed
        /// </summary>
        VerificationFailed,

        /// <summary>
        /// Error during verification
        /// </summary>
        VerificationError,

        /// <summary>
        /// Verification not required
        /// </summary>
        NotRequired,

        /// <summary>
        /// User confirmation required to proceed
        /// </summary>
        RequiresUserConfirmation,

        /// <summary>
        /// Plugin file not found
        /// </summary>
        FileNotFound
    }

    /// <summary>
    /// Level of verification required
    /// </summary>
    public enum VerificationLevel
    {
        /// <summary>
        /// No verification required
        /// </summary>
        None,

        /// <summary>
        /// Verification optional, prompt user for unverified plugins
        /// </summary>
        Optional,

        /// <summary>
        /// Verification required, reject unverified plugins
        /// </summary>
        Required
    }
}