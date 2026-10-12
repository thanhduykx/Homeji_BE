# Preserve administrator authority during lifestyle updates

Date: 2026-10-12. Tracking: Homeji_BE#17; related EXE101_Homeji#21.

## Problem and change

`UserProfile.UpdateLifestyle` assigned the submitted renter/landlord role to every profile. An existing administrator could therefore lose management access while saving ordinary preferences. Preserve an existing Admin role in this method. Continue rejecting a requested Admin role before mutation, and preserve renter/landlord self-service switching.

This is a domain guard, with no schema, endpoint or payload change. Profile creation/update paths were inspected: updating an existing profile does not assign its role. Historical production role loss cannot be attributed to a particular request without an audit trail.

## Verification

- Application unit tests: 423 passed, including domain and service administrator-preservation and privilege-escalation regressions.
- API suite: 114 passed; 13 opt-in database tests skipped.
- Five lifecycle cases exercise the actual profile endpoint with test authentication and an in-memory repository; these are not production database integration checks.
- git diff --check passed.

The named production identity admin@gmail.com was separately restored from Renter to Admin using a bounded, confirmed-identity update. Other administrator profiles remain unchanged pending the owner's decision. Production management navigation was verified on the original frontend URL; production preference submission is not included in these test results.

Deployment: commit 98b6f0c is Live on the Render service serving https://homeji-be.onrender.com (deploy dep-db6b4pbrjlhs73bi5s6g, duration 2m14s). GET /health/ready returned 200 Healthy after deployment. This confirms deployment/readiness, not a production lifestyle write.
