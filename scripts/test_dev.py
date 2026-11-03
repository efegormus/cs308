import contextlib
import io
import json
import tempfile
import unittest
from pathlib import Path

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
            self.assertEqual((root / '.local/mysql-password').stat().st_mode & 0o777, 0o600)
            env = development_environment(root)
            self.assertIn('Port=33306;', env['ConnectionStrings__DefaultConnection'])
            self.assertEqual(env['ASPNETCORE_ENVIRONMENT'], 'Development')

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


if __name__ == '__main__':
    unittest.main()
