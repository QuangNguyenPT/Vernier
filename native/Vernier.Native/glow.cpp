// The capture light: a click-through window round one monitor whose edges are DirectComposition surfaces.
// Pixels are computed on this module's own thread and presented at the compositor's rate (DwmFlush), so the wave
// keeps moving smoothly however busy the app's UI thread is. When off, the window is hidden and the thread sleeps.
#include "common.h"
#include <cmath>
#include <d3d11.h>
#include <d2d1_1.h>
#include <dcomp.h>
#include <dwmapi.h>

namespace
{
    constexpr double Pi = 3.14159265358979323846;

    struct Edge
    {
        ComPtr<IDCompositionVisual2> visual;
        ComPtr<IDCompositionSurface> surface;
        ComPtr<ID2D1Bitmap1> bitmap;
        int x = 0, y = 0, w = 0, h = 0;
        std::vector<uint32_t> pixels;
    };

    struct Glow
    {
        RECT monitor{};
        int thickness = 12;
        uint32_t argb = 0xFF6CC4FF;
        std::atomic<int> mode{ 0 };
        std::atomic<int> reduced{ 0 };
        std::atomic<bool> quit{ false };
        std::atomic<int64_t> frames{ 0 };
        std::atomic<HWND> hwnd{ nullptr };
        HANDLE wake = CreateEventW(nullptr, FALSE, FALSE, nullptr);
        HANDLE ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
        std::atomic<int32_t> startResult{ 1 };
        std::thread thread;
        ~Glow() { CloseHandle(wake); CloseHandle(ready); }
    };

    LRESULT CALLBACK WndProc(HWND h, UINT m, WPARAM w, LPARAM l)
    {
        if (m == WM_NCHITTEST) return HTTRANSPARENT; // never takes the mouse (WS_EX_TRANSPARENT does it across processes)
        if (m == WM_MOUSEACTIVATE) return MA_NOACTIVATE;
        return DefWindowProcW(h, m, w, l);
    }

    // Cheap per-pixel hash for dither: half a level of noise breaks the 8-bit steps that read as bands.
    inline float Dither(uint32_t x, uint32_t y, uint32_t t)
    {
        uint32_t n = x * 374761393u + y * 668265263u + t * 2246822519u;
        n = (n ^ (n >> 13)) * 1274126177u;
        return float((n ^ (n >> 16)) & 0xFFFF) / 65535.0f - 0.5f;
    }

    // Light intensity at perimeter position s (pixels from the top-left corner, clockwise), time t seconds.
    float Intensity(int mode, bool reduced, double s, double perimeter, double t, double width)
    {
        if (mode == 1) return 0.55f;
        if (mode == 3)
        {
            // Keeping context: a short soft pill at the top centre, steady. Visible, never in the way.
            const double half = 34, d = std::fabs(s - width / 2);
            return d > half ? 0.0f : float(0.7 * (1 - std::pow(d / half, 4)));
        }
        if (reduced) return float(0.45 + 0.15 * std::sin(2 * Pi * t / 3.2)); // a slow breath instead of travel
        const double period = 2.6;                                          // seconds per lap
        const double sigma = perimeter * 0.075;
        double crest = std::fmod(t / period, 1.0) * perimeter;
        auto around = [&](double c) { double d = std::fabs(s - c); d = std::min(d, perimeter - d); return std::exp(-(d * d) / (sigma * sigma)); };
        double wave = around(crest) + 0.55 * around(std::fmod(crest + perimeter / 2, perimeter)); // a leading crest and a softer follower
        double shimmer = 0.07 * std::sin(2 * Pi * (s / 220.0 - t * 1.4));
        return float(std::clamp(0.16 + 0.84 * wave + shimmer, 0.0, 1.0));
    }

