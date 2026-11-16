using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LightCrosshair.Contracts;
using LightCrosshair.Core;
using Xunit;

namespace LightCrosshair.Tests;

public class PipeServerTests
{
    [Fact]
    public void EnvelopeRoundTripUsesContractJson()
    {
        var request = new IpcEnvelope { Id = 12, Method = IpcMethods.Hello, Payload = PipeServer.Element(new HelloRequest { Client = "test" }) };
        var copy = JsonSerializer.Deserialize<IpcEnvelope>(JsonSerializer.Serialize(request, ContractJson.Options), ContractJson.Options)!;
        Assert.Equal("req", copy.Kind); Assert.Equal(12, copy.Id); Assert.Equal(ContractInfo.ProtocolVersion, copy.Payload!.Value.GetProperty("protocolVersion").GetInt32());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HandshakeRefusesMissingHelloOrMismatchedVersion(bool mismatch)
    {
        string name = "lc2-test-" + Guid.NewGuid().ToString("N");
        using var server = new PipeServer(name, _ => Task.FromResult<object?>(null), () => Task.CompletedTask);
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
        await client.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new IpcEnvelope { Id = 1, Method = mismatch ? IpcMethods.Hello : IpcMethods.GetSnapshot, Payload = PipeServer.Element(new HelloRequest { ProtocolVersion = -1 }) }, ContractJson.Options) + "\n"));
        var response = JsonSerializer.Deserialize<IpcEnvelope>((await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)))!, ContractJson.Options)!;
        Assert.Equal(1, response.Id); Assert.False(response.Ok);
        Assert.Equal(mismatch ? IpcErrorCodes.ProtocolMismatch : IpcErrorCodes.BadRequest, response.Error!.Code);
        Assert.Null(await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));
    }
    [Fact]
    public async Task PipelinedRequestsReceiveCorrelatedResponsesAndDisconnectEndsPreview()
    {
        string name = "lc2-test-" + Guid.NewGuid().ToString("N"); bool preview = false;
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = new PipeServer(name, request =>
        { if (request.Method == IpcMethods.Preview) preview = true; return Task.FromResult<object?>(request.Id); },
            () => { preview = false; disconnected.TrySetResult(); return Task.CompletedTask; });
        using var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        using var reader = new StreamReader(client, Encoding.UTF8, leaveOpen: true);
        async Task Send(int id, string method, object? payload = null) => await client.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new IpcEnvelope { Id = id, Method = method, Payload = PipeServer.Element(payload) }, ContractJson.Options) + "\n"));
        async Task<IpcEnvelope> Read() => JsonSerializer.Deserialize<IpcEnvelope>((await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)))!, ContractJson.Options)!;
        await Send(1, IpcMethods.Hello, new HelloRequest { Client = "test" }); Assert.True((await Read()).Ok);
        await Send(2, IpcMethods.Preview); await Send(3, IpcMethods.GetSnapshot); await Send(4, IpcMethods.GetSnapshot);
        for (int id = 2; id <= 4; id++) { var response = await Read(); Assert.Equal(id, response.Id); Assert.True(response.Ok); }
        Assert.True(preview); client.Dispose();
        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(preview);
    }
    [Fact]
    public async Task FramingIsBoundedBeforeDecodingAndPreservesPipelinedBytes()
    {
        using var valid = new MemoryStream(Encoding.UTF8.GetBytes("one\ntwo\n")); var reader = new BoundedLineReader(valid);
        Assert.Equal("one", await reader.ReadAsync(default)); Assert.Equal("two", await reader.ReadAsync(default)); Assert.Null(await reader.ReadAsync(default));
        using var oversized = new MemoryStream(new byte[BoundedLineReader.MaxLineBytes + 1]);
        await Assert.ThrowsAsync<InvalidDataException>(() => new BoundedLineReader(oversized).ReadAsync(default));
        using var invalidUtf8 = new MemoryStream(new byte[] { 255, 10 });
        await Assert.ThrowsAsync<DecoderFallbackException>(() => new BoundedLineReader(invalidUtf8).ReadAsync(default));
    }
}
