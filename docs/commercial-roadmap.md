# Commercial Asset Tracking Roadmap

This audit tracks the product areas AssTrack should cover as it grows from a location dashboard into a commercial asset tracking platform.

## Current Foundation

- Live map with asset/tracker modes, live-first latest asset positions, tracker freshness states, live update stream, marker pulse feedback, clustering, focus behavior, interpolated timeline playback, range circles, and map query-string state.
- Manual asset and device inventory, device-to-asset linking, and provider-enriched Meshtastic node profiles.
- Geofences, speed alerts, observation history, bridge delivery logs, MQTT visualization, and messaging threads.
- Alert routing rules that queue speed and geofence alerts into provider-backed message threads.
- Integration feed configuration with normalized location ingest and a pluggable bridge gateway.
- Asset classes and criticality for people, vehicles, property, pets, equipment, containers, and other assets.
- Generic sensor readings attached to assets, devices, and integration feeds.
- Sensor telemetry summaries in asset and device inventory views, including recent numeric trends and stale-sensor warnings.
- Maintenance schedules, completed service records, diagnostic-event triggers, and reminders for vehicle and equipment service intervals based on date, odometer, runtime, and sensor telemetry.
- Asset custody status, checkout/check-in, transfer events, and custody history for tools, containers, vehicles, and shared equipment.

## Commercial Verticals

| Vertical | Expected capabilities | AssTrack status |
|---|---|---|
| Fleet and vehicles | OBD/CAN telemetry, odometer, fuel, ignition, tire pressure, maintenance schedules, driver assignment, route replay. | Location, alerts, generic sensor readings, maintenance schedules, diagnostic maintenance triggers, completed service records, reminders, and custody are present; driver workflows and route replay remain. |
| People and teams | Privacy-aware tracking, check-ins, SOS, messaging, temporary sharing, audit logs. | Location, messaging foundation, and asset class are present; SOS/escalation workflow remains. |
| Pets and working animals | Wearable battery, temperature, activity, safe zones, missing-mode escalation. | Pet asset class and sensors are present; pet-specific workflow remains. |
| Property and facilities | Fixed/site assets, environmental sensors, motion/door events, service windows. | Property class and sensors are present; service workflows remain. |
| Equipment and tools | Utilization, impact/motion, runtime, battery, custody, checkout/check-in. | Equipment class, sensors, runtime maintenance schedules, diagnostic triggers, completed service records, reminders, and custody events are present; utilization reports remain. |
| Containers and cargo | Door, impact, temperature, humidity, route custody, seal state. | Container class, sensors, and custody events are present; seal state and route chain-of-custody remain. |

## Architecture Direction

- Keep provider-native ingestion additive. Raw provider facts should upsert device/profile/sensor data without overwriting operator-entered labels or asset ownership.
- Keep assets operator-owned by default. Bridges may create or enrich devices, but persistent assets should only be created by explicit enrollment/import workflows.
- Model all non-location telemetry as sensor readings first. Promote common readings into richer workflows only after the generic stream proves stable.
- Keep provider bridges isolated from the API process when they need polling, MQTT subscriptions, SDKs, secrets, or long-running reconnect logic.
- Treat communication channels as message providers. Signal, Telegram, SMS, email, Meshtastic text, and future chat bridges should converge on message threads.

## Next Iterations

1. Messaging providers: implement Signal and Telegram bridge adapters against the existing message-thread API.
2. Alert escalation policies: add retry windows, acknowledgement deadlines, recipient schedules, SMS/email providers, and per-asset routing filters.
3. Reports: add daily/weekly utilization, dwell time, geofence visits, mileage, stop summaries, and sensor exceptions.
4. Privacy: add per-asset retention, hidden zones, share links, and stricter handling for people/pet classes.
5. Import/enrollment: provide controlled bulk enrollment for known devices without auto-creating unwanted assets.
6. Mobile UX: optimize map, messaging, and alert acknowledgement for field use.
7. Operations: add integration health checks, per-feed metrics, secret rotation, and dead-letter replay.
