shader_type canvas_item;

// Matrix-rain background, single draw call, fragment-only.
// No glyph atlas — characters are faked via a per-cell hash mask, which reads
// as "blocky characters dropping" at normal viewing distance. Good enough for
// the MVP vibe; swap in an atlas later if needed.

uniform float cell_size       : hint_range(6.0, 32.0)  = 14.0;
uniform float rain_speed      : hint_range(0.1, 4.0)   = 1.4;
uniform float density         : hint_range(0.1, 1.0)   = 0.85;
uniform float glow            : hint_range(0.0, 2.0)   = 1.15;
uniform vec3  rain_color      : source_color           = vec3(0.10, 1.00, 0.28);
uniform vec3  head_color      : source_color           = vec3(0.80, 1.00, 0.85);
uniform vec3  bg_color        : source_color           = vec3(0.00, 0.02, 0.00);
uniform float trail_length    : hint_range(2.0, 40.0)  = 18.0;
uniform float flicker_amount  : hint_range(0.0, 1.0)   = 0.35;

float hash11(float p) {
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

float hash21(vec2 p) {
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

void fragment() {
    vec2 res = 1.0 / SCREEN_PIXEL_SIZE;
    vec2 pix = UV * res;

    vec2 cell  = floor(pix / cell_size);
    vec2 local = fract(pix / cell_size);

    // One independent stream per column. `r` controls speed/length/offset.
    float r = hash11(cell.x * 7.0 + 13.0);

    // Skip columns occasionally so density never feels uniform.
    if (r > density) {
        COLOR = vec4(bg_color, 1.0);
    } else {
        float speed     = mix(0.5, 1.8, hash11(cell.x * 11.0)) * rain_speed;
        float length_c  = trail_length * mix(0.6, 1.4, hash11(cell.x * 17.0));
        float offset    = hash11(cell.x * 23.0) * 50.0;
        float total_rows = res.y / cell_size;
        float head_row  = mod(TIME * speed * total_rows * 0.18 + offset, total_rows + length_c) - length_c;

        // Distance from this cell to the column's "head" (positive = above head).
        float dist = head_row - cell.y;

        float intensity = 0.0;
        vec3  color     = bg_color;

        if (dist >= 0.0 && dist <= length_c) {
            float t = 1.0 - dist / length_c;       // 1 at head, 0 at tail end
            intensity = pow(t, 1.6);
            color = mix(rain_color, head_color, smoothstep(0.85, 1.0, t));
        }

        // Per-cell glyph mask + per-cell flicker so characters seem to mutate.
        float cell_seed = hash21(cell + floor(TIME * 6.0));
        float glyph_mask = step(0.55, hash21(local * 8.0 + cell_seed * 32.0));
        intensity *= mix(1.0, cell_seed, flicker_amount);
        intensity *= mix(0.25, 1.0, glyph_mask);

        // Subtle cell-edge darkening reads as gridded characters rather than blobs.
        float edge = smoothstep(0.0, 0.12, local.x) * smoothstep(1.0, 0.88, local.x)
                   * smoothstep(0.0, 0.12, local.y) * smoothstep(1.0, 0.88, local.y);
        intensity *= mix(0.6, 1.0, edge);

        COLOR = vec4(mix(bg_color, color * glow, intensity), 1.0);
    }
}
