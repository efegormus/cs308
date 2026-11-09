#!/usr/bin/env python3
"""Manage the private, native local MySQL used by SCRUM-119. Never prints secrets."""
import argparse
import datetime
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / '.local' / 'db-redesign'


def config():
    value = json.loads((LOCAL / 'connection.json').read_text())
    if value['host'] != '127.0.0.1' or value['database'] != 'onlinestore':
        raise ValueError('This helper only manages the local onlinestore database.')
    return value


def connection(c):
    import pymysql
    return pymysql.connect(host=c['host'], port=c['port'], user='root', password=c['rootPassword'],
                           charset='utf8mb4', autocommit=True)


def environment(c, admin=False, tests=False):
    env = os.environ.copy()
    env.update(ASPNETCORE_ENVIRONMENT='Development', MySql__ServerVersion='8.4.11',
               ONLINESTORE_DATA_KEYS_FILE=str(LOCAL / 'data-keys.json'),
               Email__Transport='Local')
    env['Logging__LogLevel__Microsoft.EntityFrameworkCore'] = 'Warning'
    user = 'root' if admin else c['user']
    password = c['rootPassword'] if admin else c['password']
    env['ConnectionStrings__DefaultConnection'] = f"Server={c['host']};Port={c['port']};Database={c['database']};User ID={user};Password={password}"
    if tests:
        env['ONLINESTORE_TEST_MYSQL'] = f"Server={c['host']};Port={c['port']};Database=onlinestore_tests;User ID=onlinestore_test;Password={c['testPassword']}"
        # Tests create their own temporary key, never use demo data keys.
        env.pop('ONLINESTORE_DATA_KEYS_FILE', None)
    return env


def run(args, env=None):
    subprocess.run(args, cwd=ROOT, env=env, check=True)


def backup(c):
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    target = LOCAL / 'backups' / stamp
    target.mkdir(parents=True, mode=0o700)
    env = os.environ.copy()
    env['MYSQL_PWD'] = c['rootPassword']
    dump = target / 'onlinestore.sql'
    with dump.open('wb') as output:
        dump.chmod(0o600)
        subprocess.run([str(Path(c['mysqlBin']) / 'mysqldump'), '--host=127.0.0.1', f"--port={c['port']}",
                        '--user=root', '--single-transaction', '--no-tablespaces', '--set-gtid-purged=OFF',
                        '--routines', '--triggers', 'onlinestore'], env=env, stdout=output, check=True)
    for filename in ['data-keys.json', 'demo-accounts.json']:
        source = LOCAL / filename
        if source.exists():
            shutil.copy2(source, target / filename)
            (target / filename).chmod(0o600)
    invoices = ROOT / 'OnlineStore' / 'App_Data' / 'invoices'
    if invoices.exists():
        shutil.copytree(invoices, target / 'invoices')
    print('Backup (contains private keys and demo credentials):', target.relative_to(ROOT))
    return target


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['start', 'stop', 'status', 'backup', 'reset', 'migrate', 'seed', 'run', 'test'])
    parser.add_argument('--reset-local', action='store_true', help='explicitly allow backup + recreation of local onlinestore')
    args = parser.parse_args()
    c = config()
    if args.command == 'start':
        try:
            db = connection(c)
            db.close()
            print('MySQL is already running.')
            return
        except Exception:
            pass
        run([str(Path(c['mysqlBin']) / 'mysqld'), '--defaults-file=' + str(LOCAL / 'my.cnf'), '--daemonize'])
    elif args.command == 'stop':
        with connection(c) as db:
            with db.cursor() as cursor:
                cursor.execute('SHUTDOWN')
        print('MySQL stopped; data is retained.')
    elif args.command == 'backup':
        backup(c)
    elif args.command in ('migrate', 'reset'):
        if args.command == 'reset':
            if not args.reset_local:
                raise ValueError('Reset requires --reset-local. It backs up and then recreates only the local onlinestore database.')
            backup(c)
            with connection(c) as db:
                with db.cursor() as cursor:
                    cursor.execute('DROP DATABASE onlinestore')
                    cursor.execute('CREATE DATABASE onlinestore CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci')
        run(['dotnet', 'tool', 'restore'])
        run(['dotnet', 'ef', 'database', 'update', '--project', 'OnlineStore'], environment(c, admin=True))
        if args.command == 'reset':
            run(['dotnet', 'run', '--project', 'OnlineStore', '--no-launch-profile', '--', '--seed-demo'], environment(c))
    elif args.command == 'seed':
        run(['dotnet', 'run', '--project', 'OnlineStore', '--no-launch-profile', '--', '--seed-demo'], environment(c))
    elif args.command == 'run':
        run(['dotnet', 'run', '--project', 'OnlineStore', '--launch-profile', 'http'], environment(c))
    elif args.command == 'test':
        run(['dotnet', 'test', 'cs308.sln', '--logger', 'trx;LogFileName=redesign-all.trx'], environment(c, tests=True))
    else:
        with connection(c) as db:
            with db.cursor() as cursor:
                cursor.execute('SELECT VERSION(), @@port, @@global.time_zone')
                print('MySQL version, port, time zone:', cursor.fetchone())
                cursor.execute("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA='onlinestore' AND TABLE_TYPE='BASE TABLE' AND TABLE_NAME<>'__EFMigrationsHistory' ORDER BY TABLE_NAME")
                tables = [row[0] for row in cursor.fetchall()]
                print('Domain tables:', len(tables))
                for table in tables:
                    cursor.execute('SELECT COUNT(*) FROM onlinestore.`' + table + '`')
                    print(table, cursor.fetchone()[0])


if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        sys.exit(130)
    except ValueError as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
    except Exception:
        print('Local database command failed. Check docs/db-redesign/local-setup.md and private .local/db-redesign configuration.', file=sys.stderr)
        sys.exit(1)
