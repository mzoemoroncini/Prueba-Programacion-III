"""Herramientas de desarrollo locales. Nunca imprime credenciales."""
import argparse
import json
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / '.local'

def run(command, **kwargs):
    return subprocess.run(command, cwd=ROOT, check=True, **kwargs)

def environment():
    config = LOCAL / 'settings.json'
    if not config.exists():
        raise SystemExit('Ejecutá primero: python scripts/dev.py setup')
    values = os.environ.copy()
    values.update(json.loads(config.read_text()))
    values.setdefault('ASPNETCORE_ENVIRONMENT', 'Development')
    values.setdefault('Capture__Enabled', 'false')
    values.setdefault('API_PROXY_TARGET', 'http://127.0.0.1:5001')
    values.setdefault('OFFERS_CHECKS_API_URL', 'http://127.0.0.1:5001')
    local_dotnet = ROOT / '.tools' / 'dotnet'
    if local_dotnet.exists():
        values['DOTNET_ROOT'] = str(local_dotnet)
        values['PATH'] = str(local_dotnet) + os.pathsep + values['PATH']
    values.setdefault('DOTNET_CLI_HOME', str(ROOT / '.tools' / 'dotnet-home'))
    values.setdefault('NUGET_PACKAGES', str(ROOT / '.tools' / 'nuget'))
    return values

def setup():
    LOCAL.mkdir(exist_ok=True)
    config = LOCAL / 'settings.json'
    mysql = LOCAL / 'mysql.env'
    if not config.exists():
        if mysql.exists():
            raise SystemExit('Existe configuración MySQL sin settings.json. Recuperala antes de generar credenciales nuevas.')
        password = secrets.token_hex(24)
        config.write_text(json.dumps({
            'ConnectionStrings__Offers': f'Server=127.0.0.1;Port=3307;Database=offers;User=offers;Password={password};',
            'Admin__Username': 'developer', 'Admin__Password': secrets.token_hex(24)
        }, indent=2))
        mysql.write_text(f'MYSQL_RANDOM_ROOT_PASSWORD=yes\nMYSQL_DATABASE=offers\nMYSQL_USER=offers\nMYSQL_PASSWORD={password}\n')
        config.chmod(0o600); mysql.chmod(0o600)
    env = environment()
    run(['docker', 'compose', '--env-file', str(mysql), 'up', '-d', 'mysql'], env=env)
    for _ in range(60):
        result = subprocess.run(['docker', 'compose', '--env-file', str(mysql), 'exec', '-T', 'mysql', 'sh', '-c', 'MYSQL_PWD="$MYSQL_PASSWORD" mysql --protocol=TCP -h127.0.0.1 -u"$MYSQL_USER" "$MYSQL_DATABASE" -Nse "SELECT 1"'], cwd=ROOT, capture_output=True)
        if result.returncode == 0:
            print('MySQL disponible. Credenciales privadas en .local/settings.json.')
            return
        time.sleep(1)
    raise SystemExit('MySQL no respondió. Revisá docker compose logs mysql; no compartas logs que contengan contraseñas.')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['setup', 'migrate', 'api', 'checks', 'frontend', 'build'])
    args = parser.parse_args()
    if args.action == 'setup': setup(); return
    env = environment()
    dotnet = shutil.which('dotnet', path=env['PATH']) or 'dotnet'
    if args.action == 'migrate':
        run([dotnet, 'run', '--project', 'backend/Offers.Api', '--no-launch-profile', '--', '--migrate'], env=env)
    elif args.action == 'api':
        run([dotnet, 'run', '--project', 'backend/Offers.Api', '--no-launch-profile', '--urls', 'http://0.0.0.0:5001'], env=env)
    elif args.action == 'checks':
        run([dotnet, 'run', '--project', 'backend/Offers.Checks'], env=env)
    elif args.action == 'frontend':
        npm = 'npm.cmd' if os.name == 'nt' else 'npm'
        subprocess.run([npm, 'run', 'dev', '--', '--host', '0.0.0.0', '--port', '5175', '--strictPort'], cwd=ROOT/'frontend', env=env, check=True)
    elif args.action == 'build':
        run([dotnet, 'build', 'DescuentosIA.sln', '-m:1'], env=env)
        npm = 'npm.cmd' if os.name == 'nt' else 'npm'
        subprocess.run([npm, 'run', 'build'], cwd=ROOT/'frontend', env=env, check=True)

if __name__ == '__main__':
    main()
