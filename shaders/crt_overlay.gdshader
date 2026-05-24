shader_type canvas_item;

// CRT overlay — sits on top of the rendered scene as a translucent
// ColorRect. Adds horizontal scanlines, a vignette darkening the corners,
// and a tiny rolling flicker. Does NOT sample SCREEN_TEXTURE so it composites
// cleanly without render order surprises.

uniform float scanline_strength : hint_range(0.0, 1.0) = 0.18;
uniform float scanline_count    : hint_range(50.0, 1200.0) = 720.0;
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.45;
uniform float flicker_strength  : hint_range(0.0, 0.2) = 0.04;
uniform vec3  tint              : source_color = vec3(0.0, 0.05, 0.0);

void fragment() {
    // Horizontal scanlines: thin dark bands across the screen.
    float scan = sin(UV.y * scanline_count * 3.14159) * 0.5 + 0.5;
    float scan_dark = mix(1.0, scan, scanline_strength);

    // Subtle vignette via radial distance from center.
    vec2 centered = UV - vec2(0.5);
    float r = length(centered);
    float vig = smoothstep(0.7, 0.95, r) * vignette_strength;

    // Per-frame jitter so the whole image breathes slightly.
    float flicker = sin(TIME * 6.0) * flicker_strength;

    float darken = (1.0 - scan_dark) + vig + flicker;
    COLOR = vec4(tint, clamp(darken, 0.0, 0.9));
}
