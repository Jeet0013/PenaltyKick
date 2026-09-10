/**
 * The arena: turf, goal, stands, city, sky.
 *
 * ## What makes this read as expensive
 *
 * Not polygon count. Four things, learned the hard way on the last project:
 *
 * 1. **Image-based lighting.** Physical materials with a clearcoat reflect
 *    their surroundings. Given three point lights and nothing else, every
 *    polished surface gets three small highlights and is otherwise dead. The
 *    room is baked once through PMREM and costs nothing per frame.
 * 2. **Something to sit on.** An object floating in a void reads as a render.
 *    The turf, the stands and the city are what make the goal look photographed
 *    rather than modelled.
 * 3. **Occlusion where surfaces meet.** A goal resting on grass is grounded by
 *    darkness in the crease, not by a shadow from a high lamp.
 * 4. **Grain.** A flat fill is the thing that reads as digital.
 *
 * ## Everything is generated
 *
 * No downloaded textures, no fetched HDRIs, no third-party art. Every material
 * here is built from canvas and maths at load time, which keeps the build a
 * single self-contained file and keeps the provenance of every pixel trivially
 * answerable: we made it.
 */

import * as THREE from 'three';

import { FIELD } from '../game/BallPhysics';
import { type Quality, QUALITY } from './quality';

/** Team colours, used for the emissive trim and the crowd wash. */
export interface ArenaPalette {
  readonly home: THREE.ColorRepresentation;
  readonly away: THREE.ColorRepresentation;
}

export class Arena {
  readonly group = new THREE.Group();
  readonly #disposables: Array<{ dispose(): void }> = [];
  #crowdCards: THREE.InstancedMesh | null = null;
  #trim: THREE.MeshStandardMaterial | null = null;
  #elapsed = 0;

  constructor(quality: Quality, palette: ArenaPalette) {
    this.group.name = 'Arena';
    const preset = QUALITY[quality];

    this.group.add(
      this.#turf(preset.turfSegments),
      this.#markings(),
      this.#goal(),
      this.#net(),
      this.#stands(),
      this.#crowd(preset.crowdCount, palette),
      this.#city(),
      this.#drones(preset.droneCount),
    );
  }

  /**
   * The playing surface.
   *
   * Wet synthetic turf: dark, saturated, and reflective enough that the goal
   * and the trim throw light back off it. That reflection is most of the
   * "rain-slick" look and it costs one roughness map.
   */
  #turf(segments: number): THREE.Mesh {
    const geometry = new THREE.PlaneGeometry(90, 120, segments, segments);
    geometry.rotateX(-Math.PI / 2);

    const material = new THREE.MeshPhysicalMaterial({
      // Deep pitch green. It was reading teal: a strong cyan rim plus a cyan
      // environment tints a dark surface toward whatever is lighting it, so
      // the base colour has to carry more green than looks right on its own.
      color: 0x1a4a24,
      map: this.#track(),
      roughnessMap: this.#wetness(),
      roughness: 0.94,
      metalness: 0.0,
      /*
       * A wet pitch is a mirror at grazing angles and matte underfoot, which is
       * what a clearcoat with high roughness does. But the environment bake
       * puts a 26 m emissive panel overhead, and at clearcoat 0.55 the pitch
       * mirrored it as a white river down the middle of the penalty area that
       * swamped the green entirely. 0.05 with a rough coat keeps a hint of
       * sheen and loses the mirror.
       */
      clearcoat: 0.05,
      clearcoatRoughness: 0.85,
    });

