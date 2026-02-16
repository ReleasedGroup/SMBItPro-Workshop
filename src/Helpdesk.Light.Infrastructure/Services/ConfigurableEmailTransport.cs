using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using Helpdesk.Light.Application.Abstractions;
using Helpdesk.Light.Application.Abstractions.Email;
using Helpdesk.Light.Application.Contracts;
using Microsoft.Extensions.Logging;

namespace Helpdesk.Light.Infrastructure.Services;

public sealed class ConfigurableEmailTransport(
    IPlatformSettingsService platformSettingsService,
    IHttpClientFactory httpClientFactory,
    ILogger<ConfigurableEmailTransport> logger) : IEmailTransport
{
    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        RuntimePlatformSettings settings = await platformSettingsService.GetRuntimeSettingsAsync(cancellationToken);

        if (settings.EmailTransportMode == EmailTransportModes.Smtp)
        {
            await SendWithSmtpAsync(settings.Smtp, toAddress, subject, body, cancellationToken);
            return;
        }

        if (settings.EmailTransportMode == EmailTransportModes.Graph)
        {
            await SendWithGraphAsync(settings.Graph, toAddress, subject, body, cancellationToken);
            return;
        }

        logger.LogInformation(
            "Console email transport sent to {ToAddress} with subject {Subject}. Body length: {Length}",
            toAddress,
            subject,
            body.Length);
    }

    private static async Task SendWithSmtpAsync(
        RuntimeSmtpSettings smtp,
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            throw new InvalidOperationException("SMTP host is required.");
        }

        if (string.IsNullOrWhiteSpace(smtp.FromAddress))
        {
            throw new InvalidOperationException("SMTP from address is required.");
        }

        MailAddress fromAddress = string.IsNullOrWhiteSpace(smtp.FromDisplayName)
            ? new MailAddress(smtp.FromAddress)
            : new MailAddress(smtp.FromAddress, smtp.FromDisplayName);

        using MailMessage message = new(fromAddress, new MailAddress(toAddress))
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        using SmtpClient client = new(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(smtp.Username))
        {
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
    }

    private async Task SendWithGraphAsync(
        RuntimeGraphEmailSettings graph,
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(graph.TenantId) ||
            string.IsNullOrWhiteSpace(graph.ClientId) ||
            string.IsNullOrWhiteSpace(graph.ClientSecret) ||
            string.IsNullOrWhiteSpace(graph.SenderUserId))
        {
            throw new InvalidOperationException("Graph email settings are incomplete.");
        }

        HttpClient client = httpClientFactory.CreateClient(nameof(ConfigurableEmailTransport));
        string token = await AcquireGraphTokenAsync(client, graph, cancellationToken);

        using HttpRequestMessage sendMailRequest = new(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(graph.SenderUserId)}/sendMail");
        sendMailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            message = new
            {
                subject,
                body = new
                {
                    contentType = "Text",
                    content = body
                },
                toRecipients = new[]
                {
                    new
                    {
                        emailAddress = new
                        {
                            address = toAddress
                        }
                    }
                }
            },
            saveToSentItems = true
        };

        sendMailRequest.Content = JsonContent.Create(payload);
        using HttpResponseMessage sendMailResponse = await client.SendAsync(sendMailRequest, cancellationToken);

        if (!sendMailResponse.IsSuccessStatusCode)
        {
            string response = await sendMailResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Graph sendMail failed ({(int)sendMailResponse.StatusCode}): {response}");
        }
    }

    private static async Task<string> AcquireGraphTokenAsync(
        HttpClient client,
        RuntimeGraphEmailSettings graph,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage tokenRequest = new(
            HttpMethod.Post,
            $"https://login.microsoftonline.com/{graph.TenantId}/oauth2/v2.0/token");

        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = graph.ClientId,
            ["client_secret"] = graph.ClientSecret,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials"
        });

        using HttpResponseMessage tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
        string raw = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Graph token request failed ({(int)tokenResponse.StatusCode}): {raw}");
        }

        using JsonDocument document = JsonDocument.Parse(raw);
        if (!document.RootElement.TryGetProperty("access_token", out JsonElement tokenElement))
        {
            throw new InvalidOperationException("Graph token response did not contain access_token.");
        }

        string? token = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Graph access token is empty.");
        }

        return token;
    }
}
