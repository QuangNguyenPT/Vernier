// Finds where a mark's anchor patch now is on the screen, so a mark can follow its content (scroll, reflow) instead of
// being thrown away. Zero-mean normalised cross-correlation, coarse to fine: a full search at half the given scale, then
// a refinement at the given scale round the best coarse position. The second-best coarse peak away from the best is
// reported too: on repeated content (table rows, lines of an equation) the two are close and the caller must not trust
// the match. The managed fallback (Vernier.Core.Geometry.PatchMatcher) is the same algorithm, operation for operation.
#define VERNIER_NATIVE_EXPORTS
#include "vernier_native.h"

#include <algorithm>
#include <cmath>
#include <vector>

namespace
{
struct Gray
{
    std::vector<uint8_t> px;
    int w = 0, h = 0;
    uint8_t at(int x, int y) const { return px[static_cast<size_t>(y) * w + x]; }
};

// 2x2 box average, truncating odd edges.
Gray Half(const uint8_t* src, int w, int h)
{
    Gray g;
    g.w = w / 2;
    g.h = h / 2;
    g.px.resize(static_cast<size_t>(g.w) * g.h);
    for (int y = 0; y < g.h; ++y)
        for (int x = 0; x < g.w; ++x)
        {
            const uint8_t* r0 = src + static_cast<size_t>(2 * y) * w + 2 * x;
            const uint8_t* r1 = r0 + w;
            g.px[static_cast<size_t>(y) * g.w + x] = static_cast<uint8_t>((r0[0] + r0[1] + r1[0] + r1[1] + 2) / 4);
        }
    return g;
}

struct Stats { double sum = 0, var = 0; };

Stats PatchStats(const uint8_t* p, int pw, int ph)
{
    long long s = 0, ss = 0;
    for (int i = 0; i < pw * ph; ++i) { s += p[i]; ss += static_cast<long long>(p[i]) * p[i]; }
    const double n = static_cast<double>(pw) * ph;
    return { static_cast<double>(s), static_cast<double>(ss) - static_cast<double>(s) * static_cast<double>(s) / n };
}

double Score(const uint8_t* img, int iw, const uint8_t* p, int pw, int ph, int x, int y, Stats patch)
{
    long long s = 0, ss = 0, sp = 0;
    for (int j = 0; j < ph; ++j)
    {
        const uint8_t* row = img + static_cast<size_t>(y + j) * iw + x;
        const uint8_t* prow = p + static_cast<size_t>(j) * pw;
        int rs = 0, rss = 0, rsp = 0;
        for (int i = 0; i < pw; ++i)
        {
            const int v = row[i];
            rs += v;
            rss += v * v;
            rsp += v * prow[i];
        }
        s += rs; ss += rss; sp += rsp;
    }
    const double n = static_cast<double>(pw) * ph;
    const double varI = static_cast<double>(ss) - static_cast<double>(s) * static_cast<double>(s) / n;
    if (varI < 4.0 * n) return 0.0; // a flat window cannot match anything
    return (static_cast<double>(sp) - static_cast<double>(s) * patch.sum / n) / std::sqrt(varI * patch.var);
}
}

VN_API int32_t vn_match(const uint8_t* image, int32_t iw, int32_t ih, const uint8_t* patch, int32_t pw, int32_t ph, vn_rect search, vn_match_result* out)
{
    if (!image || !patch || !out || pw < 8 || ph < 8 || pw > iw || ph > ih) return VN_E_ARG;
    *out = {};
    const Stats fine = PatchStats(patch, pw, ph);
    if (fine.var < 36.0 * pw * ph) return VN_FLAT; // stddev under 6 grey levels: nothing to recognise

    // Candidate top-left positions at the given scale, clamped so the patch stays inside the image.
    const int x0 = std::max(0, search.left), y0 = std::max(0, search.top);
    const int x1 = std::min(iw - pw + 1, search.right), y1 = std::min(ih - ph + 1, search.bottom);
    if (x0 >= x1 || y0 >= y1) return VN_E_ARG;

    // Coarse: everything at half scale.
    const Gray ci = Half(image, iw, ih), cp = Half(patch, pw, ph);
    const Stats coarse = PatchStats(cp.px.data(), cp.w, cp.h);
    const int cx0 = x0 / 2, cy0 = y0 / 2;
    const int cx1 = std::min(ci.w - cp.w + 1, (x1 + 1) / 2), cy1 = std::min(ci.h - cp.h + 1, (y1 + 1) / 2);
    if (cx0 >= cx1 || cy0 >= cy1 || coarse.var <= 0) return VN_E_ARG;
    const int cw = cx1 - cx0;
    std::vector<double> scores(static_cast<size_t>(cw) * (cy1 - cy0));
    int bx = cx0, by = cy0;
    double best = -2.0;
    for (int y = cy0; y < cy1; ++y)
        for (int x = cx0; x < cx1; ++x)
        {
            const double s = Score(ci.px.data(), ci.w, cp.px.data(), cp.w, cp.h, x, y, coarse);
            scores[static_cast<size_t>(y - cy0) * cw + (x - cx0)] = s;
            if (s > best) { best = s; bx = x; by = y; }
        }
    const int r = std::max(2, std::min(cp.w, cp.h) / 2);
    double second = -1.0;
    for (int y = cy0; y < cy1; ++y)
        for (int x = cx0; x < cx1; ++x)
            if (std::abs(x - bx) > r || std::abs(y - by) > r) second = std::max(second, scores[static_cast<size_t>(y - cy0) * cw + (x - cx0)]);

    // Fine: round twice the coarse best.
    int fx = 2 * bx, fy = 2 * by;
    double fbest = -2.0;
    for (int y = std::max(y0, 2 * by - 3); y <= std::min(y1 - 1, 2 * by + 3); ++y)
        for (int x = std::max(x0, 2 * bx - 3); x <= std::min(x1 - 1, 2 * bx + 3); ++x)
        {
            const double s = Score(image, iw, patch, pw, ph, x, y, fine);
            if (s > fbest) { fbest = s; fx = x; fy = y; }
        }
    // The second-best peak was scored at the coarse scale; put it on the fine scale so the two can be compared.
    *out = { fx, fy, fbest, best > 0 ? second * fbest / best : second };
    return VN_OK;
}
