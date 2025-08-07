using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using LoadTestingSytem.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;
using Microsoft.Identity.Client.TestOnlySilentCBA;
using Microsoft.IdentityModel.Abstractions;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace PowerBITokenGenerator
{
    class ConsoleLogger : IIdentityLogger
    {
        public bool IsEnabled(EventLogLevel eventLogLevel)
        {
            if (eventLogLevel <= EventLogLevel.Warning)
            {
                return true;
            }
            return false;
        }

        public void Log(LogEntry entry)
        {
            if (entry.EventLogLevel <= EventLogLevel.Warning && entry.EventLogLevel != EventLogLevel.LogAlways)
            {
                if (!entry.Message.Contains("ErrorCode: user_null"))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(entry.Message);
                    Console.ResetColor();
                }
            }
            else
            {
                Console.WriteLine(entry.Message);
            }
        }
    }



    public class PowerBiCbaTokenProvider
    {
        private static readonly string commonKvUrl = $"https://ppe-ephemeral-common-kv.vault.azure.net/";
        private static readonly string adminKvUrl = $"https://ppe-ephemeral-admin-kv.vault.azure.net/";

        public static async Task<List<UserCertWorkspaceToken>> RunAsync(List<UserCertWorkspace> userCertWorkspaceList)
        {
            var output = new List<UserCertWorkspaceToken>();

            foreach (var ucw in userCertWorkspaceList)
            {
                var accessToken = await GenerateAccessTokenForUser(ucw, commonKvUrl);
                output.Add(new UserCertWorkspaceToken { UserName = ucw.UserName, CertificateName = ucw.CertificateName, WorkspaceId = ucw.WorkspaceId, AccessToken = accessToken });
            }

            return output;
        }

        public static async Task<string> GetTenantAdmin()
        {
            string mappingFile = "Creation/UserCerts.json";
            var mapJson = await File.ReadAllTextAsync(mappingFile);
            var userCertWorkspaceList = JsonSerializer.Deserialize<List<UserCertWorkspace>>(mapJson)!;
            var output = new List<UserCertWorkspaceToken>();

            var uc = userCertWorkspaceList[0];  //admin is the first one
            return await GenerateAccessTokenForUser(uc, adminKvUrl);
        }

        public static async Task<string> GenerateAccessTokenForUser(UserCertWorkspace ucw, string keyVaultUrl, int maxRetries = 3, int delayMs = 5000)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var keyVaultUri = new Uri(keyVaultUrl);
                    var client = new SecretClient(vaultUri: keyVaultUri, credential: new DefaultAzureCredential());
                    KeyVaultSecret secret = client.GetSecret(ucw.CertificateName);
                    string base64EncodedCertificate = secret.Value;
                    byte[] certBytes = Convert.FromBase64String(base64EncodedCertificate);
                    var certificate = new X509Certificate2(certBytes, string.Empty,
                        X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                    var msalLogger = new ConsoleLogger();
                    var pca = PublicClientApplicationBuilder
                        .Create("c0d2a505-13b8-4ae0-aa9e-cddd5eab0b12")
                        .WithAuthority("https://login.windows-ppe.net/organizations")
                        .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                        .WithLogging(msalLogger, enablePiiLogging: true)
                        .WithCacheOptions(CacheOptions.EnableSharedCacheOptions)
                        .Build();

                    var token = await pca.AcquireTokenInteractive(GetDefaultScopeFromResource("https://analysis.windows-int.net/powerbi/api"))
                                         .WithCustomWebUi(new SilentCbaWebUI(ucw.UserName, certificate, msalLogger))
                                         .WithCorrelationId(Guid.NewGuid())
                                         .ExecuteAsync();

                    Console.WriteLine($"Successfully got access token for {ucw.UserName} with {ucw.CertificateName} on attempt {attempt}");
                    return token.AccessToken;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Attempt {attempt} failed for {ucw.UserName} with {ucw.CertificateName}: {ex.Message}");

                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"Failed to get access token after {maxRetries} attempts.");
                        return null;
                    }

                    await Task.Delay(delayMs);
                }
            }

            return null; // Should never hit this line
        }

        private static string[] GetDefaultScopeFromResource(string resource)
        {
            if (string.IsNullOrEmpty(resource))
            {
                return [string.Empty];
            }
            else
            {
                return [resource + "/.default"];
            }
        }
    }
}