import json
import os
import threading
import unittest
import urllib.error
import urllib.request
from unittest.mock import patch
from http.server import ThreadingHTTPServer
from advisor import recommend,validate,Handler

REQUEST={'product_width_mm':160,'product_depth_mm':100,'product_height_mm':55,'fragile':True,'reusable':True}


class AdvisorTests(unittest.TestCase):
    def test_keeps_lift_off_structure(self):
        with patch.dict(os.environ,{'PACKFOLD_PROVIDER_URL':''}): result=recommend({**REQUEST,'preferred_structure':'lift_off_lid'})
        self.assertEqual(result['structure'],'lift_off_lid')
        self.assertEqual(result['width_mm'],180)

    def test_rejects_unknown_preferred_structure(self):
        with self.assertRaises(ValueError): recommend({**REQUEST,'preferred_structure':'unvalidated_shape'})

    def test_rules_fit_product(self):
        with patch.dict(os.environ,{'PACKFOLD_PROVIDER_URL':''}): result=recommend(REQUEST)
        self.assertEqual([result[f] for f in ('width_mm','depth_mm','height_mm')],[180,120,70])
        self.assertIn('no AI',result['source'])

    def test_invalid_outputs_and_oversized_product(self):
        with self.assertRaises(ValueError): recommend({**REQUEST,'product_width_mm':300})
        with self.assertRaises(ValueError): recommend({**REQUEST,'fragile':'false'})
        with self.assertRaises(ValueError): validate({'schema':'packfold.v1','structure':'unsupported'})
        with self.assertRaises(ValueError): validate({'schema':'packfold.v1','structure':'open_tray','width_mm':float('nan')})

    def test_http_contract(self):
        server=ThreadingHTTPServer(('127.0.0.1',0),Handler)
        thread=threading.Thread(target=server.serve_forever,daemon=True); thread.start()
        try:
            req=urllib.request.Request(f'http://127.0.0.1:{server.server_port}/recommend',data=json.dumps(REQUEST).encode(),headers={'Content-Type':'application/json'})
            with patch.dict(os.environ,{'PACKFOLD_PROVIDER_URL':''}):
                with urllib.request.urlopen(req,timeout=3) as response: result=json.load(response)
            self.assertEqual(result['schema'],'packfold.v1')
            self.assertEqual(result['width_mm'],180)
        finally: server.shutdown(); server.server_close(); thread.join(timeout=2)


if __name__=='__main__': unittest.main()
