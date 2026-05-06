namespace AssTrack.BridgeGateway.Adapters;

public sealed class BridgePayloadException(string message) : InvalidOperationException(message);