    void Paint(Glow& g, Edge& e, int side, double t, uint32_t frame)
    {
        const int W = g.monitor.right - g.monitor.left, H = g.monitor.bottom - g.monitor.top;
        const double P = 2.0 * (W + H);
        const float r = float((g.argb >> 16) & 0xFF), gg = float((g.argb >> 8) & 0xFF), b = float(g.argb & 0xFF);
        const int mode = g.mode.load();
        const bool reduced = g.reduced.load() != 0;
        // Intensity is computed once per position along the edge, then shaped by distance from the screen edge.
        const int along = (side == 0 || side == 2) ? e.w : e.h;
        const int across = (side == 0 || side == 2) ? e.h : e.w;
        std::vector<float> level(along);
        for (int i = 0; i < along; i++)
        {
            double s = side == 0 ? e.x + i : side == 1 ? W + (e.y + i) : side == 2 ? W + H + (W - (e.x + i)) : 2.0 * W + H + (H - (e.y + i));
            level[i] = Intensity(mode, reduced, s, P, t, W);
        }
        for (int py = 0; py < e.h; py++)
            for (int px = 0; px < e.w; px++)
            {
                int i = (side == 0 || side == 2) ? px : py;
                int d = side == 0 ? py : side == 2 ? e.h - 1 - py : side == 3 ? px : e.w - 1 - px; // 0 at the screen edge
                float fall = 1.0f - float(d) / float(across);
                fall = fall * fall * (3 - 2 * fall);                                               // smoothstep: bright at the edge, gone inward
                float a = std::clamp(level[i] * fall, 0.0f, 1.0f);
                float core = std::clamp((level[i] - 0.7f) * 2.0f, 0.0f, 1.0f) * fall;            // the crest whitens a little
                float n = Dither(uint32_t(px + e.x), uint32_t(py + e.y), frame);
                auto ch = [&](float c) { float v = (c + (255 - c) * core * 0.45f) * a + n; return uint32_t(std::clamp(v, 0.0f, 255.0f)); };
                uint32_t A = uint32_t(std::clamp(a * 255.0f + n, 0.0f, 255.0f));
                e.pixels[size_t(py) * e.w + px] = (A << 24) | (std::min(ch(r), A) << 16) | (std::min(ch(gg), A) << 8) | std::min(ch(b), A); // premultiplied
            }
    }

    int32_t Run(Glow& g)
    {
        CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        WNDCLASSW wc{};
        wc.lpfnWndProc = WndProc;
        wc.hInstance = GetModuleHandleW(nullptr);
        wc.lpszClassName = L"VernierEdgeLight";
        RegisterClassW(&wc); // a second registration fails harmlessly
        const int W = g.monitor.right - g.monitor.left, H = g.monitor.bottom - g.monitor.top;
        HWND hwnd = CreateWindowExW(WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED,
            wc.lpszClassName, L"Vernier capture light", WS_POPUP, g.monitor.left, g.monitor.top, W, H, nullptr, nullptr, wc.hInstance, nullptr);
        if (!hwnd) return int32_t(HRESULT_FROM_WIN32(GetLastError()));
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA); // a layered window is click-through with WS_EX_TRANSPARENT; DComp supplies the pixels
        g.hwnd = hwnd;

