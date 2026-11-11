# Imported publisher accounts

Run from the backend root:

```powershell
dotnet run --project scripts/data/SourcePublisherAccounts
dotnet run --project scripts/data/SourcePublisherAccounts -- --apply
dotnet run --project scripts/data/SourcePublisherAccounts -- --apply --resolutions output/source-publisher-accounts/approved-names.json
```

The first command previews eligible contacts and audits existing imported accounts. The second creates accounts via the Supabase Admin API and creates corresponding landlord profiles. It does not send messages or invent email addresses. Keys stay in the existing local configuration; the database and Auth project must match.

One normalized Vietnamese mobile number produces one account. Different names for the same number are left for manual review. Existing ordinary accounts are never changed or assigned imported listings. Imported account metadata records the source listing IDs for review; this does not convert external advertisements into native posts or grant ownership.

For user-approved conflicts, `--resolutions` accepts a JSON object mapping full local phone numbers to exact names already present in that phone group. It also aligns those listings' contact names with the approved name. Keep this contact data under the ignored output directory. Preview before applying.

Accounts initially have an undisclosed random password, no email, unconfirmed phone/email, and a login ban. The user's later instruction authorizes unique name-based placeholder emails through `CompleteSourcePublisherAccounts`; this tool accepts those reserved-domain aliases when checking existing imports. Every created or resumed imported account is checked in Auth before a profile is inserted. Repeating the command repairs missing profiles without creating duplicates. If a provider response times out, rerun to recover by phone; accounts and profile writes span two services and cannot be one transaction.

`output/source-publisher-accounts/latest-review.json` contains the summary and conflicting contacts. It contains personal contact data and must stay out of version control.

Activation requires a separate verified-phone ownership claim flow. Do not remove a ban or mark contact information verified based on imported data. This tool does not implement SMS delivery or activation.
