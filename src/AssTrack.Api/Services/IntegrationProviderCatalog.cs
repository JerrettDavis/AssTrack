using AssTrack.Domain.Contracts;

namespace AssTrack.Api.Services;

public static class IntegrationProviderCatalog
{
    private static readonly IReadOnlyList<IntegrationProviderDto> Providers =
    [
        new("generic-webhook", "Generic Webhook", "Webhook", "Push", true, true, false, "ready",
            "Accepts normalized location observations from automations, middleware, or custom tracker services.",
            "POST normalized observations to the feed ingest URL. Use this for Apple Shortcuts, IFTTT, Zapier, custom scripts, or vendor webhooks.",
            ["webhook", "automation"]),
        new("gps-http", "GPS / Cellular HTTP", "GPS", "Push", true, true, false, "ready",
            "For cellular GPS trackers or gateways that can POST JSON over HTTP.",
            "Configure the tracker or gateway to POST tracker id, timestamp, latitude, longitude, and optional speed/heading to the feed ingest URL.",
            ["gps", "cellular"]),
        new("meshtastic", "Meshtastic", "LoRa Mesh", "MQTT bridge", true, true, false, "bridge-required",
            "Meshtastic nodes can share location through MQTT; the bridge gateway can subscribe to JSON position topics and ingest node locations.",
            "Enable MQTT JSON on the gateway node, configure broker/topic settings in the bridge editor, and use a private broker when precise locations matter. Public MQTT may intentionally reduce location precision.",
            ["meshtastic", "lora"]),
        new("apple-findmy", "Apple Find My / AirTag", "Bluetooth finding network", "Share/import bridge", false, true, false, "bridge-required",
            "Apple exposes Find My accessory development through the MFi program and user sharing flows, not a general AirTag location API.",
            "Use a user-approved Share Item Location link, manual import, or a compliant internal bridge that converts authorized location updates into normalized observations. Do not scrape iCloud or bypass Apple privacy controls.",
            ["airtag", "find-my", "apple"]),
        new("google-findhub", "Google Find Hub", "Bluetooth finding network", "Partner/bridge", false, true, false, "bridge-required",
            "Google Find Hub supports certified accessory development, but does not provide a general consumer location polling API for arbitrary tags.",
            "Use a partner-approved workflow or an automation bridge that posts authorized updates to AssTrack.",
            ["google", "find-hub"]),
        new("samsung-find", "Samsung SmartThings Find", "Bluetooth finding network", "Partner/bridge", false, true, false, "bridge-required",
            "SmartThings Find supports compatible devices and owner-facing location, but tag location access is not exposed as a standard polling API.",
            "Use Samsung-supported partner flows or an approved bridge/export process to POST normalized observations.",
            ["samsung", "smarttag"]),
        new("home-assistant", "Home Assistant", "Smart home", "REST polling / webhook", true, true, true, "ready",
            "Home Assistant exposes device_tracker and sensor entities with latitude/longitude through its REST API.",
            "Create a long-lived access token, list the device_tracker entities to poll, and let the bridge gateway fetch `/api/states/{entity_id}` on an interval.",
            ["home-assistant", "device-tracker"]),
        new("owntracks", "OwnTracks", "Mobile app", "Webhook/MQTT bridge", true, true, false, "ready",
            "OwnTracks can publish phone location updates through HTTP or MQTT.",
            "Configure OwnTracks HTTP mode, or bridge MQTT messages, into the normalized feed ingest endpoint.",
            ["phone", "owntracks"]),
        new("traccar", "Traccar", "Tracking server", "Webhook/API bridge", true, true, true, "ready",
            "Traccar can aggregate many GPS devices and forward location events.",
            "Configure Traccar webhooks or a polling bridge to POST normalized observations into AssTrack.",
            ["traccar", "gps"]),
        new("signal", "Signal", "Messaging", "Bridge", true, false, false, "bridge-required",
            "Signal can carry operator-to-field messages and receive replies through a local bridge service.",
            "Run an approved Signal bridge, map contacts or groups to assets/devices, and forward inbound/outbound message events to AssTrack messaging endpoints.",
            ["signal", "messages", "chat"]),
        new("telegram", "Telegram", "Messaging", "Bot bridge", true, false, false, "bridge-required",
            "Telegram bots can provide dispatch, group, and field-user messaging channels for tracked assets.",
            "Create a Telegram bot, restrict allowed chats, and bridge bot updates into AssTrack message threads.",
            ["telegram", "messages", "bot"]),
        new("twilio-sms", "Twilio SMS", "Messaging", "Webhook/API", true, false, false, "ready",
            "SMS remains useful for low-bandwidth field messaging, check-ins, and escalation alerts.",
            "Configure Twilio inbound webhooks and outbound credentials, then map phone numbers to people, vehicles, or devices.",
            ["sms", "twilio", "messages"]),
        new("smtp-email", "Email", "Messaging", "SMTP/IMAP bridge", true, false, true, "bridge-required",
            "Email channels support reports, alert delivery, and low-friction operator workflows.",
            "Configure SMTP for outbound messages and optionally poll IMAP or mailbox webhooks for replies.",
            ["email", "smtp", "alerts"]),
        new("obd-telematics", "OBD-II / Vehicle Sensors", "Vehicle sensors", "Bridge", true, true, false, "bridge-required",
            "Vehicle gateways can report ignition, odometer, fuel, battery, diagnostic codes, and motion telemetry.",
            "Bridge OBD-II or CAN gateway payloads into sensor readings and normalized observations so vehicle health aggregates with location.",
            ["obd", "vehicle", "sensors"]),
        new("ble-sensors", "BLE / IoT Sensors", "IoT sensors", "Gateway bridge", true, true, false, "bridge-required",
            "BLE and IoT gateways can attach temperature, humidity, motion, door, impact, or battery data to assets.",
            "Publish gateway sensor readings to AssTrack with asset or device identifiers; use this for cold-chain, property, pet, or equipment telemetry.",
            ["ble", "iot", "sensors"])
    ];

    public static IReadOnlyList<IntegrationProviderDto> GetAll() => Providers;

    public static IntegrationProviderDto? Get(string provider)
        => Providers.FirstOrDefault(x => string.Equals(x.Id, provider, StringComparison.OrdinalIgnoreCase));
}
