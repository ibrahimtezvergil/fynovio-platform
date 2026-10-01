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

-- MasterData module.
GRANT USAGE ON SCHEMA masterdata TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA masterdata TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA masterdata TO fynovio_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA masterdata
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA masterdata
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

REVOKE UPDATE, DELETE ON masterdata.evidence_records FROM fynovio_app;

-- Collaboration module.
GRANT USAGE ON SCHEMA collaboration TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA collaboration TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA collaboration TO fynovio_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA collaboration
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA collaboration
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

-- TenantLifecycle module.
GRANT USAGE ON SCHEMA tenant_lifecycle TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA tenant_lifecycle TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA tenant_lifecycle TO fynovio_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA tenant_lifecycle
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA tenant_lifecycle
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

-- Identity + Access modules (share one assembly/DbContext, two schemas).
GRANT USAGE ON SCHEMA identity TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA identity TO fynovio_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA identity
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA identity
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

GRANT USAGE ON SCHEMA access TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA access TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA access TO fynovio_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA access
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA access
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

-- access.evidence_records is append-only, same convention as crm/masterdata.
REVOKE UPDATE, DELETE ON access.evidence_records FROM fynovio_app;

-- identity.auth_events is append-only (authentication audit log; never modify or delete).
REVOKE UPDATE, DELETE ON identity.auth_events FROM fynovio_app;

-- Messaging (delivery ledger). The runtime role reads and re-arms its own tenant's deliveries; only the relay role
-- (create-relay-role.sql) creates them. consumer_registrations is relay-only.
GRANT USAGE ON SCHEMA messaging TO fynovio_app;
GRANT SELECT, UPDATE ON messaging.event_deliveries TO fynovio_app;
