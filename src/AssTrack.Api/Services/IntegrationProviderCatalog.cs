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
            ["traccar", "gps"])
    ];

    public static IReadOnlyList<IntegrationProviderDto> GetAll() => Providers;

    public static IntegrationProviderDto? Get(string provider)
        => Providers.FirstOrDefault(x => string.Equals(x.Id, provider, StringComparison.OrdinalIgnoreCase));
}
