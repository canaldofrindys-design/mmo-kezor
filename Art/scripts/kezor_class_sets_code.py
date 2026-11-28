# -*- coding: utf-8 -*-
"""Gera no código do jogo os sets de classe (3 novos por classe: tiers 1, 3 e 4; o set antigo da classe é o tier 2).

Roda fora da Unity: python3 kezor_class_sets_code.py <raiz do projeto> [<raiz> ...]
Reescreve só os blocos entre marcadores kz-class-sets (ou cria os blocos na primeira vez), mantendo o fim de linha de cada arquivo.
  Item.cs          enum ArmorSet
  ArmorSets.cs     nome e bônus de cada set
  ItemDatabase.cs  as 5 peças de cada set
  Gear.cs          prefixo do modelo por faixa de Id e cor do brilho de set completo
"""
import os, re, sys

# Classe, máscara e Ids: 700 + classe*30 + (0 | 10 | 20) para os tiers 1, 3 e 4.
CLASSES = ['W', 'A', 'R', 'L', 'C', 'I', 'P']

# tier: (DropLevel, multiplicador de defesa sobre o Runica, requisito base, durabilidade base)
TIERS = {1: (15, 0.68, 55, 36), 3: (45, 1.38, 120, 50), 4: (65, 1.85, 160, 58)}
BASE_DEF = {'Helm': 16, 'Armor': 26, 'Pants': 22, 'Gloves': 11, 'Boots': 13}       # Placas Rúnicas (tier 2)
REQ_ADD = {'Helm': 5, 'Armor': 15, 'Pants': 10, 'Gloves': 0, 'Boots': 0}
DUR_ADD = {'Helm': 2, 'Armor': 8, 'Pants': 6, 'Gloves': 0, 'Boots': 2}
# defesa relativa por classe (o Véu do Arcanista tem ~metade da defesa das Placas Rúnicas)
CLASS_DEF = {'W': 1.0, 'A': 0.5, 'I': 0.5, 'R': 0.7, 'L': 0.8, 'C': 0.95, 'P': 0.7}
SLOT = {'Helm': 'Helm', 'Armor': 'Armor', 'Pants': 'Pants', 'Gloves': 'Gloves', 'Boots': 'Boots'}

# (classe, tier, enum, prefixo do modelo, nome do set, nomes das 5 peças, bônus, cor, atributo do requisito)
SETS = [
    ('W', 1, 'KnightPlate', 'Cavaleiro', 'Placas do Cavaleiro',
     ['Elmo do Cavaleiro', 'Peitoral do Cavaleiro', 'Grevas do Cavaleiro', 'Manoplas do Cavaleiro', 'Botas do Cavaleiro'],
     'Defense = 12, ReductionPercent = 3', '#c9d2dc', 'Strength'),
    ('W', 3, 'TemplarPlate', 'Templario', 'Placas do Templário',
     ['Elmo do Templário', 'Peitoral do Templário', 'Grevas do Templário', 'Manoplas do Templário', 'Botas do Templário'],
     'Defense = 30, ReductionPercent = 7, DamagePercent = 6', '#9fd4ff', 'Strength'),
    ('W', 4, 'SovereignPlate', 'Soberano', 'Placas do Soberano',
     ['Elmo do Soberano', 'Peitoral do Soberano', 'Grevas do Soberano', 'Manoplas do Soberano', 'Botas do Soberano'],
     'Defense = 42, ReductionPercent = 10, DamagePercent = 8, DefenseRatePercent = 5', '#ffcf4a', 'Strength'),
    ('A', 1, 'ApprenticeRobe', 'Aprendiz', 'Túnica do Aprendiz',
     ['Capuz do Aprendiz', 'Túnica do Aprendiz', 'Calças do Aprendiz', 'Mangas do Aprendiz', 'Botas do Aprendiz'],
     'Defense = 4, MagicPercent = 8', '#8a7bd1', 'Energy'),
    ('A', 3, 'SageRobe', 'Sabio', 'Vestes do Sábio',
     ['Chapéu do Sábio', 'Vestes do Sábio', 'Saia do Sábio', 'Mangas do Sábio', 'Botas do Sábio'],
     'Defense = 12, MagicPercent = 20, ReductionPercent = 3', '#5fa8ff', 'Energy'),
    ('A', 4, 'ArchmageRobe', 'Arquimago', 'Vestes do Arquimago',
     ['Elmo do Arquimago', 'Vestes do Arquimago', 'Calças do Arquimago', 'Braçadeiras do Arquimago', 'Botas do Arquimago'],
     'Defense = 18, MagicPercent = 28, ReductionPercent = 5, DamagePercent = 4', '#c58bff', 'Energy'),
]

BASE_ENUM = ['None', 'Leather', 'Scale', 'Veil', 'RunePlate', 'ArcaneWeave', 'Bastion', 'EchoVeil', 'EmberHide']
KINDS = ['Helm', 'Armor', 'Pants', 'Gloves', 'Boots']
B, E = '// <kz-class-sets>', '// </kz-class-sets>'


