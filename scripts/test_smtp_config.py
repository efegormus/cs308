import json
import os
from pathlib import Path
import tempfile
import unittest
from smtp_config import save_private, smtp_environment


class SmtpConfigTests(unittest.TestCase):
    def setUp(self):
        self.directory=tempfile.TemporaryDirectory();self.root=Path(self.directory.name)
        self.path=self.root/'.local'/'smtp.json'
        self.config={'Email':{'Transport':'Smtp','From':'sender@example.com','Smtp':{'Host':'smtp.example.com','Port':587,'EnableSsl':True,'Username':'sender@example.com','Password':'test-only-not-a-real-password'}}}
    def tearDown(self):self.directory.cleanup()
    def test_missing_configuration_has_no_effect(self):self.assertEqual({},smtp_environment(self.root,{}))
    def test_private_configuration_loads_dotnet_settings(self):
        save_private(self.path,self.config);env=smtp_environment(self.root,{})
        self.assertEqual('Smtp',env['Email__Transport']);self.assertEqual('true',env['Email__Smtp__EnableSsl'])
    def test_explicit_environment_wins(self):
        save_private(self.path,self.config);self.assertEqual('Local',smtp_environment(self.root,{'Email__Transport':'Local'})['Email__Transport'])
    def test_remote_plaintext_smtp_is_rejected(self):
        self.config['Email']['Smtp']['EnableSsl']=False;save_private(self.path,self.config)
        with self.assertRaises(ValueError):smtp_environment(self.root,{})
    def test_local_test_server_can_use_plaintext(self):
        self.config['Email']['Smtp'].update(Host='127.0.0.1',EnableSsl=False,Port=1025);save_private(self.path,self.config)
        self.assertEqual('false',smtp_environment(self.root,{})['Email__Smtp__EnableSsl'])
    def test_private_file_permissions_and_atomic_replacement(self):
        save_private(self.path,self.config);self.config['Email']['Smtp']['Port']=2525;save_private(self.path,self.config)
        self.assertEqual(2525,json.loads(self.path.read_text())['Email']['Smtp']['Port'])
        if os.name!='nt':self.assertEqual(0o600,self.path.stat().st_mode&0o777)
        self.assertEqual([],list(self.path.parent.glob('.smtp-*')))


if __name__=='__main__':unittest.main()
