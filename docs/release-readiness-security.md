# Release Readiness and Security Hardening

## Objective

This document tracks MVP hardening status for security, privacy, and release readiness.

## Authorization and Tenant Isolation Coverage

Coverage is enforced in service/controller authorization checks and validated by integration tests, including:

- `AuthAndTenancyIntegrationTests.Technician_CannotAccessAnotherTenantCustomerData`
- `AuthAndTenancyIntegrationTests.Technician_CannotCallAdminCustomersEndpoint`
- `TicketLifecycleIntegrationTests.EndUser_CannotReadOtherTenantTicket`
- `KnowledgeBaseIntegrationTests.Technician_CannotReadKnowledgeArticleOutsideTenant`
- `ResolverGroupIntegrationTests.Assignment_ToOtherCustomerResolverGroup_IsRejected`
- `TicketCategoryIntegrationTests.CategoryMapping_ToDifferentCustomerResolverGroup_IsRejected`

## Input Sanitization and Attachment Safeguards

Current safeguards include:

- Attachment uploads enforce max-size and content-type allowlists (`LocalAttachmentStorage`).
- Attachment file names are normalized with invalid characters replaced (`LocalAttachmentStorage`).
- Knowledge article search now escapes SQL LIKE wildcard characters before query execution (`KnowledgeBaseService`).

## AI Guardrails and Restricted Categories

Current safeguards include:

- Customer-level AI policy modes with confidence threshold checks (`AiTicketAgentService`).
- Restricted categories are excluded from automatic AI replies (`AiTicketAgentService`).
- Fallback generation paths are used when AI output is unavailable/invalid.

## Release Checklist

- [x] Backup script uses SQLite snapshot backup semantics (`scripts/backup-helpdesk.sh`).
- [x] Outbound email retry behavior performs one attempt per dispatch cycle and dead-letters once on terminal failure.
- [x] Analytics dashboard queries avoid redundant ranged/open ticket materialization and per-ticket first-response subqueries.
- [x] Unit and integration test suites pass.
- [x] Build passes with warnings treated as errors.

## Known Limitations and Mitigations

- Limitation: SQLite remains a single-file datastore and may become a throughput bottleneck at higher sustained concurrency.
  Mitigation: Keep frequent verified backups and monitor queue depth/latency; migrate to a server-grade database for scale-out.

- Limitation: AI suggestions rely on model output quality and can degrade with poor prompt/data quality.
  Mitigation: Continue technician approval workflows for non-low-risk responses and monitor AI audit events.

- Limitation: Attachment malware scanning is not included in MVP.
  Mitigation: Restrict content types and file sizes, and run upstream gateway scanning in production environments.
