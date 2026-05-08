# AssTrack Location Integrations

This guide defines the AssTrack-side contract for location integrations and the next steps for plumbing each provider into that contract.

AssTrack treats every physical tracker, phone, gateway, tag, or vendor device as a `Device`. Multiple devices can be linked to one `Asset`, so a truck can be tracked by GPS, cellular, Meshtastic, AirTag bridge, SmartThings bridge, and Google Find Hub bridge at the same time.

## AssTrack Capabilities

- Operator UI for integration feed configuration: `/integrations`.
- Provider catalog API: `GET /api/integrations/providers`.
- Feed CRUD API: `GET|POST /api/integrations`, `PUT|DELETE /api/integrations/{feedId}`.
- Normalized ingest API: `POST /api/integrations/{feedId}/observations`.
- Device metadata fields: `provider`, `externalId`, `tags`, and `integrationFeedId`.
- Auto-create mode: a feed can create a device automatically from `externalTrackerId`.
- Existing-device linking: if a device already exists with identifier `{provider}:{externalTrackerId}`, ingest links it to the feed.
- Observation metadata preservation: provider source details are serialized into the observation metadata payload.
- Generic sensor telemetry ingest: `POST /api/sensors/readings`.

## Auth

All control-plane integration endpoints require the operator key.

Integration observation ingest requires the ingest role, so either key can be used:

- Operator key: `ASSTRACK_API_KEY` in Docker, mapped to `Auth__ApiKey`.
- Ingest key: `ASSTRACK_INGEST_API_KEY` in Docker, mapped to `Auth__IngestApiKey`.

Bridge services should use the ingest key and send it as:

```http
X-Api-Key: <ingest-key>
```

## Normalized Observation Contract

Endpoint:

```text
POST /api/integrations/{feedId}/observations
```

Request:

```json
{
  "externalTrackerId": "tracker-or-vendor-id",
  "observedAt": "2026-05-06T01:30:00Z",
  "latitude": 41.8781,
  "longitude": -87.6298,
  "altitude": 182.5,
  "accuracyMeters": 12.0,
  "speedKmh": 64.4,
  "headingDegrees": 270.0,
  "label": "Truck 17 GPS",
  "assetId": "00000000-0000-0000-0000-000000000000",
  "tags": "truck, gps, primary",
  "metadata": "{\"source\":\"vendor-webhook\",\"battery\":88}"
}
```

## Sensor Reading Contract

Use sensor readings for telemetry that is not itself a location fix: vehicle fuel, odometer, ignition, battery, environmental sensors, door state, impact, motion, pet wearable data, and custom readings.

Endpoint:

```text
POST /api/sensors/readings
```

Request:

```json
{
  "assetId": "00000000-0000-0000-0000-000000000000",
  "deviceIdentifier": "obd:truck-17",
  "integrationFeedId": "00000000-0000-0000-0000-000000000000",
  "sensorType": "fuel",
  "name": "Fuel level",
  "numericValue": 72,
  "textValue": null,
  "unit": "%",
  "observedAt": "2026-05-07T15:30:00Z",
  "metadata": "{\"source\":\"obd-gateway\"}"
}
```

Required fields:

- `sensorType`
- `numericValue` or `textValue`
- at least one of `assetId`, `deviceId`, or `deviceIdentifier`

When `deviceIdentifier` is supplied and the device is linked to an asset, AssTrack automatically attaches the reading to that asset too. Sensor facts are additive; they do not overwrite operator-maintained asset labels or ownership.

Required fields:

- `externalTrackerId`
- `observedAt`
- `latitude`
- `longitude`

Optional fields:

- `altitude`
- `accuracyMeters`
- `speedKmh`
- `headingDegrees`
- `label`
- `assetId`
- `tags`
- `metadata`

Response:

```json
{
  "feedId": "feed-guid",
  "deviceId": "device-guid",
  "deviceIdentifier": "gps-http:tracker-or-vendor-id",
  "deviceCreated": true,
  "observation": {
    "id": "observation-guid"
  }
}
```

## Provider Matrix

