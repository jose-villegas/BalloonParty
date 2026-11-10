#ifndef BALLOONPARTY_COMPOSITE_INCLUDED
#define BALLOONPARTY_COMPOSITE_INCLUDED

// Porter-Duff "over" compositing: src (foreground) over dst (background).
// Both inputs use straight (non-premultiplied) alpha.
inline half4 PorterDuffOver(half4 src, half4 dst)
{
    half combinedA = src.a + dst.a * (1.0 - src.a);
    half3 combinedRGB = combinedA > 0.0001
        ? (src.rgb * src.a + dst.rgb * dst.a * (1.0 - src.a)) / combinedA
        : src.rgb;
    return half4(combinedRGB, combinedA);
}

#endif // BALLOONPARTY_COMPOSITE_INCLUDED
