using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Cheat
{
    public static class Loader
    {
        private static string PathGame;

        [STAThread]
        public static async Task Main(string[] args)
        {
            ComWrappers.RegisterForMarshalling(WinFormsComInterop.WinFormsComWrappers.Instance);

            string? folder = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--gamepath=", StringComparison.OrdinalIgnoreCase))
                {
                    folder = args[i].Substring("--gamepath=".Length).Trim('"');
                    break;
                }
                else if (args[i].Equals("--gamepath", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    folder = args[++i];
                    break;
                }
            }

            if (string.IsNullOrEmpty(folder))
            {
                folder = await PickFolderAsync("Select the VRChat installation folder");
                if (string.IsNullOrEmpty(folder))
                {
                    return;
                }
            }

            PathGame = folder;

            try
            {
                await StartBypassAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Fehler: {ex.Message}");
            }
            finally
            {
                Exit("discord.gg/8MvQgfnfvJ");
            }
        }

        public static void Exit(string name)
        {
            try
            {
                Environment.FailFast(!string.IsNullOrEmpty(name) ? name : "discord.gg/8MvQgfnfvJ, Application terminated unexpectedly.");
            }
            catch { Environment.Exit(-1); }
        }

        public static Task<string?> PickFolderAsync(string title = "Select a folder")
        {
            return Task.Run(() =>
            {
                string? result = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        using var dialog = new FolderBrowserDialog
                        {
                            Description = title,
                            ShowNewFolderButton = false,
                        };

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            result = dialog.SelectedPath;
                        }
                    }
                    catch { }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
                return result;
            });
        }

        public static async Task StartBypassAsync()
        {
            if (string.IsNullOrWhiteSpace(PathGame))
                Exit("Game path is empty. Please set a valid folder.");

            await Task.Delay(100);

            var exePath = Path.Combine(PathGame, "VRChat.exe");
            if (!File.Exists(exePath))
                Exit($"VRChat.exe not found in '{PathGame}'.");

            await Task.Delay(100);

            string destination = Path.Combine(PathGame, "VRChat_Data", "Plugins", "x86_64", "EOSSDK-Win64-Shipping.dll");
            string downloadUrl = "https://github.com/HallofFameClient/BepInResource/raw/refs/heads/main/EOSSDK-Win64-Shipping.dll";

            using var httpClient = new HttpClient();
            try
            {
                byte[] buffer = await httpClient.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(destination, buffer);
            }
            catch (Exception ex)
            {
                Exit($"Download fehlgeschlagen: {ex.Message}");
            }

            await Task.Delay(3500);

            await StartVRChatProcessAsync(exePath);
        }

        private static async Task StartVRChatProcessAsync(string exePath)
        {
            var result = await Custom.GetPort();
            Debug.WriteLine(result);
            if (!result.Success)
            {
                MessageBox.Show($"Server Error: {result.PortOrError}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string port = result.PortOrError;
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            bool hasVrMonitor = Process.GetProcessesByName("vrmonitor").Any();
            string vrFlag = hasVrMonitor ? "" : "--no-vr";

            string arguments = string.Format(
                "{0} -eac_port={1}",
                vrFlag, port
            );

            var startInfo = new ProcessStartInfo(exePath, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exePath)
            };

            Process? vrchatProcess = null;
            try
            {
                vrchatProcess = Process.Start(startInfo);
                MessageBox.Show("Game Starting... The Loader will stay in the background to keep your connection alive.", "Starting", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format(
                    "Failed to start VRChat:",
                    ex.Message
                ), ex);
            }

            if (vrchatProcess != null)
            {
                await KeepConnectionAlive(vrchatProcess);
            }
        }

        private static bool IsProcessStillRunning(Process process)
        {
            if (process == null) return false;
            try
            {
                Process.GetProcessById(process.Id);
                return !process.HasExited;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return false;
            }
        }

        private static async Task KeepConnectionAlive(Process vrchatProcess)
        {
            while (IsProcessStillRunning(vrchatProcess))
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
                if (!await Custom.SendKeepAlive())
                    break;
            }
        }

        public class Custom
        {
            private static readonly HttpClient _Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            private static readonly string _ClientVersion = "1.0.7";
            private static readonly string ServerUrl = "http://45.11.228.204:8080";

            public static async Task<(bool Success, string PortOrError)> GetPort()
            {
                string combined = "vrchat" + "|" + _ClientVersion;
                string encryptedAuth = Uri.EscapeDataString(CryptoHelper.EncryptAndSign(combined));

                string apiUrl = $"{ServerUrl}/?auth={encryptedAuth}";

                try
                {
                    string response = await _Http.GetStringAsync(apiUrl);
                    if (response.StartsWith("error|"))
                        return (false, response.Substring(6));

                    return (true, response);
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            }

            public static async Task<bool> SendKeepAlive()
            {
                string combined = "vrchat" + "|" + _ClientVersion;
                string encryptedAuth = Uri.EscapeDataString(CryptoHelper.EncryptAndSign(combined));

                string apiUrl = $"{ServerUrl}/keepalive?auth={encryptedAuth}";

                try
                {
                    string response = await _Http.GetStringAsync(apiUrl);
                    return response == "ok";
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    public static class CryptoHelper
    {
        private static readonly byte[] _key = Convert.FromBase64String("mJlvsRUAUQAQb4/JmyNY9PWs8lphaJQoLJWCHN50Jbs=");

        public static string EncryptAndSign(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            byte[] iv = aes.IV;
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            using var encryptor = aes.CreateEncryptor();
            byte[] cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            using var hmac = new HMACSHA256(_key);
            byte[] combinedForHmac = new byte[iv.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, combinedForHmac, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, combinedForHmac, iv.Length, cipher.Length);
            byte[] hmacBytes = hmac.ComputeHash(combinedForHmac);

            byte[] result = new byte[iv.Length + cipher.Length + hmacBytes.Length];
            Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, result, iv.Length, cipher.Length);
            Buffer.BlockCopy(hmacBytes, 0, result, iv.Length + cipher.Length, hmacBytes.Length);

            return Convert.ToBase64String(result);
        }
    }
}