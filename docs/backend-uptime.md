# Backend uptime monitor

The `Backend uptime` GitHub Actions workflow requests
`https://homeji-be.onrender.com/health/live` every five minutes, starting at
minute 2 of each hour (UTC). It can also be run manually from the Actions tab.
It requires no secrets, checkout, database access, or repository permissions.
The endpoint checks process liveness only; it does not prove database readiness.

The job expects HTTP 200 and the exact `Healthy` response. A request has a
90-second timeout to allow Render to start a sleeping instance, with one retry.
The job is limited to four minutes and overlapping runs are serialized.
Failures appear in the workflow run; GitHub notifications follow the user's
existing notification settings.

This is a best-effort uptime monitor, not an availability guarantee. GitHub may
delay or drop scheduled jobs during high load, and public-repository schedules
are disabled after 60 days without repository activity. Schedules run from the
default branch and require GitHub Actions to be enabled. Check runner-minute
allowances if the repository is private.

Render Free services still share 750 instance hours per workspace per month and
may restart. Keeping one service awake consumes almost all that allowance in a
31-day month. For reliable background jobs and production availability, use a
paid Render compute instance.

References:
- https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#schedule
- https://render.com/docs/free