| Provider | Feed ID | AssTrack mode | Third-party plumbing |
|---|---|---|---|
| Generic webhook | `generic-webhook` | Direct push | Any automation, script, or vendor webhook posts normalized JSON. |
| GPS / cellular HTTP | `gps-http` | Direct push | Configure tracker/gateway HTTP callback or a small protocol adapter. |
| Meshtastic | `meshtastic` | MQTT bridge | Subscribe to Meshtastic MQTT position packets and POST normalized observations. |
| Apple Find My / AirTag | `apple-findmy` | Bridge/import | Use user-approved sharing/import or an MFi-compliant workflow. Do not scrape iCloud or bypass Apple privacy controls. |
| Google Find Hub | `google-findhub` | Partner/bridge | Use Google partner-approved Find Hub accessory workflows or authorized user automation. |
| Samsung SmartThings Find | `samsung-find` | Partner/bridge | Use Samsung-supported partner flows, SmartThings-compatible device workflows, or approved export/automation. |
| OwnTracks | `owntracks` | HTTP/MQTT adapter | Configure OwnTracks HTTP mode to an adapter endpoint or bridge MQTT messages. |
| Traccar | `traccar` | Webhook/API bridge | Configure Traccar notifications/webhooks or poll Traccar positions and normalize. |
| Signal | `signal` | Messaging bridge | Bridge contacts or groups into AssTrack message threads. |
| Telegram | `telegram` | Bot bridge | Bridge bot updates into AssTrack message threads. |
| Twilio SMS | `twilio-sms` | Webhook/API | Map phone numbers to people, vehicles, assets, or devices for field messaging and escalations. |
| Email | `smtp-email` | SMTP/IMAP bridge | Send reports and alerts; optionally import replies. |
| OBD-II / Vehicle Sensors | `obd-telematics` | Bridge | Ingest vehicle health and sensor readings alongside location. |
| BLE / IoT Sensors | `ble-sensors` | Gateway bridge | Ingest environmental, door, motion, impact, battery, and wearable sensor readings. |

## Provider Playbooks

## Bridge Gateway

The repo includes a standalone pluggable bridge gateway at `src/AssTrack.BridgeGateway`.

It accepts provider-native JSON payloads, maps them through an adapter, and delivers normalized observations to AssTrack:

```text
Provider webhook/import -> POST /bridge/{feedKey} -> adapter -> POST /api/integrations/{feedId}/observations
```

Gateway endpoints:

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/bridge/providers` | List loaded adapters and aliases. |
| `GET` | `/bridge/feeds` | List configured feed keys with secrets redacted. |
| `POST` | `/bridge/{feedKey}` | Accept a provider-native payload for a configured feed. |
| `GET` | `/bridge/{feedKey}/messages/outbound` | Let a provider worker fetch queued outbound AssTrack messages. |
| `POST` | `/bridge/{feedKey}/messages/inbound` | Let a provider worker hand inbound messages into AssTrack threads. |
| `POST` | `/bridge/{feedKey}/messages/{messageId}/status` | Let a provider worker mark outbound messages sent, delivered, or failed. |

Bridge authentication:

- Configure `BridgeGateway:Feeds:{feedKey}:SharedSecret`.
- Send the secret with `X-Bridge-Secret`.
- `?secret=` also works for simple providers that cannot set custom headers, but headers are preferred.

Bridge configuration:

```json
{
  "BridgeGateway": {
    "AssTrackBaseUrl": "https://asstrack.example.com",
    "IngestApiKey": "ingest-key",
    "OperatorApiKey": "operator-key-for-dynamic-feed-config",
    "DryRun": false,
    "BridgeConfigRefreshSeconds": 30,
    "Feeds": {
      "meshtastic-main": {
        "Enabled": true,
        "FeedId": "feed-guid-from-ass-track",
        "Provider": "meshtastic",
        "SharedSecret": "provider-facing-secret",
        "DefaultTags": "bridge, meshtastic, lora",
        "LabelPrefix": "Mesh"
      }
    }
  }
}
```

The preferred development path is the main GUI at `/integrations`. The visual bridge editor stores provider settings in the feed `ConfigurationJson`. The gateway reads those GUI-created feeds from `GET /api/integrations/bridge-config` using `BridgeGateway:OperatorApiKey`, then merges them with static `appsettings` feeds.

Supported built-in adapters:

| Adapter | Feed provider value | Accepted payload shape |
|---|---|---|
| Normalized JSON | `generic-webhook`, `gps-http`, `apple-findmy`, `google-findhub`, `samsung-find` | Already-normalized location payloads, arrays, or `{ observations: [...] }`. |
| OwnTracks | `owntracks` | OwnTracks HTTP/MQTT location JSON. |
| Meshtastic | `meshtastic` | Meshtastic JSON packets with decoded `position`, including scaled `latitudeI`/`longitudeI`. |
| Traccar | `traccar` | Traccar event payloads with nested `device` and `position`, or direct position payloads. |
| Home Assistant | `home-assistant` | Bridge-managed REST polling of `device_tracker` entities from `/api/states/{entity_id}`. |

Run locally:

```powershell
dotnet run --project src\AssTrack.BridgeGateway\AssTrack.BridgeGateway.csproj --urls http://localhost:5056
```

Run with Docker Compose:

```sh
docker compose --profile integrations up --build
```

Example dry-run request:

```sh
curl -X POST http://localhost:5056/bridge/generic \
  -H "Content-Type: application/json" \
  -H "X-Bridge-Secret: change-me" \
  -d '{"externalTrackerId":"demo-1","observedAt":"2026-05-06T01:30:00Z","latitude":41.8781,"longitude":-87.6298}'
