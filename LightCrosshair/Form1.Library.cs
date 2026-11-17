using LightCrosshair.Contracts;
using LightCrosshair.Core;

namespace LightCrosshair;

internal sealed partial class Form1
{
    private async Task<object?> HandleLibraryRequestAsync(IpcEnvelope request)
    {
        var files = new LibraryFiles(Store.ImagesDirectory);
        switch (request.Method)
        {
            case IpcMethods.GetPresets: return PresetCatalog.Entries;
            case IpcMethods.EncodeShareCode: return new TextPayload { Value = ShareCode.Encode(Payload<CrosshairDesign>(request), Store.ImagesDirectory) };
            case IpcMethods.DecodeShareCode: return ShareCode.Decode(Payload<TextPayload>(request).Value, Store.ImagesDirectory);
            case IpcMethods.ImportImage:
                return await MutateAsync(async _ => new TextPayload { Value = await files.ImportImageAsync(Payload<TextPayload>(request).Value) });
            case IpcMethods.ExportCrosshairs:
                await files.ExportAsync(Payload<ExportRequest>(request), Validation.Copy(Document)); return null;
            case IpcMethods.ImportCrosshairs:
                return await MutateAsync(async next =>
                {
                    var result = await files.ImportAsync(Payload<TextPayload>(request).Value);
                    next.Crosshairs.AddRange(result.Imported); return result;
                });
            default: throw new CoreException(IpcErrorCodes.Unsupported, "Unknown IPC method.");
        }
    }
}
