// Desktop Duplication: the OS hands us each composed frame on the GPU with the rectangles that changed.
// The frame stays on the GPU (one CopyResource per frame); it is read back to the CPU only when asked for.
#include "common.h"
#include <d3d11.h>
#include <dxgi1_2.h>

namespace
{
    struct Duplication
    {
        ComPtr<ID3D11Device> device;
        ComPtr<ID3D11DeviceContext> context;
        ComPtr<IDXGIOutputDuplication> dup;
        ComPtr<ID3D11Texture2D> latest;   // GPU copy of the last frame with new pixels
        ComPtr<ID3D11Texture2D> staging;  // CPU-readable copy, refreshed lazily
        bool stagingStale = true;
        bool haveFrame = false;
        int width = 0, height = 0;
        std::vector<uint8_t> meta;
        std::mutex lock;
    };

    // Union area of rectangles on a coarse 64x64 grid: exact enough for "how much changed", and overlaps count once.
    double CoveredFraction(const std::vector<RECT>& rects, int w, int h)
    {
        if (rects.empty() || w <= 0 || h <= 0) return 0;
        constexpr int G = 64;
        bool grid[G][G] = {};
        for (const auto& r : rects)
        {
            int x0 = std::clamp(int(int64_t(r.left) * G / w), 0, G - 1), x1 = std::clamp(int((int64_t(r.right) * G + w - 1) / w), 1, G);
            int y0 = std::clamp(int(int64_t(r.top) * G / h), 0, G - 1), y1 = std::clamp(int((int64_t(r.bottom) * G + h - 1) / h), 1, G);
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) grid[y][x] = true;
        }
        int n = 0;
        for (auto& row : grid) for (bool b : row) n += b;
        return double(n) / (G * G);
    }

    HRESULT EnsureStaging(Duplication& d)
    {
        if (!d.haveFrame) return VN_E_NO_FRAME;
        if (!d.staging)
        {
            D3D11_TEXTURE2D_DESC desc{};
            d.latest->GetDesc(&desc);
            desc.Usage = D3D11_USAGE_STAGING;
            desc.BindFlags = 0;
            desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
            desc.MiscFlags = 0;
            RETURN_IF_FAILED(d.device->CreateTexture2D(&desc, nullptr, &d.staging));
        }
        if (d.stagingStale)
        {
            d.context->CopyResource(d.staging.Get(), d.latest.Get());
            d.stagingStale = false;
        }
        return S_OK;
    }
}

VN_API int32_t vn_dup_open(int32_t left, int32_t top, void** handle)
{
    if (!handle) return VN_E_ARG;
    *handle = nullptr;
    ComPtr<IDXGIFactory1> factory;
    RETURN_IF_FAILED(CreateDXGIFactory1(IID_PPV_ARGS(&factory)));
    // The device must live on the adapter that drives this output (hybrid-GPU laptops have several).
    ComPtr<IDXGIAdapter1> adapter;
    for (UINT a = 0; factory->EnumAdapters1(a, &adapter) != DXGI_ERROR_NOT_FOUND; a++)
    {
        ComPtr<IDXGIOutput> output;
        for (UINT o = 0; adapter->EnumOutputs(o, &output) != DXGI_ERROR_NOT_FOUND; o++)
        {
            DXGI_OUTPUT_DESC od{};
            output->GetDesc(&od);
            if (od.DesktopCoordinates.left != left || od.DesktopCoordinates.top != top) { output.Reset(); continue; }
            auto d = std::make_unique<Duplication>();
            D3D_FEATURE_LEVEL levels[] = { D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_1, D3D_FEATURE_LEVEL_10_0 };
            RETURN_IF_FAILED(D3D11CreateDevice(adapter.Get(), D3D_DRIVER_TYPE_UNKNOWN, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                levels, ARRAYSIZE(levels), D3D11_SDK_VERSION, &d->device, nullptr, &d->context));
            ComPtr<IDXGIOutput1> output1;
            RETURN_IF_FAILED(output.As(&output1));
            HRESULT hr = output1->DuplicateOutput(d->device.Get(), &d->dup);
            if (hr == DXGI_ERROR_UNSUPPORTED || hr == DXGI_ERROR_NOT_CURRENTLY_AVAILABLE || hr == E_ACCESSDENIED) return VN_E_UNSUPPORTED;
            RETURN_IF_FAILED(hr);
            DXGI_OUTDUPL_DESC dd{};
            d->dup->GetDesc(&dd);
            d->width = int(dd.ModeDesc.Width);
            d->height = int(dd.ModeDesc.Height);
            *handle = d.release();
            return VN_OK;
        }
    }
    return VN_E_NO_OUTPUT;
}

