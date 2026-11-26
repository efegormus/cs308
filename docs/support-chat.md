# Support chat backend (SCRUM-91)

The backend uses ASP.NET Core SignalR at `/hubs/support` with the existing
cookie authentication and existing Conversations/ChatMessages schema. No new
migration, separate users table or public staff-role assignment is introduced.
The customer/agent frontend and attachment upload are separate work.

## Starting and reconnecting

Get `/api/auth/csrf`, then POST `/api/chat/conversations` with `X-CSRF-TOKEN`.
Customers use their signed-in identity; anonymous callers receive an HttpOnly,
SameSite=Strict `OnlineStore.Chat` cookie (secure over HTTPS). Only its SHA-256
hash is stored in the conversation. Keep that cookie for reconnecting, never
send a guest token in a URL or localStorage. Staff cannot create customer chats.
Start requests are limited to five per minute per source IP.

Connect the SignalR JSON client to `/hubs/support` on the same origin, with
cookies, then invoke:

| Method | Contract |
| --- | --- |
| `Join(id)` | Subscribe to a conversation owned by this customer/guest or assigned to this agent. |
| `WatchQueue()` | SupportAgent only; receive `QueueChanged`, then reload the queue. |
| `Queue(afterId=0)` | SupportAgent only, oldest waiting IDs first, at most 50 rows; use the last ID for subsequent pages. |
| `Claim(id)` | SupportAgent only, atomically claims a waiting conversation, subscribes the agent and returns its status. Exact owner retries succeed; another agent gets 409. |
| `Send(id,body)` | Owner or assigned agent only. Stores trimmed text of 1–2000 characters and returns a server-owned ID/timestamp/sender kind. |
| `History(id,afterId=0)` | Authorized participants only. Returns messages and nextAfterId, 50 per page; reconnect using the last persisted ID. |
| `CustomerContext(id)` | Assigned SupportAgent only: bounded cart items, recent order status/totals and wishlist; guests have no account context. |

Events are `Message(conversationId,message)`, `Claimed(conversation)` and
`QueueChanged()`. Start/claim changes notify watching agents. Clients must render
message text safely. Unrelated customers, guests and agents cannot join/read a
conversation or inspect its customer context. Queue responses exclude messages,
email, addresses, tax IDs and guest credentials. Cross-origin WebSocket attempts
are rejected. Hub errors expose safe status/message text; detailed errors are off.
Authentication expiry closes authenticated connections. Each operation checks the
current database role/active state, rather than trusting a caller-supplied role.

Messages are persistent before broadcast. SignalR transport reconnect does not
guarantee delivery: reload History after reconnect to recover missed events.
A manually retried Send creates another message; the API does not claim
exactly-once message delivery. History remains readable for closed conversations,
but sending or claiming a closed conversation is rejected.

## Verification

`SupportChatTests` covers guest isolation, queue ordering, claiming/retries,
invalid/oversized messages, closed chats and assigned-agent-only context.
`SupportChatMySqlTests` races two claims on the real database, verifies one
winner, and checks persisted guest/agent sender identities and history cursors.
`SupportChatHubMySqlTests` exercises actual SignalR WebSocket handshake, room
subscription, live broadcast, persistent messages, denied room access and CSRF
on conversation creation. Run with ONLINESTORE_TEST_MYSQL pointing to a guarded
local database ending in `_tests`; never use the demo database for tests.
