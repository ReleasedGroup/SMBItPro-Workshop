namespace Helpdesk.Light.Domain.Email;

public enum OutboundEmailStatus
{
    Pending,
    Sent,
    Failed,
    DeadLetter
}
