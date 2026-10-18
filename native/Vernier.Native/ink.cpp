// The pen's ink: drawn here, on a thread of its own, straight into a DirectComposition surface.
// The learner's stroke never waits for WPF: the cursor is read directly (a 1 ms poll while the button is down), each new
// segment is rasterised into a small dirty rectangle and handed to DWM, and the whole stroke fades away by itself shortly
// after the pen lifts. The finished stroke is reported back (absolute physical pixels) so it can be sent with a question.
#include "common.h"
#include <cmath>
#include <d3d11.h>
#include <d2d1_1.h>
#include <dcomp.h>
#include <dwmapi.h>
#include <timeapi.h>

namespace
{
    struct Ink
    {
        RECT monitor{};
        uint32_t argb = 0xFFFFD84D;
        int width = 4;                       // core stroke width in pixels
        std::atomic<int> holdMs{ 1400 }, fadeMs{ 350 };
        std::atomic<bool> quit{ false };
        std::atomic<int> request{ 0 };       // 1 start, 2 stop, 3 clear now
        std::atomic<bool> watchButton{ true };
        std::mutex feedLock;
        std::vector<int32_t> feed;           // a stroke supplied by the caller (tests, replays): absolute physical x,y pairs
        std::atomic<HWND> hwnd{ nullptr };
        std::atomic<int64_t> segments{ 0 };
        std::atomic<int32_t> lastError{ 0 };
        vn_ink_cb callback = nullptr;
        void* context = nullptr;
        HANDLE wake = CreateEventW(nullptr, FALSE, FALSE, nullptr);
        HANDLE ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        std::atomic<int32_t> startResult{ 1 };
        std::thread thread;
        ~Ink() { CloseHandle(wake); CloseHandle(ready); }
    };

    LRESULT CALLBACK WndProc(HWND h, UINT m, WPARAM w, LPARAM l)
    {
        if (m == WM_NCHITTEST) return HTTRANSPARENT;
        if (m == WM_MOUSEACTIVATE) return MA_NOACTIVATE;
        return DefWindowProcW(h, m, w, l);
    }

    inline double Now()
    {
        static LARGE_INTEGER f = [] { LARGE_INTEGER x; QueryPerformanceFrequency(&x); return x; }();
        LARGE_INTEGER t; QueryPerformanceCounter(&t);
        return double(t.QuadPart) / double(f.QuadPart);
    }

    // Distance from (px,py) to the segment (ax,ay)-(bx,by).
    inline float SegDist(float px, float py, float ax, float ay, float bx, float by)
    {
        const float dx = bx - ax, dy = by - ay;
        const float len2 = dx * dx + dy * dy;
        float t = len2 > 0 ? ((px - ax) * dx + (py - ay) * dy) / len2 : 0.0f;
        t = std::clamp(t, 0.0f, 1.0f);
        const float cx = ax + t * dx - px, cy = ay + t * dy - py;
        return std::sqrt(cx * cx + cy * cy);
    }

