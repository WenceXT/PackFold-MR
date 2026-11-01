# Optional desktop packaging advisor

This Python standard-library service recommends a supported box structure and dimensions. By default it uses deterministic clearance rules and calls no AI model. It is retained as a desktop extension; the Quest runtime uses its own offline templates and does not automatically connect to this service.

Use Python 3.10 or newer:

```bash
python advisor.py --width 160 --depth 100 --height 55
python advisor.py --serve
```

The first command writes `recommendation.json`; the server listens on `127.0.0.1:8765` and accepts JSON POST requests at `/recommend`.

```json
{
  "product_width_mm": 160,
  "product_depth_mm": 100,
  "product_height_mm": 55,
  "fragile": true,
  "reusable": true,
  "preferred_structure": "lift_off_lid"
}
```

Optional environment variables `PACKFOLD_PROVIDER_URL` and `PACKFOLD_PROVIDER_TOKEN` configure an external provider. The provider receives the requested product envelope and allowed schema and must return a `packfold.v1` JSON object directly. Responses are checked for supported structures, dimension limits, and minimum product clearance. No provider is configured or endorsed in this release. Store credentials outside the repository.

Existing service tests:

```bash
python -m unittest test_advisor.py
```
