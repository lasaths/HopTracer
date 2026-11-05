# ANDURA Design System Attribution

HopTracer uses the ANDURA design system for mission-critical dark UI.

**Source:** https://github.com/lasaths/andura-skill (commit 963ae3c)  
**License:** MIT  
**Author:** lasaths

## Vendored Files

- `andura.tokens.json` - Design tokens (colors, typography, spacing, motion)
- `andura.css` - Generated CSS variables from tokens
- `andura-fonts.css` - Font loading (modified to use Google Fonts CDN)
- `hoptracer-andura.css` - HopTracer-specific ANDURA adaptation

## ANDURA Principles Applied

1. **Dark only** - No light mode variant
2. **Monochrome base** - Grey when normal, colour only for abnormal states
3. **Never colour alone** - Every status gets colour + glyph + word
4. **IBM Plex Sans/Mono** - Weights 400 (regular) and 500 (medium)
5. **8px grid** - All spacing based on 8px increments
6. **Opacity-only animation** - No transforms or position changes
7. **Whitespace before lines before boxes** - Visual hierarchy through spacing

## HopTracer Status Mapping

- **Unchanged nodes/edges**: Muted (#8A8C91) / Line (#3C3D41) greys
- **Added nodes** (low risk): Ultramarine (#3A3CFF, selection/accent)
- **Modified nodes** (medium risk): Caution (#FBB636, yellow)
- **Removed nodes** (critical/high risk): Emergency (#F45536, red)

## Original ANDURA License (MIT)

```
MIT License

Copyright (c) 2024 lasaths

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
SOFTWARE.
```
