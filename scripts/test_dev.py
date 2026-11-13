import contextlib
import io
import json
import os
import tempfile
import unittest
from pathlib import Path
from unittest import mock

import dev
from dev import development_environment, init_config


class LocalConfigurationTests(unittest.TestCase):
    def test_configuration_generates_private_secrets_without_printing_them(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                init_config(33306, root)
            password = (root / '.local/mysql-password').read_text().strip()
            root_password = (root / '.local/mysql-root-password').read_text().strip()
            self.assertNotEqual(password, root_password)
            self.assertNotIn(password, output.getvalue())
            if os.name == 'posix':
                self.assertEqual((root / '.local/mysql-password').stat().st_mode & 0o777, 0o600)
            env = development_environment(root)
            self.assertIn('Port=33306;', env['ConnectionStrings__DefaultConnection'])
            self.assertTrue(env['ConnectionStrings__DefaultConnection'].endswith('Password=' + password))
            self.assertEqual(env['ASPNETCORE_ENVIRONMENT'], 'Development')
            self.assertTrue((root / '.local/mysql-password').read_bytes().endswith(b'\n'))
            self.assertFalse((root / '.local/mysql-password').read_bytes().endswith(b'\r\n'))

    def test_connection_preserves_carriage_return_from_existing_crlf_secret(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            init_config(33306, root)
            password_path = root / '.local/mysql-password'
            password = password_path.read_bytes().rstrip(b'\n')
            password_path.write_bytes(password + b'\r\n')

            connection = development_environment(root)['ConnectionStrings__DefaultConnection']

            self.assertTrue(connection.endswith('Password="' + password.decode('ascii') + '\r"'))

    def test_repeated_initialization_keeps_database_credentials(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            init_config(3306, root)
            password = (root / '.local/mysql-password').read_bytes()
            init_config(3306, root)
            self.assertEqual(password, (root / '.local/mysql-password').read_bytes())
            with self.assertRaises(ValueError):
                init_config(33306, root)

    def test_partial_configuration_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / '.local').mkdir()
            (root / '.local/mysql-password').write_text('existing')
            with self.assertRaises(ValueError):
                init_config(3306, root)
            self.assertEqual((root / '.local/mysql-password').read_text(), 'existing')

    def test_connection_string_delimiters_in_secret_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            init_config(3306, root)
            (root / '.local/mysql-password').write_text('password;Server=elsewhere')
            with self.assertRaises(ValueError):
                development_environment(root)

    def test_invalid_ports_are_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            for port in (0, 65536, -1):
                with self.assertRaises(ValueError):
                    init_config(port, Path(directory))


class RunCommandTests(unittest.TestCase):
    def test_the_executable_found_on_the_path_is_started_not_the_bare_name(self):
        # On Windows which("npm") finds npm.cmd; the bare name "npm" cannot be started there (SCRUM-129).
        with mock.patch.object(dev.shutil, 'which', return_value=r'C:\nodejs\npm.CMD'), \
                mock.patch.object(dev.subprocess, 'run') as run:
            dev.run(['npm', '--prefix', 'frontend', 'ci'], {'A': 'b'})
        run.assert_called_once_with([r'C:\nodejs\npm.CMD', '--prefix', 'frontend', 'ci'],
                                    cwd=dev.ROOT, env={'A': 'b'}, check=True)

    def test_a_missing_executable_is_reported_by_name(self):
        with mock.patch.object(dev.shutil, 'which', return_value=None), \
                mock.patch.object(dev.subprocess, 'run') as run:
            with self.assertRaisesRegex(ValueError, 'Required executable not found: npm'):
                dev.run(['npm', 'ci'], {})
        run.assert_not_called()

    def test_an_executable_that_cannot_be_started_names_the_command_and_not_the_environment(self):
        with mock.patch.object(dev.shutil, 'which', return_value='/bin/npm'), \
                mock.patch.object(dev.subprocess, 'run', side_effect=FileNotFoundError()):
            with self.assertRaises(ValueError) as caught:
                dev.run(['npm', 'ci'], {'ConnectionStrings__DefaultConnection': 'SECRET'})
        self.assertIn('npm', str(caught.exception))
        self.assertNotIn('SECRET', str(caught.exception))


if __name__ == '__main__':
    unittest.main()
