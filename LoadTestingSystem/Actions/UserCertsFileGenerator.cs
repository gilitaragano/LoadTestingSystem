using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;
using Microsoft.Identity.Client.TestOnlySilentCBA;
using Microsoft.IdentityModel.Abstractions;

namespace Actions
{
    internal class UserCertsFileGenerator
    {
        private class ConsoleLogger : IIdentityLogger
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
        record UserCert(string user, string certificate);
        record UserCertToken(string user, string certificate, string accessToken);
        record UserNameCertNameUserId(string userName, string certificateName, string userId);
        public static async Task RunAsync()
        {
            string userCertsBaseInputFile = "Creation\\UserCertsBase.json";
            string userCertsOutputFile = "..\\..\\..\\Creation\\UserCerts.json";
            var mapJson = await File.ReadAllTextAsync(userCertsBaseInputFile);
            var pairs = JsonSerializer.Deserialize<List<UserCert>>(mapJson)!;
            var output = new List<UserNameCertNameUserId>();

            foreach (var p in pairs)
            {
                try
                {
                    var keyVaultUri = p.user.Contains("Admin") ?
                        new Uri($"https://ppe-ephemeral-admin-kv.vault.azure.net/") :
                        new Uri($"https://ppe-ephemeral-common-kv.vault.azure.net/");
                    var client = new SecretClient(vaultUri: keyVaultUri, credential: new DefaultAzureCredential());
                    KeyVaultSecret secret = client.GetSecret(p.certificate);
                    string base64EncodedCertificate = secret.Value;
                    byte[] certBytes = Convert.FromBase64String(base64EncodedCertificate);
                    var certificate = new X509Certificate2(certBytes, string.Empty, X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);


                    var msalLogger = new ConsoleLogger();
                    var pca = PublicClientApplicationBuilder
                        .Create("c0d2a505-13b8-4ae0-aa9e-cddd5eab0b12")
                        .WithAuthority("https://login.windows-ppe.net/organizations")
                        .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                        .WithLogging(msalLogger, enablePiiLogging: true)
                        .WithCacheOptions(CacheOptions.EnableSharedCacheOptions)
                        .Build();

                    var token = await pca.AcquireTokenInteractive(GetDefaultScopeFromResource("https://analysis.windows-int.net/powerbi/api"))
                                       .WithCustomWebUi(new SilentCbaWebUI(p.user, certificate, msalLogger))
                                       .WithCorrelationId(Guid.NewGuid())
                                       .ExecuteAsync();
                    output.Add(new UserNameCertNameUserId(p.user, p.certificate, token.UniqueId));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to get a token for {p.user} with {p.certificate}");
                }

            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(output, options);
            await File.WriteAllTextAsync(userCertsOutputFile, json);
        }
    }
}