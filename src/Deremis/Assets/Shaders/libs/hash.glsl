// Hash without Sine
// MIT License...
/* Copyright (c)2014 David Hoskins.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/

//----------------------------------------------------------------------------------------
//  1 out, 1 in...
float hash11(float p)
{
    p = fract(p * .1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

//----------------------------------------------------------------------------------------
//  1 out, 2 in...
float hash12(vec2 p)
{
	vec3 p3  = fract(vec3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

//----------------------------------------------------------------------------------------
//  1 out, 3 in...
float hash13(vec3 p3)
{
	p3  = fract(p3 * .1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return fract((p3.x + p3.y) * p3.z);
}

//----------------------------------------------------------------------------------------
//  2 out, 1 in...
vec2 hash21(float p)
{
	vec3 p3 = fract(vec3(p) * vec3(.1031, .1030, .0973));
	p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.xx+p3.yz)*p3.zy);

}

//----------------------------------------------------------------------------------------
///  2 out, 2 in...
vec2 hash22(vec2 p)
{
	vec3 p3 = fract(vec3(p.xyx) * vec3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx+33.33);
    return fract((p3.xx+p3.yz)*p3.zy);

}

//----------------------------------------------------------------------------------------
///  2 out, 3 in...
vec2 hash23(vec3 p3)
{
	p3 = fract(p3 * vec3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx+33.33);
    return fract((p3.xx+p3.yz)*p3.zy);
}

//----------------------------------------------------------------------------------------
//  3 out, 1 in...
vec3 hash31(float p)
{
   vec3 p3 = fract(vec3(p) * vec3(.1031, .1030, .0973));
   p3 += dot(p3, p3.yzx+33.33);
   return fract((p3.xxy+p3.yzz)*p3.zyx); 
}


//----------------------------------------------------------------------------------------
///  3 out, 2 in...
vec3 hash32(vec2 p)
{
	vec3 p3 = fract(vec3(p.xyx) * vec3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yxz+33.33);
    return fract((p3.xxy+p3.yzz)*p3.zyx);
}

//----------------------------------------------------------------------------------------
///  3 out, 3 in...
vec3 hash33(vec3 p3)
{
	p3 = fract(p3 * vec3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yxz+33.33);
    return fract((p3.xxy + p3.yxx)*p3.zyx);

}

//----------------------------------------------------------------------------------------
// 4 out, 1 in...
vec4 hash41(float p)
{
	vec4 p4 = fract(vec4(p) * vec4(.1031, .1030, .0973, .1099));
    p4 += dot(p4, p4.wzxy+33.33);
    return fract((p4.xxyz+p4.yzzw)*p4.zywx);
    
}

//----------------------------------------------------------------------------------------
// 4 out, 2 in...
vec4 hash42(vec2 p)
{
	vec4 p4 = fract(vec4(p.xyxy) * vec4(.1031, .1030, .0973, .1099));
    p4 += dot(p4, p4.wzxy+33.33);
    return fract((p4.xxyz+p4.yzzw)*p4.zywx);

}

//----------------------------------------------------------------------------------------
// 4 out, 3 in...
vec4 hash43(vec3 p)
{
	vec4 p4 = fract(vec4(p.xyzx)  * vec4(.1031, .1030, .0973, .1099));
    p4 += dot(p4, p4.wzxy+33.33);
    return fract((p4.xxyz+p4.yzzw)*p4.zywx);
}

//----------------------------------------------------------------------------------------
// 4 out, 4 in...
vec4 hash44(vec4 p4)
{
	p4 = fract(p4  * vec4(.1031, .1030, .0973, .1099));
    p4 += dot(p4, p4.wzxy+33.33);
    return fract((p4.xxyz+p4.yzzw)*p4.zywx);
}

//	<https://www.shadertoy.com/view/4dS3Wd>
//	By Morgan McGuire @morgan3d, http://graphicscodex.com
//

float noise(float x) {
	float i = floor(x);
	float f = fract(x);
	float u = f * f * (3.0 - 2.0 * f);
	return mix(hash11(i), hash11(i + 1.0), u);
}

float noise(vec2 x) {
	vec2 i = floor(x);
	vec2 f = fract(x);

	// Four corners in 2D of a tile
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));

	// Simple 2D lerp using smoothstep envelope between the values.
	// return vec3(mix(mix(a, b, smoothstep(0.0, 1.0, f.x)),
	//			mix(c, d, smoothstep(0.0, 1.0, f.x)),
	//			smoothstep(0.0, 1.0, f.y)));

	// Same code, with the clamps in smoothstep and common subexpressions
	// optimized away.
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

// This one has non-ideal tiling properties that I'm still tuning
float noise(vec3 x) {
	const vec3 step = vec3(110, 241, 171);

	vec3 i = floor(x);
	vec3 f = fract(x);
 
	// For performance, compute the base input to a 1D hash from the integer part of the argument and the 
	// incremental change to the 1D based on the 3D -> 1D wrapping
    float n = dot(i, step);

	vec3 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix( hash11(n + dot(step, vec3(0, 0, 0))), hash11(n + dot(step, vec3(1, 0, 0))), u.x),
                   mix( hash11(n + dot(step, vec3(0, 1, 0))), hash11(n + dot(step, vec3(1, 1, 0))), u.x), u.y),
               mix(mix( hash11(n + dot(step, vec3(0, 0, 1))), hash11(n + dot(step, vec3(1, 0, 1))), u.x),
                   mix( hash11(n + dot(step, vec3(0, 1, 1))), hash11(n + dot(step, vec3(1, 1, 1))), u.x), u.y), u.z);
}