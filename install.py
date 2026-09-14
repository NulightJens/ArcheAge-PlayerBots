"""Install the source module on a clean, pinned AAEmu checkout."""
import argparse, hashlib, json, shutil, subprocess
from pathlib import Path

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host', type=Path, required=True)
    args=parser.parse_args()
    root=Path(__file__).resolve().parent
    host=args.host.resolve(strict=True)
    manifest=json.loads((root/'playerbots.module.json').read_text())
    def git(*a):
        return subprocess.run(['git','-C',str(host),*a],check=True,capture_output=True,text=True).stdout.strip()
    if git('rev-parse','--show-toplevel').replace('\\','/') != host.as_posix():
        raise SystemExit('Pass the host repository root.')
    if git('rev-parse','HEAD') != manifest['host']['testedBaseCommit'] or git('status','--porcelain'):
        raise SystemExit('Use a clean checkout at the pinned host commit.')
    destination=host/manifest['modulePath']
    if destination.exists(): raise SystemExit('Module destination already exists; use a fresh checkout.')
    patch=root/manifest['install']['compatibilityPatch']
    if hashlib.sha256(patch.read_bytes()).hexdigest()!=manifest['install']['compatibilityPatchSha256']:
        raise SystemExit('Host patch checksum mismatch.')
    files=json.loads((root/'product-files.json').read_text())['files']
    for name in files:
        path=(root/name).resolve(strict=True)
        if not path.is_relative_to(root) or not path.is_file(): raise SystemExit('Invalid product file: '+name)
    git('apply','--check',str(patch))
    destination.mkdir(parents=True)
    for name in files:
        target=destination/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(root/name,target)
    git('apply',str(patch))
    print('Installed source. Build AAEmu.Game, configure your server and start it normally.')

if __name__=='__main__': main()
