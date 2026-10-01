-- Outbox relay role (docs/plans/ai-business-os/adr-event-consumption.md, E-1 (a)). Reads outbox POINTERS of every
-- tenant and writes the delivery ledger — never a payload, never a business table. The cross-tenant visibility comes
-- from the `relay_access` policies in the Messaging RLS migration; these grants are what bounds it.
-- Change the password before running. Run after every migration (SemanticCatalog before CRM, Messaging last), as the migration role, after
-- create-runtime-role.sql.
CREATE ROLE fynovio_relay LOGIN PASSWORD 'change-me' NOSUPERUSER NOBYPASSRLS;

GRANT CONNECT ON DATABASE fynovio_platform TO fynovio_relay;
GRANT USAGE ON SCHEMA crm, masterdata, access, collaboration, tenant_lifecycle, semantic, messaging TO fynovio_relay;

-- Pointer columns only; `processed_at` is the one column it may change.
GRANT SELECT (id, tenant_id, event_id, event_type, aggregate_type, aggregate_id, aggregate_version, occurred_at, processed_at),
      UPDATE (processed_at)
    ON crm.outbox_messages, masterdata.outbox_messages, access.outbox_messages,
       collaboration.outbox_messages, tenant_lifecycle.outbox_messages, semantic.outbox_messages
    TO fynovio_relay;

GRANT SELECT, INSERT, UPDATE ON messaging.event_deliveries, messaging.consumer_registrations TO fynovio_relay;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA messaging TO fynovio_relay;
