export const g7Event = {
  title: 'G7 Evian',
  periodLabel: '12-18 juin 2026',
  openCount: 7,
  closedCount: 21,
  officialUrl: 'https://www.ge.ch/document/sommet-du-g7-2026-evian-faq'
}

export const g7OpenBorders = [
  {
    name: 'Bardonnex',
    corridor: 'A40 / A41',
    guidance: 'Point principal sud. Delais forts probables aux heures pendulaires.',
    priority: 1
  },
  {
    name: 'Perly',
    corridor: 'Saint-Julien',
    guidance: 'Alternative sud-ouest pour eviter Bardonnex selon le trafic.',
    priority: 2
  },
  {
    name: 'Meyrin',
    corridor: 'Pays de Gex',
    guidance: 'Passage prioritaire pour le Pays de Gex et le secteur aeroport.',
    priority: 3
  },
  {
    name: 'Ferney-Voltaire',
    corridor: 'Pays de Gex',
    guidance: 'Option nord-ouest vers Nations, aeroport et rive droite.',
    priority: 4
  },
  {
    name: 'Moillesulaz',
    corridor: 'Annemasse',
    guidance: 'Passage urbain pour Annemasse centre. Controle permanent attendu.',
    priority: 5
  },
  {
    name: 'Thonex-Vallard',
    aliases: ['Thônex-Vallard'],
    corridor: 'Annemasse / D903',
    guidance: 'Alternative est depuis Annemasse et Thonex.',
    priority: 6
  },
  {
    name: 'Anieres',
    aliases: ['Anières'],
    corridor: 'Rive gauche / Chablais',
    guidance: 'Passage utile pour le secteur lac et Chablais.',
    priority: 7
  }
]

export type G7BorderInfo = (typeof g7OpenBorders)[number]

const normalize = (value: string) =>
  value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()

export const getG7BorderInfo = (borderName: string): G7BorderInfo | undefined => {
  const normalizedName = normalize(borderName)

  return g7OpenBorders.find((item) => {
    const names = [item.name, ...(item.aliases ?? [])]
    return names.some((name) => normalize(name) === normalizedName)
  })
}
