#ifndef BALLOONPARTY_COMPOSITE_INCLUDED
#define BALLOONPARTY_COMPOSITE_INCLUDED

// Porter-Duff "over" compositing: src (foreground) over dst (background).
// Both inputs use straight (non-premultiplied) alpha.
inline fixed4 PorterDuffOver(fixed4 src, fixed4 dst)
{
    fixed combinedA = src.a + dst.a * (1.0 - src.a);
    fixed3 combinedRGB = combinedA > 0.0001
        ? (src.rgb * src.a + dst.rgb * dst.a * (1.0 - src.a)) / combinedA
        : src.rgb;
    return fixed4(combinedRGB, combinedA);
}

#endif // BALLOONPARTY_COMPOSITE_INCLUDED
