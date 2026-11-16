// Standalone DXGI presentation probe. No game hooks or capture. Escape closes it.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <d3d11.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <wrl/client.h>
#include <cstdio>
#include <stdexcept>
#include <string>
#include <vector>
using Microsoft::WRL::ComPtr;
static unsigned long long frames;
static void check(HRESULT hr) { if (FAILED(hr)) throw hr; }
static LRESULT CALLBACK windowProc(HWND h, UINT msg, WPARAM w, LPARAM l) {
    if (msg == WM_CLOSE || (msg == WM_KEYDOWN && w == VK_ESCAPE)) { DestroyWindow(h); return 0; }
    if (msg == WM_DESTROY) { PostQuitMessage(0); return 0; }
    if (msg == WM_APP + 100) return static_cast<LRESULT>(frames);
    // Explicit focus-change test: the foreground probe transfers activation to
    // another test window, avoiding background callers' foreground-lock race.
    if (msg == WM_APP + 101 && GetForegroundWindow() == h) return SetForegroundWindow(reinterpret_cast<HWND>(w));
    return DefWindowProcW(h, msg, w, l);
}
int wmain(int argc, wchar_t** argv) {
    int api = 12, seconds = 30, pauseAfter = 0; bool borderless = false;
    std::wstring output, ready;
    for (int i = 1; i < argc; ++i) {
        std::wstring arg = argv[i];
        if (arg == L"--api" && i + 1 < argc) api = _wtoi(argv[++i]);
        else if (arg == L"--seconds" && i + 1 < argc) seconds = _wtoi(argv[++i]);
        else if (arg == L"--pause-after" && i + 1 < argc) pauseAfter = _wtoi(argv[++i]);
        else if (arg == L"--borderless") borderless = true;
        else if (arg == L"--output" && i + 1 < argc) output = argv[++i];
        else if (arg == L"--ready" && i + 1 < argc) ready = argv[++i];
        else { fwprintf(stderr, L"Unknown or incomplete argument: %s\n", arg.c_str()); return 2; }
    }
    if ((api != 11 && api != 12) || seconds < 1) return 2;
    HWND window = nullptr; HANDLE fenceEvent = nullptr;
    try {
        HINSTANCE instance = GetModuleHandleW(nullptr);
        WNDCLASSW cls{}; cls.lpfnWndProc = windowProc; cls.hInstance = instance; cls.lpszClassName = L"LightCrosshairSwapchainProbe";
        RegisterClassW(&cls);
        RECT bounds{0, 0, 960, 540};
        if (borderless) { MONITORINFO info{sizeof(info)}; GetMonitorInfoW(MonitorFromPoint(POINT{}, MONITOR_DEFAULTTOPRIMARY), &info); bounds = info.rcMonitor; }
        window = CreateWindowW(cls.lpszClassName, api == 11 ? L"LightCrosshair DX11 probe" : L"LightCrosshair DX12 probe", borderless ? WS_POPUP : WS_OVERLAPPEDWINDOW,
            bounds.left, bounds.top, bounds.right - bounds.left, bounds.bottom - bounds.top, nullptr, nullptr, instance, nullptr);
        if (!window) throw HRESULT_FROM_WIN32(GetLastError());
        // The launcher hides the console. Its startup show flag may override the
        // first ShowWindow call; the interactive probe itself must be visible.
        ShowWindow(window, SW_SHOW); ShowWindow(window, SW_SHOW); SetForegroundWindow(window);
        RECT client{}; GetClientRect(window, &client);
        ComPtr<IDXGIFactory4> factory; check(CreateDXGIFactory1(IID_PPV_ARGS(&factory)));
        ComPtr<ID3D11Device> device11; ComPtr<ID3D11DeviceContext> context11; ComPtr<ID3D11RenderTargetView> target11;
        ComPtr<ID3D12Device> device12; ComPtr<ID3D12CommandQueue> queue; ComPtr<ID3D12CommandAllocator> allocator;
        ComPtr<ID3D12GraphicsCommandList> commands; ComPtr<ID3D12DescriptorHeap> heap; ComPtr<ID3D12Fence> fence;
        ComPtr<ID3D12Resource> targets[2]; UINT descriptorSize = 0; UINT64 fenceValue = 0;
        ComPtr<IDXGISwapChain1> initial; ComPtr<IDXGISwapChain3> swapchain;
        DXGI_SWAP_CHAIN_DESC1 desc{}; desc.Width = client.right; desc.Height = client.bottom; desc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        desc.SampleDesc.Count = 1; desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT; desc.BufferCount = 2; desc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
        if (api == 11) {
            check(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0, nullptr, 0, D3D11_SDK_VERSION, &device11, nullptr, &context11));
            check(factory->CreateSwapChainForHwnd(device11.Get(), window, &desc, nullptr, nullptr, &initial));
            ComPtr<ID3D11Texture2D> buffer; check(initial->GetBuffer(0, IID_PPV_ARGS(&buffer)));
            check(device11->CreateRenderTargetView(buffer.Get(), nullptr, &target11));
        } else {
            check(D3D12CreateDevice(nullptr, D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(&device12)));
            D3D12_COMMAND_QUEUE_DESC queueDesc{}; check(device12->CreateCommandQueue(&queueDesc, IID_PPV_ARGS(&queue)));
            check(factory->CreateSwapChainForHwnd(queue.Get(), window, &desc, nullptr, nullptr, &initial));
            D3D12_DESCRIPTOR_HEAP_DESC heapDesc{}; heapDesc.NumDescriptors = 2; heapDesc.Type = D3D12_DESCRIPTOR_HEAP_TYPE_RTV;
            check(device12->CreateDescriptorHeap(&heapDesc, IID_PPV_ARGS(&heap)));
            descriptorSize = device12->GetDescriptorHandleIncrementSize(D3D12_DESCRIPTOR_HEAP_TYPE_RTV);
            auto handle = heap->GetCPUDescriptorHandleForHeapStart();
            for (UINT i = 0; i < 2; ++i) { check(initial->GetBuffer(i, IID_PPV_ARGS(&targets[i]))); device12->CreateRenderTargetView(targets[i].Get(), nullptr, handle); handle.ptr += descriptorSize; }
            check(device12->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT, IID_PPV_ARGS(&allocator)));
            check(device12->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT, allocator.Get(), nullptr, IID_PPV_ARGS(&commands))); check(commands->Close());
            check(device12->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&fence)));
            fenceEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr); if (!fenceEvent) throw HRESULT_FROM_WIN32(GetLastError());
        }
        check(initial.As(&swapchain)); factory->MakeWindowAssociation(window, DXGI_MWA_NO_ALT_ENTER);
        LARGE_INTEGER frequency{}, start{}, last{}; QueryPerformanceFrequency(&frequency); QueryPerformanceCounter(&start); last = start;
        std::vector<double> intervals; intervals.reserve(static_cast<size_t>(seconds) * 1000);
        if (!ready.empty()) { FILE* file{}; if (_wfopen_s(&file, ready.c_str(), L"w") == 0) { fprintf(file, "%lu,%llu,%lld,%lld\n", GetCurrentProcessId(), reinterpret_cast<unsigned long long>(window), start.QuadPart, frequency.QuadPart); fclose(file); } }
        MSG msg{}; bool running = true;
        while (running) {
            while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) { if (msg.message == WM_QUIT) running = false; TranslateMessage(&msg); DispatchMessageW(&msg); }
            LARGE_INTEGER now{}; QueryPerformanceCounter(&now); double elapsed = static_cast<double>(now.QuadPart - start.QuadPart) / frequency.QuadPart;
            if (!running || elapsed >= seconds) break;
            if (pauseAfter > 0 && elapsed >= pauseAfter) { Sleep(10); continue; }
            float color[]{0.025f + static_cast<float>((frames % 120) / 1200.0), 0.055f, 0.085f, 1.0f};
            if (api == 11) context11->ClearRenderTargetView(target11.Get(), color);
            else {
                UINT index = swapchain->GetCurrentBackBufferIndex(); check(allocator->Reset()); check(commands->Reset(allocator.Get(), nullptr));
                D3D12_RESOURCE_BARRIER barrier{}; barrier.Type = D3D12_RESOURCE_BARRIER_TYPE_TRANSITION; barrier.Transition.pResource = targets[index].Get();
                barrier.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES; barrier.Transition.StateBefore = D3D12_RESOURCE_STATE_PRESENT; barrier.Transition.StateAfter = D3D12_RESOURCE_STATE_RENDER_TARGET;
                commands->ResourceBarrier(1, &barrier); auto handle = heap->GetCPUDescriptorHandleForHeapStart(); handle.ptr += index * descriptorSize;
                commands->ClearRenderTargetView(handle, color, 0, nullptr);
                std::swap(barrier.Transition.StateBefore, barrier.Transition.StateAfter); commands->ResourceBarrier(1, &barrier); check(commands->Close());
                ID3D12CommandList* lists[]{commands.Get()}; queue->ExecuteCommandLists(1, lists);
            }
            check(swapchain->Present(1, 0));
            if (api == 12) { check(queue->Signal(fence.Get(), ++fenceValue)); if (fence->GetCompletedValue() < fenceValue) { check(fence->SetEventOnCompletion(fenceValue, fenceEvent)); WaitForSingleObject(fenceEvent, INFINITE); } }
            QueryPerformanceCounter(&now); intervals.push_back(1000.0 * (now.QuadPart - last.QuadPart) / frequency.QuadPart); last = now; ++frames;
        }
        if (!output.empty()) {
            FILE* file{}; if (_wfopen_s(&file, output.c_str(), L"w") != 0) throw HRESULT_FROM_WIN32(ERROR_WRITE_FAULT);
            fprintf(file, "Frame,IntervalMs,Api,Borderless\n"); for (size_t i = 0; i < intervals.size(); ++i) fprintf(file, "%zu,%.6f,%d,%d\n", i + 1, intervals[i], api, borderless ? 1 : 0); fclose(file);
        }
        printf("DX%d %s: %llu frames\n", api, borderless ? "borderless" : "windowed", frames);
        if (fenceEvent) CloseHandle(fenceEvent); if (IsWindow(window)) DestroyWindow(window); return 0;
    } catch (HRESULT hr) {
        fprintf(stderr, "Swapchain probe failed: HRESULT 0x%08lx\n", static_cast<unsigned long>(hr));
        if (fenceEvent) CloseHandle(fenceEvent); if (IsWindow(window)) DestroyWindow(window); return 1;
    }
}
