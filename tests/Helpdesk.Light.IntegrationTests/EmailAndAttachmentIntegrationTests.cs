using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Helpdesk.Light.Application.Contracts.Email;
using Helpdesk.Light.Application.Contracts.Tickets;
using Helpdesk.Light.Domain.Tickets;
using Helpdesk.Light.Infrastructure.Data;

namespace Helpdesk.Light.IntegrationTests;

public sealed class EmailAndAttachmentIntegrationTests(HelpdeskApiFactory factory) : IClassFixture<HelpdeskApiFactory>
{
    [Fact]
    public async Task InboundEmail_NewThreadReplyAndDeduplication_WorkAsExpected()
    {
        using HttpClient adminClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(adminClient, SeedDataConstants.AdminEmail);

        InboundEmailRequest newTicket = new(
            "msg-001",
            SeedDataConstants.ContosoEndUserEmail,
            "Cannot access shared mailbox",
            "Mailbox access denied after password reset.",
            null,
            DateTime.UtcNow,
            null);

        HttpResponseMessage newTicketResponse = await adminClient.PostAsJsonAsync("/api/v1/email/inbound/dev", newTicket);
        newTicketResponse.EnsureSuccessStatusCode();

        InboundEmailProcessResult createdResult = (await newTicketResponse.Content.ReadFromJsonAsync<InboundEmailProcessResult>(TestAuth.JsonOptions))!;
        Assert.True(createdResult.IsMapped);
        Assert.NotNull(createdResult.TicketId);

        using HttpClient techClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(techClient, SeedDataConstants.ContosoTechEmail);

        TicketDetailDto createdDetail = (await (await techClient.GetAsync($"/api/v1/tickets/{createdResult.TicketId}")).Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        string reference = createdDetail.Ticket.ReferenceCode;

        InboundEmailRequest threadedReply = new(
            "msg-002",
            SeedDataConstants.ContosoEndUserEmail,
            $"Re: [{reference}] Cannot access shared mailbox",
            "Issue still persists after reboot.",
            null,
            DateTime.UtcNow,
            null);

        HttpResponseMessage replyResponse = await adminClient.PostAsJsonAsync("/api/v1/email/inbound/dev", threadedReply);
        replyResponse.EnsureSuccessStatusCode();

        InboundEmailProcessResult replyResult = (await replyResponse.Content.ReadFromJsonAsync<InboundEmailProcessResult>(TestAuth.JsonOptions))!;
        Assert.Equal(createdResult.TicketId, replyResult.TicketId);

        HttpResponseMessage duplicateResponse = await adminClient.PostAsJsonAsync("/api/v1/email/inbound/dev", threadedReply);
        duplicateResponse.EnsureSuccessStatusCode();

        InboundEmailProcessResult duplicateResult = (await duplicateResponse.Content.ReadFromJsonAsync<InboundEmailProcessResult>(TestAuth.JsonOptions))!;
        Assert.True(duplicateResult.IsDuplicate);

        TicketDetailDto detail = (await (await techClient.GetAsync($"/api/v1/tickets/{createdResult.TicketId}")).Content.ReadFromJsonAsync<TicketDetailDto>(TestAuth.JsonOptions))!;
        Assert.Equal(2, detail.Messages.Count(item => item.Source == TicketMessageSource.Email));
    }

    [Fact]
    public async Task AttachmentUploadAndDownload_RespectTenantBoundaries()
    {
        using HttpClient userClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(userClient, SeedDataConstants.ContosoEndUserEmail);

        TicketSummaryDto created = (await (await userClient.PostAsJsonAsync("/api/v1/tickets", new CreateTicketRequest(
            null,
            "Need software install",
            "Please install accounting software.",
            TicketPriority.Low))).Content.ReadFromJsonAsync<TicketSummaryDto>(TestAuth.JsonOptions))!;

        byte[] fileBytes = Encoding.UTF8.GetBytes("hello-attachment");
        using MultipartFormDataContent form = new();
        using ByteArrayContent content = new(fileBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(content, "file", "note.txt");

        HttpResponseMessage uploadResponse = await userClient.PostAsync($"/api/v1/tickets/{created.Id}/attachments", form);
        uploadResponse.EnsureSuccessStatusCode();

        TicketAttachmentDto attachment = (await uploadResponse.Content.ReadFromJsonAsync<TicketAttachmentDto>(TestAuth.JsonOptions))!;

        HttpResponseMessage downloadResponse = await userClient.GetAsync($"/api/v1/tickets/{created.Id}/attachments/{attachment.Id}");
        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);

        using HttpClient otherTenantClient = factory.CreateClient();
        await TestAuth.LoginAndSetAuthHeaderAsync(otherTenantClient, SeedDataConstants.FabrikamTechEmail);

        HttpResponseMessage forbidden = await otherTenantClient.GetAsync($"/api/v1/tickets/{created.Id}/attachments/{attachment.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
