-- Infrastructure setup for the existing Homeji Supabase project.
-- Run as postgres; this script never modifies Homeji business tables.
create extension if not exists pg_cron with schema pg_catalog;
create extension if not exists pg_net with schema extensions;

-- Keep cron/net outside the Supabase Data API's exposed schemas.
-- Extension objects are owned by supabase_admin; postgres cannot revoke their
-- provider-managed default grants. Do not expose these schemas as public RPCs.

select cron.schedule(
  'homeji-render-liveness',
  '*/5 * * * *',
  $job$
    delete from cron.job_run_details
    where jobid = (select jobid from cron.job where jobname = 'homeji-render-liveness')
      and end_time < now() - interval '7 days';
    select net.http_get(
      url := 'https://homeji-be.onrender.com/health/live',
      timeout_milliseconds := 90000
    );
  $job$
) as job_id;