VN_API int32_t vn_dup_next(void* handle, int32_t timeout_ms, vn_frame_info* info)
{
    if (!handle || !info) return VN_E_ARG;
    auto& d = *static_cast<Duplication*>(handle);
    std::lock_guard guard(d.lock);
    DXGI_OUTDUPL_FRAME_INFO fi{};
    ComPtr<IDXGIResource> resource;
    HRESULT hr = d.dup->AcquireNextFrame(UINT(std::max(0, timeout_ms)), &fi, &resource);
    if (hr == DXGI_ERROR_WAIT_TIMEOUT) return VN_TIMEOUT;
    if (hr == DXGI_ERROR_ACCESS_LOST || hr == DXGI_ERROR_INVALID_CALL) return VN_E_ACCESS_LOST;
    RETURN_IF_FAILED(hr);
    struct Release { IDXGIOutputDuplication* dup; ~Release() { dup->ReleaseFrame(); } } release{ d.dup.Get() };

    // The pointer moved; the picture did not. The first frame is kept anyway: its surface already holds the desktop.
    if (fi.LastPresentTime.QuadPart == 0 && d.haveFrame) return VN_TIMEOUT;

    std::vector<RECT> changed;
    if (fi.TotalMetadataBufferSize > 0)
    {
        d.meta.resize(fi.TotalMetadataBufferSize);
        UINT used = 0;
        if (SUCCEEDED(d.dup->GetFrameMoveRects(UINT(d.meta.size()), reinterpret_cast<DXGI_OUTDUPL_MOVE_RECT*>(d.meta.data()), &used)))
            for (UINT i = 0; i < used / sizeof(DXGI_OUTDUPL_MOVE_RECT); i++) changed.push_back(reinterpret_cast<DXGI_OUTDUPL_MOVE_RECT*>(d.meta.data())[i].DestinationRect);
        used = 0;
        if (SUCCEEDED(d.dup->GetFrameDirtyRects(UINT(d.meta.size()), reinterpret_cast<RECT*>(d.meta.data()), &used)))
            for (UINT i = 0; i < used / sizeof(RECT); i++) changed.push_back(reinterpret_cast<RECT*>(d.meta.data())[i]);
    }

    ComPtr<ID3D11Texture2D> frame;
    RETURN_IF_FAILED(resource.As(&frame));
    if (!d.latest)
    {
        D3D11_TEXTURE2D_DESC desc{};
        frame->GetDesc(&desc);
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = 0;
        desc.CPUAccessFlags = 0;
        desc.MiscFlags = 0;
        RETURN_IF_FAILED(d.device->CreateTexture2D(&desc, nullptr, &d.latest));
    }
    d.context->CopyResource(d.latest.Get(), frame.Get()); // GPU to GPU; the CPU copy happens only on demand
    d.stagingStale = true;
    d.haveFrame = true;

    *info = {};
    info->width = d.width;
    info->height = d.height;
    info->dirty_count = int32_t(changed.size());
    info->accumulated = int32_t(fi.AccumulatedFrames);
    info->present_qpc = fi.LastPresentTime.QuadPart;
    if (!changed.empty())
    {
        RECT u = changed[0];
        for (auto& r : changed) { u.left = std::min(u.left, r.left); u.top = std::min(u.top, r.top); u.right = std::max(u.right, r.right); u.bottom = std::max(u.bottom, r.bottom); }
        info->dirty_bounds = { u.left, u.top, u.right, u.bottom };
        info->dirty_fraction = CoveredFraction(changed, d.width, d.height);
    }
    return VN_OK;
}