```

### Messaging Workers

The repo includes provider worker executables for messaging bridges:

| Project | Provider API | Purpose |
|---|---|---|
| `src/AssTrack.SignalWorker` | `signal-cli-rest-api` | Polls Signal receive/send APIs and bridges contact or group messages. |
| `src/AssTrack.TelegramWorker` | Telegram Bot API | Polls bot updates, sends queued replies, and tracks update offsets. |

Both workers use the same bridge handoff contract:

```text
Provider API -> worker -> POST /bridge/{feedKey}/messages/inbound
AssTrack thread -> GET /bridge/{feedKey}/messages/outbound -> worker -> Provider API
Provider send result -> POST /bridge/{feedKey}/messages/{messageId}/status
```

Signal local run:

```powershell
dotnet run --project src\AssTrack.SignalWorker\AssTrack.SignalWorker.csproj -- `
  --BridgeWorker:BridgeBaseUrl http://localhost:5056 `
  --BridgeWorker:FeedKey signal-local `
  --BridgeWorker:SharedSecret bridge-secret `
  --SignalWorker:SignalBaseUrl http://localhost:8080 `
  --SignalWorker:Account +15551234567
```

Telegram local run:

```powershell
dotnet run --project src\AssTrack.TelegramWorker\AssTrack.TelegramWorker.csproj -- `
  --BridgeWorker:BridgeBaseUrl http://localhost:5056 `
  --BridgeWorker:FeedKey telegram-local `
  --BridgeWorker:SharedSecret bridge-secret `
  --TelegramWorker:BotToken 123456:bot-token
```

Create matching `signal` or `telegram` feeds in `/integrations`, copy the bridge key/shared secret into the worker configuration, and keep provider tokens out of source-controlled settings files.

### Generic Webhook

Use this when the upstream system can already make HTTP requests.

Next steps:

1. Create a `generic-webhook` feed in `/integrations`.
2. Copy the feed ingest URL.
3. Configure the third-party webhook to send the normalized contract.
4. Put any vendor-specific payload in `metadata`.

### GPS / Cellular HTTP

Use this for devices or gateways that can POST GPS fixes over HTTP.

Next steps:

1. Create a `gps-http` feed with tags such as `gps, cellular`.
2. If the tracker can emit custom JSON, point it directly at the feed URL.
3. If the tracker uses a vendor protocol, build a protocol adapter that maps vendor fields to the normalized contract.
4. Use the vendor serial number or IMEI as `externalTrackerId`.

### Meshtastic

Meshtastic MQTT can publish raw protobuf packets and JSON messages. AssTrack should not subscribe directly from the API process; use a small bridge worker so broker credentials, channel keys, packet decoding, and retry behavior stay isolated from the web app.

Next steps:

1. Create a `meshtastic` feed.
2. Set a bridge key and shared secret in the visual bridge editor.
3. Enable `Subscribe to MQTT`.
4. Configure `MQTT host`, `MQTT port`, credentials, TLS, and the JSON topic. The UI detects the machine region from locale; on a US machine it defaults to `msh/US/2/json/LongFast/#`.
5. Configure a gateway node with MQTT JSON uplink enabled.
6. The bridge gateway parses JSON position packets and posts observations to AssTrack.

Standard public MQTT defaults from Meshtastic:

