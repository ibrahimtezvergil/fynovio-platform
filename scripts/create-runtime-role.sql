-- Application runtime role. Separate from the role that runs migrations (doc 07 §3):
-- superusers, BYPASSRLS roles and table owners are not bound by RLS the same way, so the
-- application must never connect as them. Change the password before running.
-- Run after migrations, as the migration role, against the target database.
CREATE ROLE fynovio_app LOGIN PASSWORD 'change-me' NOSUPERUSER NOBYPASSRLS;

GRANT CONNECT ON DATABASE fynovio_platform TO fynovio_app;
GRANT USAGE ON SCHEMA crm TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA crm TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA crm TO fynovio_app;

-- Tables and sequences created by later migrations (run by the same migration role).
ALTER DEFAULT PRIVILEGES IN SCHEMA crm
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA crm
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

-- evidence_records is append-only (docs/schema/crm-sales-schema.md, revision 3, item 10).
REVOKE UPDATE, DELETE ON crm.evidence_records FROM fynovio_app;
