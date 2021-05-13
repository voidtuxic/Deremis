
vec4 blur13(vec2 uv, vec2 resolution, vec2 direction, float scale) {
  vec4 color = vec4(0.0);
  vec2 off1 = scale * vec2(1.411764705882353) * direction;
  vec2 off2 = scale * vec2(3.2941176470588234) * direction;
  vec2 off3 = scale * vec2(5.176470588235294) * direction;
  color += texture(sampler2D(bloomTex,texSampler), uv) * 0.1964825501511404;
  color += texture(sampler2D(bloomTex,texSampler), uv + (off1 / resolution)) * 0.2969069646728344;
  color += texture(sampler2D(bloomTex,texSampler), uv - (off1 / resolution)) * 0.2969069646728344;
  color += texture(sampler2D(bloomTex,texSampler), uv + (off2 / resolution)) * 0.09447039785044732;
  color += texture(sampler2D(bloomTex,texSampler), uv - (off2 / resolution)) * 0.09447039785044732;
  color += texture(sampler2D(bloomTex,texSampler), uv + (off3 / resolution)) * 0.010381362401148057;
  color += texture(sampler2D(bloomTex,texSampler), uv - (off3 / resolution)) * 0.010381362401148057;
  return color;
}

vec4 blur5(vec2 uv, vec2 resolution, vec2 direction, float scale) {
  vec4 color = vec4(0.0);
  vec2 off1 = scale * vec2(1.3333333333333333) * direction;
  color += texture(sampler2D(bloomTex,texSampler), uv) * 0.29411764705882354;
  color += texture(sampler2D(bloomTex,texSampler), uv + (off1 / resolution)) * 0.35294117647058826;
  color += texture(sampler2D(bloomTex,texSampler), uv - (off1 / resolution)) * 0.35294117647058826;
  return color; 
}