| Setting | Value |
|---|---|
| Host | `mqtt.meshtastic.org` |
| Port | `1883` |
| Username | `meshdev` |
| Password | `large4cats` |
| Root topic | `msh/REGION` |
| US LongFast JSON topic | `msh/US/2/json/LongFast/#` |

For a private channel running over the public Meshtastic MQTT server:

1. Keep the region root topic, for example `msh/US`.
2. Replace `LongFast` in the JSON topic with the exact private channel name, for example `msh/US/2/json/FleetOps/#`.
3. Enable MQTT and JSON on the gateway node.
4. Enable uplink on the private channel.
5. Use a private channel PSK. Do not use the default public PSK for private operational tracking.

Reference: https://meshtastic.org/docs/software/integrations/mqtt/

#### Meshtastic Child Nodes and T1000-E Trackers

AssTrack only maps packets that contain a usable latitude and longitude for the packet originator. A gateway/base station may publish other packets to MQTT while talking to a child node:

- `type=position` with `latitude_i=0` and `longitude_i=0` is treated as a request/no-fix/null-coordinate packet, not as a tracker location.
- `type=""` packets with RSSI/SNR but no payload show that a node is reachable, but they are not position reports.
- A child node with `lora.config_ok_to_mqtt=true` still must produce its own valid position packet before AssTrack can display it.

For Seeed SenseCAP T1000-E trackers, verify the tracker itself reports coordinates before debugging AssTrack:

```powershell
meshtastic --port COM12 --get position.gps_mode --get position.gps_enabled --info
```

The local node entry should contain `position.latitudeI` and `position.longitudeI`. If it only contains `position.time`, the tracker is reachable but has not published a usable fix. On T1000-E hardware, button actions can toggle GPS/send position; if a position request still yields zero coordinates, enable GPS in the Meshtastic app or CLI, move the tracker where it can get a GNSS fix, then wait for the next position broadcast.

### Home Assistant

Home Assistant can expose phone, vehicle, and tracker positions as `device_tracker` entities. The bridge gateway can poll those entity states with a long-lived access token and ingest any entity that includes latitude and longitude attributes.

Next steps:

1. Create a `home-assistant` feed.
2. In Home Assistant, create a long-lived access token for a dedicated AssTrack bridge user.
3. Enter the Home Assistant base URL, token, and comma-separated entity IDs such as `device_tracker.pixel_9, device_tracker.truck`.
4. Keep `Auto-create trackers` enabled unless you want to pre-map every entity.
5. The gateway polls `/api/states/{entity_id}` and maps `latitude`, `longitude`, `gps_accuracy`, `friendly_name`, and state timestamps into AssTrack observations.

Reference: https://developers.home-assistant.io/docs/api/rest

### Apple Find My / AirTag

Apple exposes Find My network development through the MFi program and privacy-preserving user flows, not as a general AirTag polling API. AssTrack is ready to receive authorized location updates, but the bridge must be compliant and user-approved.

Supported AssTrack-side paths:

- Manual import from a user-approved source.
- User-approved Share Item Location workflow converted by an internal bridge.
- MFi/enterprise-approved accessory workflow.

Next steps:

1. Create an `apple-findmy` feed.
2. Decide which approved source will provide the location update.
3. Build a bridge that converts authorized updates into the normalized contract.
4. Use the shared item identifier or internal tag inventory ID as `externalTrackerId`.

Reference: https://developer.apple.com/find-my/

### Google Find Hub

Google documents Find Hub Network as a Fast Pair accessory extension with certification and partner onboarding. Google states that it does not provide a general SDK or API for this integration path; device vendors work through proposal, legal, onboarding, firmware, and certification steps.

Supported AssTrack-side paths:

- Partner-approved Find Hub accessory workflow.
- Authorized user automation/export bridge.
- Manual import of shared location data.

Next steps:

1. Create a `google-findhub` feed.
2. If building hardware, follow Google's Find Hub partner process.
3. If importing user-owned locations, implement a consented bridge that posts normalized updates.
4. Use the vendor model/serial or internal tag inventory ID as `externalTrackerId`.

References:

- https://developers.google.com/nearby/fast-pair/specifications/extensions/fmdn
- https://developers.google.com/nearby/fast-pair/landing-page-find-hub

### Samsung SmartThings Find

