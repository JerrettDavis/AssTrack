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

Scenario: View live map
  When I post an observation for the device via the API
  And I navigate to the map page
  Then the page contains "Live Map"

Scenario: View observations list
  When I post an observation for the device via the API
  And I navigate to the assets page
  Then the page contains "E2E Test Asset"

Scenario: View alerts with threshold
  When I post an observation with speed 150.0 for the device via the API
  And I navigate to the alerts page
  Then the alerts table contains a cell with "150.0"