def base_id(cls, tier):
    return 700 + CLASSES.index(cls) * 30 + {1: 0, 3: 10, 4: 20}[tier]


def read(p):
    raw = open(p, 'rb').read().decode('utf-8-sig')
    nl = '\r\n' if '\r\n' in raw else '\n'
    return raw.replace('\r\n', '\n'), nl, open(p, 'rb').read().startswith(b'\xef\xbb\xbf')


def write(p, txt, nl, bom):
    data = txt.replace('\n', nl).encode('utf-8')
    open(p, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + data)


def put_block(txt, lines, indent, anchor_before=None, anchor_after=None):
    block = '\n'.join([indent + B] + [indent + l for l in lines] + [indent + E]) + '\n'
    m = re.search(r'[ \t]*' + re.escape(B) + r'.*?' + re.escape(E) + r'[^\n]*\n', txt, re.S)
    if m:
        return txt[:m.start()] + block + txt[m.end():]
    if anchor_before:
        i = txt.index(anchor_before)
        return txt[:i] + block + txt[i:]
    i = txt.index(anchor_after) + len(anchor_after)
    return txt[:i] + block + txt[i:]


def put_block_n(txt, lines, indent, tag, **kw):
    """Vários blocos no mesmo arquivo: marcador com etiqueta."""
    global B, E
    b0, e0 = B, E
    B, E = '// <kz-class-sets %s>' % tag, '// </kz-class-sets %s>' % tag
    try:
        return put_block(txt, lines, indent, **kw)
    finally:
        B, E = b0, e0


def patch(root):
    S = os.path.join(root, 'Assets', 'Scripts')
    # Item.cs: enum
    p = os.path.join(S, 'Core', 'Items', 'Item.cs'); t, nl, bom = read(p)
    t = re.sub(r'public enum ArmorSet \{[^}]*\}', 'public enum ArmorSet { ' + ', '.join(BASE_ENUM + [s[2] for s in SETS]) + ' }', t)
    write(p, t, nl, bom)
    # ArmorSets.cs
    p = os.path.join(S, 'Core', 'Items', 'ArmorSets.cs'); t, nl, bom = read(p)
    t = put_block_n(t, ['ArmorSet.%s => "%s",' % (s[2], s[4]) for s in SETS], '            ', 'name', anchor_before='            _ => "",')
    t = put_block_n(t, ['ArmorSet.%s => new SetBonus { %s },' % (s[2], s[6]) for s in SETS], '            ', 'bonus',
                    anchor_before='            _ => default,')
    write(p, t, nl, bom)
    # ItemDatabase.cs
    p = os.path.join(S, 'Core', 'Items', 'ItemDatabase.cs'); t, nl, bom = read(p)
    lines = []
    for cls, tier, enum, pre, sname, names, _, _, attr in SETS:
        drop, k, req, dur = TIERS[tier]
        lines.append('// %s — tier %d (%s)' % (sname, tier, cls))
        dk = CLASS_DEF.get(cls, 1.0)
        for i, kind in enumerate(KINDS):
            if attr == 'Strength':
                reqs = 'ReqStrength = %d' % (req + REQ_ADD[kind])
            else:                                     # classes de magia/agilidade: pouca força e o atributo principal
                reqs = 'ReqStrength = %d, Req%s = %d' % (round((req + REQ_ADD[kind]) * 0.3), attr, round((req + REQ_ADD[kind]) * 0.75))
            lines.append('public static readonly ItemDefinition %s%s = Add(new ItemDefinition { Id = %d, Set = ArmorSet.%s, Name = "%s", '
                         'Kind = ItemKind.%s, Slot = EquipSlot.%s, Defense = %d, %s, BaseDurability = %d, AllowedClasses = %s, DropLevel = %d });'
                         % (enum, kind, base_id(cls, tier) + i, enum, names[i], kind, SLOT[kind], max(1, round(BASE_DEF[kind] * k * dk)), reqs,
                            dur + DUR_ADD[kind] - (6 if dk < 0.8 else 0), cls, drop))
    anchor = [l for l in t.split('\n') if 'RunePlateBoots = Add(' in l][0] + '\n'
    t = put_block(t, lines, '        ', anchor_after=anchor)
    write(p, t, nl, bom)
    # Gear.cs
    p = os.path.join(S, 'Client', 'Gear.cs'); t, nl, bom = read(p)
    t = put_block_n(t, [' '.join('{ "%s", KezorLook.Hex("%s") },' % (s[3], s[7]) for s in SETS[i:i + 3]) for i in range(0, len(SETS), 3)],
                    '            ', 'colors', anchor_after='{ "Igneo", KezorLook.Hex("#ff7a2a") },\n')
    t = put_block_n(t, ['if (d.Id >= %d && d.Id <= %d) return "%s";' % (base_id(s[0], s[1]), base_id(s[0], s[1]) + 4, s[3]) for s in SETS],
                    '            ', 'setof', anchor_after='if (d.Id >= 180 && d.Id <= 184) return "Igneo";\n')
    write(p, t, nl, bom)


if __name__ == '__main__':
    for r in sys.argv[1:]:
        patch(r)
        print('ok', r)