Samsung documents SmartThings Find-compatible device development through device profiles, SDK configuration, test-device registration, and partnership/support channels. Treat SmartTag/Find data as a partner or user-approved bridge source unless Samsung grants the exact API access required.

Supported AssTrack-side paths:

- SmartThings Find-compatible device program.
- SmartThings-supported integration or approved export.
- User-approved automation bridge.

Next steps:

1. Create a `samsung-find` feed.
2. Confirm whether the target account/device type exposes an approved API or export path.
3. Build a bridge that maps authorized location updates into normalized observations.
4. Use SmartThings device ID, serial, or internal tag inventory ID as `externalTrackerId`.

Reference: https://developer.samsung.com/codelab/smartthings/find-device.html

For general SmartThings Home API device inventory, Samsung documents location/device APIs, but those APIs are not the same as unrestricted SmartTag live-location polling. Use the visual bridge editor to store the approved connector/export source and post normalized updates to the bridge endpoint.

### OwnTracks

OwnTracks can publish location through HTTP mode or MQTT. AssTrack's normalized endpoint is intentionally close to the OwnTracks HTTP use case, but an adapter is still recommended because OwnTracks emits its own JSON shape.

Next steps:

1. Create an `owntracks` feed.
2. Configure OwnTracks HTTP mode to call an adapter endpoint, or bridge the MQTT topic.
3. Map `topic` or `X-Limit-U` plus `X-Limit-D` to `externalTrackerId`.
4. Map `tst` to `observedAt`, `lat` to `latitude`, and `lon` to `longitude`.
5. Map battery, tracker ID, and raw OwnTracks fields into `metadata`.

Reference: https://owntracks.org/booklet/tech/http/

### Traccar

Traccar can aggregate many GPS devices and generate notifications/events. It can be integrated either by webhook-style notifications or by a polling worker that reads positions from Traccar and posts normalized observations.

Next steps:

1. Create a `traccar` feed.
2. Prefer a Traccar notification/webhook path when position data is available in the notification payload.
3. Otherwise, build a polling worker that reads recent positions and remembers the last imported position per Traccar device.
4. Use Traccar device ID or unique ID as `externalTrackerId`.

Reference: https://www.traccar.org/events/

## Bridge Worker Requirements

The built-in bridge gateway provides the common HTTP normalization and delivery layer. Provider-specific workers are still useful when the upstream system needs a long-running connector, such as MQTT subscription, polling, OAuth refresh, or vendor SDK access. Each external worker should provide:

- Provider-specific credentials outside AssTrack, stored in the bridge runtime's secret store.
- AssTrack base URL, feed ID, and ingest key.
- Idempotency at the source layer where possible.
- Retry with backoff on non-2xx responses.
- Dead-letter logging for malformed source payloads.
- A health endpoint or heartbeat log.
- Raw provider payload capture only when allowed by privacy policy.

Recommended external worker configuration shape:

```json
{
  "assTrackBaseUrl": "https://asstrack.example.com",
  "feedId": "feed-guid",
  "ingestApiKey": "secret",
  "provider": "meshtastic",
  "source": {
    "brokerUrl": "mqtts://broker.example.com",
    "topic": "msh/US/2/json/LongFast/#"
  }
}
```

## Implementation Backlog

AssTrack API, UI, and the built-in HTTP bridge gateway are ready for normalized feed ingest. Remaining work is provider-specific source acquisition and operational hardening:

1. Meshtastic MQTT subscriber that forwards decoded JSON to the bridge gateway.
2. Traccar polling worker for deployments that cannot use Traccar notifications.
3. Apple Find My import bridge after the approved source workflow is selected.
4. Google Find Hub bridge after partner/user-approved source workflow is selected.
5. Samsung SmartThings Find bridge after partner/user-approved source workflow is selected.
6. Integration feed delivery logs showing last ingest time, last error, and event counts.
7. Per-feed secret rotation and optional feed-specific ingest token.
8. Provider-specific config validation in the Settings/Integrations UI.

## Validation Checklist

For every new integration:

1. Create an integration feed in `/integrations`.
2. POST one test observation with the ingest key.
3. Confirm the device is created or linked on `/devices`.
4. Link the device to the target asset if it was not supplied in the ingest payload.
5. Confirm latest position appears on `/map`.
6. Confirm history export includes the imported observation.
7. Confirm speed/geofence rules still evaluate for the linked asset.
8. Disable the feed and verify ingest is rejected.