VN_API int32_t vn_dup_copy(void* handle, vn_rect region, uint8_t* dst, int32_t stride)
{
    if (!handle || !dst) return VN_E_ARG;
    auto& d = *static_cast<Duplication*>(handle);
    std::lock_guard guard(d.lock);
    if (region.left < 0 || region.top < 0 || region.right > d.width || region.bottom > d.height || region.right <= region.left || region.bottom <= region.top) return VN_E_ARG;
    if (stride < (region.right - region.left) * 4) return VN_E_ARG;
    RETURN_IF_FAILED(EnsureStaging(d));
    D3D11_MAPPED_SUBRESOURCE mapped{};
    RETURN_IF_FAILED(d.context->Map(d.staging.Get(), 0, D3D11_MAP_READ, 0, &mapped));
    const size_t rowBytes = size_t(region.right - region.left) * 4;
    for (int y = region.top; y < region.bottom; y++)
        memcpy(dst + size_t(y - region.top) * stride, static_cast<const uint8_t*>(mapped.pData) + size_t(y) * mapped.RowPitch + size_t(region.left) * 4, rowBytes);
    d.context->Unmap(d.staging.Get(), 0);
    return VN_OK;
}

VN_API int32_t vn_dup_thumb(void* handle, vn_rect region, int32_t w, int32_t h, uint8_t* gray)
{
    if (!handle || !gray || w <= 0 || h <= 0) return VN_E_ARG;
    auto& d = *static_cast<Duplication*>(handle);
    std::lock_guard guard(d.lock);
    if (region.right <= region.left || region.bottom <= region.top) region = { 0, 0, d.width, d.height };
    region.left = std::clamp(region.left, 0, d.width); region.right = std::clamp(region.right, region.left + 1, d.width);
    region.top = std::clamp(region.top, 0, d.height); region.bottom = std::clamp(region.bottom, region.top + 1, d.height);
    RETURN_IF_FAILED(EnsureStaging(d));
    D3D11_MAPPED_SUBRESOURCE mapped{};
    RETURN_IF_FAILED(d.context->Map(d.staging.Get(), 0, D3D11_MAP_READ, 0, &mapped));
    const int rw = region.right - region.left, rh = region.bottom - region.top;
    // Box filter: each thumbnail cell is the mean luma (Rec. 601, integer) of the pixels it covers; sampled every 2nd pixel on large cells.
    for (int ty = 0; ty < h; ty++)
    {
        int y0 = region.top + int(int64_t(ty) * rh / h), y1 = std::max(y0 + 1, region.top + int(int64_t(ty + 1) * rh / h));
        for (int tx = 0; tx < w; tx++)
        {
            int x0 = region.left + int(int64_t(tx) * rw / w), x1 = std::max(x0 + 1, region.left + int(int64_t(tx + 1) * rw / w));
            int step = (x1 - x0) > 8 ? 2 : 1;
            uint64_t sum = 0, n = 0;
            for (int y = y0; y < y1; y += step)
            {
                const uint8_t* row = static_cast<const uint8_t*>(mapped.pData) + size_t(y) * mapped.RowPitch;
                for (int x = x0; x < x1; x += step) { const uint8_t* p = row + size_t(x) * 4; sum += (p[2] * 77u + p[1] * 150u + p[0] * 29u) >> 8; n++; }
            }
            gray[ty * w + tx] = uint8_t(n ? sum / n : 0);
        }
    }
    d.context->Unmap(d.staging.Get(), 0);
    return VN_OK;
}

VN_API void vn_dup_close(void* handle)
{
    delete static_cast<Duplication*>(handle);
}
