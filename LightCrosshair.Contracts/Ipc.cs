using System.Text.Json;

namespace LightCrosshair.Contracts;

// Transport: named pipe ContractInfo.PipeName(sessionId), Core = server, UI = client.
// Framing: one compact UTF-8 JSON IpcEnvelope per line ("\n"), serialized with ContractJson.Options.
// Every request gets exactly one response with the same Id. Events have Id = 0.

public static class IpcKinds
{
    public const string Request = "req";
    public const string Response = "res";
    public const string Event = "evt";
}

public sealed class IpcEnvelope
{
    public string Kind { get; set; } = IpcKinds.Request;
    public long Id { get; set; }
    public string? Method { get; set; }
    public bool Ok { get; set; } = true;
    public JsonElement? Payload { get; set; }
    public IpcError? Error { get; set; }
}

public sealed class IpcError
{
    public string Code { get; set; } = IpcErrorCodes.Internal;
    /// <summary>English, for logs. The UI shows its own localized text based on Code.</summary>
    public string Message { get; set; } = "";
}

public static class IpcErrorCodes
{
    public const string BadRequest = "bad_request";
    public const string NotFound = "not_found";
    public const string Validation = "validation";
    public const string Unsupported = "unsupported";
    public const string ProtocolMismatch = "protocol_mismatch";
    public const string Internal = "internal";
}

/// <summary>Request methods. Comment format: request payload -> response payload.</summary>
public static class IpcMethods
{
    /// <summary>HelloRequest -> HelloResponse. Must be the first request; mismatch -> ProtocolMismatch.</summary>
    public const string Hello = "hello";
    /// <summary>null -> Snapshot</summary>
    public const string GetSnapshot = "getSnapshot";

    /// <summary>CrosshairDesign -> CrosshairDesign (validated, clamped, UpdatedAt set). Insert or replace by Id.</summary>
    public const string SaveCrosshair = "saveCrosshair";
    /// <summary>IdRequest -> null. Fails with Validation if it is the default crosshair or used by a game profile.</summary>
    public const string DeleteCrosshair = "deleteCrosshair";
    /// <summary>GameProfile -> GameProfile. Insert or replace by Id. Writes the NVIDIA cap when it changed.</summary>
    public const string SaveGameProfile = "saveGameProfile";
    /// <summary>IdRequest -> null. Removes the NVIDIA cap it managed.</summary>
    public const string DeleteGameProfile = "deleteGameProfile";
    /// <summary>AppSettings -> AppSettings. Re-registers hotkeys; errors appear in RuntimeState.HotkeyErrors.</summary>
    public const string SaveSettings = "saveSettings";

    /// <summary>PreviewRequest -> null. Shows a transient design on the real overlay; Design = null ends it. Ends automatically when the UI disconnects.</summary>
    public const string Preview = "preview";
    /// <summary>RenderRequest -> RenderResponse. Same renderer as the overlay, so previews are pixel-identical.</summary>
    public const string Render = "render";

    /// <summary>null -> List&lt;PresetEntry&gt;</summary>
    public const string GetPresets = "getPresets";
    /// <summary>CrosshairDesign -> TextPayload (share code). Unsupported if the design has an Image layer.</summary>
    public const string EncodeShareCode = "encodeShareCode";
    /// <summary>TextPayload (share code) -> CrosshairDesign (new Id, not saved).</summary>
    public const string DecodeShareCode = "decodeShareCode";
    /// <summary>TextPayload (source PNG path) -> TextPayload (stored file name). Validates size limits.</summary>
    public const string ImportImage = "importImage";
    /// <summary>ExportRequest -> null. Writes a CrosshairExportFile.</summary>
    public const string ExportCrosshairs = "exportCrosshairs";
    /// <summary>TextPayload (file path) -> ImportResult. Imported designs get new Ids.</summary>
    public const string ImportCrosshairs = "importCrosshairs";

