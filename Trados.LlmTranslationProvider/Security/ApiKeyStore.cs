using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Trados.LlmTranslationProvider.Security
{
    /// <summary>
    /// Persists the OpenAI API key encrypted with Windows DPAPI (CurrentUser scope), so it never
    /// appears in plain text on disk and never travels inside a .sdlproj/.sdltpl's serialized
    /// provider state or URI (see LlmTranslationOptions remarks). Keys are stored one file per
    /// provider instance, named after a hash of the provider's non-secret settings, under:
    ///   %AppData%\Trados\LlmTranslationProvider\keys\
    /// </summary>
    public static class ApiKeyStore
    {
        /// <summary>
        /// Key identifier used for the single OpenAI API key shared by all instances of this
        /// provider. Kept as one shared key (rather than one per provider URI) since in practice
        /// a single OpenAI account/key is used across all configured models and clients.
        /// </summary>
        public const string OpenAiKeyId = "openai";

        private static string KeysFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Trados", "LlmTranslationProvider", "keys");

        public static void Save(string keyId, string apiKey)
        {
            Directory.CreateDirectory(KeysFolder);

            var plainBytes = Encoding.UTF8.GetBytes(apiKey ?? string.Empty);
            var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

            File.WriteAllBytes(GetPath(keyId), protectedBytes);
        }

        public static string Load(string keyId)
        {
            var path = GetPath(keyId);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                var protectedBytes = File.ReadAllBytes(path);
                var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (CryptographicException)
            {
                // Most likely: the stored value was encrypted under a different Windows user
                // account. Treat as "no key configured" rather than crashing Trados Studio.
                return null;
            }
        }

        public static void Delete(string keyId)
        {
            var path = GetPath(keyId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string GetPath(string keyId)
        {
            var safeName = MakeSafeFileName(keyId) + ".key";
            return Path.Combine(KeysFolder, safeName);
        }

        private static string MakeSafeFileName(string value)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
