// Stormsweep's lash (scripts/Stormsweep.cs): a whip of storm-light, as pixel art on the game's 2 px grid.
//
// "stormsweep looks robotic. it should be shaped and animated more like a whip." The arc was a
// uniform zigzag polyline that slid down the band in lockstep and left five even lines behind it.
// The node now lays each arc out as a whip - seventeen points it moves every frame (unfurling with a
// rolling loop, slapping down the band tip first, going slack and springing back) - and this shader
// draws it: one quad per arc, carrying the arc's index in its UV. Every fragment snaps to the WORLD's
// 2 px cell first and is then measured against that arc's points, so the lash keeps square, upright
// art pixels whatever its curve, and tapers from a thick handle to a one-pixel tip. Opaque colours
// from the element ramp, Bayer dithering the only gradient, as in riptide_wave.gdshader.
//
// THE BAND IS THE HIT. While the lash sweeps, the node lays it on exactly the curve the hit test
// sweeps (Stormsweep.PassAcross, repeated below as pass_across), and the smear behind it is the band
// between where the lash is and where it was - the ground just struck. Nothing is drawn outside the
// band and its body allowance.
//
// THE LASHES ARRIVE AS A TEXTURE (Stormsweep, "THE LASHES GO TO THE GPU AS A TEXTURE"): one row of
// four-float texels per lash, row = the index in the quad's UV.x:
//   texels 0-8  its 17 points, node-local, hand first, two to a texel (xy, zw)
//   texel 9     shape: x the half width at the hand, y at the tip, z heat (1 white-hot .. 0 cooled)
//   texel 10    frame: xy the arc's direction (unit, node-local), z its reach, w its half band
//   texel 11    smear, the swept ground behind the lash: x the across position of the pass's
//               starting edge, y the pass's progress at the head (the lash), z at the tail, w strength
//   texel 12    fx: x dissolve 0..1, y the crack's white heat on the tip 0..1, and for one of Forked
//               Lightning's outer tongues z its fan at the tip (signed, px across) and w where along the
//               arc it splits off (0..1) - its smear is the band shifted by that fan, past the split only
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D lash_data : filter_nearest, repeat_disable;
uniform float hand_reach = 14.0;
uniform float time = 0.0;
// Iron Current: the lash is beads of iron strung on the current.
uniform bool ironbound = false;
// The lash's ramp (Lightning, or Metal on the iron path) and the storm's own, for the crackle.
uniform vec4 ramp_core : source_color = vec4(1.0, 1.0, 0.941, 1.0);
uniform vec4 ramp_hot : source_color = vec4(1.0, 0.973, 0.690, 1.0);
uniform vec4 ramp_mid : source_color = vec4(1.0, 0.941, 0.416, 1.0);
uniform vec4 ramp_edge : source_color = vec4(0.878, 0.690, 0.165, 1.0);
uniform vec4 spark_core : source_color = vec4(1.0, 1.0, 0.941, 1.0);
uniform vec4 spark_hot : source_color = vec4(1.0, 0.973, 0.690, 1.0);
uniform vec4 spark_mid : source_color = vec4(1.0, 0.941, 0.416, 1.0);
// A legendary roll's gold, multiplied over every colour (Player.GetSpellVisualTint).
uniform vec4 tint : source_color = vec4(1.0);

const int MAX_DRAWN = 32;
const int PTS = 17;
const int POINT_TEXELS = 9;
const float GRID = 2.0;
const vec3 OCC = vec3(0.020, 0.027, 0.047);          // #05070C

varying vec2 world;
varying flat vec2 origin;
varying flat vec2 axis_x;
varying flat vec2 axis_y;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	origin = MODEL_MATRIX[3].xy;
	axis_x = MODEL_MATRIX[0].xy;
	axis_y = MODEL_MATRIX[1].xy;
}

float bayer4(vec2 c) {
	int x = int(mod(c.x, 4.0));
	int y = int(mod(c.y, 4.0));
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return (m[x + y * 4] + 0.5) / 16.0;
}

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float smooth01(float x) {
	x = clamp(x, 0.0, 1.0);
	return x * x * (3.0 - 2.0 * x);
}

// Stormsweep.PassAcross: where a pass's arc lies across the band at fraction s along it, c of the way
// through the pass. The far end leads.
float pass_across(float from, float c, float s) {
	float lead = smooth01(c * 1.35 - (1.0 - s) * 0.35);
	return from - 2.0 * from * lead;
}

