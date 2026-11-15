"""Add metadata only for this new owned delivery; preserve every existing GUID."""
from pathlib import Path
import uuid
root=Path(__file__).resolve().parents[3]
for folder in [root/'Assets/_OutOfReach/Scripts/Presentation/LaunchFXV03',root/'Assets/_OutOfReach/Visuals/LaunchFX_V03']:
    for item in [folder,*sorted(folder.rglob('*'))]:
        if item.suffix=='.meta': continue
        meta=Path(str(item)+'.meta')
        if meta.exists(): continue
        text='fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'
        if item.is_dir(): text+='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        elif item.suffix=='.cs': text+='MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        elif item.suffix=='.shader': text+='ShaderImporter:\n  externalObjects: {}\n  defaultTextures: []\n  nonModifiableTextures: []\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        elif item.suffix=='.asmdef': text+='AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n'
        else: raise ValueError('Unknown asset type: '+str(item))
        meta.write_text(text,encoding='utf-8')