    int32_t Run(Ink& k)
    {
        CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        timeBeginPeriod(1); // a 1 ms sleep really is about 1 ms
        WNDCLASSW wc{};
        wc.lpfnWndProc = WndProc;
        wc.hInstance = GetModuleHandleW(nullptr);
        wc.lpszClassName = L"VernierInkLayer";
        RegisterClassW(&wc);
        const int W = k.monitor.right - k.monitor.left, H = k.monitor.bottom - k.monitor.top;
        HWND hwnd = CreateWindowExW(WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED,
            wc.lpszClassName, L"Vernier ink", WS_POPUP, k.monitor.left, k.monitor.top, W, H, nullptr, nullptr, wc.hInstance, nullptr);
        if (!hwnd) return int32_t(HRESULT_FROM_WIN32(GetLastError()));
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
        k.hwnd = hwnd;

        ComPtr<ID3D11Device> d3d;
        RETURN_IF_FAILED(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION, &d3d, nullptr, nullptr));
        ComPtr<IDXGIDevice> dxgi;
        RETURN_IF_FAILED(d3d.As(&dxgi));
        ComPtr<ID2D1Factory1> factory;
        RETURN_IF_FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof(ID2D1Factory1), reinterpret_cast<void**>(factory.GetAddressOf())));
        ComPtr<ID2D1Device> d2d;
        RETURN_IF_FAILED(factory->CreateDevice(dxgi.Get(), &d2d));
        ComPtr<IDCompositionDesktopDevice> dcomp;
        RETURN_IF_FAILED(DCompositionCreateDevice2(d2d.Get(), IID_PPV_ARGS(&dcomp)));
        ComPtr<IDCompositionTarget> target;
        RETURN_IF_FAILED(dcomp->CreateTargetForHwnd(hwnd, TRUE, &target));
        ComPtr<IDCompositionVisual2> visual;
        RETURN_IF_FAILED(dcomp->CreateVisual(&visual));
        RETURN_IF_FAILED(target->SetRoot(visual.Get()));
        ComPtr<IDCompositionVisual3> visual3;
        visual.As(&visual3); // opacity for the fade; without it the ink just disappears
        RETURN_IF_FAILED(dcomp->Commit());
        k.startResult = VN_OK;
        SetEvent(k.ready);

        const float R = float((k.argb >> 16) & 0xFF), G = float((k.argb >> 8) & 0xFF), B = float(k.argb & 0xFF);
        const float radius = std::max(1.0f, k.width * 0.5f), halo = 1.8f;
        std::vector<uint32_t> pixels;           // premultiplied BGRA mirror of the surface
        std::vector<uint8_t> core, shade;       // coverage of the stroke and of its dark halo (max-merged so joints stay clean)
        // The layer is a grid of small tiles, each its own surface, created when first touched and drawn whole: a stroke
        // only ever updates the one or two tiles under it.
        constexpr int Tile = 256;
        const int tilesX = (W + Tile - 1) / Tile, tilesY = (H + Tile - 1) / Tile;
        struct TileSurface { ComPtr<IDCompositionVisual2> visual; ComPtr<IDCompositionSurface> surface; ComPtr<ID2D1Bitmap1> bitmap; bool dirty = false; };
        std::vector<TileSurface> tiles(size_t(tilesX) * tilesY);
        bool shown = false, drawing = false, hasInk = false, surfaceDirty = false;
        double lastEnd = 0;
        float lastX = 0, lastY = 0;
        std::vector<int32_t> stroke;            // absolute physical x,y pairs of the stroke in progress

        auto upload = [&](RECT r)
        {
            r.left = std::max<LONG>(0, r.left); r.top = std::max<LONG>(0, r.top);
            r.right = std::min<LONG>(W, r.right); r.bottom = std::min<LONG>(H, r.bottom);
            if (r.right <= r.left || r.bottom <= r.top) return;
            for (int ty = r.top / Tile; ty <= (r.bottom - 1) / Tile; ty++)
                for (int tx = r.left / Tile; tx <= (r.right - 1) / Tile; tx++)
                {
                    auto& t = tiles[size_t(ty) * tilesX + tx];
                    const int x0 = tx * Tile, y0 = ty * Tile, tw = std::min(Tile, W - x0), th = std::min(Tile, H - y0);
                    if (!t.surface)
                    {
                        HRESULT hr = dcomp->CreateVisual(&t.visual);
                        if (SUCCEEDED(hr)) hr = dcomp->CreateSurface(UINT(tw), UINT(th), DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_ALPHA_MODE_PREMULTIPLIED, &t.surface);
                        if (SUCCEEDED(hr)) hr = t.visual->SetContent(t.surface.Get());
                        if (SUCCEEDED(hr)) hr = t.visual->SetOffsetX(float(x0));
                        if (SUCCEEDED(hr)) hr = t.visual->SetOffsetY(float(y0));
                        if (SUCCEEDED(hr)) hr = visual->AddVisual(t.visual.Get(), FALSE, nullptr);
                        if (FAILED(hr)) { k.lastError = int32_t(hr); t.surface.Reset(); continue; }
                    }
                    ComPtr<ID2D1DeviceContext> dc;
                    POINT offset{};
                    const HRESULT begun = t.surface->BeginDraw(nullptr, IID_PPV_ARGS(&dc), &offset);
                    if (FAILED(begun)) { k.lastError = int32_t(begun); continue; }
                    if (!t.bitmap)
                    {
                        D2D1_BITMAP_PROPERTIES1 bp = D2D1::BitmapProperties1(D2D1_BITMAP_OPTIONS_NONE, D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED));
                        dc->CreateBitmap(D2D1::SizeU(UINT(tw), UINT(th)), nullptr, 0, bp, &t.bitmap);
                    }
                    if (t.bitmap)
                    {
                        t.bitmap->CopyFromMemory(nullptr, pixels.data() + size_t(y0) * W + x0, UINT(W) * 4);
                        dc->SetTransform(D2D1::Matrix3x2F::Translation(float(offset.x), float(offset.y)));
                        dc->Clear(D2D1::ColorF(0, 0, 0, 0));
                        dc->DrawBitmap(t.bitmap.Get());
                    }
                    const HRESULT ended = t.surface->EndDraw();
                    if (FAILED(ended)) k.lastError = int32_t(ended);
                    t.dirty = true;
                }
        };

        auto segment = [&](float ax, float ay, float bx, float by)
        {
            const float pad = radius + halo + 2.0f;
            RECT r{ LONG(std::floor(std::min(ax, bx) - pad)), LONG(std::floor(std::min(ay, by) - pad)),
                    LONG(std::ceil(std::max(ax, bx) + pad)), LONG(std::ceil(std::max(ay, by) + pad)) };
            r.left = std::max<LONG>(0, r.left); r.top = std::max<LONG>(0, r.top);
            r.right = std::min<LONG>(W, r.right); r.bottom = std::min<LONG>(H, r.bottom);
            if (r.right <= r.left || r.bottom <= r.top) return;
            for (LONG y = r.top; y < r.bottom; y++)
                for (LONG x = r.left; x < r.right; x++)
                {
                    const float d = SegDist(float(x) + 0.5f, float(y) + 0.5f, ax, ay, bx, by);
                    const size_t i = size_t(y) * W + x;
                    const uint8_t c = uint8_t(std::clamp(radius + 0.5f - d, 0.0f, 1.0f) * 255.0f);
                    const uint8_t s = uint8_t(std::clamp(radius + halo + 0.5f - d, 0.0f, 1.0f) * 255.0f);
                    if (c > core[i]) core[i] = c;
                    if (s > shade[i]) shade[i] = s;
                    const float ca = core[i] / 255.0f, sa = shade[i] / 255.0f * 0.55f;
                    const float a = ca + sa * (1.0f - ca);
                    pixels[i] = (uint32_t(a * 255.0f + 0.5f) << 24) | (uint32_t(R * ca + 0.5f) << 16) | (uint32_t(G * ca + 0.5f) << 8) | uint32_t(B * ca + 0.5f); // premultiplied: the halo is black
                }
            upload(r);
            surfaceDirty = true;
            hasInk = true;
        };

        auto clearNow = [&]
        {
            for (auto& t : tiles)
            {
                if (!t.dirty || !t.surface) continue;
                ComPtr<ID2D1DeviceContext> dc;
                POINT offset{};
                if (SUCCEEDED(t.surface->BeginDraw(nullptr, IID_PPV_ARGS(&dc), &offset))) { dc->Clear(D2D1::ColorF(0, 0, 0, 0)); t.surface->EndDraw(); }
                t.dirty = false;
            }
            std::vector<uint32_t>().swap(pixels);
            std::vector<uint8_t>().swap(core);
            std::vector<uint8_t>().swap(shade);
            if (visual3) visual3->SetOpacity(1.0f);
            dcomp->Commit();
            if (shown) { ShowWindow(hwnd, SW_HIDE); shown = false; }
            surfaceDirty = hasInk = false;
        };

        auto finishStroke = [&]
        {
            drawing = false;
            lastEnd = Now();
            if (stroke.size() >= 6 && k.callback) k.callback(stroke.data(), int32_t(stroke.size() / 2), k.context);
            stroke.clear();
        };

        while (!k.quit)
        {
            MSG msg;
            while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) { TranslateMessage(&msg); DispatchMessageW(&msg); }
            const int req = k.request.exchange(0);
            auto prepare = [&]
            {
                if (pixels.empty()) { pixels.assign(size_t(W) * H, 0); core.assign(size_t(W) * H, 0); shade.assign(size_t(W) * H, 0); }
                if (visual3) { visual3->SetOpacity(1.0f); dcomp->Commit(); }
                if (!shown) { ShowWindow(hwnd, SW_SHOWNOACTIVATE); SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE); shown = true; }
            };
            if (req == 3) { if (drawing) { drawing = false; stroke.clear(); } clearNow(); }
            if (req == 4 && !drawing)
            {
                std::vector<int32_t> pts;
                { std::lock_guard<std::mutex> lock(k.feedLock); pts.swap(k.feed); }
                if (pts.size() >= 4)
                {
                    prepare();
                    lastX = float(pts[0] - k.monitor.left); lastY = float(pts[1] - k.monitor.top);
                    stroke.assign(pts.begin(), pts.begin() + 2);
                    segment(lastX, lastY, lastX, lastY);
                    for (size_t i = 2; i + 1 < pts.size(); i += 2)
                    {
                        const float x = float(pts[i] - k.monitor.left), y = float(pts[i + 1] - k.monitor.top);
                        segment(lastX, lastY, x, y);
                        lastX = x; lastY = y;
                        stroke.push_back(pts[i]); stroke.push_back(pts[i + 1]);
                        k.segments++;
                    }
                    dcomp->Commit();
                    finishStroke();
                }
            }
            if (req == 1 && !drawing)
            {
                prepare();
                drawing = true;
                stroke.clear();
                POINT p{};
                GetCursorPos(&p);
                lastX = float(p.x - k.monitor.left); lastY = float(p.y - k.monitor.top);
                stroke.push_back(p.x); stroke.push_back(p.y);
                segment(lastX, lastY, lastX, lastY); // a dot, so a tap is visible at once
            }
            if (req == 2 && drawing) finishStroke();

            if (drawing)
            {
                const int vk = GetSystemMetrics(SM_SWAPBUTTON) ? VK_RBUTTON : VK_LBUTTON;
                if (k.watchButton && !(GetAsyncKeyState(vk) & 0x8000)) { finishStroke(); continue; }
                POINT p{};
                GetCursorPos(&p);
                const float x = float(p.x - k.monitor.left), y = float(p.y - k.monitor.top);
                if (std::fabs(x - lastX) + std::fabs(y - lastY) >= 1.0f && stroke.size() < 16000)
                {
                    segment(lastX, lastY, x, y);
                    lastX = x; lastY = y;
                    stroke.push_back(p.x); stroke.push_back(p.y);
                    k.segments++;
                    dcomp->Commit();
                }
                Sleep(1);
                continue;
            }

            if (hasInk)
            {
                // Hold the finished ink briefly (so a circle can be seen to be a circle), then fade it away.
                const double since = (Now() - lastEnd) * 1000.0;
                const double hold = k.holdMs.load(), fade = std::max(1, k.fadeMs.load());
                if (since >= hold + fade) { clearNow(); continue; }
                if (since >= hold && visual3)
                {
                    float o = float(1.0 - (since - hold) / fade);
                    visual3->SetOpacity(o * o);
                    dcomp->Commit();
                    DwmFlush();
                    continue;
                }
                WaitForSingleObject(k.wake, DWORD(std::max(1.0, std::min(50.0, hold - since))));
                continue;
            }
            MsgWaitForMultipleObjects(1, &k.wake, FALSE, INFINITE, QS_ALLINPUT);
        }
        DestroyWindow(hwnd);
        timeEndPeriod(1);
        CoUninitialize();
        return VN_OK;
    }
}

