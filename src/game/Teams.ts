/**
 * The teams.
 *
 * Fictional, and equal. The spec is explicit that competitive attributes are
 * identical across every team — colour, kit, entrance and crowd mood are the
 * only differences. That is not a balance decision so much as a promise: a
 * player who picks the team they like the look of must not be choosing to lose.
 *
 * There is deliberately no `attack` or `accuracy` field here. A stat that does
 * not exist cannot quietly acquire a use later.
 */

export interface Team {
  readonly id: string;
  readonly name: string;
  readonly identity: string;
  readonly primary: number;
  readonly secondary: number;
  readonly mood: string;
}

export const TEAMS: readonly Team[] = [
  { id: 'helix', name: 'Helix United', identity: 'Precision engineers', primary: 0x2ad4f5, secondary: 0xf2f7ff, mood: 'mechanical claps, laser grid' },
  { id: 'vipers', name: 'Solar Vipers', identity: 'Desert speed', primary: 0xffb020, secondary: 0x14100a, mood: 'heat haze, snake-light ribbons' },
  { id: 'nova', name: 'Nova Dynasty', identity: 'Imperial glamour', primary: 0x9a6bff, secondary: 0xffd45e, mood: 'hologram banners, orchestral bass' },
  { id: 'polar', name: 'Polar Circuit', identity: 'Ice-cold defence', primary: 0x9fdcff, secondary: 0x3a4450, mood: 'fog jets, synchronised chant' },
  { id: 'crimson', name: 'Crimson Orbit', identity: 'Relentless pressure', primary: 0xff3b48, secondary: 0x201d20, mood: 'pulsing reactor lights' },
  { id: 'comets', name: 'Emerald Comets', identity: 'Agile counterattack', primary: 0x35e08a, secondary: 0xd8dfe6, mood: 'comet trails, bright crowd cards' },
  { id: 'obsidian', name: 'Obsidian Forge', identity: 'Industrial power', primary: 0xff7a1a, secondary: 0x141416, mood: 'sparks, heavy drumline' },
  { id: 'aurora', name: 'Aurora FC', identity: 'Balanced showmanship', primary: 0xff4fd8, secondary: 0x2ad4c8, mood: 'aurora canopy, melodic crowd choir' },
];

export function teamById(id: string): Team {
  const team = TEAMS.find((t) => t.id === id);
  if (!team) throw new Error(`teamById: no team "${id}"`);
  return team;
}

/** The two the vertical slice ships with, per the spec. */
export const SLICE_TEAMS = {
  home: teamById('helix'),
  away: teamById('crimson'),
} as const;