    const mesh = new THREE.Mesh(geometry, material);
    mesh.name = 'Turf';
    mesh.receiveShadow = true;
    mesh.position.z = -FIELD.spotToGoal + 30;
    this.#keep(geometry, material);
    return mesh;
  }


  /**
   * Pitch markings, as a decal above the turf.
   *
   * The most valuable 60 lines in this file. Without them the pitch is a green
   * field with a goal on it and the eye has nothing to measure against — the
   * goal could be any size, at any distance. The six-yard box and the penalty
   * arc converging toward the goal line are what give the shot its depth, and
   * they are the reason a player can judge whether a keeper is off his line.
   *
   * Regulation, in metres, measured from a spot at the origin 11 m out:
   *
   * | marking          | distance from goal line | half-width |
   * |------------------|-------------------------|------------|
   * | goal area        | 5.5                     | 9.16       |
   * | penalty area     | 16.5                    | 20.16      |
   * | penalty arc      | radius 9.15 about the spot          |
   *
   * A decal rather than paint in the turf's own colour map, because that map
   * tiles 6x8 and a tiled penalty area would give the pitch six penalty spots.
   */
  #markings(): THREE.Mesh {
    // The strip the camera can actually see: 60 m across, from 3 m behind the
    // goal line to 10 m behind the spot.
    const worldWidth = 60;
    const worldDepth = 24;
    const zNear = 10;
    const zFar = -14;

    const w = 2048;
    const h = Math.round((w * worldDepth) / worldWidth);
    const ctx = canvas(w);
    ctx.canvas.height = h;
    ctx.clearRect(0, 0, w, h);

    const px = (x: number) => ((x + worldWidth / 2) / worldWidth) * w;
    /*
     * Canvas row 0 is the *far* edge, not the near one.
     *
     * A PlaneGeometry rotated -90 degrees about X maps its +Y to world -Z, and
     * `flipY` puts the image's top row at +Y. So the top of this canvas is
     * z = zFar, behind the goal. Getting this backwards paints the penalty arc
     * on the wrong side of the spot, which looks almost right and is not.
     */
    const py = (z: number) => ((z - zFar) / worldDepth) * h;
    // 12 cm of paint, the real width — thinner reads as a scratch, thicker as
    // a road marking.
    const lineWidth = (0.12 / worldWidth) * w;

    ctx.strokeStyle = 'rgba(255,255,255,0.82)';
    ctx.lineWidth = lineWidth;
    ctx.lineCap = 'butt';

    const goalLine = -FIELD.spotToGoal;
    const box = (fromGoalLine: number, halfWidth: number) => {
      const z = goalLine + fromGoalLine;
      ctx.beginPath();
      ctx.moveTo(px(-halfWidth), py(goalLine));
      ctx.lineTo(px(-halfWidth), py(z));
      ctx.lineTo(px(halfWidth), py(z));
      ctx.lineTo(px(halfWidth), py(goalLine));
      ctx.stroke();
    };

    // Goal line, running the full width.
    ctx.beginPath();
    ctx.moveTo(0, py(goalLine));
    ctx.lineTo(w, py(goalLine));
    ctx.stroke();

    box(5.5, 9.16);
    box(16.5, 20.16);

    /*
     * The penalty arc: a 9.15 m circle about the spot, clipped to the part
     * outside the penalty area. Drawing the whole circle is a common mistake
     * and instantly wrong to anyone who has stood on a pitch.
     */
    const arcR = (9.15 / worldWidth) * w;
    const boxEdgeZ = goalLine + 16.5;
    // Half-angle from the spot at which the circle crosses the box edge.
    const theta = Math.acos((boxEdgeZ - 0) / 9.15);
    // Canvas angles run clockwise because canvas y points down; +PI/2 is
    // therefore world +z, away from the goal, which is the half we want.
    ctx.beginPath();
    ctx.arc(px(0), py(0), arcR, Math.PI / 2 - theta, Math.PI / 2 + theta);
    ctx.stroke();

    // The penalty spot: a filled disc, not a ring, and 22 cm across — the same
    // width as the ball that sits on it. At twice that it read as a puddle.
    ctx.fillStyle = 'rgba(255,255,255,0.85)';
    ctx.beginPath();
    ctx.arc(px(0), py(0), (0.11 / worldWidth) * w, 0, Math.PI * 2);
    ctx.fill();

    const texture = new THREE.CanvasTexture(ctx.canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.anisotropy = 16;
    this.#keep(texture);

    const geometry = new THREE.PlaneGeometry(worldWidth, worldDepth);
    geometry.rotateX(-Math.PI / 2);
    const material = new THREE.MeshStandardMaterial({
      map: texture,
      transparent: true,
      roughness: 0.9,
      // Paint sits on grass, so it takes the same light — but it must never
      // z-fight with the turf one millimetre below it.
      polygonOffset: true,
      polygonOffsetFactor: -2,
      polygonOffsetUnits: -2,
      depthWrite: false,
    });
    this.#keep(geometry, material);

    const mesh = new THREE.Mesh(geometry, material);
    mesh.name = 'Markings';
    mesh.receiveShadow = true;
    mesh.position.set(0, 0.006, (zNear + zFar) / 2);
    return mesh;
  }

  /** Mown stripes and a worn penalty spot, painted rather than modelled. */
  #track(): THREE.CanvasTexture {
    const size = 1024;
    const ctx = canvas(size);

    /*
     * Near-neutral, because this map is *multiplied* by the material colour.
     *
     * It started at #16341f — the same dark green as `color` — so the two
     * multiplied out to an albedo of roughly (0.009, 0.059, 0.017): a surface
     * that reflects almost nothing. The pitch then took its entire visible
     * value from specular and the cyan environment, which is why it read as
     * water rather than grass. The map carries the stripes and the blade
     * noise; the colour carries the hue. Only one of them gets to be dark.
     */
    ctx.fillStyle = '#c4c4c4';
    ctx.fillRect(0, 0, size, size);

    // Mowing stripes: alternate bands of very slightly different value. Real
    // stripes are the same grass lying in opposite directions, so the
    // difference should be small enough to be felt rather than seen.
    for (let i = 0; i < 16; i += 1) {
      ctx.fillStyle = i % 2 === 0 ? 'rgba(255,255,255,0.05)' : 'rgba(0,0,0,0.07)';
      ctx.fillRect(0, (i * size) / 16, size, size / 16);
    }

    // Blade noise, so a close camera does not see a flat field.
    for (let i = 0; i < 9000; i += 1) {
      const x = Math.random() * size;
      const y = Math.random() * size;
      ctx.fillStyle = `rgba(${Math.random() > 0.5 ? '235,255,235' : '40,70,45'},${
        0.03 + Math.random() * 0.06
      })`;
      ctx.fillRect(x, y, 1, 1 + Math.random() * 2);
    }

    const texture = new THREE.CanvasTexture(ctx.canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(6, 8);
    texture.anisotropy = 8;
    this.#keep(texture);
    return texture;
  }

  /** Uneven wetness. Uniform roughness is the clearest tell of a CG surface. */
  #wetness(): THREE.CanvasTexture {
    const size = 512;
    const ctx = canvas(size);
    /*
     * Mid-high, and a narrow range around it.
     *
     * This map *multiplies* the material's roughness, which is the detail that
     * broke the first version: a 70/255 texel took a 0.62 material down to
     * 0.17, and 0.17 is a mirror. Every wet patch reflected the floodlight
     * panel, and the pitch came out cyan. Wet grass is glossier than dry grass
     * by a little, not by a factor of four.
     */
    ctx.fillStyle = '#dcdcdc';
    ctx.fillRect(0, 0, size, size);

    for (let i = 0; i < 140; i += 1) {
      const x = Math.random() * size;
      const y = Math.random() * size;
      const r = size * (0.02 + Math.random() * 0.11);
      const wet = Math.random() > 0.45;
      const g = ctx.createRadialGradient(x, y, 0, x, y, r);
      const tone = wet ? '170,170,170' : '230,230,230';
      g.addColorStop(0, `rgba(${tone},0.55)`);
      g.addColorStop(1, `rgba(${tone},0)`);
      ctx.fillStyle = g;
      ctx.fillRect(x - r, y - r, r * 2, r * 2);
    }

    const texture = new THREE.CanvasTexture(ctx.canvas);
    texture.colorSpace = THREE.NoColorSpace;
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(6, 8);
    this.#keep(texture);
    return texture;
  }

  /**
   * Carbon-fibre frame with emissive trim.
   *
   * Regulation dimensions. A goal that is not the right size makes every
   * instinct a player brought with them wrong, and a penalty game is played
   * almost entirely on instinct.
   */
  #goal(): THREE.Group {
    const goal = new THREE.Group();
    goal.name = 'Goal';

    const carbon = new THREE.MeshPhysicalMaterial({
      color: 0x0a0d12,
      roughness: 0.34,
      metalness: 0.6,
      clearcoat: 0.85,
      clearcoatRoughness: 0.14,
    });

    const trim = new THREE.MeshStandardMaterial({
      color: 0x0a1418,
      emissive: 0x27d3f5,
      emissiveIntensity: 2.4,
      roughness: 0.4,
    });
    this.#trim = trim;
    this.#keep(carbon, trim);

    const half = FIELD.goalWidth / 2;
    const r = FIELD.postRadius;

    const post = new THREE.CylinderGeometry(r, r, FIELD.goalHeight, 16);
    const bar = new THREE.CylinderGeometry(r, r, FIELD.goalWidth + r * 2, 16);
    bar.rotateZ(Math.PI / 2);
    this.#keep(post, bar);

    for (const side of [-1, 1]) {
      const p = new THREE.Mesh(post, carbon);
      p.position.set(side * half, FIELD.goalHeight / 2, -FIELD.spotToGoal);
      p.castShadow = true;
      goal.add(p);

      // A thin emissive strip down the inside face of each post: the cyan the
      // whole arena is keyed to, and a clear read on where the frame is.
      const strip = new THREE.Mesh(new THREE.BoxGeometry(0.02, FIELD.goalHeight, 0.03), trim);
      strip.position.set(side * (half - r), FIELD.goalHeight / 2, -FIELD.spotToGoal + r);
      goal.add(strip);
    }

    const crossbar = new THREE.Mesh(bar, carbon);
    crossbar.position.set(0, FIELD.goalHeight, -FIELD.spotToGoal);
    crossbar.castShadow = true;
    goal.add(crossbar);

    const barStrip = new THREE.Mesh(
      new THREE.BoxGeometry(FIELD.goalWidth, 0.02, 0.03),
      trim,
    );
    barStrip.position.set(0, FIELD.goalHeight - r, -FIELD.spotToGoal + r);
    goal.add(barStrip);

    return goal;
  }

  /**
   * The net.
   *
   * A plane of line segments rather than a mesh: it is mostly holes, and a
   * wireframe grid is both cheaper and more honest about what a net is. The
   * ripple on a goal is applied to these vertices directly.
   */
  #net(): THREE.Group {
    const net = new THREE.Group();
    net.name = 'Net';

    const material = new THREE.LineBasicMaterial({
      color: 0xdff3ff,
      transparent: true,
      opacity: 0.24,
    });
    this.#keep(material);

    const half = FIELD.goalWidth / 2;
    const depth = 1.9;
    const z = -FIELD.spotToGoal;
    const cells = 22;

    const points: THREE.Vector3[] = [];
    // Back face.
    for (let i = 0; i <= cells; i += 1) {
      const x = -half + (FIELD.goalWidth * i) / cells;
      points.push(new THREE.Vector3(x, 0, z - depth), new THREE.Vector3(x, FIELD.goalHeight, z - depth));
    }
    for (let i = 0; i <= 10; i += 1) {
      const y = (FIELD.goalHeight * i) / 10;
      points.push(new THREE.Vector3(-half, y, z - depth), new THREE.Vector3(half, y, z - depth));
    }
    // Sides and roof, so the net has volume from an angled camera.
    for (const side of [-1, 1]) {
      for (let i = 0; i <= 8; i += 1) {
        const y = (FIELD.goalHeight * i) / 8;
        points.push(new THREE.Vector3(side * half, y, z), new THREE.Vector3(side * half, y, z - depth));
      }
    }
    for (let i = 0; i <= 8; i += 1) {
      const x = -half + (FIELD.goalWidth * i) / 8;
      points.push(
        new THREE.Vector3(x, FIELD.goalHeight, z),
        new THREE.Vector3(x, FIELD.goalHeight, z - depth),
      );
    }

    const geometry = new THREE.BufferGeometry().setFromPoints(points);
    this.#keep(geometry);
    net.add(new THREE.LineSegments(geometry, material));
    return net;
  }

  /** Dark megastructure stands, kept simple — they are a silhouette, not a subject. */
  #stands(): THREE.Group {
    const stands = new THREE.Group();
    stands.name = 'Stands';

    /*
     * Lifted off pure black.
     *
     * At 0x090c14 the bowl was indistinguishable from the void behind it, so
     * the crowd cards read as confetti hanging in empty space. A stand only has
     * to be a few values above the sky to give the crowd something to sit on.
     */
    const material = new THREE.MeshStandardMaterial({
      color: 0x1a2029,
      roughness: 0.9,
      metalness: 0.1,
    });
    this.#keep(material);

    const tiers = [
      { radius: 34, height: 9, y: 3 },
      { radius: 44, height: 15, y: 8 },
    ];
    for (const tier of tiers) {
      const geometry = new THREE.CylinderGeometry(tier.radius, tier.radius - 4, tier.height, 40, 1, true);
      this.#keep(geometry);
      const mesh = new THREE.Mesh(geometry, material);
      mesh.position.set(0, tier.y, -FIELD.spotToGoal + 8);
      mesh.material.side = THREE.BackSide;
      stands.add(mesh);
    }
    return stands;
  }

  /**
   * The crowd, as instanced cards.
   *
   * One draw call for thousands of spectators. They are billboards tinted in
   * team colours, and they move — a still crowd is worse than no crowd, because
   * the eye reads stillness as a texture rather than as people.
   */
  #crowd(count: number, palette: ArenaPalette): THREE.InstancedMesh {
    // Person-sized at 30 m. They were 0.42 x 0.72 and read as a wall of cards.
    const geometry = new THREE.PlaneGeometry(0.24, 0.42);
    const material = new THREE.MeshBasicMaterial({
      transparent: true,
      opacity: 0.85,
      side: THREE.DoubleSide,
    });
    this.#keep(geometry, material);

    const mesh = new THREE.InstancedMesh(geometry, material, count);
    mesh.name = 'Crowd';
    mesh.instanceColor = new THREE.InstancedBufferAttribute(new Float32Array(count * 3), 3);

    const home = new THREE.Color(palette.home);
    const away = new THREE.Color(palette.away);
    const dummy = new THREE.Object3D();

    /*
     * Seated in rows, which the first version was not.
     *
     * It walked the angle monotonically while taking the tier from `i % 7`, so
     * every consecutive spectator jumped to a different height: the bowl came
     * out as a zig-zag of confetti rather than as a crowd. People sit in rows.
     * Filling one row at a time, with sub-seat jitter for the irregularity that
     * stops the rows reading as a grid, is the whole fix.
     */
    const rows = 9;
    const perRow = Math.ceil(count / rows);

    for (let i = 0; i < count; i += 1) {
      const row = Math.floor(i / perRow);
      const seat = i % perRow;
      // Half a seat of stagger per row, so the rows interlock the way real
      // seating does instead of stacking into vertical columns.
      const angle = ((seat + (row % 2) * 0.5) / perRow) * Math.PI * 2;
      const jitter = (Math.random() - 0.5) * 0.012;

      const ring = 26.5 + row * 1.9;
      const height = 3.0 + row * 1.32 + (Math.random() - 0.5) * 0.18;

      dummy.position.set(
        Math.cos(angle + jitter) * ring,
        height,
        Math.sin(angle + jitter) * ring - FIELD.spotToGoal + 8,
      );
      dummy.lookAt(0, 2, -FIELD.spotToGoal);
      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);

      // Split the bowl between the two teams, with a scatter of neutrals so it
      // is a crowd rather than two solid blocks of colour. Kept dark: these are
      // unlit cards 30 m away in a night stadium, and at full value they glowed
      // brighter than the pitch.
      const neutral = Math.random() < 0.45;
      const tint = neutral ? new THREE.Color(0x6c7c90) : angle < Math.PI ? home : away;
      const c = tint.clone().multiplyScalar(0.14 + Math.random() * 0.26);
      mesh.setColorAt(i, c);
    }
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;

    this.#crowdCards = mesh;
    return mesh;
  }

  /** A skyline behind the stands. Boxes, unlit, purely a silhouette with windows. */
  #city(): THREE.Group {
    const city = new THREE.Group();
    city.name = 'City';

    const material = new THREE.MeshBasicMaterial({ map: this.#windows(), color: 0x121a2c });
    this.#keep(material);

    for (let i = 0; i < 46; i += 1) {
      const angle = (i / 46) * Math.PI * 2;
      const distance = 78 + (i % 4) * 16;
      const height = 22 + ((i * 37) % 60);
      const width = 6 + ((i * 13) % 9);

      const geometry = new THREE.BoxGeometry(width, height, width);
      this.#keep(geometry);
      const tower = new THREE.Mesh(geometry, material);
      tower.position.set(
        Math.cos(angle) * distance,
        height / 2,
        Math.sin(angle) * distance - FIELD.spotToGoal,
      );
      city.add(tower);
    }
    return city;
  }

  /** Lit windows, as a texture. Cheaper than geometry and reads the same at distance. */
  #windows(): THREE.CanvasTexture {
    const size = 256;
    const ctx = canvas(size);
    ctx.fillStyle = '#080c16';
    ctx.fillRect(0, 0, size, size);

    for (let y = 6; y < size; y += 12) {
      for (let x = 5; x < size; x += 10) {
        if (Math.random() > 0.62) continue;
        const warm = Math.random() > 0.7;
        ctx.fillStyle = warm ? 'rgba(255,205,140,0.85)' : 'rgba(120,220,255,0.7)';
        ctx.fillRect(x, y, 4, 6);
      }
    }

    const texture = new THREE.CanvasTexture(ctx.canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    this.#keep(texture);
    return texture;
  }

  /** Camera drones: small moving lights that give the sky a sense of scale. */
  #drones(count: number): THREE.Points {
    const positions = new Float32Array(count * 3);
    for (let i = 0; i < count; i += 1) {
      positions[i * 3] = (Math.random() - 0.5) * 70;
      positions[i * 3 + 1] = 16 + Math.random() * 22;
      positions[i * 3 + 2] = (Math.random() - 0.5) * 70 - FIELD.spotToGoal;
    }
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

    const material = new THREE.PointsMaterial({
      size: 0.5,
      color: 0x8ff0ff,
      transparent: true,
      opacity: 0.75,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
    });
    this.#keep(geometry, material);

    const points = new THREE.Points(geometry, material);
    points.name = 'Drones';
    return points;
  }

  /**
   * Per-frame life.
   *
   * Only two things move: the crowd breathes and the trim pulses. Both are
   * cheap, and between them they stop the arena reading as a photograph.
   */
  update(delta: number, intensity = 0): void {
    this.#elapsed += delta;

    if (this.#trim) {
      // Base glow with a slow breath, lifted while something is happening.
      this.#trim.emissiveIntensity = 2.2 + Math.sin(this.#elapsed * 1.6) * 0.25 + intensity * 2.4;
    }

    const crowd = this.#crowdCards;
    if (crowd) {
      // A shared sway rather than per-instance animation: one uniform-ish
      // motion for thousands of cards, which is the only version that is free.
      crowd.rotation.z = Math.sin(this.#elapsed * 2.1) * 0.006 * (1 + intensity * 6);
    }
  }

  /** Recolour the trim and crowd when the teams change. */
  setAccent(color: THREE.ColorRepresentation): void {
    this.#trim?.emissive.set(color);
  }

  dispose(): void {
    for (const item of this.#disposables) item.dispose();
    this.#disposables.length = 0;
  }

  #keep(...items: Array<{ dispose(): void }>): void {
    this.#disposables.push(...items);
  }
}

function canvas(size: number): CanvasRenderingContext2D {
  const element = document.createElement('canvas');
  element.width = size;
  element.height = size;
  const ctx = element.getContext('2d');
  if (!ctx) throw new Error('Arena: 2D canvas unavailable');
  return ctx;
}