VN_API int32_t vn_ink_create(vn_rect monitor, uint32_t argb, int32_t width_px, vn_ink_cb callback, void* context, void** handle)
{
    if (!handle || monitor.right <= monitor.left || monitor.bottom <= monitor.top) return VN_E_ARG;
    auto k = std::make_unique<Ink>();
    k->monitor = { monitor.left, monitor.top, monitor.right, monitor.bottom };
    k->argb = argb;
    k->width = std::clamp(width_px, 2, 16);
    k->callback = callback;
    k->context = context;
    Ink* raw = k.get();
    raw->thread = std::thread([raw] { int32_t r = Run(*raw); if (r != VN_OK) { raw->startResult = r; SetEvent(raw->ready); } });
    WaitForSingleObject(raw->ready, 5000);
    if (raw->startResult != VN_OK)
    {
        int32_t r = raw->startResult == 1 ? int32_t(E_FAIL) : raw->startResult.load();
        raw->quit = true;
        SetEvent(raw->wake);
        if (raw->thread.joinable()) raw->thread.join();
        return r;
    }
    *handle = k.release();
    return VN_OK;
}

// Starts following the cursor. With watch_button the stroke ends by itself when the primary button is released
// (mouse); for pen and touch, which do not move that button, the caller ends it with vn_ink_stop.
VN_API int32_t vn_ink_start(void* handle, int32_t watch_button)
{
    if (!handle) return VN_E_ARG;
    auto& k = *static_cast<Ink*>(handle);
    k.watchButton = watch_button != 0;
    k.request = 1;
    SetEvent(k.wake);
    return VN_OK;
}

