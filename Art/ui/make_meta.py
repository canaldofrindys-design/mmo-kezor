"""Cria os .meta da Unity para a arte premium (GUID fixo derivado do caminho: regerar não quebra referências)."""
import hashlib
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
UI = os.path.join(ROOT, 'Assets', 'Resources', 'UI')
TEMPLATE = os.path.join(UI, 'Art', 'panel.png.meta')


def guid(rel):
    return hashlib.md5(('kezor-premium:' + rel).encode()).hexdigest()


def write(path, text):
    if not os.path.exists(path):
        with open(path, 'w', newline='\n') as f:
            f.write(text)


def main():
    tpl = open(TEMPLATE).read()
    old = tpl.split('guid: ')[1].split('\n')[0]
    prem = os.path.join(UI, 'Premium')
    write(prem + '.meta', f"fileFormatVersion: 2\nguid: {guid('Premium')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    for f in sorted(os.listdir(prem)):
        if f.endswith('.png'):
            write(os.path.join(prem, f + '.meta'), tpl.replace(old, guid('Premium/' + f)))
    uss = open(os.path.join(UI, 'Kezor.uss.meta')).read()
    write(os.path.join(UI, 'KezorPremium.uss.meta'), uss.replace(uss.split('guid: ')[1].split('\n')[0], guid('KezorPremium.uss')))


if __name__ == '__main__':
    main()
