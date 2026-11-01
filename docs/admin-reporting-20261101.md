# Admin registrations and Excel reports

Admin reporting lists registered authentication accounts, including those without a completed profile, and separately lists each Premium registration/renewal. Deleted accounts are excluded from the account directory; historical subscriptions remain distinguishable. Package state is evaluated separately from payment state and payment-less grants. Search and pagination execute in PostgreSQL. No N+1 profile or payment reads are introduced.

All routes under `/api/admin/reporting` require authentication and a current admin profile. Accounts, member lists, export history and file download authorize through `UserContext.EnsureAdmin`. The stored report table has RLS enabled and browser-role access revoked. Responses disable caching. Reports contain personal account data and are available only through authenticated admin downloads.

- `GET accounts?keyword=&page=1&pageSize=25`
- `GET members?keyword=&state=all&page=1&pageSize=25`
- `POST exports` with `{ requestId, days }` (7–90 days)
- `GET exports` returns metadata for the latest 30 exports, excluding file bytes.
- `GET exports/{id}/download` downloads the stored XLSX without sending another email.

Each explicitly created report stores immutable XLSX bytes in PostgreSQL and queues an attachment to `AdminReports:Recipient`, defaulting to the owner's requested `duongduy12314@gmail.com`. Opening the report section never queues mail. A unique `(RequestedById, RequestId)` index makes uncertain retries and concurrent requests reuse one report. Reusing a request ID with different days is rejected. Exports exceeding 20,000 rows per directory or 5 MiB fail explicitly instead of silently truncating data.

`AdminReportEmailWorker` uses the existing transactional email transport and runs every 15 seconds when background jobs are enabled. Claims use atomic `FOR UPDATE SKIP LOCKED` leases. Known failures retry with backoff up to eight attempts; ambiguous outcomes and expired sending leases become `uncertain`, requiring review rather than risking duplicate mail. `accepted` means the sending service accepted the attachment; it does not prove delivery into the Gmail inbox. Downloads remain available during email errors.

The workbook contains five sheets: Tổng quan, Tài khoản, Gói thành viên, Hoạt động, Khu vực. Account and subscription directories contain the current full retained history; activity metrics use the chosen day window. Dates use UTC+7 and numeric amounts use a VND display format. Snapshot facts and source meanings remain separate from revenue claims. No passwords, tokens, private conversations or bank account numbers are included.

The renderer follows [Microsoft SpreadsheetML packaging](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/structure-of-a-spreadsheetml-document) and writes user-controlled values as inline text. It never creates user-controlled formulas, hyperlinks or external workbook references.

Validation:

- Release backend build: zero warnings/errors.
- Disposable PostgreSQL migration + reporting suite: five passing tests, covering admin-only access, unfinished accounts, status filters, paging, durable/idempotent export, uncertain email outcomes and workbook/query validity.
- Independent XLSX reader verified all five sheets, numeric VND cells, UTC+7 dates and zero formula cells.
- Frontend: 455 tests, build and changed-file ESLint passed. Browser fixtures covered the new account/member/export flows, retry deduplication, double-click protection, downloads without resending email and 320/390px layouts; existing admin moderation/withdrawal/verification flows passed in desktop/mobile and light/dark themes.
- One clearly labeled empty sample workbook was sent to the requested recipient using the actual configured Brevo transport. The provider accepted the message and its XLSX attachment. This was a transport test, not an export of live customer data or proof of Gmail inbox placement.

Run the isolated database suite with:

```powershell
./scripts/quality/Test-LocalBackend.ps1 -SkipLoadTest -TestFilter 'FullyQualifiedName~AdminReportingDatabaseTests|FullyQualifiedName~AdminReportWorkbookTests'
```