void fragment() {
	int me = clamp(int(UV.x + 0.5), 0, MAX_DRAWN - 1);
	vec2 cell = floor(world / GRID);
	vec2 centre = cell * GRID + GRID * 0.5;
	vec2 p = inverse(mat2(axis_x, axis_y)) * (centre - origin);
	float b = bayer4(cell);
	vec2 pts[17];
	for (int x = 0; x < POINT_TEXELS; x++) {
		vec4 t = texelFetch(lash_data, ivec2(x, me), 0);
		pts[2 * x] = t.xy;
		if (2 * x + 1 < PTS)
			pts[2 * x + 1] = t.zw;
	}
	vec4 sh = texelFetch(lash_data, ivec2(POINT_TEXELS, me), 0);
	vec4 fr = texelFetch(lash_data, ivec2(POINT_TEXELS + 1, me), 0);
	vec4 sm = texelFetch(lash_data, ivec2(POINT_TEXELS + 2, me), 0);
	vec4 f = texelFetch(lash_data, ivec2(POINT_TEXELS + 3, me), 0);
	float flicker = floor(time / 0.035);

	// The nearest point of the lash: how far off it this cell is, how far along it (0 hand, 1 tip),
	// and how many px of lash lie between it and the hand.
	float best = 1e6;
	float best_t = 0.0;
	float best_run = 0.0;
	float run = 0.0;
	for (int j = 0; j < PTS - 1; j++) {
		vec2 a = pts[j];
		vec2 ab = pts[j + 1] - a;
		float len2 = dot(ab, ab);
		float u = len2 > 0.0001 ? clamp(dot(p - a, ab) / len2, 0.0, 1.0) : 0.0;
		float d = length(p - a - ab * u);
		float seg = sqrt(len2);
		if (d < best) {
			best = d;
			best_t = (float(j) + u) / float(PTS - 1);
			best_run = run + seg * u;
		}
		run += seg;
	}
	// Thick at the hand, thinning fast into a long lash, one art pixel at the tip.
	float w = sh.y + (sh.x - sh.y) * pow(1.0 - best_t, 1.4);

	vec3 col = vec3(0.0);
	bool hit = false;
	if (best <= w) {
		float r = best / max(w, 0.75);
		if (ironbound) {
			// Beads of iron strung on the current, long and square by turns, with a dark seam
			// between them so a row reads as matter rather than as a stroke...
			float bead = best_run / 6.0;
			bool odd = mod(floor(bead), 2.0) > 0.5;
			col = fract(bead) < (odd ? 0.16 : 0.3) ? OCC : (r > 0.72 ? ramp_edge.rgb : (odd ? ramp_mid.rgb : ramp_hot.rgb));
			// ...and the storm crawling over them, re-chosen every flicker.
			if (best < 1.2 && hash12(vec2(floor(best_run / 4.0), flicker)) > 0.45)
				col = spark_core.rgb;
		} else {
			// Light running down the lash from the hand to the tip, and the tip itself white-hot.
			float pulse = fract(best_run / 34.0 - time * 7.0);
			float lvl = r + (1.0 - sh.z) * 0.75;
			if (pulse < 0.22) lvl -= 0.3;
			if (best_t > 0.86) lvl -= 0.4;
			col = lvl < 0.34 ? ramp_core.rgb : (lvl < 0.62 ? ramp_hot.rgb : (lvl < 0.86 ? ramp_mid.rgb : ramp_edge.rgb));
		}
		hit = true;
	} else if (best <= w + 2.0 && sh.z > 0.35) {
		// The crackle: sparks a pixel off the lash, a fresh few every flicker.
		float h = hash12(cell + flicker * 17.0);
		if (h > 0.9) {
			col = h > 0.965 ? spark_hot.rgb : spark_mid.rgb;
			hit = true;
		}
	}

	// The crack: the tip white-hot for a moment after it snaps straight.
	if (f.y > 0.0 && length(p - pts[PTS - 1]) < 2.0 + 4.0 * f.y) {
		col = spark_core.rgb;
		hit = true;
	}

	// The ground just swept, behind the lash: densest where the lash has just passed, thinning back
	// toward where it was, and broken into flickering streaks across the band - speed, not a fill.
	if (!hit && sm.w > 0.0) {
		vec2 dir = fr.xy;
		vec2 acr = abs(dir.x) > 0.5 ? vec2(0.0, 1.0) : vec2(1.0, 0.0);
		float along = dot(p, dir);
		float across = dot(p, acr);
		float s = (along - hand_reach) / max(1.0, fr.z - hand_reach);
		// A forked tongue smears only the stretch past its split, shifted by how far it has fanned there
		// (Stormsweep.ForkFan).
		float shift = 0.0;
		bool tongue = abs(f.z) > 0.001;
		if (tongue)
			shift = s <= f.w ? 0.0 : f.z * smooth01((s - f.w) / max(0.001, 1.0 - f.w));
		if (s >= (tongue ? f.w : 0.0) && s <= 1.0) {
			float ah = pass_across(sm.x, sm.y, s) + shift;
			float at = pass_across(sm.x, sm.z, s) + shift;
			float lo = min(ah, at);
			float hi = max(ah, at);
			if (across >= lo && across <= hi && hi - lo > 0.5) {
				float g = 1.0 - abs(across - ah) / (hi - lo);
				float streak = hash12(vec2(floor(along / GRID), flicker));
				if (sm.w * g * g * 0.85 > b && streak > 0.45) {
					col = g > 0.8 ? ramp_mid.rgb : ramp_edge.rgb;
					hit = true;
				}
			}
		}
	}

	if (!hit || f.x > b)
		discard;
	COLOR = vec4(col * tint.rgb, tint.a);
}
