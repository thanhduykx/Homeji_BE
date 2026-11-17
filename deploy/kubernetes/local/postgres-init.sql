CREATE ROLE anon;
CREATE ROLE authenticated;
CREATE ROLE service_role;

CREATE SCHEMA auth;
CREATE TABLE auth.users (
    id uuid PRIMARY KEY,
    email text,
    email_confirmed_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz
);
