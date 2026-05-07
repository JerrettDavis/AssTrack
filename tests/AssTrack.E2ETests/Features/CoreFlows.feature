Feature: Core Flows

Background:
  Given an asset named "E2E Test Asset" exists via the API
  And a device with identifier "E2E-DEVICE-001" is linked to the asset

Scenario: Create an observation for an asset
  When I post an observation for the device via the API
  And I navigate to the assets page
  Then the page contains "E2E Test Asset"

Scenario: View asset details
  When I navigate to the assets page
  Then the page contains "E2E Test Asset"

Scenario: View asset maintenance due status
  Given a due maintenance schedule named "Mileage inspection" exists for the asset
  When I navigate to the assets page
  Then the page contains "Maintenance"
  And the page contains "Mileage inspection"
  And the page contains "due"

Scenario: Complete due asset maintenance
  Given a due maintenance schedule named "Mileage inspection" exists for the asset
  When I navigate to the assets page
  And I complete maintenance schedule "Mileage inspection"
  Then the page contains "Recent service"
  And the page contains "current"

Scenario: View live map
  When I post an observation for the device via the API
  And I navigate to the map page
  Then the page contains "Live Map"
  And the map layer controls are available
  When I select the first map node
  Then the map node details panel is available

Scenario: View geofence authoring controls
  When I navigate to the geofences page
  Then the page contains "Click the map to set the center."
  And the page contains "Radius presets"
  And the page contains "Shape"

Scenario: View bridge-created tracking signals
  Given an unassigned bridge device named "E2E Meshtastic Signal" exists via the API
  When I post an observation for the unassigned bridge device via the API
  And I navigate to the devices page
  Then the page contains "Tracking signal inbox"
  And the page contains "E2E Meshtastic Signal"
  And the page contains "Create asset"

Scenario: View observations list
  When I post an observation for the device via the API
  And I navigate to the assets page
  Then the page contains "E2E Test Asset"

Scenario: View alerts with threshold
  When I post an observation with speed 150.0 for the device via the API
  And I navigate to the alerts page
  Then the alerts table contains a cell with "150.0"

Scenario: Filter unacknowledged alerts
  When I post an observation with speed 150.0 for the device via the API
  And I navigate to the alerts page
  Then the alerts page has filter tab "Unacknowledged"

Scenario: Bulk acknowledge alerts
  When I post an observation with speed 150.0 for the device via the API
  And I post an observation with speed 155.0 for the device via the API
  And I navigate to the alerts page
  Then the alerts page has filter tab "Unacknowledged"

Scenario: Configure bridge providers from the main UI
  When I navigate to the bridge page
  And I configure bridge provider "Meshtastic"
  Then the bridge feed form is focused with bridge key "meshtastic"
  And the Meshtastic public MQTT defaults are configured
  And the bridge checkbox "Bridge enabled" is compact
  When I configure bridge provider "Home Assistant"
  Then the bridge feed form is focused with bridge key "home-assistant"
  And the page contains "Home Assistant location polling"