        ComPtr<ID3D11Device> d3d;
        RETURN_IF_FAILED(D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, D3D11_CREATE_DEVICE_BGRA_SUPPORT, nullptr, 0, D3D11_SDK_VERSION, &d3d, nullptr, nullptr));
        ComPtr<IDXGIDevice> dxgi;
        RETURN_IF_FAILED(d3d.As(&dxgi));
        ComPtr<ID2D1Factory1> d2dFactory;
        RETURN_IF_FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof(ID2D1Factory1), reinterpret_cast<void**>(d2dFactory.GetAddressOf())));
        ComPtr<ID2D1Device> d2d;
        RETURN_IF_FAILED(d2dFactory->CreateDevice(dxgi.Get(), &d2d));
        ComPtr<IDCompositionDesktopDevice> dcomp;
        RETURN_IF_FAILED(DCompositionCreateDevice2(d2d.Get(), IID_PPV_ARGS(&dcomp)));
        ComPtr<IDCompositionTarget> target;
        RETURN_IF_FAILED(dcomp->CreateTargetForHwnd(hwnd, TRUE, &target));
        ComPtr<IDCompositionVisual2> root;
        RETURN_IF_FAILED(dcomp->CreateVisual(&root));
        RETURN_IF_FAILED(target->SetRoot(root.Get()));

        const int T = std::max(4, g.thickness);
        Edge edges[4];
        int geometry[4][4] = { { 0, 0, W, T }, { W - T, T, T, H - 2 * T }, { 0, H - T, W, T }, { 0, T, T, H - 2 * T } }; // top, right, bottom, left
        for (int i = 0; i < 4; i++)
        {
            auto& e = edges[i];
            e.x = geometry[i][0]; e.y = geometry[i][1]; e.w = geometry[i][2]; e.h = geometry[i][3];
            e.pixels.resize(size_t(e.w) * e.h);
            RETURN_IF_FAILED(dcomp->CreateVisual(&e.visual));
            RETURN_IF_FAILED(dcomp->CreateSurface(UINT(e.w), UINT(e.h), DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_ALPHA_MODE_PREMULTIPLIED, &e.surface));
            RETURN_IF_FAILED(e.visual->SetContent(e.surface.Get()));
            RETURN_IF_FAILED(e.visual->SetOffsetX(float(e.x)));
            RETURN_IF_FAILED(e.visual->SetOffsetY(float(e.y)));
            RETURN_IF_FAILED(root->AddVisual(e.visual.Get(), FALSE, nullptr));
        }
        RETURN_IF_FAILED(dcomp->Commit());
        g.startResult = VN_OK;
        SetEvent(g.ready);

        LARGE_INTEGER freq, start, now;
        QueryPerformanceFrequency(&freq);
        QueryPerformanceCounter(&start);
        bool shown = false;
        int painted = -1;
        uint32_t frame = 0;
        double lastPaint = -1;
        while (!g.quit)
        {
            MSG msg;
            while (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE)) { TranslateMessage(&msg); DispatchMessageW(&msg); }
            const int mode = g.mode.load();
            if (mode == 0)
            {
                if (shown) { ShowWindow(hwnd, SW_HIDE); shown = false; painted = -1; }
                MsgWaitForMultipleObjects(1, &g.wake, FALSE, INFINITE, QS_ALLINPUT);
                continue;
            }
            const bool animated = mode == 2;
            if (!animated && painted == mode && shown)
            {
                MsgWaitForMultipleObjects(1, &g.wake, FALSE, INFINITE, QS_ALLINPUT); // steady: nothing to redraw until the mode changes
                continue;
            }
            QueryPerformanceCounter(&now);
            const double t = double(now.QuadPart - start.QuadPart) / double(freq.QuadPart);
            // At most ~60 paints a second: on a high-refresh display, wait out the extra compositor frames instead of painting them.
            if (animated && shown && t - lastPaint < 1.0 / 62) { DwmFlush(); continue; }
            lastPaint = t;
            for (int i = 0; i < 4; i++)
            {
                auto& e = edges[i];
                Paint(g, e, i, t, frame);
                ComPtr<ID2D1DeviceContext> dc;
                POINT offset{};
                if (FAILED(e.surface->BeginDraw(nullptr, IID_PPV_ARGS(&dc), &offset))) continue;
                if (!e.bitmap)
                {
                    D2D1_BITMAP_PROPERTIES1 bp = D2D1::BitmapProperties1(D2D1_BITMAP_OPTIONS_NONE, D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED));
                    dc->CreateBitmap(D2D1::SizeU(UINT(e.w), UINT(e.h)), nullptr, 0, bp, &e.bitmap);
                }
                if (e.bitmap)
                {
                    e.bitmap->CopyFromMemory(nullptr, e.pixels.data(), UINT(e.w) * 4);
                    dc->SetTransform(D2D1::Matrix3x2F::Translation(float(offset.x), float(offset.y)));
                    dc->Clear(D2D1::ColorF(0, 0, 0, 0));
                    dc->DrawBitmap(e.bitmap.Get());
                }
                e.surface->EndDraw();
            }
            dcomp->Commit();
            if (!shown) { ShowWindow(hwnd, SW_SHOWNOACTIVATE); SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE); shown = true; }
            painted = mode;
            frame++;
            g.frames++;
            DwmFlush(); // paced by the compositor, not a timer
        }
        DestroyWindow(hwnd);
        CoUninitialize();
        return VN_OK;
    }
}

VN_API int32_t vn_glow_create(vn_rect monitor, int32_t thickness_px, uint32_t argb, void** handle)
{
    if (!handle || monitor.right <= monitor.left || monitor.bottom <= monitor.top) return VN_E_ARG;
    auto g = std::make_unique<Glow>();
    g->monitor = { monitor.left, monitor.top, monitor.right, monitor.bottom };
    g->thickness = thickness_px;
    g->argb = argb;
    Glow* raw = g.get();
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
    *handle = g.release();
    return VN_OK;
}

VN_API int32_t vn_glow_set(void* handle, int32_t mode, int32_t reduced_motion)
{
    if (!handle || mode < 0 || mode > 3) return VN_E_ARG;
    auto& g = *static_cast<Glow*>(handle);
    g.reduced = reduced_motion;
    g.mode = mode;
    SetEvent(g.wake);
    return VN_OK;
}

VN_API void* vn_glow_hwnd(void* handle) { return handle ? static_cast<Glow*>(handle)->hwnd.load() : nullptr; }

VN_API int64_t vn_glow_frames(void* handle) { return handle ? static_cast<Glow*>(handle)->frames.load() : 0; }

VN_API void vn_glow_destroy(void* handle)
{
    auto* g = static_cast<Glow*>(handle);
    if (!g) return;
    g->quit = true;
    SetEvent(g->wake);
    if (HWND h = g->hwnd.load()) PostMessageW(h, WM_NULL, 0, 0);
    if (g->thread.joinable()) g->thread.join();
    delete g;
}
