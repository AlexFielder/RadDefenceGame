# Sprite Generation Brief for "Rad Defence" (Tower Defence Game)

## Technical Requirements

- **Format**: PNG with transparent background (alpha channel)
- **Canvas size**: 32x32 pixels per sprite
- **Render size in game**: 24x24 pixels (scaled down, so a bit of extra detail is fine)
- **Style**: Top-down or 3/4 view, neon/sci-fi aesthetic on a dark background -- think glowing outlines and bright colours against space-black. Inspired by "Radiant Defense" on Steam.
- **Background**: Must be fully transparent (no background colour at all)
- **Colour key**: If transparency isn't possible, use solid magenta (#FF00FF) as the background -- the game engine treats this as transparent

## Sprites Needed

Please generate each sprite as a **separate 32x32 PNG image** with a transparent background.

### Enemy Sprites (6 types, representing escalating difficulty)

Each enemy should look like an alien creature or drone viewed from above, fitting a neon sci-fi theme. They should be visually distinct from each other so players can tell them apart at a glance.

1. **Scout** -- Small, fast-looking. Simple shape, single bright colour (green/cyan glow). The weakest enemy.
2. **Grunt** -- Standard foot soldier. Slightly bulkier than the Scout. Blue/purple glow.
3. **Tank** -- Heavy, armoured. Chunky/rounded shape suggesting toughness. Red/dark red glow. Visibly larger within the 32x32 canvas.
4. **Speeder** -- Streamlined, elongated shape suggesting speed. Yellow/gold glow with motion lines or a pointed shape.
5. **Shielded** -- Has a visible energy shield or bubble effect around it. White/cyan outer ring with a distinct inner body.
6. **Boss** -- The biggest and meanest. Fills most of the 32x32 canvas. Multi-coloured glow (magenta/purple core with cyan highlights). Detailed and intimidating.

### Tower Sprites (5 types)

Each tower sits on a 40x40 pixel grid cell, so the 32x32 sprite should have a couple of pixels of padding. Towers should look like sci-fi weapon platforms viewed from above.

1. **Gun Tower** -- Basic turret. Blue glow. Simple barrel pointing upward.
2. **Sniper Tower** -- Long-range precision. Orange glow. Longer, thinner barrel.
3. **Rapid Tower** -- Multi-barrel or gatling style. Green glow. Multiple small barrels.
4. **Rocket Tower** -- Chunky launcher. Dark red glow. Visible missile or launch tube.
5. **Flame Tower** -- Nozzle or flame emitter. Orange/yellow glow. Rounded tip suggesting heat.

### Projectile Sprites (5 types, smaller)

These are tiny -- **8x8 pixels** each, still PNG with transparency.

1. **Bullet** (Gun) -- Small bright blue dot/streak
2. **Sniper round** -- Orange/yellow elongated streak
3. **Rapid shot** -- Small green dot
4. **Rocket** -- Red/orange with a small flame trail
5. **Flame burst** -- Orange/yellow blob, slightly irregular shape

### Wall Tile

- **Size**: 40x40 pixels
- A dark metallic or crystalline block that looks like it belongs in a space station corridor. Subtle neon purple edge glow. Should tile seamlessly when placed next to other wall tiles.

### Path Tile

- **Size**: 40x40 pixels
- Dark floor/ground tile -- darker than the wall, with subtle grid markings or panel lines. Enemies walk on this. Should also tile well.

## Delivery Format

- Each sprite as a **separate PNG file**
- Name them clearly: enemy_scout.png, enemy_grunt.png, tower_gun.png, projectile_bullet.png, tile_wall.png, etc.
- If generating a sprite sheet instead, please arrange them in a grid with consistent spacing and provide the layout coordinates

## Art Direction Notes

- Think "Geometry Wars meets tower defence" -- bright neon shapes on dark backgrounds
- Keep shapes simple and readable at small sizes -- fine detail will be lost at 24x24
- Each sprite should have a clear silhouette that's recognisable even without colour
- Glow effects should be subtle (1-2 pixel soft edge) not overwhelming
- Consistent lighting direction (top-left light source if applicable)
