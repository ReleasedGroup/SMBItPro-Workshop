# Helpdesk-Light KPI Definitions

This document defines the dashboard metrics exposed by `GET /api/v1/analytics/dashboard`.

## Filters and Scope

- `customerId`: optional for MSP admin; ignored for non-admin users (tenant scope is forced to caller's customer).
- `fromUtc` / `toUtc`: optional UTC range. Default is last 30 days when omitted.

## Operational KPIs

- **Total Ticket Volume**
  - Count of tickets where `Ticket.CreatedUtc` is inside the selected time range.

- **Open Ticket Count**
  - Count of ranged tickets where `Ticket.Status` is not `Resolved` and not `Closed`.

- **Open Tickets By Priority**
  - Group of open ranged tickets by `Ticket.Priority`.

- **Channel Split**
  - Group of ranged tickets by `Ticket.Channel` (`Web`, `Email`).

- **Average First Response Minutes**
  - For each ranged ticket, measure minutes between `Ticket.CreatedUtc` and first message by `Technician` or `Agent`.
  - Report average across tickets that have at least one technician/agent response.

- **Average Resolution Minutes**
  - For tickets with `Ticket.ResolvedUtc` inside range, measure minutes between `Ticket.CreatedUtc` and `Ticket.ResolvedUtc`.
  - Report average across resolved tickets in range.

## AI KPIs

- **Total AI Suggestions**
  - Count of `TicketAiSuggestion` rows where `CreatedUtc` is inside range and ticket is in scope.

- **Accepted AI Suggestions**
  - Count of suggestions where status is `Approved` or `AutoSent`.

- **Suggestion Acceptance Rate**
  - `AcceptedAiSuggestions / TotalAiSuggestions`.

- **Auto Response Count**
  - Count of suggestions where status is `AutoSent`.

- **Auto Response Rate**
  - `AutoResponseCount / TotalAiSuggestions`.

- **AI Generated Article Count**
  - Count of `KnowledgeArticle` where `AiGenerated = true` and `CreatedUtc` is in range and tenant scope.

- **Published AI Generated Article Count**
  - Count of scoped AI-generated articles where `PublishedUtc` is not null.

- **Article Draft Acceptance Rate**
  - `PublishedAiGeneratedArticleCount / AiGeneratedArticleCount`.
