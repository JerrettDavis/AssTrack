# Commercial Asset Tracking Roadmap

This audit tracks the product areas AssTrack should cover as it grows from a location dashboard into a commercial asset tracking platform.

## Current Foundation

- Live map with asset/tracker modes, live-first latest asset positions, tracker freshness states, live update stream, marker pulse feedback, clustering, focus behavior, interpolated timeline playback, range circles, and map query-string state.
- Manual asset and device inventory, device-to-asset linking, and provider-enriched Meshtastic node profiles.
- Geofences, speed alerts, observation history, bridge delivery logs, MQTT visualization, and messaging threads.
- Alert routing rules that queue speed and geofence alerts into provider-backed message threads, with per-asset route filters for targeted dispatch.
- Integration feed configuration with normalized location ingest, enterprise signal/event publishing, and a pluggable bridge gateway.
- Signal and Telegram bridge handoff foundation for inbound messages, outbound queues, and delivery status updates through the message-thread API.
- Concrete Signal and Telegram message worker packages that poll provider APIs and use the bridge handoff endpoints.
- Asset classes and criticality for people, vehicles, property, pets, equipment, containers, and other assets.
- Generic sensor readings attached to assets, devices, and integration feeds.
- Sensor telemetry summaries in asset and device inventory views, including recent numeric trends and stale-sensor warnings.
- Maintenance schedules, completed service records, diagnostic-event triggers, and reminders for vehicle and equipment service intervals based on date, odometer, runtime, and sensor telemetry.
- Asset custody status, checkout/check-in, transfer events, and custody history for tools, containers, vehicles, and shared equipment.
- Utilization reports that summarize distance, moving time, idle time, stops, max speed, and observation counts by tracked device over bounded date ranges.
- RBAC foundation with viewer/operator/admin/ingest roles, deployment access tiers, capability discovery, admin-only destructive maintenance endpoints, searchable enterprise audit events for operational changes, enterprise signal fan-out into hooks/live events/messaging routes/audit, event-specific webhook subscriptions, subscription health telemetry, audited webhook subscription changes, and operator replay for retained webhook payloads.

## Commercial Verticals

| Vertical | Expected capabilities | AssTrack status |
|---|---|---|
| Fleet and vehicles | OBD/CAN telemetry, odometer, fuel, ignition, tire pressure, maintenance schedules, driver assignment, route replay. | Location, alerts, generic sensor readings, maintenance schedules, diagnostic maintenance triggers, completed service records, reminders, custody, and utilization reporting are present; driver workflows and route replay remain. |
| People and teams | Privacy-aware tracking, check-ins, SOS, messaging, temporary sharing, audit logs. | Location, messaging foundation, Signal/Telegram bridge handoff, message workers, asset class, and per-asset alert routing are present; SOS/escalation workflow remains. |
| Pets and working animals | Wearable battery, temperature, activity, safe zones, missing-mode escalation. | Pet asset class and sensors are present; pet-specific workflow remains. |
| Property and facilities | Fixed/site assets, environmental sensors, motion/door events, service windows. | Property class and sensors are present; service workflows remain. |
| Equipment and tools | Utilization, impact/motion, runtime, battery, custody, checkout/check-in. | Equipment class, sensors, runtime maintenance schedules, diagnostic triggers, completed service records, reminders, custody events, and utilization reports are present; richer utilization analytics remain. |
| Containers and cargo | Door, impact, temperature, humidity, route custody, seal state. | Container class, sensors, and custody events are present; seal state and route chain-of-custody remain. |

## Architecture Direction

- Keep provider-native ingestion additive. Raw provider facts should upsert device/profile/sensor data without overwriting operator-entered labels or asset ownership.
- Keep assets operator-owned by default. Bridges may create or enrich devices, but persistent assets should only be created by explicit enrollment/import workflows.
- Model all non-location telemetry as sensor readings first. Promote common readings into richer workflows only after the generic stream proves stable.
- Keep provider bridges isolated from the API process when they need polling, MQTT subscriptions, SDKs, secrets, or long-running reconnect logic.
- Treat communication channels as message providers. Signal, Telegram, SMS, email, Meshtastic text, and future chat bridges should converge on message threads.
- Keep enterprise access controls additive. API-key RBAC is the current bootstrap path; future user, tenant, and SSO work should map into the same roles, tiers, and capability contracts.

## Next Iterations

1. Alert escalation policies: add retry windows, acknowledgement deadlines, recipient schedules, SMS/email providers, and richer route filters.
2. Reports: expand utilization into daily/weekly rollups, dwell time, geofence visits, mileage, stop summaries, and sensor exceptions.
3. Privacy: add per-asset retention, hidden zones, share links, and stricter handling for people/pet classes.
4. Import/enrollment: provide controlled bulk enrollment for known devices without auto-creating unwanted assets.
5. Mobile UX: optimize map, messaging, and alert acknowledgement for field use.
6. Operations: add integration health checks, per-feed metrics, secret rotation, connector-specific signal adapters, webhook subscription delivery policies, and durable delivery queues.
7. Messaging hardening: add deployment recipes, provider-specific retry backoff, duplicate inbound suppression, and operator metrics for Signal/Telegram workers.
8. Enterprise identity: add persistent users, tenant/workspace boundaries, SSO/OIDC, scoped API keys, richer audit coverage, and role assignment UI.