    /// <summary>null -> List&lt;ProcessEntry&gt; (processes with a visible top-level window).</summary>
    public const string ListProcesses = "listProcesses";
    /// <summary>TextPayload (process name) -> GameProfile (created with the default crosshair).</summary>
    public const string AcceptSuggestion = "acceptSuggestion";
    /// <summary>TextPayload (process name) -> null. Adds it to DismissedSuggestions.</summary>
    public const string DismissSuggestion = "dismissSuggestion";

    /// <summary>InputCaptureRequest -> null. While Active, Core suspends every hotkey and hook action so the UI can capture a new binding. Reset on disconnect and after 60 s without a renewal.</summary>
    public const string SetInputCapture = "setInputCapture";

    /// <summary>DisplayColorsPreviewRequest -> null. Applies colors live while the user drags sliders; Colors = null ends the preview and restores the context's colors. Ends on disconnect and when the UI parks.</summary>
    public const string PreviewDisplayColors = "previewDisplayColors";

    /// <summary>null -> null. Exits Core (and therefore the app).</summary>
    public const string Quit = "quit";
}

/// <summary>Core -> UI notifications.</summary>
public static class IpcEvents
{
    /// <summary>Snapshot. Sent after any store change, from any source (UI, hotkey, migration).</summary>
    public const string SnapshotChanged = "snapshotChanged";
    /// <summary>RuntimeState. Sent when runtime state changes; coalesced to at most 10 per second.</summary>
    public const string RuntimeChanged = "runtimeChanged";
    /// <summary>NavigateRequest or null. UI must activate its window (and navigate when a page is given).</summary>
    public const string FocusRequested = "focusRequested";
    /// <summary>null. UI must close (OpenSettings hotkey pressed while open).</summary>
    public const string CloseRequested = "closeRequested";
}

public sealed class HelloRequest
{
    public int ProtocolVersion { get; set; } = ContractInfo.ProtocolVersion;
    public string Client { get; set; } = "";
}

public sealed class HelloResponse
{
    public int ProtocolVersion { get; set; } = ContractInfo.ProtocolVersion;
    public string CoreVersion { get; set; } = "";
}

public sealed class IdRequest
{
    public string Id { get; set; } = "";
}

public sealed class TextPayload
{
    public string Value { get; set; } = "";
}

public sealed class PreviewRequest
{
    public CrosshairDesign? Design { get; set; }
}

public sealed class RenderRequest
{
    public CrosshairDesign Design { get; set; } = new();
    /// <summary>Square canvas side in pixels, 16..512. The design is drawn at 1:1 pixel scale, centered.</summary>
    public int CanvasSize { get; set; } = 128;
    /// <summary>"#RRGGBB" background, or null for transparent.</summary>
    public string? Background { get; set; }
}

public sealed class RenderResponse
{
    public string PngBase64 { get; set; } = "";
}

public sealed class ExportRequest
{
    public string Path { get; set; } = "";
    public List<string> CrosshairIds { get; set; } = [];
}

public sealed class DisplayColorsPreviewRequest
{
    public DisplayColorSettings? Colors { get; set; }
}

public sealed class InputCaptureRequest
{
    public bool Active { get; set; }
}

public sealed class ImportResult
{
    public List<CrosshairDesign> Imported { get; set; } = [];
    /// <summary>English, for logs; the UI shows a generic localized warning count.</summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>Page ids also used by the UI command line: --page &lt;id&gt; [--suggest &lt;process&gt;].</summary>
public static class UiPages
{
    public const string Studio = "studio";
    public const string Library = "library";
    public const string Games = "games";
    public const string Hotkeys = "hotkeys";
    public const string Settings = "settings";
}

public sealed class NavigateRequest
{
    public string Page { get; set; } = UiPages.Studio;
    /// <summary>Process name to prefill on the Games page.</summary>
    public string? SuggestProcess { get; set; }
}
