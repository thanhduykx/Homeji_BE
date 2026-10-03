# Backend uptime without GitHub billing

Homeji uses Supabase Cron in its existing database to request
`https://homeji-be.onrender.com/health/live` every five minutes. The job is named
`homeji-render-liveness`. No GitHub runner, Edge Function, new subscription,
payment method, or API secret is required. The former GitHub workflow has been
removed because its runner was blocked by an account billing issue.

`pg_cron` queues an asynchronous GET using `pg_net`, with a 90-second timeout to
allow Render to start a sleeping instance. The endpoint does not query business
tables. The scheduler itself uses a small amount of existing database compute
and storage: 288 requests/day, about 8,928 in a 31-day month. This is not zero
resource usage. Supabase maintenance or project suspension can interrupt it.

Run history for this job is retained for seven days; cleanup only touches this
job's records. HTTP responses use the extension's default six-hour retention.
Cron success means the HTTP request was queued; verify HTTP status and response
separately. Requests should return HTTP 200 and `Healthy`.

Setup: run `scripts/operations/configure-backend-uptime.sql` as `postgres` in the
Supabase SQL Editor. It is infrastructure configuration, independent of EF Core
migrations and the API deployment. Re-running updates the named job instead of
creating another. Keep `cron` and `net` outside the Supabase Data API's exposed
schemas. These extensions use provider-managed grants; the SQL setup does not
claim to revoke grants owned by `supabase_admin`.

Verified on 2026-10-04 (Asia/Saigon): the job is active and its first scheduled
run completed at 06:25. Its HTTP response was `200 Healthy`, without timeout or
network error. An anonymous Data API probe rejected the `net` schema with
`PGRST106`; only `public` and `graphql_public` were exposed. The security advisor
reported no errors after setup; the pre-existing Auth password-protection
warning remains unrelated to this schedule.

Inspect:

```sql
select jobid, jobname, schedule, active
from cron.job where jobname = 'homeji-render-liveness';

select status, return_message, start_time, end_time
from cron.job_run_details
where jobid = (select jobid from cron.job where jobname = 'homeji-render-liveness')
order by start_time desc limit 10;

-- This project currently uses pg_net only for this uptime job.
select id, status_code, content, timed_out, error_msg, created
from net._http_response order by id desc limit 10;
```

Stop only this job (leave extensions and other jobs intact):

```sql
select cron.unschedule(jobid)
from cron.job where jobname = 'homeji-render-liveness';
```

Render Free can still restart and shares 750 instance hours per workspace each
month. Keeping one service awake consumes almost all that allowance in a
31-day month. This schedule reduces idle sleep; it does not guarantee uptime.

References:
- https://supabase.com/docs/guides/cron/quickstart
- https://supabase.com/docs/guides/database/extensions/pg_net
- https://render.com/docs/free