// Draws a stroke from supplied points (absolute physical x,y pairs, count points) as if the cursor had made it.
VN_API int32_t vn_ink_feed(void* handle, const int32_t* xy, int32_t count)
{
    if (!handle || !xy || count < 2 || count > 8000) return VN_E_ARG;
    auto& k = *static_cast<Ink*>(handle);
    { std::lock_guard<std::mutex> lock(k.feedLock); k.feed.assign(xy, xy + size_t(count) * 2); }
    k.request = 4;
    SetEvent(k.wake);
    return VN_OK;
}

VN_API int32_t vn_ink_stop(void* handle)
{
    if (!handle) return VN_E_ARG;
    auto& k = *static_cast<Ink*>(handle);
    k.request = 2;
    SetEvent(k.wake);
    return VN_OK;
}

VN_API int32_t vn_ink_clear(void* handle)
{
    if (!handle) return VN_E_ARG;
    auto& k = *static_cast<Ink*>(handle);
    k.request = 3;
    SetEvent(k.wake);
    return VN_OK;
}

VN_API int32_t vn_ink_timing(void* handle, int32_t hold_ms, int32_t fade_ms)
{
    if (!handle) return VN_E_ARG;
    auto& k = *static_cast<Ink*>(handle);
    k.holdMs = std::clamp(hold_ms, 0, 60000);
    k.fadeMs = std::clamp(fade_ms, 1, 5000);
    return VN_OK;
}

VN_API void* vn_ink_hwnd(void* handle) { return handle ? static_cast<Ink*>(handle)->hwnd.load() : nullptr; }
VN_API int32_t vn_ink_error(void* handle) { return handle ? static_cast<Ink*>(handle)->lastError.load() : 0; }
VN_API int64_t vn_ink_segments(void* handle) { return handle ? static_cast<Ink*>(handle)->segments.load() : 0; }

VN_API void vn_ink_destroy(void* handle)
{
    auto* k = static_cast<Ink*>(handle);
    if (!k) return;
    k->quit = true;
    SetEvent(k->wake);
    if (HWND h = k->hwnd.load()) PostMessageW(h, WM_NULL, 0, 0);
    if (k->thread.joinable()) k->thread.join();
    delete k;
}
